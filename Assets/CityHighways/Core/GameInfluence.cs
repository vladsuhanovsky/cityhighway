using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>На что можно потратить влияние (раз в ход, до основного действия, ход не тратится).</summary>
    public enum InfluenceAction
    {
        /// <summary>Контроль рынка: весь открытый ряд уходит в сброс, выкладывается новый.</summary>
        RefreshMarket,
        /// <summary>Контроль рынка: взять контракты с выбором на один больше (это и есть действие хода).</summary>
        ExtraContracts,
        /// <summary>Влияние на город: разрешение — в этот ход парад и снег на вас не действуют.</summary>
        Permit,
        /// <summary>Влияние на город: отменить текущее событие для всех (кроме «Нового района»).</summary>
        CancelEvent
    }

    public static class InfluenceInfo
    {
        public static readonly InfluenceAction[] All =
            { InfluenceAction.RefreshMarket, InfluenceAction.ExtraContracts, InfluenceAction.Permit, InfluenceAction.CancelEvent };

        public static int Cost(InfluenceAction action, GameConfig config)
        {
            switch (action)
            {
                case InfluenceAction.RefreshMarket: return config.InfluenceRefreshCost;
                case InfluenceAction.ExtraContracts: return config.InfluenceExtraContractCost;
                case InfluenceAction.Permit: return config.InfluencePermitCost;
                default: return config.InfluenceCancelEventCost;
            }
        }

        public static string Title(InfluenceAction action)
        {
            switch (action)
            {
                case InfluenceAction.RefreshMarket: return "Обновить рынок";
                case InfluenceAction.ExtraContracts: return "Контракты с выбором";
                case InfluenceAction.Permit: return "Разрешение мэрии";
                default: return "Отменить событие";
            }
        }

        public static string Describe(InfluenceAction action, GameConfig config)
        {
            switch (action)
            {
                case InfluenceAction.RefreshMarket: return "Все открытые карты уходят в сброс, выкладываются новые. Ход продолжается.";
                case InfluenceAction.ExtraContracts: return $"Взять {config.ContractsDrawn + 1} контракта вместо {config.ContractsDrawn} и выбрать из них. Это действие хода.";
                case InfluenceAction.Permit: return "В этот ход парад не закрывает для вас дороги, а снег не удорожает щебёнку.";
                default: return "Текущее событие заканчивается для всех прямо сейчас («Новый район» не отменить).";
            }
        }

        /// <summary>Город (разрешение, отмена события) или рынок — для группировки в интерфейсе.</summary>
        public static bool IsCityAction(InfluenceAction action) =>
            action == InfluenceAction.Permit || action == InfluenceAction.CancelEvent;
    }

    // Влияние: начисление и траты.
    public sealed partial class Game
    {
        bool _influenceUsedThisTurn;
        bool _permitActive;

        /// <summary>Влияние в этот ход уже тратилось.</summary>
        public bool InfluenceUsedThisTurn => _influenceUsedThisTurn;
        /// <summary>У текущего игрока в этот ход есть разрешение мэрии.</summary>
        public bool PermitActive => _permitActive;

        /// <summary>Почему текущий игрок не может сейчас потратить влияние так; null — может.</summary>
        public string GetInfluenceBlocker(InfluenceAction action)
        {
            if (!Config.InfluenceEnabled) return "Влияние выключено.";
            if (Phase != GamePhase.AwaitingAction) return "Влияние тратят в начале хода, до основного действия.";
            if (_influenceUsedThisTurn) return "Влияние уже тратилось в этот ход.";
            int cost = InfluenceInfo.Cost(action, Config);
            if (CurrentPlayer.Influence < cost) return $"Нужно влияния: {cost}, у вас {CurrentPlayer.Influence}.";
            switch (action)
            {
                case InfluenceAction.RefreshMarket:
                    if (_deck.AvailableCount == 0) return "Колода пуста — выкладывать нечего.";
                    break;
                case InfluenceAction.ExtraContracts:
                    if (_contractDeck.Count == 0) return "Контракты закончились.";
                    break;
                case InfluenceAction.Permit:
                    if (CurrentEvent == null || (CurrentEvent.Kind != CityEventKind.Parade && CurrentEvent.Kind != CityEventKind.Snow))
                        return "Разрешение нужно только в парад или снег.";
                    break;
                case InfluenceAction.CancelEvent:
                    if (CurrentEvent == null) return "Сейчас нет события.";
                    if (CurrentEvent.Kind == CityEventKind.NewDistrict) return "Новый район уже открыт — это не отменить.";
                    break;
            }
            return null;
        }

        public bool CanUseInfluence(InfluenceAction action) => GetInfluenceBlocker(action) == null;

        ActionResult DoUseInfluence(InfluenceAction action)
        {
            var blocker = GetInfluenceBlocker(action);
            if (blocker != null) return ActionResult.Fail(blocker);
            var player = CurrentPlayer;
            int cost = InfluenceInfo.Cost(action, Config);
            player.Influence -= cost;
            player.InfluenceSpent += cost;
            _influenceUsedThisTurn = true;

            switch (action)
            {
                case InfluenceAction.RefreshMarket:
                    for (int i = 0; i < _faceUp.Length; i++)
                    {
                        if (!_faceUp[i].HasValue) continue;
                        _deck.Discard(_faceUp[i].Value);
                        _faceUp[i] = null;
                    }
                    RefillFaceUpRow();
                    Emit($"{player.Name} тратит {cost} влияния: рынок выложен заново.");
                    Changed();
                    break;
                case InfluenceAction.ExtraContracts:
                    Emit($"{player.Name} тратит {cost} влияния на выбор контрактов.");
                    return DoDrawContracts(1);
                case InfluenceAction.Permit:
                    _permitActive = true;
                    Emit($"{player.Name} получает разрешение мэрии за {cost} влияния: «{CurrentEvent.Title}» в этот ход ему не мешает.");
                    Changed();
                    break;
                default:
                    Emit($"{player.Name} тратит {cost} влияния и отменяет событие «{CurrentEvent.Title}».");
                    CurrentEvent = null;
                    _repairRoads.Clear();
                    Changed();
                    break;
            }
            return ActionResult.Ok();
        }

        // ================================================================== Начисление

        void EarnInfluence(PlayerState player, int amount, string why)
        {
            if (!Config.InfluenceEnabled || amount <= 0) return;
            player.Influence += amount;
            player.InfluenceEarned += amount;
            Emit($"{player.Name}: +{amount} влияния ({why}).");
        }

        /// <summary>
        /// После постройки: новый район в сети строителя и впервые выполненные контракты — у всех игроков
        /// (чужая дорога тоже может замкнуть путь контракта). Первая дорога компании за районы не платит.
        /// </summary>
        void AwardInfluenceForRoad(PlayerState builder, Road road)
        {
            if (!Config.InfluenceEnabled) return;
            bool first = builder.InfluenceDistricts.Count == 0;
            bool newDistrict = false;
            foreach (var district in new[] { Map.DistrictOf(road.A), Map.DistrictOf(road.B) })
                if (district != null && builder.InfluenceDistricts.Add(district)) newDistrict = true;
            if (newDistrict && !first) EarnInfluence(builder, Config.InfluencePerNewDistrict, "новый район");

            foreach (var player in _players)
            {
                foreach (var contract in player.Contracts)
                {
                    if (player.RewardedContracts.Contains(contract.Id)) continue;
                    if (!CheckContract(player.Index, contract).Completed) continue;
                    player.RewardedContracts.Add(contract.Id);
                    bool complex = contract.Kind != ContractKind.Link;
                    EarnInfluence(player, complex ? Config.InfluencePerComplexContract : Config.InfluencePerContract,
                        $"контракт {Map.DistrictName(contract.DistrictA)} — {Map.DistrictName(contract.DistrictB)} выполнен");
                }
            }
        }

        /// <summary>Начало круга: заметно отстающим (по видимым очкам) — немного влияния, чтобы держать интригу.</summary>
        void AwardCatchUpInfluence()
        {
            if (!Config.InfluenceEnabled || TurnNumber <= 1 || _players.Count < 2) return;
            var visible = _players.Select(VisibleScore).ToList();
            int best = visible.Max(), worst = visible.Min();
            if (best - worst < Config.InfluenceCatchUpGap) return;
            for (int i = 0; i < _players.Count; i++)
                if (visible[i] == worst) EarnInfluence(_players[i], Config.InfluenceCatchUp, "поддержка отстающих");
        }

        /// <summary>Очки, которые видны всем во время партии (без контрактов, они секретны).</summary>
        public int VisibleScore(PlayerState player) => player.RoadPoints + player.GoalPoints + player.LuckPoints;

        void ResetInfluenceTurn()
        {
            _influenceUsedThisTurn = false;
            _permitActive = false;
        }

        // ================================================================== Для тестов

        internal void Debug_SetInfluence(int playerIndex, int amount) => _players[playerIndex].Influence = amount;
    }
}
