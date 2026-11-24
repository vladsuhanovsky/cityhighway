using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Net
{
    /// <summary>
    /// Канал связи «хост — клиенты»: Steam, локальная сеть (TCP) или петля в памяти для тестов.
    /// Все события приходят только из Poll() — то есть в главном потоке Unity.
    /// </summary>
    public interface INetTransport : IDisposable
    {
        bool IsHost { get; }
        /// <summary>Свой id; у клиента HostId — id хоста.</summary>
        ulong LocalId { get; }
        ulong HostId { get; }
        /// <summary>Готов к работе: хост открыл лобби, клиент соединился с хостом.</summary>
        bool IsReady { get; }
        /// <summary>Короткое название канала для интерфейса: «Steam», «Локальная сеть».</summary>
        string Kind { get; }
        /// <summary>Подсказка, как к вам подключиться (адреса для локальной сети, «пригласите через Steam»).</summary>
        string ConnectHint { get; }

        event Action Ready;
        event Action<ulong> PeerConnected;
        event Action<ulong> PeerDisconnected;
        event Action<ulong, byte[]> Received;
        /// <summary>Не удалось открыть или связь потеряна; текст — для игрока.</summary>
        event Action<string> Failed;

        void Send(ulong peer, byte[] data);
        void Poll();

        /// <summary>Можно ли пригласить друга (оверлей Steam).</summary>
        bool CanInvite { get; }
        void Invite();

        /// <summary>
        /// Хост: показывать ли лобби как открытое для новых игроков. После старта — нет (лобби пропадает из
        /// списков), но канал остаётся: отключившийся игрок может вернуться на своё место.
        /// </summary>
        void SetJoinable(bool joinable);
    }

    /// <summary>Друг в Steam (для рекордов друзей и рейдов).</summary>
    public struct SteamFriend
    {
        public string SteamId;
        public string Name;
    }

    /// <summary>Друг, который сейчас в лобби City Highways (для списка «К кому подключиться»).</summary>
    public struct JoinableLobby
    {
        public string Name;
        public ulong Lobby;
    }

    /// <summary>
    /// Платформа Steam. Реализация лежит в отдельной сборке CityHighways.Steam, которая собирается только
    /// при установленном Steamworks.NET и регистрирует себя здесь. Без Steamworks.NET Steam == null.
    /// </summary>
    public interface ISteamPlatform
    {
        /// <summary>Steam запущен и SteamAPI инициализирован.</summary>
        bool Available { get; }
        /// <summary>Почему недоступен (для подсказки в меню).</summary>
        string Problem { get; }
        string PersonaName { get; }
        /// <summary>Свой SteamID строкой (для привязки профиля).</summary>
        string SteamId { get; }
        /// <summary>
        /// Билет входа для своего сервера (ISteamUser::GetAuthTicketForWebApi): done получает билет в hex или null
        /// (Steam недоступен, отказ). Сервер проверяет его у Steam и так узнаёт SteamID игрока.
        /// </summary>
        void RequestWebApiTicket(string identity, Action<string> done);
        /// <summary>Большой аватар из Steam; null — ещё грузится или его нет (спросите позже).</summary>
        Texture2D Avatar { get; }
        INetTransport CreateHost(int maxMembers);
        INetTransport Join(ulong lobby);
        List<JoinableLobby> FriendsInLobbies();
        /// <summary>Все друзья из списка Steam (SteamID и ник) — по ним сервер находит рекорды друзей.</summary>
        List<SteamFriend> Friends();
        /// <summary>Друг пригласил нас, и мы нажали «Принять» в Steam (или запустили игру с +connect_lobby).</summary>
        event Action<ulong> JoinRequested;
        /// <summary>Отложенное приглашение из командной строки; 0 — нет. Читается один раз.</summary>
        ulong TakePendingJoin();
        void Poll();
        void Shutdown();
    }

    /// <summary>Открытое лобби Unity (для списка «К кому подключиться»).</summary>
    public struct OpenLobby
    {
        public string Name;
        public string Id;
        public int Players;
        public int MaxPlayers;
    }

    /// <summary>
    /// Unity Gaming Services: Lobby (поиск игры, код лобби) и Relay (связь через серверы Unity, без проброса
    /// портов). Реализация — в сборке CityHighways.Ugs, которая собирается только при установленном пакете
    /// Multiplayer Services (com.unity.services.multiplayer) и сама регистрируется здесь.
    /// </summary>
    public interface IUgsPlatform
    {
        /// <summary>Создать лобби и выделить Relay; код лобби появится в ConnectHint, когда канал будет готов.</summary>
        INetTransport CreateHost(string lobbyName, int maxPlayers);
        INetTransport JoinByCode(string code);
        INetTransport JoinById(string lobbyId);
        /// <summary>
        /// Открытые лобби с местами; done(список, ошибка) вызывается в главном потоке.
        /// background = true — фоновая проверка (значок в меню): после сетевой ошибки она не ходит в сеть,
        /// пока не пройдёт пауза (1, 2, 4… до 15 минут), чтобы без интернета не засорять консоль повторами входа.
        /// </summary>
        void QueryLobbies(Action<List<OpenLobby>, string> done, bool background = false);
    }

    /// <summary>
    /// Optional Steam achievement publishing capability. Separate from
    /// ISteamPlatform so non-Steam builds and test fakes stay compatible.
    /// Implementation queues IDs and waits for user stats before publishing.
    /// </summary>
    public interface ISteamAchievements
    {
        void QueueUnlockedAchievements(IReadOnlyCollection<string> achievementIds);
    }

    public static class NetPlatforms
    {
        public static ISteamPlatform Steam;
        public static ISteamAchievements SteamAchievements;
        public static IUgsPlatform Ugs;
    }
}
