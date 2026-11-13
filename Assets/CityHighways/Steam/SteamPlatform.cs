#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CityHighways.Net;
using Steamworks;
using UnityEngine;

namespace CityHighways.Steam
{
    /// <summary>
    /// Steam для City Highways: инициализация SteamAPI, лобби друзей, приглашения через оверлей и
    /// сообщения через SteamNetworkingMessages (с ретрансляцией Valve — без проброса портов).
    /// Сборка компилируется только при установленном Steamworks.NET и сама регистрируется в NetPlatforms.
    /// Для разработки используется тестовый AppID 480 (файл steam_appid.txt в корне проекта).
    /// </summary>
    public sealed class SteamPlatform : ISteamPlatform
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            NetPlatforms.Steam = new SteamPlatform();
        }

        bool _tried;
        bool _initialized;
        string _problem = "";
        ulong _pendingJoin;
        Callback<GameLobbyJoinRequested_t> _joinRequested;

        public event Action<ulong> JoinRequested;

        public bool Available
        {
            get
            {
                EnsureInit();
                return _initialized;
            }
        }

        public string Problem
        {
            get
            {
                EnsureInit();
                return _problem;
            }
        }

        void EnsureInit()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                if (!SteamAPI.Init())
                {
                    _problem = "Steam не запущен или игра не найдена (нужен steam_appid.txt с AppID).";
                    return;
                }
                _initialized = true;
                SteamNetworkingUtils.InitRelayNetworkAccess();
                _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
                _pendingJoin = ParseCommandLineLobby();
                Application.quitting += Shutdown;
            }
            catch (Exception e)
            {
                _problem = "Steamworks не загрузился: " + e.Message;
            }
        }

        static ulong ParseCommandLineLobby()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var lobby)) return lobby;
            return 0;
        }

        void OnJoinRequested(GameLobbyJoinRequested_t request)
        {
            ulong lobby = request.m_steamIDLobby.m_SteamID;
            if (JoinRequested != null) JoinRequested.Invoke(lobby);
            else _pendingJoin = lobby;
        }

        public ulong TakePendingJoin()
        {
            EnsureInit();
            ulong lobby = _pendingJoin;
            _pendingJoin = 0;
            return lobby;
        }

        public string PersonaName => Available ? SteamFriends.GetPersonaName() : "";

        public INetTransport CreateHost(int maxMembers) => Available ? new SteamTransport(true, 0, maxMembers) : null;

        public INetTransport Join(ulong lobby) => Available ? new SteamTransport(false, lobby, 0) : null;

        public List<JoinableLobby> FriendsInLobbies()
        {
            var result = new List<JoinableLobby>();
            if (!Available) return result;
            var app = SteamUtils.GetAppID();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t info)) continue;
                if (info.m_gameID.AppID() != app || !info.m_steamIDLobby.IsValid()) continue;
                result.Add(new JoinableLobby { Name = SteamFriends.GetFriendPersonaName(friend), Lobby = info.m_steamIDLobby.m_SteamID });
            }
            return result;
        }

        public void Poll()
        {
            if (_initialized) SteamAPI.RunCallbacks();
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            SteamAPI.Shutdown();
        }
    }

    /// <summary>Лобби Steam как канал связи: владелец лобби — хост, остальные участники — клиенты.</summary>
    public sealed class SteamTransport : INetTransport
    {
        const int Channel = 0;
        const int SendFlags = Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;

        readonly int _maxMembers;
        readonly IntPtr[] _incoming = new IntPtr[32];
        readonly HashSet<ulong> _members = new HashSet<ulong>();
        CSteamID _lobby;
        bool _disposed;

        readonly CallResult<LobbyCreated_t> _lobbyCreated;
        readonly Callback<LobbyEnter_t> _lobbyEnter;
        readonly Callback<LobbyChatUpdate_t> _chatUpdate;
        readonly Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;

        public bool IsHost { get; }
        public ulong LocalId { get; }
        public ulong HostId { get; private set; }
        public bool IsReady { get; private set; }
        public string Kind => "Steam";
        public string ConnectHint => IsHost ? "Пригласите друзей через Steam — кнопка «Пригласить друга»." : "";
        public bool CanInvite => IsHost && IsReady;

        public event Action Ready;
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, byte[]> Received;
        public event Action<string> Failed;

        public SteamTransport(bool host, ulong lobby, int maxMembers)
        {
            IsHost = host;
            _maxMembers = Math.Max(2, maxMembers);
            LocalId = SteamUser.GetSteamID().m_SteamID;
            if (host) HostId = LocalId;

            _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
            _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
            _lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);

            if (host) _lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxMembers));
            else SteamMatchmaking.JoinLobby(new CSteamID(lobby));
        }

        void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Failed?.Invoke("Steam не создал лобби: " + (ioFailure ? "ошибка связи" : result.m_eResult.ToString()));
                return;
            }
            _lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobby, "game", "CityHighways");
            SteamMatchmaking.SetLobbyData(_lobby, "version", NetCodec.ProtocolVersion.ToString());
            IsReady = true;
            Ready?.Invoke();
        }

        void OnLobbyEnter(LobbyEnter_t entered)
        {
            if (IsHost) return; // хост входит в своё лобби сам при создании
            if ((EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Failed?.Invoke("Не удалось войти в лобби (оно закрыто или заполнено).");
                return;
            }
            _lobby = new CSteamID(entered.m_ulSteamIDLobby);
            HostId = SteamMatchmaking.GetLobbyOwner(_lobby).m_SteamID;
            IsReady = true;
            Ready?.Invoke();
            PeerConnected?.Invoke(HostId);
        }

        void OnChatUpdate(LobbyChatUpdate_t update)
        {
            if (update.m_ulSteamIDLobby != _lobby.m_SteamID) return;
            ulong user = update.m_ulSteamIDUserChanged;
            uint change = update.m_rgfChatMemberStateChange;
            if ((change & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                if (IsHost && _members.Add(user)) PeerConnected?.Invoke(user);
                return;
            }
            const uint gone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
                                    | EChatMemberStateChange.k_EChatMemberStateChangeKicked | EChatMemberStateChange.k_EChatMemberStateChangeBanned);
            if ((change & gone) != 0)
            {
                if (IsHost)
                {
                    if (_members.Remove(user)) PeerDisconnected?.Invoke(user);
                }
                else if (user == HostId)
                {
                    PeerDisconnected?.Invoke(user);
                    Failed?.Invoke("Хост вышел из лобби.");
                }
            }
        }

        void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t request)
        {
            // Принимаем сессию только от участников нашего лобби.
            ulong from = request.m_identityRemote.GetSteamID().m_SteamID;
            bool known = IsHost ? IsLobbyMember(from) : from == HostId;
            if (!known) return;
            // Сообщение могло обогнать уведомление о входе в лобби — считаем участника подключившимся.
            if (IsHost && _members.Add(from)) PeerConnected?.Invoke(from);
            var identity = request.m_identityRemote;
            SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
        }

        bool IsLobbyMember(ulong user)
        {
            if (!_lobby.IsValid()) return false;
            int count = SteamMatchmaking.GetNumLobbyMembers(_lobby);
            for (int i = 0; i < count; i++)
                if (SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i).m_SteamID == user) return true;
            return false;
        }

        public void Send(ulong peer, byte[] data)
        {
            if (_disposed || data == null) return;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(peer));
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                SteamNetworkingMessages.SendMessageToUser(ref identity, buffer, (uint)data.Length, SendFlags, Channel);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Poll()
        {
            if (_disposed) return;
            SteamAPI.RunCallbacks();
            while (true)
            {
                int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _incoming, _incoming.Length);
                if (count <= 0) break;
                for (int i = 0; i < count; i++)
                {
                    var message = SteamNetworkingMessage_t.FromIntPtr(_incoming[i]);
                    try
                    {
                        var data = new byte[message.m_cbSize];
                        Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);
                        Received?.Invoke(message.m_identityPeer.GetSteamID().m_SteamID, data);
                    }
                    finally
                    {
                        SteamNetworkingMessage_t.Release(_incoming[i]);
                    }
                }
            }
        }

        public void Invite()
        {
            if (CanInvite) SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_lobby.IsValid()) SteamMatchmaking.LeaveLobby(_lobby);
            _lobbyEnter?.Dispose();
            _chatUpdate?.Dispose();
            _sessionRequest?.Dispose();
            _lobbyCreated?.Dispose();
        }
    }
}
#endif
