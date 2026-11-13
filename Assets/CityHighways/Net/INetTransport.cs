using System;
using System.Collections.Generic;

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
        INetTransport CreateHost(int maxMembers);
        INetTransport Join(ulong lobby);
        List<JoinableLobby> FriendsInLobbies();
        /// <summary>Друг пригласил нас, и мы нажали «Принять» в Steam (или запустили игру с +connect_lobby).</summary>
        event Action<ulong> JoinRequested;
        /// <summary>Отложенное приглашение из командной строки; 0 — нет. Читается один раз.</summary>
        ulong TakePendingJoin();
        void Poll();
        void Shutdown();
    }

    public static class NetPlatforms
    {
        public static ISteamPlatform Steam;
    }
}
