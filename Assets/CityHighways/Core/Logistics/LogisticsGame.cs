using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    public enum CargoKind
    {
        /// <summary>Обычный груз.</summary>
        Regular,
        /// <summary>Скоропорт: много очков, но за каждый ход сверх нормы — минус.</summary>
        Perishable,
        /// <summary>Тяжеловес: только по широким дорогам — бетон, мосты и магистрали.</summary>
        Heavy,
        /// <summary>Опасный груз: въезд в Центр — взятка влиянием, иначе конфискация.</summary>
        Hazard
    }

    public sealed class Cargo
    {
        public string Id;
        public CargoKind Kind;
        public string From, To;
        /// <summary>Очки за доставку (у скоропорта — до вычета за опоздание).</summary>
        public int Value;
        /// <summary>Скоропорт: за сколько раундов от появления доставка без штрафа.</summary>
        public int Norm;
        public int SpawnRound;
        /// <summary>Фура, которая везёт (-1 — ждёт на хабе).</summary>
        public int Truck = -1;
        public bool BribePaid;
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

    public sealed class MagnatePlayer
    {
        public int Index;
        public string Name;
        public bool IsBot;
        public int Score, Influence, BuildPoints;
        public int Delivered, Jams, TollIn, TollOut, Confiscated;
    }

    public enum MagnatePhase { Build, Plan, GameOver }

    /// <summary>Что случилось с фурой за раунд — для анимации и журнала.</summary>
    public sealed class TruckMove
    {
        public int Truck;
        public string From;
        /// <summary>Пройденные места по порядку (From — первое).</summary>
        public List<string> Stops = new List<string>();
        public string JammedOn;
        public string Delivered;
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
        public int Rounds = 15;
        public int TrucksPerPlayer = 3;
        /// <summary>Сколько участков дороги фура проезжает за раунд.</summary>
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
        public int MaxWaitingPerHub = 2;
        public double SpawnChance = 0.65;
        public int CargoLifetime = 6;
        public string BribeDistrict = "center";
    }

    /// <summary>
    /// «Логистический магнат»: игроки — владельцы транспортных компаний. Город сгенерирован (CityGenerator),
    /// городская сеть дорог есть с начала, свои дороги строятся за очки стройки. Раунд: каждый по очереди
    /// строит и тайно планирует маршруты фур, затем все фуры едут одновременно. Дорога, на которую за раунд
    /// заехало больше фур, чем её пропускная способность, — пробка: все эти фуры стоят, владельцы теряют очки.
    /// Грузы появляются на хабах (аэропорты, вокзалы, порты): обычные, скоропорт, тяжеловес, опасные.
    /// Доска (Board) — обычная партия Game: по ней рисуются карта, 3D-город и дороги.
    /// </summary>
    public sealed class LogisticsGame
    {
        readonly Random _rng;
        readonly List<MagnatePlayer> _players = new List<MagnatePlayer>();
        readonly List<Truck> _trucks = new List<Truck>();
        readonly Dictionary<string, Cargo> _cargo = new Dictionary<string, Cargo>();
        readonly Dictionary<string, int> _lastTraffic = new Dictionary<string, int>();
        int _cargoCounter;
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
            UpdateClock();
            Emit($"Город {Map.Name}: {Map.Locations.Count} мест, {Hubs.Count} хабов. Раунд 1.");
        }

        // ================================================================== Дороги

        public bool IsBuilt(string roadId) => Board.GetRoadOwner(roadId).HasValue;
        public bool IsPublic(string roadId) => Board.GetRoadOwner(roadId) == PublicOwner;
        public bool IsHighway(string roadId) => Board.IsRushHourRoad(roadId);

        /// <summary>Широкая дорога (для тяжеловеса): бетон, мост или магистраль.</summary>
        public bool IsWide(Road road) =>
            road.Resource == Resource.Concrete || road.Resource == Resource.Steel || IsHighway(road.Id);

        public int Capacity(Road road) =>
            IsHighway(road.Id) ? Config.CapacityHighway : IsWide(road) ? Config.CapacityWide : Config.CapacityStreet;

        public int BuildCost(Road road) => road.Length + (road.IsBridge ? Config.BridgeExtraCost : 0);

        public int UpgradeCost(Road road) => (road.Length + 1) / 2 + 1;

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

        public void FinishBuild()
        {
            if (Phase != MagnatePhase.Build) return;
            Phase = MagnatePhase.Plan;
            Changed();
        }

        // ================================================================== Маршруты

        /// <summary>Можно ли везти этот груз по дороге.</summary>
        public bool Allowed(Road road, CargoKind? kind) => IsBuilt(road.Id) && (kind != CargoKind.Heavy || IsWide(road));

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
                    if (!Allowed(road, kind) || !Board.IsRoadOpen(road.Id)) continue;
                    string v = road.A == u ? road.B : road.A;
                    float cost = road.Length;
                    var owner = Board.GetRoadOwner(road.Id);
                    if (owner.HasValue && owner != player && owner != PublicOwner) cost += Config.TollPerRoad * 1.5f;
                    if (avoidJams)
                    {
                        int load = LastTraffic(road.Id) + (planned != null && planned.TryGetValue(road.Id, out var n) ? n : 0);
                        if (load >= Capacity(road)) cost += 2.5f * (load - Capacity(road) + 1);
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

        /// <summary>Сколько раундов ехать по маршруту.</summary>
        public int RouteRounds(IEnumerable<string> route) => Math.Max(1, (RouteLength(route) + Config.TruckSpeed - 1) / Config.TruckSpeed);

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

        /// <summary>Очки за доставку, если довезти через rounds раундов от сейчас (скоропорт — со штрафом).</summary>
        public int ValueIfDelivered(Cargo cargo, int rounds)
        {
            if (cargo.Kind != CargoKind.Perishable) return cargo.Value;
            int late = Math.Max(0, Round + rounds - 1 - cargo.SpawnRound - cargo.Norm);
            return Math.Max(1, cargo.Value - late * Config.PerishableLatePenalty);
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
                return kind == CargoKind.Heavy ? "Тяжеловесу туда не проехать: нужны широкие дороги — бетон, мосты, магистрали." : "Туда не проехать по построенным дорогам.";
            truck.PlannedCargo = cargoId;
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
            truck.Target = truck.Cargo != null ? truck.Target : null;
            if (truck.Cargo == null) truck.Route.Clear();
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

        void Resolve()
        {
            var report = new RoundReport { Round = Round };
            var order = Enumerable.Range(0, _players.Count).Select(i => (_startPlayer + i) % _players.Count).ToList();

            // 1. Погрузка — по очереди хода раунда: кто раньше, тот и забрал.
            foreach (int p in order)
                foreach (var truck in _trucks.Where(t => t.Owner == p && t.PlannedCargo != null))
                {
                    var cargo = GetCargo(truck.PlannedCargo);
                    truck.PlannedCargo = null;
                    if (cargo == null || cargo.Truck >= 0 || cargo.From != truck.Location)
                    {
                        truck.Route.Clear();
                        truck.Target = null;
                        report.Messages.Add($"{_players[p].Name}: груз на «{Map.LocationName(truck.Location)}» забрал соперник — фура ждёт.");
                        continue;
                    }
                    cargo.Truck = truck.Id;
                    truck.Cargo = cargo.Id;
                }

            // 2. Что каждая фура проедет за раунд (не больше скорости, дорога целиком).
            var steps = new Dictionary<int, List<string>>();
            foreach (var truck in _trucks)
            {
                var list = new List<string>();
                int budget = Config.TruckSpeed;
                foreach (var id in truck.Route)
                {
                    var road = Map.GetRoad(id);
                    if (road == null || road.Length > budget || !IsBuilt(id)) break;
                    budget -= road.Length;
                    list.Add(id);
                }
                steps[truck.Id] = list;
                foreach (var id in list) report.Traffic[id] = (report.Traffic.TryGetValue(id, out var n) ? n : 0) + 1;
            }
            foreach (var pair in report.Traffic)
                if (pair.Value > Capacity(Map.GetRoad(pair.Key))) report.Jams.Add(pair.Key);

            // 3. Движение: перед дорогой в пробке фура встаёт; плата за чужие дороги; опасный груз и Центр; доставка.
            foreach (var truck in _trucks)
            {
                var move = new TruckMove { Truck = truck.Id, From = truck.Location };
                move.Stops.Add(truck.Location);
                var owner = _players[truck.Owner];
                foreach (var id in steps[truck.Id])
                {
                    var road = Map.GetRoad(id);
                    if (report.Jams.Contains(id))
                    {
                        move.JammedOn = id;
                        owner.Score -= Config.JamPenalty;
                        owner.Jams++;
                        move.Points -= Config.JamPenalty;
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
                            _cargo.Remove(cargo.Id);
                            truck.Cargo = null;
                            truck.Route.Clear();
                            truck.Target = null;
                            owner.Score -= Config.ConfiscationPenalty;
                            owner.Confiscated++;
                            move.Points -= Config.ConfiscationPenalty;
                            move.Note = "опасный груз конфискован в Центре";
                            report.Messages.Add($"{owner.Name}: опасный груз конфискован — не хватило влияния на взятку.");
                            break;
                        }
                    }
                    if (cargo != null && truck.Cargo != null && next == cargo.To)
                    {
                        int points = ValueIfDelivered(cargo, 1);
                        owner.Score += points;
                        owner.Delivered++;
                        if (cargo.Kind == CargoKind.Hazard) owner.Influence += 1;
                        move.Delivered = cargo.Id;
                        move.Points += points;
                        report.Messages.Add($"{owner.Name} доставил {KindTitle(cargo.Kind).ToLowerInvariant()} в «{Map.LocationName(next)}»: +{points}.");
                        _cargo.Remove(cargo.Id);
                        truck.Cargo = null;
                        truck.Route.Clear();
                        truck.Target = null;
                        break;
                    }
                }
                if (truck.Route.Count == 0 && truck.Cargo == null) truck.Target = null;
                report.Moves.Add(move);
            }
            foreach (var id in report.Jams)
            {
                var road = Map.GetRoad(id);
                report.Messages.Add($"Пробка: {Map.LocationName(road.A)} — {Map.LocationName(road.B)} ({report.Traffic[id]} фур при пропускной {Capacity(road)}).");
            }

            _lastTraffic.Clear();
            foreach (var pair in report.Traffic) _lastTraffic[pair.Key] = pair.Value;
            Board.LogisticsSetTraffic(report.Traffic, report.Jams);
            LastReport = report;
            foreach (var message in report.Messages) Emit(message);

            // 4. Новый раунд.
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
            _startPlayer = (_startPlayer + 1) % _players.Count;
            _turnInRound = 0;
            CurrentPlayerIndex = _startPlayer;
            Phase = MagnatePhase.Build;
            UpdateClock();
            Emit($"Раунд {Round}.");
            Changed();
        }

        // ================================================================== Грузы

        void SpawnCargo()
        {
            foreach (var hub in City.Hubs)
            {
                if (WaitingAt(hub.LocationId).Count() >= Config.MaxWaitingPerHub || _rng.NextDouble() > Config.SpawnChance) continue;
                var kind = RollKind(hub.Kind);
                var destination = PickDestination(hub.LocationId, kind, out int distance);
                if (destination == null) continue;
                var cargo = new Cargo
                {
                    Id = "k" + (++_cargoCounter), Kind = kind, From = hub.LocationId, To = destination, SpawnRound = Round,
                    Value = kind == CargoKind.Perishable ? 4 + (distance + 1) / 2
                        : kind == CargoKind.Regular ? 2 + (distance + 2) / 3
                        : 3 + (int)Math.Ceiling(distance / 2.5),
                    Norm = (distance + Config.TruckSpeed - 1) / Config.TruckSpeed + 2
                };
                _cargo[cargo.Id] = cargo;
            }
        }

        CargoKind RollKind(HubKind hub)
        {
            double r = _rng.NextDouble();
            switch (hub)
            {
                case HubKind.Airport: return r < 0.4 ? CargoKind.Perishable : r < 0.8 ? CargoKind.Regular : CargoKind.Hazard;
                case HubKind.Port: return r < 0.45 ? CargoKind.Heavy : r < 0.7 ? CargoKind.Hazard : CargoKind.Regular;
                default: return r < 0.4 ? CargoKind.Regular : r < 0.65 ? CargoKind.Heavy : r < 0.85 ? CargoKind.Perishable : CargoKind.Hazard;
            }
        }

        /// <summary>Получатель: не хаб, не ближе 5 участков по всем дорогам карты (построены они или нет).</summary>
        string PickDestination(string from, CargoKind kind, out int distance)
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

        public static string KindTitle(CargoKind kind) =>
            kind == CargoKind.Perishable ? "Скоропорт" : kind == CargoKind.Heavy ? "Тяжеловес" : kind == CargoKind.Hazard ? "Опасный груз" : "Обычный груз";

        public static string KindRule(CargoKind kind, MagnateConfig config) =>
            kind == CargoKind.Perishable ? $"Довезти быстро: за каждый раунд сверх нормы −{config.PerishableLatePenalty}."
            : kind == CargoKind.Heavy ? "Только широкие дороги: бетон, мосты, магистрали."
            : kind == CargoKind.Hazard ? $"Въезд в Центр — взятка {config.HazardBribe} влияния, иначе конфискация (−{config.ConfiscationPenalty})."
            : "Без особых условий.";

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
