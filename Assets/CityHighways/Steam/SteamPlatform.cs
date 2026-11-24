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
    public sealed class SteamPlatform : ISteamPlatform, IAccountBoundSteamAchievements
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var platform = new SteamPlatform();
            NetPlatforms.Steam = platform;
            NetPlatforms.SteamAchievements = platform;
        }

        bool _tried;
        bool _initialized;
        string _problem = "";
        ulong _pendingJoin;
        Callback<GameLobbyJoinRequested_t> _joinRequested;
        Callback<UserStatsReceived_t> _statsReceived;
        Callback<UserStatsStored_t> _statsStored;
        // This queue is bound to a single authenticated SteamID. All
        // pending/staged IDs are discarded on account switch or logout.
        readonly SteamAchievementAccountQueue _achievementQueue =
            new SteamAchievementAccountQueue();
        bool _statsReady;
        bool _storePending;
        ulong _pendingStoreAccount;
        float _nextStatsRequest;
        float _nextStoreAttempt;

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
                _statsReceived = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
                _statsStored = Callback<UserStatsStored_t>.Create(OnStatsStored);
                RequestAchievementStats();
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

        public string SteamId => Available ? SteamUser.GetSteamID().m_SteamID.ToString() : "";

        Callback<GetTicketForWebApiResponse_t> _webApiTicket;
        readonly List<(HAuthTicket handle, Action<string> done)> _ticketWaiters = new List<(HAuthTicket, Action<string>)>();

        public void RequestWebApiTicket(string identity, Action<string> done)
        {
            if (!Available)
            {
                done?.Invoke(null);
                return;
            }
            if (_webApiTicket == null) _webApiTicket = Callback<GetTicketForWebApiResponse_t>.Create(OnWebApiTicket);
            var handle = SteamUser.GetAuthTicketForWebApi(identity);
            if (handle == HAuthTicket.Invalid)
            {
                done?.Invoke(null);
                return;
            }
            _ticketWaiters.Add((handle, done));
        }

        void OnWebApiTicket(GetTicketForWebApiResponse_t response)
        {
            int index = _ticketWaiters.FindIndex(w => w.handle == response.m_hAuthTicket);
            if (index < 0) return;
            var done = _ticketWaiters[index].done;
            _ticketWaiters.RemoveAt(index);
            if (response.m_eResult != EResult.k_EResultOK || response.m_cubTicket <= 0)
            {
                done?.Invoke(null);
                return;
            }
            var hex = new System.Text.StringBuilder(response.m_cubTicket * 2);
            for (int i = 0; i < response.m_cubTicket; i++) hex.Append(response.m_rgubTicket[i].ToString("X2"));
            done?.Invoke(hex.ToString());
        }

        Texture2D _avatar;

        /// <summary>
        /// Большой аватар (184×184). Steam может отдать его не сразу: GetLargeFriendAvatar вернёт -1 и догрузит
        /// в фоне, поэтому интерфейс спрашивает ещё раз через полсекунды.
        /// </summary>
        public Texture2D Avatar
        {
            get
            {
                if (!Available) return null;
                if (_avatar != null) return _avatar;
                int handle = SteamFriends.GetLargeFriendAvatar(SteamUser.GetSteamID());
                if (handle <= 0) return null;
                if (!SteamUtils.GetImageSize(handle, out uint width, out uint height) || width == 0 || height == 0) return null;
                int size = (int)(width * height * 4);
                var raw = new byte[size];
                if (!SteamUtils.GetImageRGBA(handle, raw, size)) return null;
                // Строки у Steam сверху вниз, у текстуры Unity — снизу вверх.
                var flipped = new byte[size];
                int row = (int)width * 4;
                for (int y = 0; y < height; y++)
                    Buffer.BlockCopy(raw, y * row, flipped, ((int)height - 1 - y) * row, row);
                var texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                texture.LoadRawTextureData(flipped);
                texture.Apply();
                _avatar = texture;
                return _avatar;
            }
        }

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

        public List<SteamFriend> Friends()
        {
            var result = new List<SteamFriend>();
            if (!Available) return result;
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                result.Add(new SteamFriend { SteamId = friend.m_SteamID.ToString(), Name = SteamFriends.GetFriendPersonaName(friend) });
            }
            return result;
        }

        /// <summary>
        /// Rebind all asynchronous stats/upload state to the *current* local
        /// Steam account. Any change forces a fresh RequestCurrentStats before
        /// this instance is allowed to publish more achievements.
        /// </summary>
        bool EnsureAchievementAccount()
        {
            if (!_initialized) return false;
            ulong activeId = SteamUser.GetSteamID().m_SteamID;
            if (_achievementQueue.ObserveAccount(activeId))
            {
                _statsReady = false;
                _storePending = false;
                _pendingStoreAccount = 0UL;
                _nextStatsRequest = 0f;
                _nextStoreAttempt = 0f;
                // Steam avatars are also account-owned. Do not show the
                // previous user's cached texture after an identity change.
                if (_avatar != null)
                {
                    UnityEngine.Object.Destroy(_avatar);
                    _avatar = null;
                }
            }
            return _achievementQueue.BelongsTo(activeId);
        }

        // Steamworks stats arrive asynchronously. Do not call SetAchievement
        // until UserStatsReceived confirms the current local Steam account.
        void RequestAchievementStats()
        {
            if (!EnsureAchievementAccount()) return;
            _nextStatsRequest = Time.unscaledTime + 15f;
            if (_statsReady) return;
            if (!SteamUserStats.RequestCurrentStats())
                Debug.LogWarning("City Highways: Steam stats not ready; will retry.");
        }

        void OnStatsReceived(UserStatsReceived_t result)
        {
            if (!EnsureAchievementAccount() ||
                result.m_steamIDUser.m_SteamID != _achievementQueue.AccountId)
                return;
            _statsReady = result.m_eResult == EResult.k_EResultOK;
            if (!_statsReady)
                _nextStatsRequest = Time.unscaledTime + 15f;
        }

        /// <summary>
        /// Backward-compatible publisher API for legacy callers. The normal
        /// profile bridge uses the account-bound overload below.
        /// </summary>
        public void QueueUnlockedAchievements(IReadOnlyCollection<string> achievementIds)
        {
            if (achievementIds == null || !Available ||
                !EnsureAchievementAccount()) return;
            TryQueueUnlockedAchievementsForAccount(
                _achievementQueue.AccountId.ToString(), achievementIds);
        }

        /// <summary>
        /// Publish only achievements from the profile for the exact currently
        /// authenticated SteamID. No amount of local dispatch caching can
        /// authorize a prior account after Steam changed mid-frame.
        /// </summary>
        public bool TryQueueUnlockedAchievementsForAccount(string sourceSteamId,
            IReadOnlyCollection<string> achievementIds)
        {
            if (achievementIds == null || !Available ||
                !EnsureAchievementAccount()) return false;
            if (!string.Equals(sourceSteamId,
                    _achievementQueue.AccountId.ToString(), StringComparison.Ordinal))
                return false;

            _achievementQueue.QueueForAccount(
                _achievementQueue.AccountId, achievementIds);
            if (_statsReady) FlushQueuedAchievements();
            else if (Time.unscaledTime >= _nextStatsRequest)
                RequestAchievementStats();
            return true;
        }

        void FlushQueuedAchievements()
        {
            if (!EnsureAchievementAccount() || !_statsReady || _storePending ||
                Time.unscaledTime < _nextStoreAttempt ||
                _achievementQueue.PendingCount == 0)
                return;

            // Take a snapshot so already-unlocked remote IDs can leave the
            // queue safely without mutating an enumerated HashSet.
            foreach (string id in _achievementQueue.PendingSnapshot())
            {
                if (!SteamUserStats.GetAchievement(id, out bool achieved))
                {
                    if (_achievementQueue.FirstUnknownId(id))
                        Debug.LogWarning("City Highways: Steam achievement API Name '" + id +
                            "' is unavailable. Check published Steamworks Partner achievements.");
                    continue; // Unknown or unpublished Steamworks API name.
                }
                if (achieved)
                {
                    // If a previous StoreStats failed, the local Steam cache
                    // can still say achieved; retry persistence instead.
                    _achievementQueue.ForgetPersisted(id);
                }
                else if (SteamUserStats.SetAchievement(id))
                    _achievementQueue.Stage(id);
            }

            // Invalid/unpublished Steam API IDs must not create an endless
            // StoreStats loop; retry them on a later stats synchronization.
            if (_achievementQueue.StagedCount == 0)
            {
                _nextStoreAttempt = Time.unscaledTime + 60f;
                return;
            }

            if (SteamUserStats.StoreStats())
            {
                _storePending = true;
                _pendingStoreAccount = _achievementQueue.AccountId;
            }
            else
                _nextStoreAttempt = Time.unscaledTime + 30f;
        }

        void OnStatsStored(UserStatsStored_t result)
        {
            if (!EnsureAchievementAccount() || !_storePending ||
                !_achievementQueue.BelongsTo(_pendingStoreAccount))
                return;

            ulong storeAccount = _pendingStoreAccount;
            _storePending = false;
            _pendingStoreAccount = 0UL;
            if (result.m_eResult == EResult.k_EResultOK)
            {
                _achievementQueue.CompleteStore(storeAccount, true);
            }
            else
            {
                // Keep all staged changes for this account's delayed retry.
                _nextStoreAttempt = Time.unscaledTime + 30f;
            }
        }

        public void Poll()
        {
            if (!_initialized) return;
            // Check identity before any Steam callback is allowed to
            // complete an upload from a previous signed-in account.
            EnsureAchievementAccount();
            SteamAPI.RunCallbacks();
            if (!EnsureAchievementAccount()) return;
            if (!_statsReady && Time.unscaledTime >= _nextStatsRequest)
                RequestAchievementStats();
            if (_statsReady) FlushQueuedAchievements();
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            _statsReady = false;
            _storePending = false;
            _pendingStoreAccount = 0UL;
            _statsReceived?.Dispose();
            _statsStored?.Dispose();
            _achievementQueue.Reset();
            if (_avatar != null)
            {
                UnityEngine.Object.Destroy(_avatar);
                _avatar = null;
            }
            if (NetPlatforms.SteamAchievements == this)
                NetPlatforms.SteamAchievements = null;
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

        public void SetJoinable(bool joinable)
        {
            // Лобби не закрываем — иначе отключившийся друг не вернётся; просто помечаем, что партия идёт.
            if (IsHost && _lobby.IsValid()) SteamMatchmaking.SetLobbyData(_lobby, "started", joinable ? "0" : "1");
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
