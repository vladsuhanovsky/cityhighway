using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Бот «Логистического магната».
    ///   Стройка: покупает дорогу, которая сильнее всего сокращает его пути — от фур к грузам и от грузов к
    ///   получателям (с учётом платы за чужие дороги и прошлых пробок); денег мало — улучшает до магистрали
    ///   самую загруженную свою дорогу.
    ///   План: фура с грузом едет к получателю; пустая на хабе берёт самый выгодный груз (очки за раунд пути,
    ///   тяжеловес — только если проедет, опасный — если хватит влияния или есть объезд Центра); иначе едет
    ///   к ближайшему хабу с грузом, за которым не едет другая его фура. Свои уже запланированные фуры
    ///   учитываются как загрузка дорог — бот не устраивает пробку сам себе.
    /// </summary>
    public static class LogisticsBot
    {
        public static void Build(LogisticsGame game, Random rng)
        {
            int me = game.CurrentPlayerIndex;
            var player = game.CurrentPlayer;
            for (int guard = 0; guard < 4; guard++)
            {
                var pairs = Demand(game, me);
                if (pairs.Count == 0) break;
                float baseCost = pairs.Sum(p => PathCost(game, me, p.from, p.to, p.kind, null));
                Road best = null;
                float bestGain = 0.6f;
                foreach (var road in game.Map.Roads)
                {
                    if (game.IsBuilt(road.Id) || !game.Board.IsRoadOpen(road.Id) || game.BuildCost(road) > player.BuildPoints) continue;
                    float cost = pairs.Sum(p => PathCost(game, me, p.from, p.to, p.kind, road.Id));
                    float gain = (baseCost - cost) / game.BuildCost(road) + (float)rng.NextDouble() * 0.05f;
                    if (gain > bestGain) { bestGain = gain; best = road; }
                }
                if (best == null) break;
                if (game.BuildOrUpgrade(best.Id) != null) break;
            }
            // Свободные очки — магистраль на самой загруженной своей улице (снимает пробки и пускает тяжеловесов).
            var busy = game.Map.Roads
                .Where(r => game.Board.GetRoadOwner(r.Id) == me && !game.IsHighway(r.Id) && game.LastTraffic(r.Id) >= game.Capacity(r) - 1)
                .OrderByDescending(r => game.LastTraffic(r.Id)).FirstOrDefault();
            if (busy != null && player.BuildPoints >= game.UpgradeCost(busy) + 2) game.BuildOrUpgrade(busy.Id);
            game.FinishBuild();
        }

        public static void Plan(LogisticsGame game, Random rng)
        {
            int me = game.CurrentPlayerIndex;
            var planned = new Dictionary<string, int>();
            void Count(Truck truck)
            {
                int budget = game.Config.TruckSpeed;
                foreach (var id in truck.Route)
                {
                    var road = game.Map.GetRoad(id);
                    if (road.Length > budget) break;
                    budget -= road.Length;
                    planned[id] = (planned.TryGetValue(id, out var n) ? n : 0) + 1;
                }
            }
            var taken = new HashSet<string>();
            foreach (var truck in game.Trucks.Where(t => t.Owner == me).OrderBy(t => t.Cargo == null ? 1 : 0))
            {
                if (truck.Cargo != null)
                {
                    game.PlanTruck(truck.Id, null, game.GetCargo(truck.Cargo).To, planned);
                    Count(truck);
                    continue;
                }
                // На хабе — взять лучший груз.
                Cargo bestCargo = null;
                float bestScore = 0f;
                foreach (var cargo in game.WaitingAt(truck.Location))
                {
                    if (taken.Contains(cargo.Id)) continue;
                    var route = game.FindRoute(me, truck.Location, cargo.To, cargo.Kind, true, planned);
                    if (route == null) continue;
                    if (cargo.Kind == CargoKind.Hazard && game.CurrentPlayer.Influence < game.Config.HazardBribe
                        && game.RouteEntersBribeZone(truck.Location, route)) continue;
                    int rounds = game.RouteRounds(route);
                    float score = game.ValueIfDelivered(cargo, rounds) / (float)rounds;
                    if (score > bestScore) { bestScore = score; bestCargo = cargo; }
                }
                if (bestCargo != null && game.PlanTruck(truck.Id, bestCargo.Id, null, planned) == null)
                {
                    taken.Add(bestCargo.Id);
                    Count(truck);
                    continue;
                }
                // Иначе — к хабу с выгодным грузом, куда ещё не едет своя фура.
                string bestHub = null;
                float bestHubScore = float.MinValue;
                foreach (var hub in game.Hubs)
                {
                    if (hub.LocationId == truck.Location) continue;
                    var waiting = game.WaitingAt(hub.LocationId).Where(c => !taken.Contains(c.Id)).ToList();
                    if (waiting.Count == 0) continue;
                    if (game.Trucks.Any(t => t.Owner == me && t != truck && t.Target == hub.LocationId && t.Cargo == null)) continue;
                    var route = game.FindRoute(me, truck.Location, hub.LocationId, null, true, planned);
                    if (route == null) continue;
                    float score = waiting.Max(c => c.Value) - game.RouteLength(route) * 0.6f + (float)rng.NextDouble() * 0.3f;
                    if (score > bestHubScore) { bestHubScore = score; bestHub = hub.LocationId; }
                }
                if (bestHub != null && game.PlanTruck(truck.Id, null, bestHub, planned) == null) Count(truck);
            }
            game.FinishPlanning();
        }

        /// <summary>Пары «откуда — куда», важные боту: грузы в его фурах, грузы на хабах (от ближайшей его фуры).</summary>
        static List<(string from, string to, CargoKind? kind)> Demand(LogisticsGame game, int me)
        {
            var result = new List<(string, string, CargoKind?)>();
            foreach (var truck in game.Trucks.Where(t => t.Owner == me))
            {
                var cargo = game.GetCargo(truck.Cargo);
                if (cargo != null) result.Add((truck.Location, cargo.To, cargo.Kind));
            }
            foreach (var cargo in game.AllCargo.Where(c => c.Truck < 0))
            {
                result.Add((cargo.From, cargo.To, cargo.Kind));
                var nearest = game.Trucks.Where(t => t.Owner == me && t.Cargo == null).Select(t => t.Location).FirstOrDefault();
                if (nearest != null && nearest != cargo.From) result.Add((nearest, cargo.From, null));
            }
            return result;
        }

        /// <summary>Цена пути с учётом дороги extra, будто она уже своя; нет пути — большой штраф.</summary>
        static float PathCost(LogisticsGame game, int me, string from, string to, CargoKind? kind, string extra)
        {
            if (from == to) return 0f;
            var distance = new Dictionary<string, float> { [from] = 0f };
            var open = new SortedSet<(float, string)> { (0f, from) };
            while (open.Count > 0)
            {
                var (du, u) = open.Min;
                open.Remove(open.Min);
                if (u == to) return du;
                foreach (var road in game.Map.RoadsAt(u))
                {
                    bool mine = road.Id == extra;
                    if (!mine && !game.Allowed(road, kind)) continue;
                    if (mine && kind == CargoKind.Heavy && !game.IsWide(road)) continue;
                    string v = road.A == u ? road.B : road.A;
                    float cost = road.Length;
                    var owner = game.Board.GetRoadOwner(road.Id);
                    if (!mine && owner.HasValue && owner != me && owner != game.PublicOwner) cost += game.Config.TollPerRoad * 1.5f;
                    if (!mine && game.LastTraffic(road.Id) >= game.Capacity(road)) cost += 2f;
                    float nd = du + cost;
                    if (distance.TryGetValue(v, out var old) && nd >= old) continue;
                    if (distance.ContainsKey(v)) open.Remove((old, v));
                    distance[v] = nd;
                    open.Add((nd, v));
                }
            }
            return 40f;
        }
    }
}
