using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;

namespace CityHighways.Net
{
    /// <summary>
    /// Сетевая партия поверх любого канала. Хост держит настоящую партию и рассылает принятые команды,
    /// клиент применяет их по порядку. Игровой код не знает о сети: он вызывает Perform(действие),
    /// а сессия решает, выполнить ли действие сразу (хост) или проверить на копии и отправить хосту (клиент).
    /// </summary>
    public abstract class NetSession : IDisposable
    {
        public const int MaxSeats = 5;

        protected readonly Func<string, CityMap> LoadMap;

        protected NetSession(INetTransport transport, Func<string, CityMap> loadMap)
        {
            Transport = transport;
            LoadMap = loadMap;
            Transport.Received += (peer, data) =>
            {
                var message = NetCodec.Decode(data);
                if (message != null) OnMessage(peer, message);
            };
            Transport.PeerConnected += OnPeerConnected;
            Transport.PeerDisconnected += OnPeerDisconnected;
            Transport.Failed += text => RaiseClosed(text);
        }

        public INetTransport Transport { get; }
        public abstract bool IsHost { get; }
        public List<NetSeat> Seats { get; protected set; } = new List<NetSeat>();
        public int LocalSeat { get; protected set; } = -1;
        public bool InGame { get; protected set; }
        public bool IsClosed { get; private set; }
        public string MapPath { get; protected set; } = "";
        public int TurnSeconds { get; protected set; }

        /// <summary>Партия этой сессии (после старта). Клиент заменяет её целиком при пересинхронизации.</summary>
        public Game Game { get; protected set; }
        public CityMap Map { get; protected set; }

        /// <summary>Состав стола изменился (лобби или замена вышедшего игрока ботом).</summary>
        public event Action SeatsChanged;
        /// <summary>Партия началась (или клиент переподключился к идущей).</summary>
        public event Action Started;
        /// <summary>Клиент получил партию заново целиком — интерфейсу нужно перепривязаться к новому Game.</summary>
        public event Action Resynced;
        /// <summary>Сессия закончилась: хост ушёл, нас отключили, связь потеряна. Текст — для игрока.</summary>
        public event Action<string> Closed;
        /// <summary>Сообщение без разрыва (например, хост отклонил ход).</summary>
        public event Action<string> Notice;

        protected void RaiseSeatsChanged() => SeatsChanged?.Invoke();
        protected void RaiseStarted() => Started?.Invoke();
        protected void RaiseResynced() => Resynced?.Invoke();
        protected void RaiseNotice(string text) => Notice?.Invoke(text);

        protected void RaiseClosed(string text)
        {
            if (IsClosed) return;
            IsClosed = true;
            Closed?.Invoke(text);
        }

        public bool IsBotSeat(int seat) => seat >= 0 && seat < Seats.Count && Seats[seat].isBot;

        public virtual void Poll()
        {
            if (!IsClosed) Transport.Poll();
        }

        /// <summary>
        /// Выполнить действие местного игрока. action получает партию, на которой нужно действовать:
        /// у хоста — настоящую, у клиента — копию для проверки (принятое хостом придёт обратно командами).
        /// </summary>
        public abstract ActionResult Perform(Func<Game, ActionResult> action);

        /// <summary>Клиент ждёт ответа хоста на свой ход — новых действий пока не принимаем.</summary>
        public virtual bool AwaitingHost => false;

        protected void Send(ulong peer, NetMessage message) => Transport.Send(peer, NetCodec.Encode(message));

        protected abstract void OnMessage(ulong peer, NetMessage message);
        protected abstract void OnPeerConnected(ulong peer);
        protected abstract void OnPeerDisconnected(ulong peer);

        protected Game BuildGame(GameStartInfo start, IEnumerable<GameCommand> history)
        {
            Map = LoadMap(MapPath);
            return Game.Replay(Map, start, history);
        }

        public virtual void Dispose()
        {
            IsClosed = true;
            Transport.Dispose();
        }
    }

    // ====================================================================== Хост

    public sealed class HostSession : NetSession
    {
        int _sent;

        public HostSession(INetTransport transport, Func<string, CityMap> loadMap, string hostName) : base(transport, loadMap)
        {
            Seats.Add(new NetSeat { name = Clean(hostName, "Хост") });
            LocalSeat = 0;
        }

        public override bool IsHost => true;

