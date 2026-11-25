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
                if (IsClosed) return;
                var message = NetCodec.Decode(data);
                if (message != null) OnMessage(peer, message);
            };
            Transport.PeerConnected += peer => { if (!IsClosed) OnPeerConnected(peer); };
            Transport.PeerDisconnected += peer => { if (!IsClosed) OnPeerDisconnected(peer); };
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
            var map = LoadMap(MapPath);
            var game = Game.Replay(map, start, history);
            Map = map;
            return game;
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
        // Host-only credentials keyed by the seat object, unaffected by lobby reindexing.
        readonly Dictionary<NetSeat, string> _reconnectKeys = new Dictionary<NetSeat, string>();

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
                if (Seats[i].peer == key && Seats[i].connected && !Seats[i].isBot) return i;
            return -1;
        }

        // ---------------------------------------------------------------- Лобби

        public bool CanAddSeat => !IsClosed && !InGame && Seats.Count < MaxSeats;

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
            if (IsClosed || InGame || seat <= 0 || seat >= Seats.Count) return;
            var removed = Seats[seat];
            Seats.RemoveAt(seat);
            _reconnectKeys.Remove(removed);
            if (!removed.isBot && removed.PeerId != 0)
                Send(removed.PeerId, new NetMessage { type = NetMessage.Bye, text = "Хост убрал вас из лобби." });
            BroadcastLobby();
        }

        public void SetCharacter(int seat, int character)
        {
            if (IsClosed || InGame || seat < 0 || seat >= Seats.Count) return;
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
            if (IsClosed || InGame) return;
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

        public override ActionResult Perform(Func<Game, ActionResult> action)
        {
            if (IsClosed) return ActionResult.Fail("Связь потеряна.");
            if (Game == null) return ActionResult.Fail("Партия ещё не началась.");
            return action(Game);
        }

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
                _reconnectKeys.Remove(Seats[seat]);
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
            if (peer == 0 || peer == Transport.LocalId) return;
            if (SeatOf(peer) >= 0) return;
            if (!ReconnectCredentialCache.IsValid(message.reconnectKey))
            {
                Send(peer, new NetMessage { type = NetMessage.Reject, text = "Не удалось подтвердить ключ подключения." });
                return;
            }
            string name = Clean(message.name, "Игрок");
            string key = peer.ToString();

            if (InGame)
            {
                // Peer IDs may change on reconnect; names are not credentials.
                int back = Seats.FindIndex(s => !s.connected && s.isBot &&
                    _reconnectKeys.TryGetValue(s, out var credential) &&
                    string.Equals(credential, message.reconnectKey, StringComparison.Ordinal));
                if (back < 0)
                {
                    Send(peer, new NetMessage { type = NetMessage.Reject, text = "Партия уже идёт. Не найдено ваше отключённое место." });
                    return;
                }
                Seats[back].peer = key;
                Seats[back].connected = true;
                Seats[back].emblem = message.emblem ?? Seats[back].emblem;
                Seats[back].title = message.title ?? Seats[back].title;
                Seats[back].frame = message.frame ?? Seats[back].frame;
                Seats[back].outfit = message.outfit ?? Seats[back].outfit;
                Seats[back].clubTag = message.clubTag ?? Seats[back].clubTag;
                Seats[back].clubLogo = message.clubLogo ?? Seats[back].clubLogo;
                Seats[back].isBot = false;
                Send(peer, StartMessage(back));
                RaiseNotice($"{Seats[back].name} вернулся в партию.");
                BroadcastSeats();
                return;
            }
            if (_reconnectKeys.Values.Contains(message.reconnectKey))
            {
                Send(peer, new NetMessage { type = NetMessage.Reject, text = "Это место уже занято." });
                return;
            }
            if (Seats.Count >= MaxSeats)
            {
                Send(peer, new NetMessage { type = NetMessage.Reject, text = "За столом уже нет мест." });
                return;
            }
            // Одинаковые имена путают — добавляем номер.
            string unique = name;
            for (int n = 2; Seats.Any(s => s.name == unique); n++) unique = Clean(name, "Игрок") + " " + n;
            var joined = new NetSeat
            {
                name = unique, peer = key, emblem = message.emblem ?? "", title = message.title ?? "", frame = message.frame ?? "", outfit = message.outfit ?? "",
                clubTag = message.clubTag ?? "", clubLogo = message.clubLogo ?? ""
            };
            _reconnectKeys.Add(joined, message.reconnectKey);
            Seats.Add(joined);
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
        readonly string _reconnectKey;
        bool _helloSent;
        readonly Func<double> _now;
        readonly ClientRecovery _recovery = new ClientRecovery();

        public ClientSession(INetTransport transport, Func<string, CityMap> loadMap, string name,
            Func<double> now = null, string reconnectKey = null) : base(transport, loadMap)
        {
            _name = name;
            _reconnectKey = ReconnectCredentialCache.IsValid(reconnectKey) ? reconnectKey : ReconnectCredentialCache.NewKey();
            _now = now ?? (() => (double)System.Diagnostics.Stopwatch.GetTimestamp() /
                System.Diagnostics.Stopwatch.Frequency);
            Transport.Ready += SendHello;
        }

        public override bool IsHost => false;
        public bool Joined => Seats.Count > 0;
        /// <summary>Оформление своего профиля — уходит хосту в hello.</summary>
        public string Emblem = "", Title = "", Frame = "", Outfit = "";
        /// <summary>Клуб строителей — тег и логотип для лобби.</summary>
        public string ClubTag = "", ClubLogo = "";
        public override bool AwaitingHost => !IsClosed && _recovery.Pending;
        public bool Synchronizing => !IsClosed && _recovery.Synchronizing;

        void SendHello()
        {
            if (IsClosed || _helloSent) return;
            _helloSent = true;
            Send(Transport.HostId, new NetMessage
            {
                type = NetMessage.Hello, version = NetCodec.ProtocolVersion, name = _name,
                reconnectKey = _reconnectKey,
                emblem = Emblem, title = Title, frame = Frame, outfit = Outfit, clubTag = ClubTag, clubLogo = ClubLogo
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
            if (IsClosed) return;
            switch (_recovery.Tick(_now()))
            {
                case ClientRecovery.NextAction.RequestSync:
                    SendSyncRequest();
                    break;
                case ClientRecovery.NextAction.Close:
                    RaiseClosed("Хост не ответил на запрос синхронизации.");
                    break;
            }
        }

        void SendSyncRequest() => Send(Transport.HostId, new NetMessage { type = NetMessage.SyncRequest });

        void RequestSync()
        {
            if (IsClosed || !_recovery.BeginSync(_now())) return;
            SendSyncRequest();
        }

        public override ActionResult Perform(Func<Game, ActionResult> action)
        {
            if (IsClosed) return ActionResult.Fail("Связь потеряна.");
            if (Game == null) return ActionResult.Fail("Партия ещё не началась.");
            if (AwaitingHost) return ActionResult.Fail("Ждём ответа хоста…");
            // Копия партии по истории: действие проверяется по тем же правилам, но настоящая партия
            // меняется только командами от хоста — так у всех одинаковый порядок ходов.
            Game copy;
            try { copy = Game.Replay(Map, Game.StartInfo, Game.History); }
            catch (Exception)
            {
                RequestSync();
                return ActionResult.Fail("Не удалось проверить ход. Ждём синхронизации с хостом.");
            }
            int before = copy.History.Count;
            var result = action(copy);
            if (copy.History.Count > before)
            {
                _recovery.BeginSubmit(_now());
                Send(Transport.HostId, new NetMessage
                {
                    type = NetMessage.Submit,
                    from = Game.History.Count,
                    commands = copy.History.Skip(before).ToArray()
                });
            }
            return result;
        }

        protected override void OnMessage(ulong peer, NetMessage message)
        {
            if (peer != Transport.HostId) return;
            switch (message.type)
            {
                case NetMessage.Lobby:
                    if (InGame) break;
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
                    if (InGame) break;
                    try
                    {
                        if (message.start?.players == null || message.seats == null ||
                            message.seats.Length != message.start.players.Length ||
                            message.seat < 0 || message.seat >= message.seats.Length ||
                            message.seats.Any(seat => seat == null) || string.IsNullOrEmpty(message.map))
                            throw new InvalidOperationException("Некорректные начальные условия.");
                        MapPath = message.map;
                        Game = BuildGame(message.start, message.commands);
                        Seats = message.seats.ToList();
                        LocalSeat = message.seat;
                        TurnSeconds = message.turnSeconds;
                        InGame = true;
                        _recovery.Complete();
                    }
                    catch (Exception e)
                    {
                        RaiseClosed("Не удалось начать сетевую партию: " + e.Message);
                        break;
                    }
                    RaiseStarted();
                    break;
                case NetMessage.Seats:
                    if (!InGame || message.seats == null || message.seats.Length != Seats.Count ||
                        message.seats.Any(seat => seat == null)) break;
                    Seats = message.seats.ToList();
                    RaiseSeatsChanged();
                    break;
                case NetMessage.Commands:
                    Apply(message);
                    break;
                case NetMessage.Sync:
                    if (!InGame || Game == null) break;
                    // A delayed snapshot must never roll back commands already accepted locally.
                    if ((message.commands?.Length ?? 0) < Game.History.Count) break;
                    try
                    {
                        if (message.start?.players == null || message.start.players.Length != Seats.Count ||
                            (message.seats != null && (message.seats.Length != Seats.Count ||
                             message.seats.Any(seat => seat == null))))
                            throw new InvalidOperationException("Некорректный состав партии.");
                        Game = BuildGame(message.start, message.commands);
                        if (message.seats != null) Seats = message.seats.ToList();
                    }
                    catch (Exception e)
                    {
                        RaiseClosed("Не удалось восстановить партию: " + e.Message);
                        return;
                    }
                    _recovery.Complete();
                    RaiseResynced();
                    break;
            }
        }

        void Apply(NetMessage message)
        {
            if (Game == null || _recovery.Synchronizing) return;
            int have = Game.History.Count;
            if (message.from < 0 || message.from > have || message.commands == null)
            {
                RequestSync();
                return;
            }
            // Duplicate/empty batches cannot acknowledge a pending move.
            int skip = have - message.from;
            if (skip >= message.commands.Length) return;
            try
            {
                for (int i = skip; i < message.commands.Length; i++)
                {
                    int before = Game.History.Count;
                    if (!Game.Execute(message.commands[i]).Success || Game.History.Count != before + 1)
                    {
                        RequestSync();
                        return;
                    }
                }
            }
            catch (Exception)
            {
                RequestSync();
                return;
            }
            _recovery.Complete();
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
