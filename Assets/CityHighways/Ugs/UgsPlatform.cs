#if CH_UGS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CityHighways.Net;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace CityHighways.Ugs
{
    /// <summary>
    /// Unity Gaming Services для City Highways. Lobby — список открытых игр и короткий код лобби;
    /// Relay — связь через серверы Unity (работает из любых сетей, без проброса портов и белого IP).
    /// Вход анонимный: каждый запуск игры — отдельный анонимный игрок (свой профиль), поэтому можно
    /// запустить две копии на одном компьютере. Сборка компилируется только при установленном пакете
    /// Multiplayer Services и сама регистрируется в NetPlatforms.
    /// </summary>
    public sealed class UgsPlatform : IUgsPlatform
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            NetPlatforms.Ugs = new UgsPlatform();
        }

        static Task _signIn;

        /// <summary>Инициализация UGS и анонимный вход — один раз за запуск (при ошибке следующая попытка заново).</summary>
        internal static Task EnsureSignedIn()
        {
            if (_signIn == null || _signIn.IsFaulted || _signIn.IsCanceled) _signIn = SignIn();
            return _signIn;
        }

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // Свой профиль на каждый запуск: две копии игры на одном компьютере — два разных игрока.
                options.SetProfile("ch" + UnityEngine.Random.Range(100000, 999999));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        internal static string Describe(Exception e)
        {
            if (e is AggregateException aggregate && aggregate.InnerException != null) e = aggregate.InnerException;
            string text = e.Message;
            if (text.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                text += " — привяжите проект к Unity Cloud: Edit → Project Settings → Services.";
            return text;
        }

        public INetTransport CreateHost(string lobbyName, int maxPlayers) => UgsTransport.Host(lobbyName, maxPlayers);
        public INetTransport JoinByCode(string code) => UgsTransport.Join(code, true);
        public INetTransport JoinById(string lobbyId) => UgsTransport.Join(lobbyId, false);

        public async void QueryLobbies(Action<List<OpenLobby>, string> done)
        {
            try
            {
                await EnsureSignedIn();
                var options = new QueryLobbiesOptions
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                        new QueryFilter(QueryFilter.FieldOptions.S1, UgsTransport.OpenFlag, QueryFilter.OpOptions.EQ)
                    }
                };
                var response = await LobbyService.Instance.QueryLobbiesAsync(options);
                var list = response.Results.Select(l => new OpenLobby
                {
                    Name = l.Name,
                    Id = l.Id,
                    Players = l.Players != null ? l.Players.Count : 0,
                    MaxPlayers = l.MaxPlayers
                }).ToList();
                done(list, null);
            }
            catch (Exception e)
            {
                done(new List<OpenLobby>(), Describe(e));
            }
        }
    }

    /// <summary>
    /// Канал «хост — клиенты» через Relay и Unity Transport. Сообщения сжимаются и режутся на куски, которые
    /// уходят по надёжному упорядоченному каналу; кусок, не влезший в окно отправки, ждёт следующего кадра.
    /// </summary>
    public sealed class UgsTransport : INetTransport
    {
        internal const string OpenFlag = "open";
        const string RelayKey = "relay";
        const string VersionKey = "version";
        const string Protocol = "dtls";
        const int ChunkSize = 900;
        const ulong HostPeer = 1;
        const double HeartbeatSeconds = 15.0;

        readonly Queue<Action> _events = new Queue<Action>();
        readonly Dictionary<ulong, NetworkConnection> _connections = new Dictionary<ulong, NetworkConnection>();
        readonly Dictionary<NetworkConnection, ulong> _ids = new Dictionary<NetworkConnection, ulong>();
        readonly Dictionary<ulong, Queue<byte[]>> _outgoing = new Dictionary<ulong, Queue<byte[]>>();
        readonly Dictionary<ulong, MemoryStream> _incoming = new Dictionary<ulong, MemoryStream>();

        NetworkDriver _driver;
        NetworkPipeline _pipeline;
        bool _driverReady;
        bool _disposed;
        bool _failed;
        ulong _nextId = 2;
        string _lobbyId;
        DateTime _nextHeartbeat;
        // Клиент: соединиться с хостом, как только драйвер привяжется к Relay.
        bool _connectPending;
        NetworkEndpoint _relayEndpoint;

        public bool IsHost { get; }
        public ulong LocalId => IsHost ? HostPeer : 0;
        public ulong HostId => HostPeer;
        public bool IsReady { get; private set; }
        public string Kind => "Unity Relay";
        public string ConnectHint { get; private set; } = "";
        public string LobbyCode { get; private set; } = "";
        public bool CanInvite => false;
        public void Invite() { }

        public event Action Ready;
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, byte[]> Received;
        public event Action<string> Failed;

        UgsTransport(bool host) { IsHost = host; }

        // ================================================================== Создание и вход

        internal static UgsTransport Host(string lobbyName, int maxPlayers)
        {
            var transport = new UgsTransport(true);
            transport.StartHost(lobbyName, Mathf.Clamp(maxPlayers, 2, 16));
            return transport;
        }

        internal static UgsTransport Join(string codeOrId, bool byCode)
        {
            var transport = new UgsTransport(false);
            transport.StartJoin((codeOrId ?? "").Trim(), byCode);
            return transport;
        }

        async void StartHost(string lobbyName, int maxPlayers)
        {
            try
            {
                await UgsPlatform.EnsureSignedIn();
                if (_disposed) return;
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if (_disposed) return;

                CreateDriver(allocation.ToRelayServerData(Protocol));
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || _driver.Listen() != 0)
                    throw new Exception("не удалось занять место на сервере Relay");

                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        // Код Relay видят только вошедшие в лобби.
                        { RelayKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
                        { VersionKey, new DataObject(DataObject.VisibilityOptions.Public, NetCodec.ProtocolVersion.ToString()) },
                        { "state", new DataObject(DataObject.VisibilityOptions.Public, OpenFlag, DataObject.IndexOptions.S1) }
                    }
                };
                var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                _lobbyId = lobby.Id;
                if (_disposed)
                {
                    DeleteLobby();
                    return;
                }
                LobbyCode = lobby.LobbyCode;
                ConnectHint = $"Код лобби: {LobbyCode} — продиктуйте его друзьям или пусть найдут игру в списке.";
                _nextHeartbeat = DateTime.UtcNow.AddSeconds(HeartbeatSeconds);
                IsReady = true;
                _events.Enqueue(() => Ready?.Invoke());
            }
            catch (Exception e)
            {
                Fail("Не удалось создать лобби: " + UgsPlatform.Describe(e));
            }
        }

        async void StartJoin(string codeOrId, bool byCode)
        {
            try
            {
                if (codeOrId.Length == 0) throw new Exception("введите код лобби");
                await UgsPlatform.EnsureSignedIn();
                if (_disposed) return;
                var lobby = byCode
                    ? await LobbyService.Instance.JoinLobbyByCodeAsync(codeOrId.ToUpperInvariant())
                    : await LobbyService.Instance.JoinLobbyByIdAsync(codeOrId);
                _lobbyId = lobby.Id;
                if (_disposed)
                {
                    LeaveLobby();
                    return;
                }
                if (lobby.Data == null || !lobby.Data.TryGetValue(RelayKey, out var relay) || string.IsNullOrEmpty(relay.Value))
                    throw new Exception("хост ещё не готов, попробуйте через пару секунд");
                if (lobby.Data.TryGetValue(VersionKey, out var version) && version.Value != NetCodec.ProtocolVersion.ToString())
                    throw new Exception("у хоста другая версия игры");

                var allocation = await RelayService.Instance.JoinAllocationAsync(relay.Value);
                if (_disposed) return;
                var data = allocation.ToRelayServerData(Protocol);
                CreateDriver(data);
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) throw new Exception("не удалось подключиться к серверу Relay");
                _relayEndpoint = data.Endpoint;
                _connectPending = true; // соединимся в Poll, когда драйвер привяжется
            }
            catch (Exception e)
            {
                Fail("Не удалось войти в лобби: " + UgsPlatform.Describe(e));
            }
        }

        void CreateDriver(RelayServerData data)
        {
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);
            _driver = NetworkDriver.Create(settings);
            _pipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            _driverReady = true;
        }

        void Fail(string text)
        {
            if (_failed || _disposed) return;
            _failed = true;
            _events.Enqueue(() => Failed?.Invoke(text));
        }

        // ================================================================== Отправка и приём

        public void Send(ulong peer, byte[] data)
        {
            if (_disposed || data == null) return;
            if (!_outgoing.TryGetValue(peer, out var queue))
            {
                queue = new Queue<byte[]>();
                _outgoing[peer] = queue;
            }
            // Сжатие (JSON жмётся в 5–10 раз) и нарезка: [флаг 0/1 — последний кусок] + данные.
            var packed = Compress(data);
            for (int offset = 0; offset < packed.Length || offset == 0; offset += ChunkSize)
            {
                int length = Math.Min(ChunkSize, packed.Length - offset);
                var chunk = new byte[length + 1];
                chunk[0] = (byte)(offset + length >= packed.Length ? 1 : 0);
                Buffer.BlockCopy(packed, offset, chunk, 1, length);
                queue.Enqueue(chunk);
                if (packed.Length == 0) break;
            }
        }

        public void Poll()
        {
            if (!_disposed && _driverReady)
            {
                _driver.ScheduleUpdate().Complete();

                if (_connectPending && _driver.Bound)
                {
                    _connectPending = false;
                    var toHost = _driver.Connect(_relayEndpoint);
                    _connections[HostPeer] = toHost;
                    _ids[toHost] = HostPeer;
                }

                if (IsHost)
                {
                    NetworkConnection incoming;
                    while ((incoming = _driver.Accept()) != default(NetworkConnection))
                    {
                        ulong id = _nextId++;
                        _connections[id] = incoming;
                        _ids[incoming] = id;
                        PeerConnected?.Invoke(id);
                    }
                }

                NetworkEvent.Type type;
                while ((type = _driver.PopEvent(out var connection, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (!_ids.TryGetValue(connection, out var peer)) continue;
                    switch (type)
                    {
                        case NetworkEvent.Type.Connect:
                            if (!IsHost)
                            {
                                IsReady = true;
                                Ready?.Invoke();
                                PeerConnected?.Invoke(HostPeer);
                            }
                            break;
                        case NetworkEvent.Type.Data:
                            var bytes = new NativeArray<byte>(reader.Length, Allocator.Temp);
                            reader.ReadBytes(bytes);
                            var chunk = bytes.ToArray();
                            bytes.Dispose();
                            OnChunk(peer, chunk);
                            break;
                        case NetworkEvent.Type.Disconnect:
                            Forget(peer);
                            PeerDisconnected?.Invoke(peer);
                            if (!IsHost) Fail(IsReady ? "Связь с хостом потеряна." : "Хост не ответил.");
                            break;
                    }
                }

                FlushOutgoing();

                if (IsHost && IsReady && _lobbyId != null && DateTime.UtcNow >= _nextHeartbeat)
                {
                    _nextHeartbeat = DateTime.UtcNow.AddSeconds(HeartbeatSeconds);
                    Heartbeat();
                }
            }

            while (_events.Count > 0) _events.Dequeue()();
        }

        void OnChunk(ulong peer, byte[] chunk)
        {
            if (chunk.Length == 0) return;
            if (!_incoming.TryGetValue(peer, out var buffer))
            {
                buffer = new MemoryStream();
                _incoming[peer] = buffer;
            }
            buffer.Write(chunk, 1, chunk.Length - 1);
            if (chunk[0] != 1) return;
            var packed = buffer.ToArray();
            buffer.SetLength(0);
            byte[] data;
            try
            {
                data = Decompress(packed);
            }
            catch (Exception)
            {
                return; // повреждённое сообщение — сессия попросит синхронизацию по таймауту
            }
            Received?.Invoke(peer, data);
        }

        void FlushOutgoing()
        {
            foreach (var pair in _outgoing)
            {
                if (!_connections.TryGetValue(pair.Key, out var connection)) continue;
                if (_driver.GetConnectionState(connection) != NetworkConnection.State.Connected) continue;
                var queue = pair.Value;
                while (queue.Count > 0)
                {
                    var chunk = queue.Peek();
                    if (_driver.BeginSend(_pipeline, connection, out var writer) != 0) break; // окно занято — в следующий кадр
                    if (writer.Capacity < chunk.Length)
                    {
                        _driver.AbortSend(writer);
                        Debug.LogError($"City Highways: кусок {chunk.Length} байт не влезает в пакет ({writer.Capacity}).");
                        queue.Dequeue();
                        continue;
                    }
                    var native = new NativeArray<byte>(chunk, Allocator.Temp);
                    writer.WriteBytes(native);
                    native.Dispose();
                    if (_driver.EndSend(writer) < 0) break; // не ушло — повторим этот же кусок
                    queue.Dequeue();
                }
            }
        }

        void Forget(ulong peer)
        {
            if (_connections.TryGetValue(peer, out var connection)) _ids.Remove(connection);
            _connections.Remove(peer);
            _outgoing.Remove(peer);
            _incoming.Remove(peer);
        }

        static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                    gzip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        // ================================================================== Лобби

        public void SetJoinable(bool joinable)
        {
            if (!IsHost || _lobbyId == null) return;
            UpdateState(joinable ? OpenFlag : "playing");
        }

        async void UpdateState(string state)
        {
            try
            {
                await LobbyService.Instance.UpdateLobbyAsync(_lobbyId, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "state", new DataObject(DataObject.VisibilityOptions.Public, state, DataObject.IndexOptions.S1) }
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("City Highways: не удалось обновить лобби: " + e.Message);
            }
        }

        async void Heartbeat()
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_lobbyId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("City Highways: пульс лобби не дошёл: " + e.Message);
            }
        }

        async void DeleteLobby()
        {
            string id = _lobbyId;
            if (id == null) return;
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(id);
            }
            catch (Exception)
            {
                // лобби само исчезнет без пульса
            }
        }

        async void LeaveLobby()
        {
            string id = _lobbyId;
            if (id == null) return;
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(id, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception)
            {
                // нас и так уберут из лобби
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_driverReady)
            {
                // Последние куски (например, «хост закрыл игру») стараемся отправить перед закрытием.
                FlushOutgoing();
                _driver.ScheduleUpdate().Complete();
                foreach (var connection in _connections.Values)
                    if (connection.IsCreated) _driver.Disconnect(connection);
                _driver.ScheduleUpdate().Complete();
                _driver.Dispose();
                _driverReady = false;
            }
            _disposed = true;
            if (IsHost) DeleteLobby();
            else LeaveLobby();
            _connections.Clear();
            _ids.Clear();
        }
    }
}
#endif
