using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>План бота: какие дороги построить, чтобы выполнить контракты.</summary>
    public sealed class RoadPlan
    {
        /// <summary>Свободные дороги, запланированные к постройке.</summary>
        public readonly HashSet<string> PlannedRoadIds = new HashSet<string>();
        public readonly List<Road> RoadsToBuild = new List<Road>();
        public int PiecesNeeded;
        /// <summary>Сколько очков придётся заплатить за проезд по чужим дорогам на запланированных путях.</summary>
        public int TollsExpected;

        public readonly List<Contract> Completed = new List<Contract>();
        public readonly List<Contract> Reachable = new List<Contract>();
        public readonly List<Contract> Unreachable = new List<Contract>();
    }

    /// <summary>
    /// Поиск путей для бота с учётом платного проезда (Дейкстра по состояниям «место, сколько чужих дорог,
    /// была ли своя»). Своя или запланированная дорога — 0, свободная — её длина,
    /// чужая — ForeignCost (не больше MaxForeignRoadsPerContract на путь).
    /// </summary>
    public sealed class BotPlanner
    {
        readonly Game _game;
        readonly int _me;
        readonly float _foreignCost;

        public BotPlanner(Game game, int playerIndex, float foreignCost)
        {
            _game = game;
            _me = playerIndex;
            _foreignCost = foreignCost;
        }

        PlayerState Me => _game.Players[_me];

        /// <summary>Лучший путь для обычного контракта (его единственного плеча); null — путей нет.</summary>
        public List<Road> ShortestPath(Contract contract, HashSet<string> planned) =>
            ShortestPath(contract.Legs[0], planned);

        /// <summary>Лучший путь для плеча контракта; null — путей нет.</summary>
        public List<Road> ShortestPath(ContractLeg leg, HashSet<string> planned)
        {
            var map = _game.Map;
            int maxForeign = _game.Config.MaxForeignRoadsPerContract;
            var distance = new Dictionary<(string, int, bool, bool, string), float>();
            var previous = new Dictionary<(string, int, bool, bool, string), ((string, int, bool, bool, string) state, Road road)>();
            var done = new HashSet<(string, int, bool, bool, string)>();

            var queue = new MinHeap<(string, int, bool, bool, string)>();
            foreach (var start in map.LocationsIn(leg.From))
            {
                var state = (start.Id, 0, false, leg.Via == null || leg.From == leg.Via, (string)null);
                distance[state] = 0f;
                queue.Push(0, state);
            }

            (string, int, bool, bool, string)? target = null;
            while (queue.TryPop(out _, out var current))
            {
                if (!done.Add(current)) continue; // устаревшая запись очереди
                float best = distance[current];

                string location = current.Item1;
                int foreignUsed = current.Item2;
                bool hasOwn = current.Item3;
                bool passedVia = current.Item4;
                string cameBy = current.Item5;
                if (hasOwn && passedVia && map.DistrictOf(location) == leg.To)
                {
                    target = current;
                    break;
                }

                foreach (var road in map.RoadsAt(location))
                {
                    if (road.Id == cameBy) continue; // без разворота, как в Scoring.CheckLeg
                    var owner = _game.GetRoadOwner(road.Id);
                    float weight;
                    int nextForeign = foreignUsed;
                    bool nextOwn = true;

                    if (owner == _me || (planned != null && planned.Contains(road.Id))) weight = 0f;
                    else if (!owner.HasValue)
                    {
                        if (!_game.IsRoadOpen(road.Id)) continue; // район ещё не открыт
                        weight = road.Length;
                    }
                    else
                    {
                        if (foreignUsed + 1 > maxForeign) continue;
                        // Магистраль часа пик дороже — во столько же раз дороже и для планировщика.
                        weight = _foreignCost * _game.TollFor(road.Id) / _game.Config.TollPerRoad;
                        nextForeign = foreignUsed + 1;
                        nextOwn = hasOwn;
                    }

                    string nextLocation = road.OtherEnd(location);
                    var next = (nextLocation, nextForeign, nextOwn, passedVia || map.DistrictOf(nextLocation) == leg.Via, road.Id);
                    if (done.Contains(next)) continue;
                    float candidate = best + weight;
                    if (distance.TryGetValue(next, out var old) && old <= candidate) continue;
                    distance[next] = candidate;
                    previous[next] = (current, road);
                    queue.Push(candidate, next);
                }
            }

            if (!target.HasValue) return null;
            var path = new List<Road>();
            var cursor = target.Value;
            while (previous.TryGetValue(cursor, out var step))
            {
                path.Add(step.road);
                cursor = step.state;
            }
            path.Reverse();
            return path;
        }

        /// <summary>Общая сеть под набор контрактов: дорогие прокладываются первыми, дальше пользуются уже запланированным.</summary>
        public RoadPlan BuildPlan(IEnumerable<Contract> contracts)
        {
            var plan = new RoadPlan();
            foreach (var contract in contracts.OrderByDescending(c => c.Points))
            {
                if (_game.CheckContract(_me, contract).Completed)
                {
                    plan.Completed.Add(contract);
                    continue;
                }

                // Плечи прокладываются по очереди: второе плечо узла пользуется дорогами, запланированными под первое.
                // В общий план контракт попадает, только если дошли все плечи.
                var network = new HashSet<string>(plan.PlannedRoadIds);
                var newRoads = new List<Road>();
                var tolls = new Dictionary<string, int>();
                bool reachable = true;
                foreach (var leg in contract.Legs)
                {
                    var path = ShortestPath(leg, network);
                    if (path == null)
                    {
                        reachable = false;
                        break;
                    }
                    foreach (var road in path)
                    {
                        var owner = _game.GetRoadOwner(road.Id);
                        if (!owner.HasValue)
                        {
                            if (network.Add(road.Id)) newRoads.Add(road);
                        }
                        else if (owner.Value != _me)
                        {
                            tolls[road.Id] = _game.TollFor(road.Id);
                        }
                    }
                }
                if (!reachable)
                {
                    plan.Unreachable.Add(contract);
                    continue;
                }

                plan.Reachable.Add(contract);
                foreach (var road in newRoads)
                {
                    plan.PlannedRoadIds.Add(road.Id);
                    plan.RoadsToBuild.Add(road);
                    plan.PiecesNeeded += road.Length;
                }
                plan.TollsExpected += tolls.Values.Sum();
            }
            return plan;
        }
    }
}
