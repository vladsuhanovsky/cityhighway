using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Результат проверки контракта: выполнен ли он и по каким чужим дорогам пришлось проехать.</summary>
    public sealed class ContractCheck
    {
        public Contract Contract { get; internal set; }
        public bool Completed { get; internal set; }
        /// <summary>Чужие участки на лучшем пути (за каждый — плата владельцу).</summary>
        public IReadOnlyList<Road> ForeignRoads { get; internal set; } = new Road[0];
        /// <summary>Весь найденный путь (пусто, если контракт не выполнен).</summary>
        public IReadOnlyList<Road> Path { get; internal set; } = new Road[0];
        /// <summary>Сколько очков стоит проезд по чужим участкам этого пути.</summary>
        public int Toll { get; internal set; }
        /// <summary>Сколько плеч контракта уже выполнено (у обычного и транзита плечо одно, у узла — два).</summary>
        public int LegsCompleted { get; internal set; }
        public int LegsTotal { get; internal set; }
    }

    public sealed class PlayerScore
    {
        public int PlayerIndex { get; internal set; }
        public string PlayerName { get; internal set; }
        public int RoadPoints { get; internal set; }
        /// <summary>Плюс за выполненные контракты, минус за невыполненные.</summary>
        public int ContractPoints { get; internal set; }
        public int CompletedContracts { get; internal set; }
        public int FailedContracts { get; internal set; }
        /// <summary>Сколько игрок заплатил другим за проезд (положительное число).</summary>
        public int TollsPaid { get; internal set; }
        /// <summary>Сколько игроку заплатили за проезд по его дорогам.</summary>
        public int TollsEarned { get; internal set; }
        /// <summary>Сколько районов охватывает самая большая связная сеть игрока.</summary>
        public int NetworkDistricts { get; internal set; }
        public int NetworkBonus { get; internal set; }
        /// <summary>Награды за общие цели мэрии.</summary>
        public int GoalPoints { get; internal set; }
        /// <summary>Итог карт удачи (может быть меньше нуля).</summary>
        public int LuckPoints { get; internal set; }
        /// <summary>Персонаж и очки его способности в конце (Чиновник — за контракты, Мостовик — за мосты).</summary>
        public CharacterKind Character { get; internal set; }
        public int CharacterPoints { get; internal set; }
        /// <summary>Влияние на конец партии — в очки не входит, уходит в кошелёк профиля.</summary>
        public int Influence { get; internal set; }
        /// <summary>Бонус за выполненный контракт мэрии (выигранный на аукционе).</summary>
        public int MayorBonus { get; internal set; }
        /// <summary>Штрафы за взятки и ложные обвинения (≤ 0).</summary>
        public int Penalties { get; internal set; }
        public int InfluenceEarned { get; internal set; }

        public int Total => RoadPoints + ContractPoints - TollsPaid + TollsEarned + NetworkBonus + GoalPoints + LuckPoints + CharacterPoints + MayorBonus + Penalties;
    }

    public sealed class GameResult
    {
        /// <summary>Игроки по местам: очки, затем выполненные контракты, затем размер сети.</summary>
        public IReadOnlyList<PlayerScore> Ranking { get; }
        public IReadOnlyList<PlayerScore> Winners { get; }

        internal GameResult(List<PlayerScore> scores)
        {
            var sorted = scores
                .OrderByDescending(s => s.Total)
                .ThenByDescending(s => s.CompletedContracts)
                .ThenByDescending(s => s.NetworkDistricts)
                .ToList();
            Ranking = sorted;
            var top = sorted[0];
            Winners = sorted.Where(s => s.Total == top.Total
                                        && s.CompletedContracts == top.CompletedContracts
                                        && s.NetworkDistricts == top.NetworkDistricts).ToList();
        }
    }

    public static class Scoring
    {
        /// <summary>
        /// Выполнен ли контракт игрока: выполнено каждое его плечо (см. CheckLeg).
        /// Путь и чужие участки — объединение по плечам; чужой участок, общий для двух плеч узла,
        /// оплачивается один раз.
        /// </summary>
        public static ContractCheck CheckContract(CityMap map, Contract contract, int playerIndex,
            Func<string, int?> ownerOf, int maxForeign, Func<string, int> tollOf)
        {
            var result = new ContractCheck { Contract = contract, LegsTotal = contract.Legs.Count };
            var path = new List<Road>();
            var foreign = new List<Road>();
            var seen = new HashSet<string>();
            bool all = true;
            foreach (var leg in contract.Legs)
            {
                var legPath = CheckLeg(map, leg, playerIndex, ownerOf, maxForeign, tollOf);
                if (legPath == null)
                {
                    all = false;
                    continue;
                }
                result.LegsCompleted++;
                foreach (var road in legPath)
                {
                    if (!seen.Add(road.Id)) continue;
                    path.Add(road);
                    if (ownerOf(road.Id) != playerIndex) foreign.Add(road);
                }
            }
            if (!all) return result;

            result.Completed = true;
            result.Path = path;
            result.ForeignRoads = foreign;
            result.Toll = foreign.Sum(r => tollOf(r.Id));
            return result;
        }

        /// <summary>
        /// Путь одного плеча по построенным дорогам из любого места района From в любое место района To
        /// (если задан Via — проходящий через место района Via), где есть хотя бы одна своя дорога
        /// и не больше maxForeign чужих. Из подходящих путей выбирается самый дешёвый по плате за проезд,
        /// при равенстве — с меньшим числом чужих участков (Дейкстра по состояниям
        /// «место, была ли своя дорога, сколько чужих, пройден ли Via, по какой дороге приехали»).
        /// Разворачиваться по дороге, по которой только что приехали, нельзя: иначе транзит
        /// «заезжал бы» в район тупиком и обратно. null — пути нет.
        /// </summary>
        public static List<Road> CheckLeg(CityMap map, ContractLeg leg, int playerIndex,
            Func<string, int?> ownerOf, int maxForeign, Func<string, int> tollOf)
        {
            var cost = new Dictionary<(string, bool, int, bool, string), (int toll, int count)>();
            var previous = new Dictionary<(string, bool, int, bool, string), ((string, bool, int, bool, string) state, Road road)>();
            var done = new HashSet<(string, bool, int, bool, string)>();

            var queue = new MinHeap<(string, bool, int, bool, string)>();
            foreach (var start in map.LocationsIn(leg.From))
            {
                var state = (start.Id, false, 0, leg.Via == null || leg.From == leg.Via, (string)null);
                cost[state] = (0, 0);
                queue.Push(0, state);
            }

            (string, bool, int, bool, string)? target = null;
            while (queue.TryPop(out _, out var current))
            {
                if (!done.Add(current)) continue; // устаревшая запись очереди
                var best = cost[current];

                string location = current.Item1;
                bool usedOwn = current.Item2;
                int foreignCount = current.Item3;
                bool passedVia = current.Item4;
                string cameBy = current.Item5;
                if (usedOwn && passedVia && map.DistrictOf(location) == leg.To)
                {
                    target = current;
                    break;
                }

                foreach (var road in map.RoadsAt(location))
                {
                    if (road.Id == cameBy) continue; // разворот по той же дороге не считается
                    var owner = ownerOf(road.Id);
                    if (!owner.HasValue) continue; // по непостроенной дороге не проехать

                    bool mine = owner.Value == playerIndex;
                    int nextCount = foreignCount + (mine ? 0 : 1);
                    if (nextCount > maxForeign) continue;

                    string nextLocation = road.OtherEnd(location);
                    var next = (nextLocation, usedOwn || mine, nextCount, passedVia || map.DistrictOf(nextLocation) == leg.Via, road.Id);
                    if (done.Contains(next)) continue;
                    var candidate = (toll: best.toll + (mine ? 0 : tollOf(road.Id)), count: nextCount);
                    if (cost.TryGetValue(next, out var old)
                        && (old.toll < candidate.toll || (old.toll == candidate.toll && old.count <= candidate.count))) continue;

                    cost[next] = candidate;
                    previous[next] = (current, road);
                    queue.Push(candidate.toll * 1000.0 + candidate.count, next);
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

        /// <summary>Сколько районов охватывает самая большая связная сеть из этих дорог.</summary>
        public static int NetworkDistricts(CityMap map, IEnumerable<Road> roads)
        {
            var set = new DisjointSet();
            var locations = new HashSet<string>();
            foreach (var road in roads)
            {
                set.Union(road.A, road.B);
                locations.Add(road.A);
                locations.Add(road.B);
            }

            var districtsByRoot = new Dictionary<string, HashSet<string>>();
            foreach (var location in locations)
            {
                string root = set.Find(location);
                if (!districtsByRoot.TryGetValue(root, out var districts))
                    districtsByRoot[root] = districts = new HashSet<string>();
                var district = map.DistrictOf(location);
                if (district != null) districts.Add(district);
            }
            return districtsByRoot.Count == 0 ? 0 : districtsByRoot.Values.Max(d => d.Count);
        }

        /// <summary>Итоговый подсчёт очков.</summary>
        public static GameResult Calculate(CityMap map, IReadOnlyList<PlayerState> players,
            Func<string, int?> ownerOf, Func<string, int> tollOf, GameConfig config)
        {
            var scores = players.Select(p => new PlayerScore
            {
                PlayerIndex = p.Index,
                PlayerName = p.Name,
                RoadPoints = p.RoadPoints,
                GoalPoints = p.GoalPoints,
                LuckPoints = p.LuckPoints,
                Influence = p.Influence,
                Penalties = p.PenaltyPoints,
                InfluenceEarned = p.InfluenceEarned,
                Character = p.Character,
                CharacterPoints = p.Character == CharacterKind.Bridger
                    ? config.BridgerPointsPerBridge * p.Roads.Count(r => r.IsBridge)
                    : 0,
                NetworkDistricts = NetworkDistricts(map, p.Roads)
            }).ToList();

            foreach (var player in players)
            {
                var score = scores[player.Index];
                foreach (var contract in player.Contracts)
                {
                    var check = CheckContract(map, contract, player.Index, ownerOf, config.MaxForeignRoadsPerContract, tollOf);
                    if (!check.Completed)
                    {
                        score.ContractPoints -= contract.Points;
                        score.FailedContracts++;
                        continue;
                    }

                    score.ContractPoints += contract.Points;
                    score.CompletedContracts++;
                    if (contract.Id == player.MayorContractId) score.MayorBonus += config.AuctionBonus;
                    if (player.Character == CharacterKind.Official) score.CharacterPoints += config.OfficialBonusPerContract;
                    foreach (var road in check.ForeignRoads)
                    {
                        int toll = tollOf(road.Id);
                        score.TollsPaid += toll;
                        var owner = ownerOf(road.Id);
                        if (owner.HasValue) scores[owner.Value].TollsEarned += toll;
                    }
                }
            }

            int best = scores.Max(s => s.NetworkDistricts);
            if (best > 0)
                foreach (var score in scores)
                    if (score.NetworkDistricts == best) score.NetworkBonus = config.NetworkBonus;

            return new GameResult(scores);
        }
    }

    internal sealed class DisjointSet
    {
        readonly Dictionary<string, string> _parent = new Dictionary<string, string>();

        public string Find(string x)
        {
            if (!_parent.TryGetValue(x, out var parent))
            {
                _parent[x] = x;
                return x;
            }
            if (parent == x) return x;
            var root = Find(parent);
            _parent[x] = root;
            return root;
        }

        public void Union(string a, string b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) _parent[ra] = rb;
        }
    }
}
