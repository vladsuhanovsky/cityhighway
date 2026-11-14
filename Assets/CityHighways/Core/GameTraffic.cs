using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Один оплаченный проезд по чужой дороге: кому и сколько, и сколько ушло городу за пробку.</summary>
    public sealed class PaidPassage
    {
        public string RoadId { get; }
        public int Owner { get; }
        public int Toll { get; }
        public int JamFee { get; }

        public PaidPassage(string roadId, int owner, int toll, int jamFee)
        {
            RoadId = roadId;
            Owner = owner;
            Toll = toll;
            JamFee = jamFee;
        }
    }

    /// <summary>Маршрут выполненного контракта: зафиксирован и оплачен в момент выполнения.</summary>
    public sealed class PaidRoute
    {
        public string ContractId { get; }
        public IReadOnlyList<string> Path { get; }
        public IReadOnlyList<PaidPassage> Passages { get; }
        public int Toll => Passages.Sum(p => p.Toll);
        public int JamFee => Passages.Sum(p => p.JamFee);

        public PaidRoute(string contractId, IReadOnlyList<string> path, IReadOnlyList<PaidPassage> passages)
        {
            ContractId = contractId;
            Path = path;
            Passages = passages;
        }
    }

    /// <summary>
    /// Проезд, загрузка дорог, пробки и пропуски.
    /// Контракт, как только выполнен, фиксирует свой маршрут (самый дешёвый на этот момент) и сразу платит проезд.
    /// Загрузка дороги — сколько зафиксированных маршрутов соперников владельца по ней едет. Обычная дорога
    /// с загрузкой JamThreshold и больше — в пробке: каждый следующий проезд платит ещё JamFee очков городу (сгорают).
    /// Магистраль в пробку не встаёт. Пропуск (через сделку) — бесплатный проезд по всем дорогам продавца.
    /// </summary>
    public sealed partial class Game
    {
        readonly Dictionary<string, int> _traffic = new Dictionary<string, int>();

        /// <summary>Растёт, когда кто-то оплачивает проезд, — для всплывающих сообщений.</summary>
        public int PassageNumber { get; private set; }
        /// <summary>Последний оплаченный маршрут (для интерфейса): кто платил и за что.</summary>
        public PaidRoute LastPaidRoute { get; private set; }
        public int LastPaidPlayer { get; private set; } = -1;

        /// <summary>Сколько маршрутов соперников владельца едет по дороге.</summary>
        public int TrafficOn(string roadId) => roadId != null && _traffic.TryGetValue(roadId, out var n) ? n : 0;

        /// <summary>Дорога в пробке: не магистраль, а загрузка не меньше порога.</summary>
        public bool IsJammed(string roadId) =>
            Config.JamThreshold > 0 && !_rushHourRoads.Contains(roadId) && TrafficOn(roadId) >= Config.JamThreshold;

        /// <summary>Сколько сверх проезда стоит проехать по дороге сейчас (уходит городу).</summary>
        public int JamFeeFor(string roadId) => IsJammed(roadId) ? Config.JamFee : 0;

        /// <summary>Есть ли у игрока пропуск по дорогам владельца.</summary>
        public bool HasPass(int holder, int owner) =>
            holder >= 0 && holder < _players.Count && _players[holder].PassesFrom.Contains(owner);

        /// <summary>Плата владельцу за проезд игрока payer по дороге (пропуск — бесплатно).</summary>
        public int TollFor(string roadId, int payer)
        {
            var owner = GetRoadOwner(roadId);
            if (!owner.HasValue || owner.Value == payer) return 0;
            return HasPass(payer, owner.Value) ? 0 : TollFor(roadId);
        }

        /// <summary>Полная цена проезда для игрока: владельцу и городу за пробку.</summary>
        public int PassageCost(string roadId, int payer) => TollFor(roadId, payer) + JamFeeFor(roadId);

        /// <summary>Зафиксированный маршрут контракта игрока или null, если контракт ещё не выполнен.</summary>
        public PaidRoute PaidRouteOf(int playerIndex, Contract contract) =>
            contract != null && _players[playerIndex].PaidRoutes.TryGetValue(contract.Id, out var route) ? route : null;

        /// <summary>Проверка по зафиксированному маршруту — выполненный контракт так и остаётся выполненным.</summary>
        ContractCheck CheckFromPaid(Contract contract, PaidRoute route) => new ContractCheck
        {
            Contract = contract,
            Completed = true,
            Paid = true,
            LegsCompleted = contract.Legs.Count,
            LegsTotal = contract.Legs.Count,
            Path = route.Path.Select(Map.GetRoad).Where(r => r != null).ToList(),
            ForeignRoads = route.Passages.Select(p => Map.GetRoad(p.RoadId)).Where(r => r != null).ToList(),
            Toll = route.Toll,
            JamFee = route.JamFee
        };

        /// <summary>
        /// Зафиксировать и оплатить маршруты всех только что выполненных контрактов (у всех игроков:
        /// чужая дорога тоже может замкнуть путь). Порядок — по игрокам и по контрактам, так что исход
        /// не зависит от того, кто смотрит.
        /// </summary>
        void SettleContracts(bool quiet = false)
        {
            foreach (var player in _players)
            {
                foreach (var contract in player.Contracts)
                {
                    if (player.PaidRoutes.ContainsKey(contract.Id)) continue;
                    var check = Scoring.CheckContract(Map, contract, player.Index, GetRoadOwner,
                        Config.MaxForeignRoadsPerContract, id => TollFor(id, player.Index), JamFeeFor);
                    if (!check.Completed) continue;

                    var passages = new List<PaidPassage>();
                    foreach (var road in check.ForeignRoads)
                    {
                        int owner = GetRoadOwner(road.Id) ?? -1;
                        passages.Add(new PaidPassage(road.Id, owner, TollFor(road.Id, player.Index), JamFeeFor(road.Id)));
                    }
                    var route = new PaidRoute(contract.Id, check.Path.Select(r => r.Id).ToList(), passages);
                    player.PaidRoutes[contract.Id] = route;
                    foreach (var passage in passages)
                    {
                        player.TollsPaid += passage.Toll;
                        player.JamFees += passage.JamFee;
                        if (passage.Owner >= 0) _players[passage.Owner].TollsEarned += passage.Toll;
                    }
                    // Загрузка растёт после оплаты: пробка касается следующих проездов.
                    foreach (var passage in passages)
                    {
                        bool wasJammed = IsJammed(passage.RoadId);
                        _traffic[passage.RoadId] = TrafficOn(passage.RoadId) + 1;
                        if (!quiet && !wasJammed && IsJammed(passage.RoadId))
                        {
                            var road = Map.GetRoad(passage.RoadId);
                            Emit($"Пробка на дороге {Map.LocationName(road.A)} — {Map.LocationName(road.B)}: дальше проезд по ней ещё +{Config.JamFee} очк. городу. Владелец может улучшить её до магистрали.");
                        }
                    }
                    if (quiet || passages.Count == 0) continue;

                    LastPaidRoute = route;
                    LastPaidPlayer = player.Index;
                    PassageNumber++;
                    var byOwner = passages.GroupBy(p => p.Owner)
                        .Select(g => $"{(g.Key >= 0 ? _players[g.Key].Name : "?")} {g.Sum(p => p.Toll)}");
                    string jam = route.JamFee > 0 ? $", пробка: ещё {route.JamFee} городу" : "";
                    Emit($"{player.Name} платит за проезд: {string.Join(", ", byOwner)} очк.{jam}.");
                }
            }
        }

        /// <summary>Загрузка дорог по сохранённым маршрутам (после загрузки партии).</summary>
        void RebuildTraffic()
        {
            _traffic.Clear();
            foreach (var player in _players)
                foreach (var route in player.PaidRoutes.Values)
                    foreach (var passage in route.Passages)
                        _traffic[passage.RoadId] = TrafficOn(passage.RoadId) + 1;
        }
    }
}
