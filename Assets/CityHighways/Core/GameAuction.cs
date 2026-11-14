using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Итог аукциона мэрии: кто выиграл (-1 — никто), за сколько и все ставки по игрокам.</summary>
    public sealed class AuctionResult
    {
        public Contract Contract { get; }
        public int Winner { get; }
        public int Price { get; }
        public IReadOnlyList<int> Bids { get; }

        public AuctionResult(Contract contract, int winner, int price, IReadOnlyList<int> bids)
        {
            Contract = contract;
            Winner = winner;
            Price = price;
            Bids = bids;
        }
    }

    /// <summary>
    /// Аукцион мэрии: раз за партию, когда кто-то построил треть своих участков, мэрия выставляет выгодный
    /// контракт (самый дорогой из верхних в колоде). Каждый по кругу делает одну тайную ставку влиянием;
    /// выигрывает самая высокая (при равенстве — отстающий по видимым очкам, затем раньше по кругу) и платит
    /// свою ставку. Контракт становится обычным контрактом победителя, а если он выполнен к концу партии —
    /// ещё +AuctionBonus очков. Не выполнен — штраф как у любого контракта. Ставок нет — контракт под колоду.
    /// </summary>
    public sealed partial class Game
    {
        bool _auctionHeld;
        Contract _auctionContract;
        int[] _bids;           // -1 — игрок ещё не ставил
        int _auctionStarter;

        public bool AuctionHeld => _auctionHeld;
        /// <summary>Контракт, который сейчас разыгрывается (только в фазе Bidding).</summary>
        public Contract AuctionContract => Phase == GamePhase.Bidding ? _auctionContract : null;
        public AuctionResult LastAuction { get; private set; }
        /// <summary>Растёт при начале и итоге аукциона — интерфейс по нему замечает перемены.</summary>
        public int AuctionNumber { get; private set; }

        /// <summary>Сделал ли игрок ставку в текущем аукционе (саму ставку до итога не раскрываем).</summary>
        public bool HasBid(int playerIndex) => _bids != null && playerIndex >= 0 && playerIndex < _bids.Length && _bids[playerIndex] >= 0;

        /// <summary>Чья очередь ставить: первый по кругу от начавшего, кто ещё не ставил.</summary>
        int AuctionBidder
        {
            get
            {
                if (_bids == null) return -1;
                for (int k = 0; k < _players.Count; k++)
                {
                    int i = (_auctionStarter + k) % _players.Count;
                    if (_bids[i] < 0) return i;
                }
                return -1;
            }
        }

        /// <summary>Начало круга: пора ли аукциону (раз за партию, когда кто-то построил долю своих участков).</summary>
        void TryStartAuction(bool force = false)
        {
            if (!Config.AuctionEnabled || !Config.InfluenceEnabled || _auctionHeld) return;
            if (Phase != GamePhase.AwaitingAction) return;
            if (!force)
            {
                if (TurnNumber <= 1) return;
                int start = Config.RoadPiecesFor(_players.Count);
                int threshold = (int)Math.Ceiling(start * Config.AuctionTriggerShare);
                if (!_players.Any(p => start - p.RoadPieces >= threshold)) return;
            }

            var contract = _contractDeck.Cards.Take(Config.AuctionCandidates).Where(IsContractOpen)
                .OrderByDescending(c => c.Points).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (contract == null) return; // колода пуста — попробуем в следующем круге
            _contractDeck.Remove(contract);

            _auctionHeld = true;
            _auctionContract = contract;
            _bids = Enumerable.Repeat(-1, _players.Count).ToArray();
            _auctionStarter = CurrentPlayerIndex;
            Phase = GamePhase.Bidding;
            AuctionNumber++;
            Emit($"Аукцион мэрии: контракт {Map.DistrictName(contract.DistrictA)} — {Map.DistrictName(contract.DistrictB)} " +
                 $"({contract.Points} очк., выполнен — ещё +{Config.AuctionBonus}). Тайные ставки влиянием.");
            // Без влияния ставить нечего — ставка 0 сразу.
            foreach (var player in _players)
                if (player.Influence == 0) _bids[player.Index] = 0;
            if (AuctionBidder < 0) ResolveAuction();
        }

        ActionResult DoBid(int amount)
        {
            if (Phase != GamePhase.Bidding) return ActionResult.Fail("Сейчас нет аукциона.");
            int bidder = AuctionBidder;
            if (bidder < 0) return ActionResult.Fail("Все ставки уже сделаны.");
            var player = _players[bidder];
            if (amount < 0) return ActionResult.Fail("Ставка не может быть меньше нуля.");
            if (amount > player.Influence) return ActionResult.Fail($"У вас только {player.Influence} влияния.");
            _bids[bidder] = amount;
            Emit($"{player.Name} делает тайную ставку.");
            if (AuctionBidder < 0) ResolveAuction();
            else Changed();
            return ActionResult.Ok();
        }

        void ResolveAuction()
        {
            var contract = _auctionContract;
            int best = _bids.Max();
            int winner = -1;
            if (best > 0)
            {
                // Равные ставки: отстающий по видимым очкам, затем раньше по кругу от начавшего.
                winner = Enumerable.Range(0, _players.Count)
                    .Select(k => (_auctionStarter + k) % _players.Count)
                    .Where(i => _bids[i] == best)
                    .OrderBy(i => VisibleScore(_players[i]))
                    .ThenBy(i => (i - _auctionStarter + _players.Count) % _players.Count)
                    .First();
            }
            string bids = string.Join(", ", _players.Select(p => $"{p.Name} {_bids[p.Index]}"));
            if (winner >= 0)
            {
                var player = _players[winner];
                player.Influence -= best;
                player.InfluenceSpent += best;
                player.ContractList.Add(contract);
                player.MayorContractId = contract.Id;
                Emit($"Аукцион выигрывает {player.Name} за {best} влияния (ставки: {bids}).");
                SettleContracts();
            }
            else
            {
                _contractDeck.ReturnToBottom(new[] { contract });
                Emit($"На аукционе никто не поставил — контракт уходит под колоду (ставки: {bids}).");
            }
            LastAuction = new AuctionResult(contract, winner, Math.Max(0, best), _bids.ToArray());
            AuctionNumber++;
            _auctionContract = null;
            _bids = null;
            Phase = GamePhase.AwaitingAction;
            Changed();
        }

        // ================================================================== Для тестов

        /// <summary>Начать аукцион прямо сейчас (не дожидаясь трети участков).</summary>
        internal void Debug_StartAuction() => TryStartAuction(true);
    }
}