        static string Clean(string name, string fallback)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) name = fallback;
            return name.Length > 16 ? name.Substring(0, 16) : name;
        }

        int SeatOf(ulong peer)
        {
            string key = peer.ToString();
            for (int i = 0; i < Seats.Count; i++)
                if (Seats[i].peer == key && !Seats[i].isBot) return i;
            return -1;
        }

        // ---------------------------------------------------------------- Лобби

        public bool CanAddSeat => !InGame && Seats.Count < MaxSeats;

        public void AddBot(int botLevel)
        {
            if (!CanAddSeat) return;
            int bots = Seats.Count(s => s.isBot);
            Seats.Add(new NetSeat { name = "Бот " + (bots + 1), isBot = true, botLevel = botLevel });
            BroadcastLobby();
        }

        /// <summary>Убрать место: бота — просто, человека — с сообщением «вас отключили».</summary>
        public void RemoveSeat(int seat)
        {
            if (InGame || seat <= 0 || seat >= Seats.Count) return;
            var removed = Seats[seat];
            Seats.RemoveAt(seat);
            if (!removed.isBot && removed.PeerId != 0)
                Send(removed.PeerId, new NetMessage { type = NetMessage.Bye, text = "Хост убрал вас из лобби." });
            BroadcastLobby();
        }

        public void SetCharacter(int seat, int character)
        {
            if (InGame || seat < 0 || seat >= Seats.Count) return;
            Seats[seat].character = character;
            BroadcastLobby();
        }

        void BroadcastLobby()
        {
            for (int i = 0; i < Seats.Count; i++)
            {
                var s = Seats[i];
                if (s.isBot || s.PeerId == 0) continue;
                Send(s.PeerId, new NetMessage { type = NetMessage.Lobby, seats = Seats.ToArray(), seat = i, map = MapPath });
            }
            RaiseSeatsChanged();
        }

        void BroadcastSeats()
        {
            foreach (var s in Seats)
                if (!s.isBot && s.PeerId != 0 && s.connected)
                    Send(s.PeerId, new NetMessage { type = NetMessage.Seats, seats = Seats.ToArray() });
            RaiseSeatsChanged();
        }

        // ---------------------------------------------------------------- Старт

        /// <summary>
        /// Начать партию: start собирает меню (имена по местам, персонажи, зерно, правила). Каждый клиент
        /// получает начальные условия и свой номер места; у всех строится одна и та же партия.
        /// </summary>
        public void StartGame(GameStartInfo start, string mapPath, int turnSeconds)
        {
            if (InGame) return;
            MapPath = mapPath;
            TurnSeconds = turnSeconds;
            // Хост строит партию из того же JSON, что получат клиенты: числа правил совпадут до бита.
            start = NetCodec.RoundTrip(start);
            Game = BuildGame(start, null);
            _sent = Game.History.Count;
            InGame = true;
            Transport.SetJoinable(false); // новых игроков в идущую партию не пускаем (вернуться можно)
            for (int i = 0; i < Seats.Count; i++)
            {
                var s = Seats[i];
                if (s.isBot || s.PeerId == 0) continue;
                Send(s.PeerId, StartMessage(i));
            }
            RaiseStarted();
        }

        NetMessage StartMessage(int seat) => new NetMessage
        {
            type = NetMessage.Start,
            seat = seat,
            start = Game.StartInfo,
            commands = Game.History.ToArray(),
            seats = Seats.ToArray(),
            map = MapPath,
            turnSeconds = TurnSeconds
        };

        // ---------------------------------------------------------------- Ход партии

        public override ActionResult Perform(Func<Game, ActionResult> action) => action(Game);

        public override void Poll()
        {
            base.Poll();
            Flush();
        }

        /// <summary>Разослать команды, появившиеся с прошлого раза (ходы хоста, ботов и принятые ходы клиентов).</summary>
        public void Flush()
        {
            if (Game == null || IsClosed || Game.History.Count <= _sent) return;
            var message = new NetMessage
            {
                type = NetMessage.Commands,
                from = _sent,
                commands = Game.History.Skip(_sent).ToArray()
            };
            _sent = Game.History.Count;
            var data = NetCodec.Encode(message);
            foreach (var s in Seats)
                if (!s.isBot && s.PeerId != 0 && s.connected) Transport.Send(s.PeerId, data);
        }

        protected override void OnPeerConnected(ulong peer)
        {
            // Ждём hello с именем и версией.
        }

        protected override void OnPeerDisconnected(ulong peer)
        {
            int seat = SeatOf(peer);
            if (seat < 0) return;
            if (!InGame)
            {
                Seats.RemoveAt(seat);
                BroadcastLobby();
                return;
            }
            // Во время партии место не пропадает: за игрока доигрывает бот, а сам он может вернуться.
            Seats[seat].connected = false;
            Seats[seat].isBot = true;
            RaiseNotice($"{Seats[seat].name} отключился — за него играет бот.");
            BroadcastSeats();
        }

        protected override void OnMessage(ulong peer, NetMessage message)
        {
            switch (message.type)
            {
                case NetMessage.Hello:
                    OnHello(peer, message);
                    break;
                case NetMessage.Submit:
                    OnSubmit(peer, message);
                    break;
                case NetMessage.SyncRequest:
                    if (InGame && SeatOf(peer) >= 0)
                        Send(peer, new NetMessage { type = NetMessage.Sync, start = Game.StartInfo, commands = Game.History.ToArray(), seats = Seats.ToArray() });
                    break;
                case NetMessage.Bye:
                    OnPeerDisconnected(peer);
                    break;
            }
        }

        void OnHello(ulong peer, NetMessage message)
        {
            if (message.version != NetCodec.ProtocolVersion)
            {
                Send(peer, new NetMessage { type = NetMessage.Reject, text = "Разные версии игры: обновите City Highways у всех игроков." });
                return;
            }
            string name = Clean(message.name, "Игрок");
            string key = peer.ToString();

            if (InGame)
            {
                // Вернуться можно на своё место (по сетевому id или по имени), пока за него играет бот.
                int back = Seats.FindIndex(s => !s.connected && (s.peer == key || s.name == name));
                if (back < 0)
                {
                    Send(peer, new NetMessage { type = NetMessage.Reject, text = "Партия уже идёт." });
                    return;
                }
                Seats[back].peer = key;
                Seats[back].connected = true;
                Seats[back].emblem = message.emblem ?? Seats[back].emblem;
                Seats[back].title = message.title ?? Seats[back].title;
                Seats[back].frame = message.frame ?? Seats[back].frame;
                Seats[back].outfit = message.outfit ?? Seats[back].outfit;
                Seats[back].isBot = false;
                Send(peer, StartMessage(back));
                RaiseNotice($"{name} вернулся в партию.");
                BroadcastSeats();
                return;
            }
            if (SeatOf(peer) >= 0) return;
            if (Seats.Count >= MaxSeats)
            {
                Send(peer, new NetMessage { type = NetMessage.Reject, text = "За столом уже нет мест." });
                return;
            }
            // Одинаковые имена путают — добавляем номер.
            string unique = name;
            for (int n = 2; Seats.Any(s => s.name == unique); n++) unique = Clean(name, "Игрок") + " " + n;
            Seats.Add(new NetSeat { name = unique, peer = key, emblem = message.emblem ?? "", title = message.title ?? "", frame = message.frame ?? "", outfit = message.outfit ?? "" });
            BroadcastLobby();
        }

        void OnSubmit(ulong peer, NetMessage message)
        {
            int seat = SeatOf(peer);
            if (!InGame || seat < 0 || message.commands == null) return;
            if (message.from != Game.History.Count)
            {
                // Клиент отстал или опередил — пусть возьмёт партию целиком.
                Send(peer, new NetMessage { type = NetMessage.Sync, start = Game.StartInfo, commands = Game.History.ToArray(), seats = Seats.ToArray() });
                return;
            }
            foreach (var command in message.commands)
            {
                string error = null;
                if (command == null) error = "Пустая команда.";
                else if (command.player != seat) error = "Это не ваш ход.";
                else
                {
                    var result = Game.Execute(command);
                    if (!result.Success) error = result.Error;
                }
                if (error != null)
                {
                    Send(peer, new NetMessage { type = NetMessage.Reject, text = "Хост не принял ход: " + error });
                    Send(peer, new NetMessage { type = NetMessage.Sync, start = Game.StartInfo, commands = Game.History.ToArray(), seats = Seats.ToArray() });
                    break;
                }
            }
            Flush();
        }

        public override void Dispose()
        {
            foreach (var s in Seats)
                if (!s.isBot && s.PeerId != 0 && s.connected)
                    Send(s.PeerId, new NetMessage { type = NetMessage.Bye, text = "Хост закрыл игру." });
            Transport.Poll();
            base.Dispose();
        }
    }

    // ====================================================================== Клиент

    public sealed class ClientSession : NetSession
    {
        readonly string _name;
        bool _helloSent;
        DateTime _awaitingSince = DateTime.MinValue;
        const double AwaitTimeoutSeconds = 6.0;

        public ClientSession(INetTransport transport, Func<string, CityMap> loadMap, string name) : base(transport, loadMap)
        {
            _name = name;
            Transport.Ready += SendHello;
        }

        public override bool IsHost => false;
        public bool Joined => Seats.Count > 0;
        /// <summary>Оформление своего профиля — уходит хосту в hello.</summary>
        public string Emblem = "", Title = "", Frame = "", Outfit = "";
        public override bool AwaitingHost => _awaitingSince != DateTime.MinValue;

        void SendHello()
        {
            if (_helloSent) return;
            _helloSent = true;
            Send(Transport.HostId, new NetMessage
            {
                type = NetMessage.Hello, version = NetCodec.ProtocolVersion, name = _name,
                emblem = Emblem, title = Title, frame = Frame, outfit = Outfit
            });
        }

        protected override void OnPeerConnected(ulong peer)
        {
            if (peer == Transport.HostId) SendHello();
        }

        protected override void OnPeerDisconnected(ulong peer)
        {
            if (peer == Transport.HostId) RaiseClosed("Хост вышел из игры.");
        }

        public override void Poll()
        {
            base.Poll();
            if (AwaitingHost && (DateTime.UtcNow - _awaitingSince).TotalSeconds > AwaitTimeoutSeconds)
            {
                _awaitingSince = DateTime.MinValue;
                RequestSync();
            }
        }

        void RequestSync() => Send(Transport.HostId, new NetMessage { type = NetMessage.SyncRequest });

        public override ActionResult Perform(Func<Game, ActionResult> action)
        {
            if (Game == null) return ActionResult.Fail("Партия ещё не началась.");
            if (AwaitingHost) return ActionResult.Fail("Ждём ответа хоста…");
            // Копия партии по истории: действие проверяется по тем же правилам, но настоящая партия
            // меняется только командами от хоста — так у всех одинаковый порядок ходов.
            var copy = Game.Replay(Map, Game.StartInfo, Game.History);
            int before = copy.History.Count;
            var result = action(copy);
            if (copy.History.Count > before)
            {
                Send(Transport.HostId, new NetMessage
                {
                    type = NetMessage.Submit,
                    from = Game.History.Count,
                    commands = copy.History.Skip(before).ToArray()
                });
                _awaitingSince = DateTime.UtcNow;
            }
            return result;
        }

        protected override void OnMessage(ulong peer, NetMessage message)
        {
            if (peer != Transport.HostId) return;
            switch (message.type)
            {
                case NetMessage.Lobby:
                    Seats = (message.seats ?? new NetSeat[0]).ToList();
                    LocalSeat = message.seat;
                    MapPath = message.map ?? MapPath;
                    RaiseSeatsChanged();
                    break;
                case NetMessage.Reject:
                    if (InGame) RaiseNotice(message.text);
                    else RaiseClosed(message.text);
                    break;
                case NetMessage.Bye:
                    RaiseClosed(string.IsNullOrEmpty(message.text) ? "Хост закрыл игру." : message.text);
                    break;
                case NetMessage.Start:
                    Seats = (message.seats ?? new NetSeat[0]).ToList();
                    LocalSeat = message.seat;
                    MapPath = message.map;
                    TurnSeconds = message.turnSeconds;
                    Game = BuildGame(message.start, message.commands);
                    InGame = true;
                    _awaitingSince = DateTime.MinValue;
                    RaiseStarted();
                    break;
                case NetMessage.Seats:
                    Seats = (message.seats ?? new NetSeat[0]).ToList();
                    RaiseSeatsChanged();
                    break;
                case NetMessage.Commands:
                    Apply(message);
                    break;
                case NetMessage.Sync:
                    if (message.seats != null) Seats = message.seats.ToList();
                    try
                    {
                        Game = BuildGame(message.start, message.commands);
                    }
                    catch (Exception e)
                    {
                        RaiseClosed("Не удалось восстановить партию: " + e.Message);
                        return;
                    }
                    _awaitingSince = DateTime.MinValue;
                    RaiseResynced();
                    break;
            }
        }

        void Apply(NetMessage message)
        {
            if (Game == null || message.commands == null) return;
            int have = Game.History.Count;
            if (message.from > have)
            {
                RequestSync(); // пропустили часть ходов
                return;
            }
            for (int i = 0; i < message.commands.Length; i++)
            {
                if (message.from + i < have) continue; // уже применено
                var result = Game.Execute(message.commands[i]);
                if (!result.Success)
                {
                    RequestSync();
                    return;
                }
            }
            _awaitingSince = DateTime.MinValue;
        }

        public override void Dispose()
        {
            if (!IsClosed && Transport.IsReady)
            {
                Send(Transport.HostId, new NetMessage { type = NetMessage.Bye });
                Transport.Poll();
            }
            base.Dispose();
        }
    }
}
