using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    public enum CargoKind
    {
        /// <summary>Обычный груз.</summary>
        Regular,
        /// <summary>Скоропорт: много очков, за каждый раунд сверх нормы — минус; едет быстрее.</summary>
        Perishable,
        /// <summary>Тяжеловес: только широкие дороги — бетон, мосты, магистрали; едет медленнее.</summary>
        Heavy,
        /// <summary>Опасный груз: въезд в Центр — взятка влиянием, иначе конфискация.</summary>
        Hazard,
        /// <summary>Хрупкий: каждая пробка на пути отнимает очки у груза.</summary>
        Fragile,
        /// <summary>Ценный: без охраны (1 влияние при погрузке) могут украсть в пути.</summary>
        Valuable,
        /// <summary>Негабаритный: только широкие дороги, медленно, и на его дороге другие фуры в этот раунд стоят.</summary>
        Oversized
    }

    public sealed class Cargo
    {
        public string Id;
        public CargoKind Kind;
        public string From, To;
        /// <summary>Очки за доставку (у скоропорта — до вычета за опоздание, у хрупкого — уже за вычетом пробок).</summary>
        public int Value;
        /// <summary>Скоропорт: за сколько раундов от появления доставка без штрафа.</summary>
        public int Norm;
        public int SpawnRound;
        /// <summary>Фура, которая везёт (-1 — ждёт на хабе).</summary>
        public int Truck = -1;
        public bool BribePaid, Escorted;
    }

    public sealed class Truck
    {
        public int Id;
        public int Owner;
        public string Location;
        /// <summary>Груз в кузове (null — пустая).</summary>
        public string Cargo;
        /// <summary>Груз, который фура заберёт на своём хабе в начале движения.</summary>
        public string PlannedCargo;
        /// <summary>Куда едет и маршрут (дороги по порядку); остаток переходит на следующий раунд.</summary>
        public string Target;
        public List<string> Route = new List<string>();
    }

    public enum ReputationLevel { Novice, Amateur, Professional, Master, Legendary }

    public sealed class MagnatePlayer
    {
        public int Index;
        public string Name;
        public bool IsBot;
        public int Score, Influence, BuildPoints;
        /// <summary>Репутация 0–120: растёт за доставки вовремя и заказы мэрии, падает за пробки и потери.</summary>
        public int Reputation;
        public int Delivered, Jams, TollIn, TollOut, Confiscated, Thefts, ContractsWon, PerishableOnTime, HubFees;

        public ReputationLevel Level =>
            Reputation >= 100 ? ReputationLevel.Legendary : Reputation >= 75 ? ReputationLevel.Master
            : Reputation >= 50 ? ReputationLevel.Professional : Reputation >= 25 ? ReputationLevel.Amateur : ReputationLevel.Novice;
    }

    public enum MagnatePhase { Build, Plan, GameOver }

    public enum MagnateDifficulty { Easy, Normal, Hard, Expert }

    /// <summary>Заказ мэрии: кто первым доставит Needed грузов нужного вида (или любых) до EndRound — получает награду.</summary>
    public sealed class LogisticsContract
    {
        public string Id, Title;
        /// <summary>Вид груза; null — любой.</summary>
        public CargoKind? Kind;
        public int Needed, StartRound, EndRound, RewardPoints, RewardInfluence;
        public int[] Progress;
        public int Winner = -1;
    }

    public enum LogisticsEventKind { Accident, RoadRepair, FuelShortage, CustomsCheck, Storm, Festival }

    public sealed class LogisticsEvent
    {
        public LogisticsEventKind Kind;
        public string Title, Description;
        /// <summary>Закрытая дорога (ДТП, ремонт).</summary>
        public string Road;
        public int StartRound, EndRound;
    }

    public enum StopReason { None, Jam, Oversized, Closed, Customs }

    /// <summary>Что случилось с фурой за раунд — для анимации и журнала.</summary>
    public sealed class TruckMove
    {
        public int Truck;
        public string From;
        /// <summary>Пройденные места по порядку (From — первое).</summary>
        public List<string> Stops = new List<string>();
        /// <summary>Перед какой дорогой встала и почему.</summary>
        public string StoppedBefore;
        public StopReason Reason;
        public string Delivered;
        public CargoKind? CargoKind;
        public int Points;
        public string Note;
    }

    public sealed class RoundReport
    {
        public int Round;
        public readonly List<TruckMove> Moves = new List<TruckMove>();
        public readonly HashSet<string> Jams = new HashSet<string>();
        public readonly Dictionary<string, int> Traffic = new Dictionary<string, int>();
        public readonly List<string> Messages = new List<string>();
    }

    /// <summary>Числа режима «Логистический магнат» — в одном месте для баланса.</summary>
    public sealed class MagnateConfig
    {
        public MagnateDifficulty Difficulty = MagnateDifficulty.Normal;
        public int Rounds = 15;
        public int TrucksPerPlayer = 3;
        /// <summary>Сколько участков дороги фура проезжает за раунд (скоропорт +1, тяжёлые −1, нехватка топлива −2).</summary>
        public int TruckSpeed = 6;
        public int StartBuildPoints = 6, BuildPointsPerRound = 4, MaxBuildPoints = 12;
        public int BridgeExtraCost = 2;
        public int StartInfluence = 2, InfluencePerRound = 1;
        /// <summary>Пропускная способность за раунд: улица, широкая дорога (бетон, мост), магистраль.</summary>
        public int CapacityStreet = 2, CapacityWide = 3, CapacityHighway = 4;
        public int TollPerRoad = 1;
        public int JamPenalty = 1;
        public int HazardBribe = 2, ConfiscationPenalty = 3;
        public int PerishableLatePenalty = 2;
        public int FragileJamPenalty = 2;
        public int EscortCost = 1, TheftPenalty = 2;
        public double TheftChance = 0.2;
        public int CustomsBribe = 1;
        public int HubFee = 1;
        /// <summary>Цена следующего уровня хаба (индекс — уровень, до которого растёт).</summary>
        public int[] HubUpgradeCost = { 0, 0, 3, 5, 7, 9 };
        public double SpawnChance = 0.65;
        public int CargoLifetime = 6;
        public double RewardMultiplier = 1.0;
        public double EventChance = 0.25;
        public int ActiveContracts = 2;
        public string BribeDistrict = "center";

        public static MagnateConfig For(MagnateDifficulty difficulty)
        {
            var c = new MagnateConfig { Difficulty = difficulty };
            switch (difficulty)
            {
                case MagnateDifficulty.Easy:
                    c.SpawnChance = 0.8; c.RewardMultiplier = 1.2; c.EventChance = 0.15; c.TheftChance = 0.12; c.StartBuildPoints = 8;
                    break;
                case MagnateDifficulty.Hard:
                    c.SpawnChance = 0.55; c.RewardMultiplier = 0.9; c.EventChance = 0.35; c.TheftChance = 0.25; c.JamPenalty = 2;
                    break;
                case MagnateDifficulty.Expert:
                    c.SpawnChance = 0.5; c.RewardMultiplier = 0.8; c.EventChance = 0.45; c.TheftChance = 0.3; c.JamPenalty = 2;
                    c.StartBuildPoints = 4; c.BuildPointsPerRound = 3;
                    break;
            }
            return c;
        }

        public static string Title(MagnateDifficulty difficulty) =>
            difficulty == MagnateDifficulty.Easy ? "Лёгкая" : difficulty == MagnateDifficulty.Hard ? "Сложная"
            : difficulty == MagnateDifficulty.Expert ? "Эксперт" : "Обычная";
    }

    /// <summary>
    /// «Логистический магнат»: игроки — владельцы транспортных компаний. Город сгенерирован (CityGenerator),
    /// городская сеть дорог есть с начала, свои дороги и хабы развиваются за очки стройки. Раунд: каждый по
    /// очереди строит и тайно планирует маршруты фур, затем все фуры едут одновременно. Дорога, на которую за
    /// раунд заехало больше фур, чем её пропускная способность, — пробка: все эти фуры стоят.
    /// Семь видов грузов, заказы мэрии, репутация, случайные события раунда, четыре уровня сложности.
    /// Доска (Board) — обычная партия Game: по ней рисуются карта, 3D-город и дороги.
    /// </summary>
    public sealed class LogisticsGame
    {
        readonly Random _rng;
        readonly List<MagnatePlayer> _players = new List<MagnatePlayer>();
        readonly List<Truck> _trucks = new List<Truck>();
        readonly Dictionary<string, Cargo> _cargo = new Dictionary<string, Cargo>();
        readonly Dictionary<string, int> _lastTraffic = new Dictionary<string, int>();
        readonly List<LogisticsContract> _contracts = new List<LogisticsContract>();
        readonly List<LogisticsEvent> _events = new List<LogisticsEvent>();
        readonly HashSet<string> _closed = new HashSet<string>();
        int _cargoCounter, _contractCounter;
        int _startPlayer;
        int _turnInRound;

        public Game Board { get; }
        public CityMap Map => Board.Map;
        public GeneratedCity City { get; }
        public MagnateConfig Config { get; }
        public IReadOnlyList<MagnatePlayer> Players => _players;
        public IReadOnlyList<Truck> Trucks => _trucks;
        public IReadOnlyList<Hub> Hubs => City.Hubs;
        public IEnumerable<Cargo> AllCargo => _cargo.Values;
        public IReadOnlyList<LogisticsContract> Contracts => _contracts;
        /// <summary>События, действующие в этом раунде.</summary>
        public IReadOnlyList<LogisticsEvent> Events => _events;
        public int Round { get; private set; } = 1;
        public MagnatePhase Phase { get; private set; } = MagnatePhase.Build;
        public int CurrentPlayerIndex { get; private set; }
        public MagnatePlayer CurrentPlayer => _players[CurrentPlayerIndex];
        public RoundReport LastReport { get; private set; }
        /// <summary>Текущий игрок — последний в раунде: после его «Готово» фуры поедут.</summary>
        public bool IsLastTurnInRound => _turnInRound == _players.Count - 1;
        /// <summary>Индекс владельца городских дорог (за пределами списка игроков).</summary>
        public int PublicOwner => _players.Count;

        public event Action<string> Log;
        public event Action StateChanged;

        public LogisticsGame(GeneratedCity city, IList<string> names, IList<bool> bots, int seed, MagnateConfig config = null)
        {
            City = city ?? throw new ArgumentNullException(nameof(city));
            Config = config ?? new MagnateConfig();
            _rng = new Random(seed);
            var boardConfig = new GameConfig
            {
                EventsEnabled = false, TradesEnabled = false, UpgradeEnabled = false, InfluenceEnabled = false,
                MayorGoalsDealt = 0, LuckEnabled = false, FinalOrderBonus = 0, FinalAuditPercent = 0, CharacterOffer = 0
            };
            Board = new Game(city.Map, names, boardConfig, seed);
            for (int i = 0; i < names.Count; i++)
                _players.Add(new MagnatePlayer
                {
                    Index = i, Name = names[i], IsBot = bots != null && i < bots.Count && bots[i],
                    BuildPoints = Config.StartBuildPoints, Influence = Config.StartInfluence
                });
            foreach (var id in city.PublicRoads) Board.LogisticsSetOwner(id, PublicOwner);

            // Фуры — по хабам, по кругу, чтобы компании начинали в разных местах.
            int truckId = 0;
            for (int k = 0; k < Config.TrucksPerPlayer; k++)
                for (int p = 0; p < _players.Count; p++)
                {
                    var hub = city.Hubs[(p + k * _players.Count + _rng.Next(2)) % city.Hubs.Count];
                    _trucks.Add(new Truck { Id = truckId++, Owner = p, Location = hub.LocationId });
                }
            for (int i = 0; i < 2; i++) SpawnCargo();
            RefillContracts();
            UpdateClock();
            Emit($"Город {Map.Name}: {Map.Locations.Count} мест, {Hubs.Count} хабов. Сложность: {MagnateConfig.Title(Config.Difficulty)}. Раунд 1.");
        }

        // ================================================================== Дороги и хабы

        public bool IsBuilt(string roadId) => Board.GetRoadOwner(roadId).HasValue;
        public bool IsPublic(string roadId) => Board.GetRoadOwner(roadId) == PublicOwner;
        public bool IsHighway(string roadId) => Board.IsRushHourRoad(roadId);
        /// <summary>Дорога закрыта событием раунда (ДТП, ремонт, фестиваль).</summary>
        public bool IsClosed(string roadId) => _closed.Contains(roadId);
        public bool HasEvent(LogisticsEventKind kind) => _events.Any(e => e.Kind == kind);

        /// <summary>Широкая дорога (для тяжеловеса и негабарита): бетон, мост или магистраль.</summary>
        public bool IsWide(Road road) =>
            road.Resource == Resource.Concrete || road.Resource == Resource.Steel || IsHighway(road.Id);

        public int Capacity(Road road)
        {
            int capacity = IsHighway(road.Id) ? Config.CapacityHighway : IsWide(road) ? Config.CapacityWide : Config.CapacityStreet;
            if (road.IsBridge && HasEvent(LogisticsEventKind.Storm)) capacity = Math.Max(1, capacity - 1);
            return capacity;
        }

        public int BuildCost(Road road) => road.Length + (road.IsBridge ? Config.BridgeExtraCost : 0);

        public int UpgradeCost(Road road) => (road.Length + 1) / 2 + 1;

        public Hub HubAt(string locationId) => City.Hubs.FirstOrDefault(h => h.LocationId == locationId);

        public int HubUpgradeCost(Hub hub) => hub.Level >= 5 ? 0 : Config.HubUpgradeCost[hub.Level + 1];

        /// <summary>Сколько фур проехало по дороге в прошлом раунде (подсказка о загрузке).</summary>
        public int LastTraffic(string roadId) => _lastTraffic.TryGetValue(roadId, out var n) ? n : 0;

        public string CanBuild(int player, Road road)
        {
            if (Phase != MagnatePhase.Build || player != CurrentPlayerIndex) return "Сейчас не ваша стройка.";
            if (IsBuilt(road.Id)) return Board.GetRoadOwner(road.Id) == player ? null : "Дорога уже построена.";
            if (_players[player].BuildPoints < BuildCost(road)) return $"Нужно очков стройки: {BuildCost(road)}.";
            return null;
        }

        /// <summary>Купить дорогу; своя дорога — улучшить до магистрали.</summary>
        public string BuildOrUpgrade(string roadId)
        {
            var road = Map.GetRoad(roadId);
            if (road == null) return "Нет такой дороги.";
            int p = CurrentPlayerIndex;
            var player = _players[p];
            if (Phase != MagnatePhase.Build) return "Сейчас не стройка.";
            if (Board.GetRoadOwner(roadId) == p)
            {
                if (IsHighway(roadId)) return "Это уже магистраль.";
                int cost = UpgradeCost(road);
                if (player.BuildPoints < cost) return $"Магистраль стоит {cost} очков стройки.";
                player.BuildPoints -= cost;
                Board.LogisticsSetHighway(roadId);
                Emit($"{player.Name}: магистраль {Map.LocationName(road.A)} — {Map.LocationName(road.B)}.");
                Changed();
                return null;
            }
            var error = CanBuild(p, road);
            if (error != null) return error;
            player.BuildPoints -= BuildCost(road);
            Board.LogisticsSetOwner(roadId, p);
            Emit($"{player.Name} строит {(road.IsBridge ? "мост" : "дорогу")} {Map.LocationName(road.A)} — {Map.LocationName(road.B)}.");
            Changed();
            return null;
        }

        /// <summary>
        /// Развить хаб на уровень: склады (2), краны и погрузчики (3), территория (4), комплекс (5). Первый, кто
        /// развивает городской хаб, становится его владельцем; чужой хаб развивать нельзя.
        /// </summary>
        public string UpgradeHub(string locationId)
        {
            var hub = HubAt(locationId);
            if (hub == null) return "Здесь нет хаба.";
            if (Phase != MagnatePhase.Build) return "Хабы развиваются в стройку.";
            int p = CurrentPlayerIndex;
            if (hub.Owner >= 0 && hub.Owner != p) return $"Хаб развивает {_players[hub.Owner].Name}.";
            if (hub.Level >= 5) return "Хаб уже максимального уровня.";
            int cost = HubUpgradeCost(hub);
            if (_players[p].BuildPoints < cost) return $"Уровень {hub.Level + 1} стоит {cost} очков стройки.";
            _players[p].BuildPoints -= cost;
            hub.Owner = p;
            hub.Level++;
            Emit($"{_players[p].Name} развивает «{Map.LocationName(locationId)}» до уровня {hub.Level}.");
            Changed();
            return null;
        }

        public void FinishBuild()
        {
            if (Phase != MagnatePhase.Build) return;
            Phase = MagnatePhase.Plan;
            Changed();
        }

        // ================================================================== Маршруты

        static bool NeedsWide(CargoKind? kind) => kind == CargoKind.Heavy || kind == CargoKind.Oversized;

        /// <summary>Можно ли везти этот груз по дороге сейчас.</summary>
        public bool Allowed(Road road, CargoKind? kind)
        {
            if (!IsBuilt(road.Id) || IsClosed(road.Id) || !Board.IsRoadOpen(road.Id)) return false;
            if (!NeedsWide(kind)) return true;
            if (!IsWide(road)) return false;
            return !(road.IsBridge && HasEvent(LogisticsEventKind.Storm));
        }

        public int SpeedOf(CargoKind? kind)
        {
            int speed = Config.TruckSpeed + (kind == CargoKind.Perishable ? 1 : NeedsWide(kind) ? -1 : 0);
            if (HasEvent(LogisticsEventKind.FuelShortage)) speed -= 2;
            return Math.Max(2, speed);
        }

        public int SpeedOf(Truck truck) => SpeedOf(KindOf(truck));

        CargoKind? KindOf(Truck truck) =>
            truck.Cargo != null ? GetCargo(truck.Cargo)?.Kind : truck.PlannedCargo != null ? GetCargo(truck.PlannedCargo)?.Kind : (CargoKind?)null;

        /// <summary>
        /// Лучший маршрут по построенным дорогам: длина + плата за чужие + (если avoidJams) прошлогодняя загрузка
        /// сверх пропускной способности + уже запланированные свои фуры. null — не доехать.
        /// </summary>
        public List<string> FindRoute(int player, string from, string to, CargoKind? kind, bool avoidJams = true,
            IDictionary<string, int> planned = null)
        {
            if (from == to) return new List<string>();
            var distance = new Dictionary<string, float> { [from] = 0f };
            var via = new Dictionary<string, Road>();
            var open = new SortedSet<(float, string)> { (0f, from) };
            while (open.Count > 0)
            {
                var (du, u) = open.Min;
                open.Remove(open.Min);
                if (u == to) break;
                foreach (var road in Map.RoadsAt(u))
                {
                    if (!Allowed(road, kind)) continue;
                    string v = road.A == u ? road.B : road.A;
                    float cost = road.Length;
                    var owner = Board.GetRoadOwner(road.Id);
                    if (owner.HasValue && owner != player && owner != PublicOwner) cost += Config.TollPerRoad * 1.5f;
                    if (avoidJams)
                    {
                        int load = LastTraffic(road.Id) + (planned != null && planned.TryGetValue(road.Id, out var n) ? n : 0);
                        if (load >= Capacity(road)) cost += (kind == CargoKind.Fragile ? 5f : 2.5f) * (load - Capacity(road) + 1);
                    }
                    // Опасный груз без влияния на взятку объезжает Центр (если есть объезд).
                    if (kind == CargoKind.Hazard && _players[player].Influence < Config.HazardBribe && Map.DistrictOf(v) == Config.BribeDistrict) cost += 60f;
                    float nd = du + cost;
                    if (distance.TryGetValue(v, out var old) && nd >= old) continue;
                    if (distance.ContainsKey(v)) open.Remove((old, v));
                    distance[v] = nd;
                    via[v] = road;
                    open.Add((nd, v));
                }
            }
            if (!via.ContainsKey(to)) return null;
            var route = new List<string>();
            string at = to;
            while (at != from)
            {
                var road = via[at];
                route.Add(road.Id);
                at = road.A == at ? road.B : road.A;
            }
            route.Reverse();
            return route;
        }

        public int RouteLength(IEnumerable<string> route) => route?.Sum(id => Map.GetRoad(id)?.Length ?? 0) ?? 0;

        /// <summary>Сколько раундов ехать по маршруту с этим грузом.</summary>
        public int RouteRounds(IEnumerable<string> route, CargoKind? kind = null)
        {
            int speed = SpeedOf(kind);
            return Math.Max(1, (RouteLength(route) + speed - 1) / speed);
        }

        /// <summary>Сколько очков уйдёт за проезд по чужим дорогам маршрута.</summary>
        public int RouteTolls(int player, IEnumerable<string> route)
        {
            if (route == null) return 0;
            int count = 0;
            foreach (var id in route)
            {
                var owner = Board.GetRoadOwner(id);
                if (owner.HasValue && owner != player && owner != PublicOwner) count++;
            }
            return count * Config.TollPerRoad;
        }

        /// <summary>Сколько дорог маршрута были забиты в прошлом раунде (риск пробки).</summary>
        public int RouteRisk(IEnumerable<string> route) =>
            route == null ? 0 : route.Count(id => LastTraffic(id) >= Capacity(Map.GetRoad(id)));

        /// <summary>Заходит ли маршрут в район взяток (для опасного груза).</summary>
        public bool RouteEntersBribeZone(string from, IEnumerable<string> route)
        {
            string at = from;
            if (route == null) return false;
            foreach (var id in route)
            {
                var road = Map.GetRoad(id);
                at = road.A == at ? road.B : road.A;
                if (Map.DistrictOf(at) == Config.BribeDistrict) return true;
            }
            return false;
        }

        public IEnumerable<Cargo> WaitingAt(string locationId) =>
            _cargo.Values.Where(c => c.Truck < 0 && c.From == locationId).OrderByDescending(c => c.Value);

        public Cargo GetCargo(string id) => id != null && _cargo.TryGetValue(id, out var c) ? c : null;

        public Truck GetTruck(int id) => id >= 0 && id < _trucks.Count ? _trucks[id] : null;

        public static double LevelMultiplier(ReputationLevel level) =>
            level == ReputationLevel.Legendary ? 1.5 : level == ReputationLevel.Master ? 1.35
            : level == ReputationLevel.Professional ? 1.2 : level == ReputationLevel.Amateur ? 1.1 : 1.0;

        public static string LevelTitle(ReputationLevel level) =>
            level == ReputationLevel.Legendary ? "Легенда" : level == ReputationLevel.Master ? "Мастер"
            : level == ReputationLevel.Professional ? "Профи" : level == ReputationLevel.Amateur ? "Любитель" : "Новичок";

        /// <summary>Очки за доставку через rounds раундов от сейчас: скоропорт — со штрафом, плюс множитель репутации.</summary>
        public int ValueIfDelivered(Cargo cargo, int rounds, int player = -1)
        {
            int value = cargo.Value;
            if (cargo.Kind == CargoKind.Perishable)
            {
                int late = Math.Max(0, Round + rounds - 1 - cargo.SpawnRound - cargo.Norm);
                value = Math.Max(1, value - late * Config.PerishableLatePenalty);
            }
            if (player >= 0) value = (int)Math.Round(value * LevelMultiplier(_players[player].Level));
            return value;
        }

        /// <summary>
        /// Задать фуре план: взять груз на её хабе (cargoId) и везти его к получателю, либо ехать к месту target.
        /// Возвращает ошибку или null.
        /// </summary>
        public string PlanTruck(int truckId, string cargoId, string target, IDictionary<string, int> planned = null)
        {
            var truck = GetTruck(truckId);
            if (truck == null) return "Нет такой фуры.";
            if (Phase != MagnatePhase.Plan || truck.Owner != CurrentPlayerIndex) return "Сейчас не ваше планирование.";
            CargoKind? kind = truck.Cargo != null ? GetCargo(truck.Cargo)?.Kind : (CargoKind?)null;
            if (cargoId != null)
            {
                if (truck.Cargo != null) return "Фура уже везёт груз.";
                var cargo = GetCargo(cargoId);
                if (cargo == null || cargo.Truck >= 0) return "Груз уже забрали.";
                if (cargo.From != truck.Location) return "Груз на другом хабе — сначала доедьте до него.";
                if (_trucks.Any(t => t != truck && t.Owner == truck.Owner && t.PlannedCargo == cargoId)) return "Этот груз уже берёт другая ваша фура.";
                kind = cargo.Kind;
                target = cargo.To;
            }
            else if (truck.Cargo != null) target = GetCargo(truck.Cargo)?.To ?? target;
            if (target == null) return "Выберите, куда ехать.";
            var route = FindRoute(truck.Owner, truck.Location, target, kind, true, planned);
            if (route == null)
                return NeedsWide(kind) ? "Туда не проехать: этому грузу нужны широкие дороги — бетон, мосты, магистрали." : "Туда не проехать по открытым дорогам.";
            if (cargoId != null) truck.PlannedCargo = cargoId;
            else if (truck.Cargo == null) truck.PlannedCargo = null;
            truck.Target = target;
            truck.Route = route;
            Changed();
            return null;
        }

        public void ClearTruck(int truckId)
        {
            var truck = GetTruck(truckId);
            if (truck == null || truck.Owner != CurrentPlayerIndex || Phase != MagnatePhase.Plan) return;
            truck.PlannedCargo = null;
            if (truck.Cargo == null)
            {
                truck.Target = null;
                truck.Route.Clear();
            }
            Changed();
        }

        /// <summary>Игрок закончил ход. Последний в раунде — все фуры едут, начинается следующий раунд.</summary>
        public void FinishPlanning()
        {
            if (Phase != MagnatePhase.Plan) return;
            _turnInRound++;
            if (_turnInRound < _players.Count)
            {
                CurrentPlayerIndex = (_startPlayer + _turnInRound) % _players.Count;
                Phase = MagnatePhase.Build;
                Changed();
                return;
            }
            Resolve();
        }

        // ================================================================== Движение

        static void AddReputation(MagnatePlayer player, int delta) => player.Reputation = Math.Max(0, Math.Min(120, player.Reputation + delta));

        void Resolve()
        {
            var report = new RoundReport { Round = Round };
            var order = Enumerable.Range(0, _players.Count).Select(i => (_startPlayer + i) % _players.Count).ToList();

            // 1. Погрузка: на развитом хабе первым грузит владелец, дальше — по очереди хода раунда.
            var loading = _trucks.Where(t => t.PlannedCargo != null)
                .OrderBy(t => HubAt(t.Location)?.Owner == t.Owner ? 0 : 1)
                .ThenBy(t => order.IndexOf(t.Owner)).ToList();
            foreach (var truck in loading)
            {
                var cargo = GetCargo(truck.PlannedCargo);
                truck.PlannedCargo = null;
                var owner = _players[truck.Owner];
                if (cargo == null || cargo.Truck >= 0 || cargo.From != truck.Location)
                {
                    truck.Route.Clear();
                    truck.Target = null;
                    report.Messages.Add($"{owner.Name}: груз на «{Map.LocationName(truck.Location)}» забрал соперник — фура ждёт.");
                    continue;
                }
                cargo.Truck = truck.Id;
                truck.Cargo = cargo.Id;
                var hub = HubAt(truck.Location);
                if (hub != null && hub.Owner >= 0 && hub.Owner != truck.Owner)
                {
                    owner.Score -= Config.HubFee;
                    _players[hub.Owner].Score += Config.HubFee;
                    _players[hub.Owner].HubFees += Config.HubFee;
                }
                if (cargo.Kind == CargoKind.Valuable && owner.Influence >= Config.EscortCost)
                {
                    owner.Influence -= Config.EscortCost;
                    cargo.Escorted = true;
                }
            }

            // 2. Таможня: опасные и ценные грузы без взятки стоят раунд.
            var held = new HashSet<int>();
            if (HasEvent(LogisticsEventKind.CustomsCheck))
                foreach (var truck in _trucks)
                {
                    var cargo = GetCargo(truck.Cargo);
                    if (cargo == null || (cargo.Kind != CargoKind.Hazard && cargo.Kind != CargoKind.Valuable) || truck.Route.Count == 0) continue;
                    var owner = _players[truck.Owner];
                    if (owner.Influence >= Config.CustomsBribe) owner.Influence -= Config.CustomsBribe;
                    else held.Add(truck.Id);
                }

            // 3. Что каждая фура проедет за раунд: не больше своей скорости, дорога целиком, до закрытой дороги.
            var steps = new Dictionary<int, List<string>>();
            var oversizedRoads = new Dictionary<string, int>();
            foreach (var truck in _trucks)
            {
                var list = new List<string>();
                if (!held.Contains(truck.Id))
                {
                    int budget = SpeedOf(truck);
                    foreach (var id in truck.Route)
                    {
                        var road = Map.GetRoad(id);
                        if (road == null || road.Length > budget || !IsBuilt(id) || IsClosed(id)) break;
                        budget -= road.Length;
                        list.Add(id);
                    }
                }
                steps[truck.Id] = list;
                foreach (var id in list) report.Traffic[id] = (report.Traffic.TryGetValue(id, out var n) ? n : 0) + 1;
                if (GetCargo(truck.Cargo)?.Kind == CargoKind.Oversized)
                    foreach (var id in list) if (!oversizedRoads.ContainsKey(id)) oversizedRoads[id] = truck.Id;
            }
            foreach (var pair in report.Traffic)
                if (pair.Value > Capacity(Map.GetRoad(pair.Key))) report.Jams.Add(pair.Key);
            var trueJams = new HashSet<string>(report.Jams);

            // 4. Движение.
            foreach (var truck in _trucks)
            {
                var cargoAtStart = GetCargo(truck.Cargo);
                var move = new TruckMove { Truck = truck.Id, From = truck.Location, CargoKind = cargoAtStart?.Kind };
                move.Stops.Add(truck.Location);
                var owner = _players[truck.Owner];
                if (held.Contains(truck.Id))
                {
                    move.Reason = StopReason.Customs;
                    move.StoppedBefore = truck.Route.FirstOrDefault();
                    move.Note = "таможня: не хватило влияния";
                }
                foreach (var id in steps[truck.Id])
                {
                    var road = Map.GetRoad(id);
                    bool blockedByOversized = oversizedRoads.TryGetValue(id, out var big) && big != truck.Id;
                    if (trueJams.Contains(id) || blockedByOversized)
                    {
                        move.StoppedBefore = id;
                        move.Reason = trueJams.Contains(id) ? StopReason.Jam : StopReason.Oversized;
                        owner.Score -= Config.JamPenalty;
                        owner.Jams++;
                        AddReputation(owner, -1);
                        move.Points -= Config.JamPenalty;
                        var fragile = GetCargo(truck.Cargo);
                        if (fragile != null && fragile.Kind == CargoKind.Fragile)
                        {
                            fragile.Value = Math.Max(1, fragile.Value - Config.FragileJamPenalty);
                            move.Note = $"хрупкий груз пострадал в пробке: −{Config.FragileJamPenalty}";
                        }
                        report.Jams.Add(id);
                        break;
                    }
                    string next = road.A == truck.Location ? road.B : road.A;
                    var roadOwner = Board.GetRoadOwner(id);
                    if (roadOwner.HasValue && roadOwner.Value != truck.Owner && roadOwner.Value != PublicOwner)
                    {
                        owner.Score -= Config.TollPerRoad;
                        owner.TollOut += Config.TollPerRoad;
                        _players[roadOwner.Value].Score += Config.TollPerRoad;
                        _players[roadOwner.Value].TollIn += Config.TollPerRoad;
                        move.Points -= Config.TollPerRoad;
                    }
                    truck.Location = next;
                    truck.Route.RemoveAt(0);
                    move.Stops.Add(next);

                    var cargo = GetCargo(truck.Cargo);
                    if (cargo != null && cargo.Kind == CargoKind.Hazard && !cargo.BribePaid && Map.DistrictOf(next) == Config.BribeDistrict)
                    {
                        if (owner.Influence >= Config.HazardBribe)
                        {
                            owner.Influence -= Config.HazardBribe;
                            cargo.BribePaid = true;
                            move.Note = $"взятка {Config.HazardBribe} влияния за опасный груз";
                        }
                        else
                        {
                            LoseCargo(truck, cargo, owner, Config.ConfiscationPenalty);
                            owner.Confiscated++;
                            move.Points -= Config.ConfiscationPenalty;
                            move.Note = "опасный груз конфискован в Центре";
                            report.Messages.Add($"{owner.Name}: опасный груз конфискован — не хватило влияния на взятку.");
                            break;
                        }
                    }
                    if (cargo != null && truck.Cargo != null && next == cargo.To)
                    {
                        Deliver(truck, cargo, owner, move, report);
                        break;
                    }
                }
                // Ценный груз без охраны: риск кражи за каждый раунд в пути.
                var valuable = GetCargo(truck.Cargo);
                if (valuable != null && valuable.Kind == CargoKind.Valuable && !valuable.Escorted && _rng.NextDouble() < Config.TheftChance)
                {
                    LoseCargo(truck, valuable, owner, Config.TheftPenalty);
                    owner.Thefts++;
                    move.Points -= Config.TheftPenalty;
                    move.Note = "ценный груз украли — не было охраны";
                    report.Messages.Add($"{owner.Name}: ценный груз украли в пути (−{Config.TheftPenalty}).");
                }
                if (truck.Route.Count == 0 && truck.Cargo == null) truck.Target = null;
                report.Moves.Add(move);
            }
            foreach (var id in report.Jams)
            {
                var road = Map.GetRoad(id);
                report.Messages.Add(trueJams.Contains(id)
                    ? $"Пробка: {Map.LocationName(road.A)} — {Map.LocationName(road.B)} ({report.Traffic[id]} фур при пропускной {Capacity(road)})."
                    : $"Негабарит перекрыл {Map.LocationName(road.A)} — {Map.LocationName(road.B)}.");
            }

            _lastTraffic.Clear();
            foreach (var pair in report.Traffic) _lastTraffic[pair.Key] = pair.Value;
            Board.LogisticsSetTraffic(report.Traffic, report.Jams);
            LastReport = report;
            foreach (var message in report.Messages) Emit(message);

            // 5. Новый раунд.
            foreach (var cargo in _cargo.Values.Where(c => c.Truck < 0 && Round - c.SpawnRound >= Config.CargoLifetime).ToList())
                _cargo.Remove(cargo.Id);
            if (Round >= Config.Rounds)
            {
                Phase = MagnatePhase.GameOver;
                Board.LogisticsSetPhase(GamePhase.GameOver);
                var winner = _players.OrderByDescending(p => p.Score).First();
                Emit($"Партия окончена. Победил {winner.Name}: {winner.Score} очков.");
                Changed();
                return;
            }
            Round++;
            foreach (var player in _players)
            {
                player.BuildPoints = Math.Min(Config.MaxBuildPoints, player.BuildPoints + Config.BuildPointsPerRound);
                player.Influence += Config.InfluencePerRound;
            }
            SpawnCargo();
            RefillContracts();
            RollEvents();
            _startPlayer = (_startPlayer + 1) % _players.Count;
            _turnInRound = 0;
            CurrentPlayerIndex = _startPlayer;
            Phase = MagnatePhase.Build;
            UpdateClock();
            Emit($"Раунд {Round}.");
            Changed();
        }

        void Deliver(Truck truck, Cargo cargo, MagnatePlayer owner, TruckMove move, RoundReport report)
        {
            int points = ValueIfDelivered(cargo, 1, owner.Index);
            bool onTime = cargo.Kind != CargoKind.Perishable || Round - cargo.SpawnRound <= cargo.Norm;
            owner.Score += points;
            owner.Delivered++;
            if (cargo.Kind == CargoKind.Perishable && onTime) owner.PerishableOnTime++;
            if (cargo.Kind == CargoKind.Hazard) owner.Influence += 1;
            AddReputation(owner, onTime ? 3 : 0);
            move.Delivered = cargo.Id;
            move.Points += points;
            report.Messages.Add($"{owner.Name} доставил {KindTitle(cargo.Kind).ToLowerInvariant()} в «{Map.LocationName(cargo.To)}»: +{points}.");
            foreach (var contract in _contracts.Where(c => c.Winner < 0 && (!c.Kind.HasValue || c.Kind == cargo.Kind)))
            {
                contract.Progress[owner.Index]++;
                if (contract.Progress[owner.Index] < contract.Needed) continue;
                contract.Winner = owner.Index;
                owner.Score += contract.RewardPoints;
                owner.Influence += contract.RewardInfluence;
                owner.ContractsWon++;
                AddReputation(owner, 5);
                report.Messages.Add($"{owner.Name} выполнил заказ мэрии «{contract.Title}»: +{contract.RewardPoints} очков и +{contract.RewardInfluence} влияния.");
            }
            _cargo.Remove(cargo.Id);
            truck.Cargo = null;
            truck.Route.Clear();
            truck.Target = null;
        }

        void LoseCargo(Truck truck, Cargo cargo, MagnatePlayer owner, int penalty)
        {
            _cargo.Remove(cargo.Id);
            truck.Cargo = null;
            truck.Route.Clear();
            truck.Target = null;
            owner.Score -= penalty;
            AddReputation(owner, -4);
        }

        // ================================================================== Грузы

        void SpawnCargo()
        {
            foreach (var hub in City.Hubs)
            {
                if (WaitingAt(hub.LocationId).Count() >= 1 + hub.Level) continue;
                if (_rng.NextDouble() > Config.SpawnChance + 0.08 * (hub.Level - 1)) continue;
                var kind = RollKind(hub.Kind);
                var destination = PickDestination(hub.LocationId, out int distance);
                if (destination == null) continue;
                int value;
                switch (kind)
                {
                    case CargoKind.Perishable: value = 4 + (distance + 1) / 2; break;
                    case CargoKind.Regular: value = 2 + (distance + 2) / 3; break;
                    case CargoKind.Valuable: value = 5 + (distance + 1) / 2; break;
                    case CargoKind.Oversized: value = 5 + (int)Math.Ceiling(distance / 2.0); break;
                    case CargoKind.Fragile: value = 4 + (distance + 2) / 3; break;
                    default: value = 3 + (int)Math.Ceiling(distance / 2.5); break;
                }
                value = (int)Math.Round((value + (hub.Level - 1) / 2) * Config.RewardMultiplier);
                int speed = SpeedOf(kind);
                var cargo = new Cargo
                {
                    Id = "k" + (++_cargoCounter), Kind = kind, From = hub.LocationId, To = destination, SpawnRound = Round,
                    Value = Math.Max(1, value),
                    Norm = (distance + speed - 1) / speed + 2
                };
                _cargo[cargo.Id] = cargo;
            }
        }

        CargoKind RollKind(HubKind hub)
        {
            double r = _rng.NextDouble();
            switch (hub)
            {
                case HubKind.Airport:
                    return r < 0.32 ? CargoKind.Perishable : r < 0.55 ? CargoKind.Regular : r < 0.72 ? CargoKind.Valuable
                        : r < 0.86 ? CargoKind.Fragile : CargoKind.Hazard;
                case HubKind.Port:
                    return r < 0.3 ? CargoKind.Heavy : r < 0.48 ? CargoKind.Oversized : r < 0.66 ? CargoKind.Hazard
                        : r < 0.84 ? CargoKind.Regular : CargoKind.Fragile;
                default:
                    return r < 0.32 ? CargoKind.Regular : r < 0.5 ? CargoKind.Heavy : r < 0.64 ? CargoKind.Perishable
                        : r < 0.76 ? CargoKind.Fragile : r < 0.86 ? CargoKind.Valuable : r < 0.94 ? CargoKind.Oversized : CargoKind.Hazard;
            }
        }

        /// <summary>Получатель: не хаб, не ближе 5 участков по всем дорогам карты (построены они или нет).</summary>
        string PickDestination(string from, out int distance)
        {
            var all = AllDistances(from);
            var hubs = new HashSet<string>(City.Hubs.Select(h => h.LocationId));
            var options = all.Where(p => p.Value >= 5 && !hubs.Contains(p.Key)).ToList();
            distance = 0;
            if (options.Count == 0) return null;
            var pick = options[_rng.Next(options.Count)];
            distance = pick.Value;
            return pick.Key;
        }

        Dictionary<string, int> AllDistances(string from)
        {
            var distance = new Dictionary<string, int> { [from] = 0 };
            var open = new SortedSet<(int, string)> { (0, from) };
            while (open.Count > 0)
            {
                var (du, u) = open.Min;
                open.Remove(open.Min);
                foreach (var road in Map.RoadsAt(u))
                {
                    string v = road.A == u ? road.B : road.A;
                    int nd = du + road.Length;
                    if (distance.TryGetValue(v, out var old) && nd >= old) continue;
                    if (distance.ContainsKey(v)) open.Remove((old, v));
                    distance[v] = nd;
                    open.Add((nd, v));
                }
            }
            return distance;
        }

        // ================================================================== Заказы мэрии

        void RefillContracts()
        {
            foreach (var contract in _contracts.Where(c => c.Winner < 0 && Round > c.EndRound).ToList())
            {
                _contracts.Remove(contract);
                Emit($"Заказ мэрии «{contract.Title}» никто не выполнил.");
            }
            _contracts.RemoveAll(c => c.Winner >= 0);
            var kinds = new CargoKind?[] { null, CargoKind.Perishable, CargoKind.Heavy, CargoKind.Fragile, CargoKind.Valuable, CargoKind.Hazard, CargoKind.Oversized };
            while (_contracts.Count < Config.ActiveContracts && Round <= Config.Rounds - 3)
            {
                CargoKind? kind = kinds[_rng.Next(kinds.Length)];
                for (int attempt = 0; attempt < 10 && _contracts.Any(c => c.Kind == kind); attempt++) kind = kinds[_rng.Next(kinds.Length)];
                int needed = kind.HasValue ? 2 : 3;
                var contract = new LogisticsContract
                {
                    Id = "m" + (++_contractCounter), Kind = kind, Needed = needed, StartRound = Round, EndRound = Round + 5,
                    RewardPoints = (int)Math.Round((kind.HasValue ? 7 : 6) * Config.RewardMultiplier), RewardInfluence = 2,
                    Progress = new int[_players.Count], Title = ContractTitle(kind, needed)
                };
                _contracts.Add(contract);
                Emit($"Новый заказ мэрии: «{contract.Title}» до конца раунда {contract.EndRound}.");
            }
        }

        static string ContractTitle(CargoKind? kind, int needed)
        {
            switch (kind)
            {
                case CargoKind.Perishable: return $"Свежие продукты: {needed} скоропорта";
                case CargoKind.Heavy: return $"Стройка века: {needed} тяжеловеса";
                case CargoKind.Fragile: return $"Музейные экспонаты: {needed} хрупких груза";
                case CargoKind.Valuable: return $"Казначейство: {needed} ценных груза";
                case CargoKind.Hazard: return $"Химзавод: {needed} опасных груза";
                case CargoKind.Oversized: return $"Новый мост: {needed} негабарита";
                default: return $"Снабжение города: {needed} любых груза";
            }
        }

        // ================================================================== События

        void RollEvents()
        {
            _events.RemoveAll(e => e.EndRound < Round);
            if (_events.Count == 0 && _rng.NextDouble() < Config.EventChance)
            {
                var kind = (LogisticsEventKind)_rng.Next(Enum.GetValues(typeof(LogisticsEventKind)).Length);
                var e = new LogisticsEvent { Kind = kind, StartRound = Round, EndRound = Round };
                var built = Map.Roads.Where(r => IsBuilt(r.Id)).ToList();
                bool ok = true;
                switch (kind)
                {
                    case LogisticsEventKind.Accident:
                    {
                        var busy = built.OrderByDescending(r => LastTraffic(r.Id) + _rng.NextDouble()).Take(5).ToList();
                        if (busy.Count == 0) { ok = false; break; }
                        e.Road = busy[_rng.Next(busy.Count)].Id;
                        e.Title = "ДТП";
                        e.Description = $"Фура сломалась на дороге {RoadName(e.Road)}: дорога закрыта на этот раунд — фуры объезжают.";
                        break;
                    }
                    case LogisticsEventKind.RoadRepair:
                        if (built.Count == 0) { ok = false; break; }
                        e.Road = built[_rng.Next(built.Count)].Id;
                        e.EndRound = Round + 1;
                        e.Title = "Ремонт дороги";
                        e.Description = $"Дорога {RoadName(e.Road)} закрыта на ремонт на 2 раунда.";
                        break;
                    case LogisticsEventKind.FuelShortage:
                        e.Title = "Нехватка топлива";
                        e.Description = "Все фуры проезжают на 2 участка меньше в этот раунд.";
                        break;
                    case LogisticsEventKind.CustomsCheck:
                        e.Title = "Таможенная проверка";
                        e.Description = $"Опасные и ценные грузы в пути платят {Config.CustomsBribe} влияния — иначе стоят раунд.";
                        break;
                    case LogisticsEventKind.Storm:
                        e.Title = "Штормовое предупреждение";
                        e.Description = "Мосты пропускают на одну фуру меньше, тяжеловесы и негабарит по мостам не едут.";
                        break;
                    default:
                        e.Title = "Фестиваль";
                        e.Description = "Улицы Центра перекрыты на этот раунд.";
                        break;
                }
                if (ok)
                {
                    _events.Add(e);
                    Emit($"Событие: {e.Title}. {e.Description}");
                }
            }
            _closed.Clear();
            foreach (var e in _events)
            {
                if (e.Road != null) _closed.Add(e.Road);
                if (e.Kind == LogisticsEventKind.Festival)
                    foreach (var road in Map.Roads)
                        if (Map.DistrictOf(road.A) == Config.BribeDistrict && Map.DistrictOf(road.B) == Config.BribeDistrict) _closed.Add(road.Id);
            }
            // Маршруты через закрытые дороги — пересчитать (объезд), а если объезда нет — фура стоит.
            foreach (var truck in _trucks.Where(t => t.Target != null && t.Route.Any(IsClosed)))
                truck.Route = FindRoute(truck.Owner, truck.Location, truck.Target, KindOf(truck)) ?? new List<string>();
        }

        string RoadName(string roadId)
        {
            var road = Map.GetRoad(roadId);
            return $"{Map.LocationName(road.A)} — {Map.LocationName(road.B)}";
        }

        public static string KindTitle(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Perishable: return "Скоропорт";
                case CargoKind.Heavy: return "Тяжеловес";
                case CargoKind.Hazard: return "Опасный груз";
                case CargoKind.Fragile: return "Хрупкий груз";
                case CargoKind.Valuable: return "Ценный груз";
                case CargoKind.Oversized: return "Негабарит";
                default: return "Обычный груз";
            }
        }

        public static string KindRule(CargoKind kind, MagnateConfig config)
        {
            switch (kind)
            {
                case CargoKind.Perishable: return $"Едет быстрее (+1 участок), но за каждый раунд сверх нормы −{config.PerishableLatePenalty}.";
                case CargoKind.Heavy: return "Только широкие дороги: бетон, мосты, магистрали; едет медленнее (−1).";
                case CargoKind.Hazard: return $"Въезд в Центр — взятка {config.HazardBribe} влияния, иначе конфискация (−{config.ConfiscationPenalty}).";
                case CargoKind.Fragile: return $"Каждая пробка на пути — груз дешевеет на {config.FragileJamPenalty}.";
                case CargoKind.Valuable: return $"Охрана — {config.EscortCost} влияния при погрузке (берётся сама, если хватает); без охраны каждый раунд риск кражи.";
                case CargoKind.Oversized: return "Только широкие дороги, медленно; на его дороге другие фуры в этот раунд стоят.";
                default: return "Без особых условий.";
            }
        }

        // ================================================================== Служебное

        /// <summary>Часы на карте: утро в начале партии, вечер к последним раундам (через запас участков доски).</summary>
        void UpdateClock()
        {
            int start = Board.Config.RoadPiecesFor(_players.Count);
            int finish = Board.Config.FinalWarningPieces;
            int pieces = start - (int)Math.Round((start - finish) * (Round - 1) / (double)Math.Max(1, Config.Rounds - 1));
            foreach (var player in Board.Players) player.RoadPieces = Math.Max(0, pieces);
        }

        void Emit(string message) => Log?.Invoke(message);
        void Changed() => StateChanged?.Invoke();
    }
}
