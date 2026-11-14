using System;
using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>
    /// Предложение обмена: From отдаёт GiveCount карт GiveResource, GiveInfluence влияния и (GivePass) пропуск по своим
    /// дорогам, а получает от To GetCount карт GetResource, GetInfluence влияния и (GetPass) пропуск по дорогам To.
    /// Каждая сторона может состоять из чего угодно из этого, но не из пустоты.
    /// </summary>
    public sealed class TradeOffer
    {
        public int From { get; }
        public int To { get; }
        public Resource GiveResource { get; }
        public int GiveCount { get; }
        public Resource GetResource { get; }
        public int GetCount { get; }
        public int GiveInfluence { get; }
        public int GetInfluence { get; }
        /// <summary>From даёт To пропуск по своим дорогам / просит у To пропуск по его дорогам.</summary>
        public bool GivePass { get; }
        public bool GetPass { get; }

        public TradeOffer(int from, int to, Resource giveResource, int giveCount, Resource getResource, int getCount,
            int giveInfluence = 0, int getInfluence = 0, bool givePass = false, bool getPass = false)
        {
            GivePass = givePass;
            GetPass = getPass;
            From = from;
            To = to;
            GiveResource = giveResource;
            GiveCount = giveCount;
            GetResource = getResource;
            GetCount = getCount;
            GiveInfluence = giveInfluence;
            GetInfluence = getInfluence;
        }

        public static string Side(int count, Resource resource, int influence, bool pass = false)
        {
            var parts = new List<string>();
            if (count > 0) parts.Add($"{count} × {ResourceInfo.RussianName(resource)}");
            if (influence > 0) parts.Add($"{influence} влияния");
            if (pass) parts.Add("пропуск по дорогам");
            return parts.Count > 0 ? string.Join(" и ", parts) : "ничего";
        }

        public string Describe(Game game) =>
            $"{game.Players[From].Name} предлагает {game.Players[To].Name}: " +
            $"{Side(GiveCount, GiveResource, GiveInfluence, GivePass)} за {Side(GetCount, GetResource, GetInfluence, GetPass)}";
    }

    // Сделки между игроками и улучшение дорог.
    public sealed partial class Game
    {
        bool _tradeUsedThisTurn;

        /// <summary>Предложение, на которое сейчас отвечает его получатель (фаза RespondingTrade).</summary>
        public TradeOffer PendingTrade { get; private set; }
        /// <summary>Последняя сделка и её итог — для интерфейса (TradeNumber растёт с каждым ответом).</summary>
        public TradeOffer LastTrade { get; private set; }
        public bool LastTradeAccepted { get; private set; }
        public int TradeNumber { get; private set; }
        public bool TradeUsedThisTurn => _tradeUsedThisTurn;

        // ================================================================== Сделки

        /// <summary>Можно ли сейчас предложить сделку: раз в ход, до основного действия.</summary>
        public bool CanProposeTrade() =>
            Config.TradesEnabled && Phase == GamePhase.AwaitingAction && !_tradeUsedThisTurn && _players.Count > 1;

        ActionResult DoProposeTrade(int to, Resource giveResource, int giveCount, Resource getResource, int getCount,
            int giveInfluence = 0, int getInfluence = 0, bool givePass = false, bool getPass = false)
        {
            if (!Config.TradesEnabled) return ActionResult.Fail("Сделки выключены.");
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сделку можно предложить только в начале хода.");
            if (_tradeUsedThisTurn) return ActionResult.Fail("Сделку можно предложить только одну за ход.");
            if (to < 0 || to >= _players.Count || to == CurrentPlayerIndex) return ActionResult.Fail("Выберите другого игрока.");
            int maxInfluence = Config.InfluenceEnabled ? Config.TradeMaxInfluence : 0;
            if (giveCount < 0 || giveCount > Config.TradeMaxCards || getCount < 0 || getCount > Config.TradeMaxCards)
                return ActionResult.Fail($"В сделке до {Config.TradeMaxCards} карт с каждой стороны.");
            if (giveInfluence < 0 || giveInfluence > maxInfluence || getInfluence < 0 || getInfluence > maxInfluence)
                return ActionResult.Fail(maxInfluence > 0 ? $"В сделке до {maxInfluence} влияния с каждой стороны." : "Влияние выключено.");
            if ((givePass || getPass) && !Config.PassesEnabled) return ActionResult.Fail("Пропуска выключены.");
            if (givePass && HasPass(to, CurrentPlayerIndex)) return ActionResult.Fail($"У {_players[to].Name} уже есть ваш пропуск.");
            if (getPass && HasPass(CurrentPlayerIndex, to)) return ActionResult.Fail($"У вас уже есть пропуск {_players[to].Name}.");
            if ((giveCount + giveInfluence == 0 && !givePass) || (getCount + getInfluence == 0 && !getPass))
                return ActionResult.Fail("Каждая сторона должна что-то отдать: карты, влияние или пропуск.");
            if (giveCount > 0 && getCount > 0 && giveResource == getResource) return ActionResult.Fail("Меняют разные ресурсы.");
            var me = CurrentPlayer;
            if (giveCount > 0 && me.CardCount(giveResource) < giveCount) return ActionResult.Fail("У вас нет столько карт.");
            if (me.Influence < giveInfluence) return ActionResult.Fail("У вас нет столько влияния.");

            _tradeUsedThisTurn = true;
            var offer = new TradeOffer(me.Index, to, giveResource, giveCount, getResource, getCount, giveInfluence, getInfluence,
                givePass, getPass);
            Emit(offer.Describe(this) + ".");

            if ((getCount > 0 && _players[to].CardCount(getResource) < getCount) || _players[to].Influence < getInfluence)
            {
                // Отвечать нечем — сделка не состоялась (без лишнего окна у получателя).
                Emit($"У {_players[to].Name} не нашлось нужного — сделки нет.");
                FinishTrade(offer, false);
                Changed();
                return ActionResult.Ok();
            }
            PendingTrade = offer;
            Phase = GamePhase.RespondingTrade;
            Changed();
            return ActionResult.Ok();
        }

        ActionResult DoRespondTrade(bool accept)
        {
            if (Phase != GamePhase.RespondingTrade || PendingTrade == null) return ActionResult.Fail("Сейчас нет предложения.");
            var offer = PendingTrade;
            var from = _players[offer.From];
            var to = _players[offer.To];
            if (accept)
            {
                if (from.CardCount(offer.GiveResource) < offer.GiveCount || to.CardCount(offer.GetResource) < offer.GetCount
                    || from.Influence < offer.GiveInfluence || to.Influence < offer.GetInfluence)
                    return ActionResult.Fail("Для сделки не хватает карт или влияния.");
                if (offer.GiveCount > 0) from.RemoveCards(offer.GiveResource, offer.GiveCount);
                for (int i = 0; i < offer.GiveCount; i++) to.AddCard(offer.GiveResource);
                if (offer.GetCount > 0) to.RemoveCards(offer.GetResource, offer.GetCount);
                for (int i = 0; i < offer.GetCount; i++) from.AddCard(offer.GetResource);
                from.Influence += offer.GetInfluence - offer.GiveInfluence;
                to.Influence += offer.GiveInfluence - offer.GetInfluence;
                if (offer.GivePass) to.PassesFrom.Add(from.Index);
                if (offer.GetPass) from.PassesFrom.Add(to.Index);
                Emit($"{to.Name} соглашается на сделку.");
                if (offer.GivePass) Emit($"{to.Name} теперь ездит по дорогам {from.Name} бесплатно.");
                if (offer.GetPass) Emit($"{from.Name} теперь ездит по дорогам {to.Name} бесплатно.");
            }
            else Emit($"{to.Name} отказывается от сделки.");

            PendingTrade = null;
            Phase = GamePhase.AwaitingAction;
            FinishTrade(offer, accept);
            Changed();
            return ActionResult.Ok();
        }

        void FinishTrade(TradeOffer offer, bool accepted)
        {
            LastTrade = offer;
            LastTradeAccepted = accepted;
            TradeNumber++;
        }

        // ================================================================== Улучшение дорог

        /// <summary>Сколько карт стоит улучшить дорогу: половина длины с округлением вверх.</summary>
        public int UpgradeCost(Road road) => (road.Length + 1) / 2;

        /// <summary>Можно ли улучшить: своя, ещё не магистраль, сейчас время основного действия.</summary>
        public string GetUpgradeBlocker(string roadId)
        {
            if (!Config.UpgradeEnabled) return "Улучшение дорог выключено.";
            if (Phase != GamePhase.AwaitingAction) return "Сейчас нельзя улучшать дороги.";
            var road = Map.GetRoad(roadId);
            if (road == null) return "Нет такой дороги.";
            if (GetRoadOwner(roadId) != CurrentPlayerIndex) return "Улучшать можно только свои дороги.";
            if (_rushHourRoads.Contains(roadId)) return "Эта дорога уже магистраль.";
            return null;
        }

        public IReadOnlyList<PaymentOption> GetUpgradeOptions(string roadId)
        {
            var options = new List<PaymentOption>();
            if (GetUpgradeBlocker(roadId) != null) return options;
            var road = Map.GetRoad(roadId);
            var player = CurrentPlayer;
            int cost = UpgradeCost(road);
            int machinery = player.CardCount(Resource.Machinery);
            IEnumerable<Resource> resources = road.Resource.HasValue
                ? new[] { road.Resource.Value }
                : (IEnumerable<Resource>)ResourceInfo.Building;
            foreach (var resource in resources)
            {
                int have = player.CardCount(resource);
                if (have == 0) continue;
                int needMachinery = cost - Math.Min(have, cost);
                if (needMachinery <= machinery) options.Add(new PaymentOption(resource, needMachinery));
            }
            if (machinery >= cost) options.Add(new PaymentOption(Resource.Machinery, cost));
            return options;
        }

        /// <summary>Улучшить свою дорогу до магистрали: проезд ×RushHourTollMultiplier, +UpgradePoints очков. Это ход.</summary>
        ActionResult DoUpgradeRoad(string roadId, Resource resource, int machinery)
        {
            var blocker = GetUpgradeBlocker(roadId);
            if (blocker != null) return ActionResult.Fail(blocker);
            bool allowed = false;
            foreach (var option in GetUpgradeOptions(roadId))
                if (option.Resource == resource && option.Machinery == machinery) allowed = true;
            if (!allowed) return ActionResult.Fail("Так оплатить улучшение нельзя.");

            var road = Map.GetRoad(roadId);
            var player = CurrentPlayer;
            int cards = UpgradeCost(road) - machinery;
            if (resource != Resource.Machinery) Pay(player, resource, cards);
            Pay(player, Resource.Machinery, machinery);
            _rushHourRoads.Add(roadId);
            player.RoadPoints += Config.UpgradePoints;
            Emit($"{player.Name} улучшает дорогу {Map.LocationName(road.A)} — {Map.LocationName(road.B)} до магистрали (+{Config.UpgradePoints}).");
            EndTurn();
            return ActionResult.Ok();
        }

        // ================================================================== Для тестов

        internal void Debug_ResetTradeTurn() => _tradeUsedThisTurn = false;
    }
}
