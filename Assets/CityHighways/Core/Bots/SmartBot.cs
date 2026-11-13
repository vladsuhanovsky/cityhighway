using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Умный бот City Highways:
    /// 1) берёт контракты, которые выгодно складываются в одну сеть и помещаются в запас участков;
    /// 2) каждый ход заново ищет лучшие пути — своими дорогами или с проездом по чужим (дешевле, чем строить длинный объезд);
    /// 3) строит дорогу из плана, как только хватает карт, иначе берёт нужные ресурсы;
    /// 4) выполнив контракты, берёт новые или тянет сеть к новым районам (бонус «Крупнейшая сеть»);
    /// 5) в конце партии строит самое длинное, что может оплатить;
    /// 6) без дел по контрактам тянет сеть к открытым целям мэрии, а карту удачи берёт, когда ресурсы не горят.
    /// Логика сначала отлажена на Python-модели (тысячи партий), этот код повторяет её один в один.
    /// </summary>
    public sealed class SmartBot : IBot
    {
        /// <summary>Насколько бот не любит длинные сети: очки контракта минус Lambda × нужные участки.</summary>
        public float Lambda = 0.7f;
        /// <summary>Во сколько «участков» бот оценивает проезд по одной чужой дороге.</summary>
        public float ForeignCost = 2.5f;
        public int PieceReserve = 2;
        /// <summary>Когда у кого-то остаётся столько участков или меньше, бот переходит в режим конца партии.</summary>
        public int EndgamePieces = 5;
        public float Comfort = 0.75f;
        public float OverflowPenalty = 1.5f;
        /// <summary>Брать карту удачи вместо карты вслепую из колоды, если ресурсы не горят.</summary>
        public bool UseLuck = true;
        /// <summary>Удача — риск: бот берёт её, только когда не хватает не больше LuckDeficit карт или в руке LuckHand и больше.</summary>
        public int LuckDeficit = 2;
        public int LuckHand = 8;
        /// <summary>Предлагать и принимать сделки; улучшать дороги в конце партии.</summary>
        public bool UseTrades = true;
        public bool UseUpgrades = true;
        int _lastOfferTurn = -99;

        readonly Random _rng;

        public SmartBot(int seed = 0)
        {
            _rng = new Random(seed);
        }

        public ActionResult Act(Game game)
        {
            switch (game.Phase)
            {
                case GamePhase.SetupContracts:
                {
                    var player = game.Players[game.ActingPlayerIndex];
                    return game.KeepInitialContracts(player.Index, ChooseContracts(game, player, game.Config.InitialContractsMinKeep));
                }
                case GamePhase.ChoosingContracts:
                    return game.KeepContracts(ChooseContracts(game, game.CurrentPlayer, game.Config.ContractsMinKeep));
                case GamePhase.DrawingSecondCard:
                    return DrawBestCard(game, Analyze(game), true);
                case GamePhase.AwaitingAction:
                    return TakeTurn(game);
                case GamePhase.RespondingTrade:
                    return game.RespondTrade(UseTrades && Accepts(game, game.PendingTrade));
                case GamePhase.ChoosingLuck:
                {
                    // Везунчик оставляет карту, которая сейчас выгоднее; при равенстве — первую.
                    var choice = game.PendingLuckChoice;
                    var me = game.CurrentPlayer;
                    int best = game.LuckValue(me, choice[1]) > game.LuckValue(me, choice[0]) ? 1 : 0;
                    return game.ChooseLuckCard(best);
                }
                default:
                    return ActionResult.Fail("Игра окончена.");
            }
        }

        // ================================================================== Выбор контрактов

        List<string> ChooseContracts(Game game, PlayerState player, int minKeep)
        {
            var pending = player.PendingContracts.ToList();
            int required = Math.Min(minKeep, pending.Count);
            var planner = new BotPlanner(game, player.Index, ForeignCost);
            var basePlan = planner.BuildPlan(player.Contracts);
            int budget = player.RoadPieces - PieceReserve;

            List<Contract> best = null;
            double bestScore = double.NegativeInfinity;
            List<Contract> fallback = null;
            int fallbackKey = int.MaxValue;

            for (int mask = 1; mask < (1 << pending.Count); mask++)
            {
                var subset = new List<Contract>();
                for (int i = 0; i < pending.Count; i++)
                    if ((mask & (1 << i)) != 0) subset.Add(pending[i]);
                if (subset.Count < required) continue;

                var plan = planner.BuildPlan(player.Contracts.Concat(subset));
                bool reachable = subset.All(c => !plan.Unreachable.Contains(c));
                int extra = plan.PiecesNeeded - basePlan.PiecesNeeded;
                int extraToll = plan.TollsExpected - basePlan.TollsExpected;

                int key = extra + (reachable ? 0 : 1000) + subset.Count;
                if (key < fallbackKey)
                {
                    fallbackKey = key;
                    fallback = subset;
                }

                if (!reachable || plan.PiecesNeeded > budget) continue;

                double score = subset.Sum(c => c.Points) - Lambda * extra - extraToll;
                double comfortable = player.RoadPieces * Comfort;
                if (plan.PiecesNeeded > comfortable) score -= (plan.PiecesNeeded - comfortable) * OverflowPenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = subset;
                }
            }

            var chosen = best ?? fallback ?? pending.Take(required).ToList();
            return chosen.Select(c => c.Id).ToList();
        }

        // ================================================================== Анализ позиции

        // ================================================================== Сделки (как в Python-модели)

        /// <summary>Сколько карт ресурса лишние. Технику бережём: она джокер.</summary>
        static int SurplusOf(Analysis a, Resource x)
        {
            int have = a.Me.CardCount(x);
            if (x == Resource.Machinery) return have - 3;
            return have - a.Need[(int)x] - (a.LongestAnyNeed > 0 ? a.LongestAnyNeed : 0);
        }

        /// <summary>
        /// Предложить излишек за нужный ресурс — редко: когда до нужной дороги не хватает 1–2 карт,
        /// и не чаще раза в три своих хода. Получатель — соперники по кругу.
        /// </summary>
        ActionResult? TryPropose(Game game, Analysis a)
        {
            var me = a.Me;
            int deficit = ResourceInfo.Building.Sum(x => Math.Max(0, a.Need[(int)x] - me.CardCount(x)));
            if (!a.HasGoals || deficit == 0 || deficit > 2) return null;
            if (game.TurnNumber - _lastOfferTurn < 3 * game.Players.Count) return null;

            var want = ResourceInfo.Building.Where(x => a.Need[(int)x] > me.CardCount(x)).ToList();
            var spare = ResourceInfo.Building.Where(x => SurplusOf(a, x) >= 1 && !want.Contains(x)).ToList();
            if (want.Count == 0 || spare.Count == 0) return null;

            var get = want.OrderByDescending(x => a.Need[(int)x] - me.CardCount(x)).ThenBy(x => (int)x).First();
            var give = spare.OrderByDescending(x => SurplusOf(a, x)).ThenBy(x => (int)x).First();
            var others = game.Players.Where(p => p.Index != me.Index).Select(p => p.Index).ToList();
            int to = others[game.TurnNumber % others.Count];
            int giveCount = SurplusOf(a, give) >= 2 ? 2 : 1;
            _lastOfferTurn = game.TurnNumber;
            var result = game.ProposeTrade(to, give, giveCount, get, 1);
            return result.Success ? result : (ActionResult?)null;
        }

        /// <summary>Принять сделку, если отдаём лишнее, а получаем нужное (или больше, чем отдаём).</summary>
        bool Accepts(Game game, TradeOffer offer)
        {
            if (offer == null) return false;
            var a = Analyze(game, offer.To);
            var me = a.Me;
            if (offer.GetCount > 0 && (me.CardCount(offer.GetResource) < offer.GetCount || SurplusOf(a, offer.GetResource) < offer.GetCount))
                return false;
            // Влияние бот отдаёт неохотно: не больше половины своего запаса.
            if (offer.GetInfluence > me.Influence / 2) return false;
            bool useful = offer.GiveCount > 0
                          && (a.Need[(int)offer.GiveResource] > me.CardCount(offer.GiveResource)
                              || (a.LongestAnyNeed > 0 && offer.GiveResource != Resource.Machinery));
            if (useful) return offer.GetInfluence == 0 || offer.GiveCount >= offer.GetInfluence;
            // Ненужное берём, только если получаем больше, чем отдаём (карта ≈ единица влияния).
            return offer.GiveCount + offer.GiveInfluence > offer.GetCount + offer.GetInfluence;
        }

        // ================================================================== Влияние

        /// <summary>
        /// Траты влияния (как в модели Tools~/ch_influence.py): разрешение — если нужную дорогу закрыл парад
        /// или удорожил снег, а с разрешением её можно оплатить; обновить рынок — если ресурсы нужны, а в ряду
        /// нет ни одной полезной карты и строить нечего. Остальное влияние бот копит.
        /// </summary>
        ActionResult? TrySpendInfluence(Game game, Analysis a)
        {
            if (!game.Config.InfluenceEnabled || game.InfluenceUsedThisTurn) return null;
            var me = a.Me;
            var ev = game.CurrentEvent;
            if (ev != null && (ev.Kind == CityEventKind.Parade || ev.Kind == CityEventKind.Snow)
                && game.CanUseInfluence(InfluenceAction.Permit))
            {
                foreach (var road in a.Desired)
                {
                    if (game.GetPaymentOptions(road.Id).Count > 0 || me.RoadPieces < road.Length || game.GetRoadOwner(road.Id).HasValue) continue;
                    bool paradeBlocked = game.IsParadeClosed(road.Id);
                    bool snowCost = ev.Kind == CityEventKind.Snow && road.Resource == Resource.Gravel;
                    if (!paradeBlocked && !snowCost) continue;
                    int normalCost = road.IsBridge ? game.BuildCost(road, me) : road.Length;
                    if (CanPay(me, road, normalCost)) return game.UseInfluence(InfluenceAction.Permit);
                }
            }
            int refresh = game.Config.InfluenceRefreshCost;
            if (me.Influence >= refresh + 1 && game.CanUseInfluence(InfluenceAction.RefreshMarket))
            {
                var want = ResourceInfo.Building.Where(x => a.Need[(int)x] > me.CardCount(x)).ToList();
                bool usefulFaceUp = game.FaceUpCards.Any(c => c.HasValue && want.Contains(c.Value));
                if (want.Count > 0 && !usefulFaceUp && Affordable(game, a.Desired).Count == 0)
                    return game.UseInfluence(InfluenceAction.RefreshMarket);
            }
            return null;
        }

        static bool CanPay(PlayerState me, Road road, int cost)
        {
            int machinery = me.CardCount(Resource.Machinery);
            if (road.Resource.HasValue) return me.CardCount(road.Resource.Value) + machinery >= cost;
            return ResourceInfo.Building.Any(x => me.CardCount(x) + machinery >= cost) || machinery >= cost;
        }

        /// <summary>Взять контракты: при запасе влияния — с выбором на один больше.</summary>
        ActionResult DrawContractsSmart(Game game)
        {
            if (game.CurrentPlayer.Influence >= game.Config.InfluenceExtraContractCost + 1
                && game.CanUseInfluence(InfluenceAction.ExtraContracts))
                return game.UseInfluence(InfluenceAction.ExtraContracts);
            return game.DrawContracts();
        }

        sealed class Analysis
        {
            public PlayerState Me;
            public List<Road> Desired;
            public bool HasGoals;
            public readonly int[] Need = new int[ResourceInfo.Count];
            public int LongestAnyNeed;
        }

        Analysis Analyze(Game game, int? who = null)
        {
            var me = who.HasValue ? game.Players[who.Value] : game.CurrentPlayer;
            var plan = new BotPlanner(game, me.Index, ForeignCost).BuildPlan(me.Contracts);
            var a = new Analysis { Me = me };
            a.Desired = plan.RoadsToBuild.Where(r => r.Length <= me.RoadPieces).ToList();
            a.HasGoals = a.Desired.Count > 0;

            if (!a.HasGoals)
            {
                // Контракты выполнены — тянем сеть дальше: сначала дороги, которые продвигают открытую цель мэрии,
                // затем примыкающие к своей сети, потом длинные.
                var mine = new HashSet<string>();
                foreach (var road in me.Roads)
                {
                    mine.Add(road.A);
                    mine.Add(road.B);
                }
                var liveGoals = game.ActiveGoals.Where(g => !game.GoalOwner(g.Id).HasValue).ToList();
                var progress = liveGoals.ToDictionary(g => g.Id, g => g.Progress(game.Map, me.Roads));
                Func<Road, int> helps = r =>
                {
                    var withRoad = me.Roads.Concat(new[] { r }).ToList();
                    return liveGoals.Count(g => g.Progress(game.Map, withRoad) > progress[g.Id]);
                };
                a.Desired = game.Map.Roads
                    .Where(r => !game.GetRoadOwner(r.Id).HasValue && r.Length <= me.RoadPieces && game.IsRoadOpen(r.Id))
                    .OrderByDescending(helps)
                    .ThenByDescending(r => mine.Contains(r.A) || mine.Contains(r.B))
                    .ThenByDescending(r => r.Length)
                    .ThenBy(r => r.Id, StringComparer.Ordinal)
                    .Take(3)
                    .ToList();
            }

            foreach (var road in a.Desired)
            {
                if (road.Resource.HasValue) a.Need[(int)road.Resource.Value] += road.Length;
                else a.LongestAnyNeed = Math.Max(a.LongestAnyNeed, road.Length);
            }
            return a;
        }

        // ================================================================== Ход

        ActionResult TakeTurn(Game game)
        {
            var a = Analyze(game);
            var me = a.Me;
            if (UseTrades && game.CanProposeTrade())
            {
                var offer = TryPropose(game, a);
                if (offer.HasValue) return offer.Value;
            }
            var spent = TrySpendInfluence(game, a);
            if (spent.HasValue && spent.Value.Success) return spent.Value;
            bool endgame = game.IsFinalRound || game.Players.Any(p => p.RoadPieces <= EndgamePieces);

            // Прораб тратит бесплатную стройку на нужную дорогу полной длины (или на любую нужную в конце партии).
            if (me.Character == CharacterKind.Foreman && me.FreeRoadsLeft > 0)
            {
                var free = a.Desired
                    .Where(r => game.CanBuildRoadFree(r.Id) && (r.Length == game.Config.ForemanMaxLength || endgame))
                    .OrderByDescending(r => r.Length)
                    .ThenByDescending(r => r.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (free != null) return game.BuildRoadFree(free.Id);
            }

            var affordable = Affordable(game, a.Desired);
            if (a.HasGoals && affordable.Count > 0) return Build(game, a, Longest(affordable));

            if (endgame)
            {
                var any = Affordable(game, game.Map.Roads);
                if (any.Count > 0) return Build(game, a, Longest(any));

                // Строить нечего — улучшаем самую «проезжую» свою дорогу (больше всего соседних дорог).
                if (UseUpgrades)
                {
                    var upgrade = me.Roads
                        .Select(r => (road: r, options: game.GetUpgradeOptions(r.Id)))
                        .Where(x => x.options.Count > 0)
                        .OrderByDescending(x => game.Map.RoadsAt(x.road.A).Count() + game.Map.RoadsAt(x.road.B).Count())
                        .ThenByDescending(x => x.road.Length)
                        .ThenByDescending(x => x.road.Id, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (upgrade.road != null)
                    {
                        var pay = upgrade.options
                            .OrderBy(o => o.Resource == Resource.Machinery ? 1 : 0)
                            .ThenBy(o => o.Machinery)
                            .First();
                        return game.UpgradeRoad(upgrade.road.Id, pay.Resource, pay.Machinery);
                    }
                }
            }

            if (!a.HasGoals)
            {
                bool calm = !game.Players.Any(p => p.RoadPieces <= 12);
                if (game.ContractDeckCount > 0 && me.RoadPieces >= 14 && calm && !game.IsFinalRound)
                    return DrawContractsSmart(game);
                if (affordable.Count > 0)
                {
                    var longest = Longest(affordable);
                    if (longest.Key.Length >= 3 || me.HandSize >= 10) return Build(game, a, longest);
                }
            }

            if (game.CanDrawAnyCard()) return DrawBestCard(game, a, false);

            // Карт нет совсем: строим что угодно, иначе берём контракты (пасовать при непустой колоде нельзя).
            var fallback = Affordable(game, game.Map.Roads);
            if (fallback.Count > 0) return Build(game, a, Longest(fallback));
            if (game.ContractDeckCount > 0) return game.DrawContracts();
            return game.Pass();
        }

        static List<KeyValuePair<Road, IReadOnlyList<PaymentOption>>> Affordable(Game game, IEnumerable<Road> roads)
        {
            var result = new List<KeyValuePair<Road, IReadOnlyList<PaymentOption>>>();
            foreach (var road in roads)
            {
                var options = game.GetPaymentOptions(road.Id);
                if (options.Count > 0) result.Add(new KeyValuePair<Road, IReadOnlyList<PaymentOption>>(road, options));
            }
            return result;
        }

        static KeyValuePair<Road, IReadOnlyList<PaymentOption>> Longest(List<KeyValuePair<Road, IReadOnlyList<PaymentOption>>> options) =>
            options.OrderByDescending(x => x.Key.Length).ThenByDescending(x => x.Key.Id, StringComparer.Ordinal).First();

        /// <summary>Платим, тратя меньше техники и не трогая ресурсы, нужные для следующих дорог.</summary>
        static ActionResult Build(Game game, Analysis a, KeyValuePair<Road, IReadOnlyList<PaymentOption>> choice)
        {
            var road = choice.Key;
            var option = choice.Value
                .OrderBy(o => o.Resource == Resource.Machinery ? 1 : 0)
                .ThenBy(o => o.Machinery)
                .ThenByDescending(o => Surplus(a, road, o))
                .First();
            return game.BuildRoad(road.Id, option.Resource, option.Machinery);
        }

        static int Surplus(Analysis a, Road road, PaymentOption option)
        {
            if (option.Resource == Resource.Machinery) return 0;
            int spent = road.Length - option.Machinery;
            int need = a.Need[(int)option.Resource] - (road.Resource == option.Resource ? road.Length : 0);
            return a.Me.CardCount(option.Resource) - spent - need;
        }

        // ================================================================== Карты

        ActionResult DrawBestCard(Game game, Analysis a, bool isSecond)
        {
            var me = a.Me;
            int deficit = ResourceInfo.Building.Sum(r => Math.Max(0, a.Need[(int)r] - me.CardCount(r)));
            Func<Resource, int> surplus = r => me.CardCount(r) - a.Need[(int)r];

            // Дороги «любой ресурс» удобнее оплачивать тем, чего больше всего «лишнего».
            var anyResource = ResourceInfo.Building.OrderByDescending(r => surplus(r)).ThenBy(r => (int)r).First();
            int anyDeficit = a.LongestAnyNeed > 0 ? Math.Max(0, a.LongestAnyNeed - Math.Max(0, surplus(anyResource))) : 0;
            deficit += anyDeficit;

            int bestSlot = -1;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < game.FaceUpCards.Count; i++)
            {
                var card = game.FaceUpCards[i];
                if (!card.HasValue) continue;
                if (isSecond && card.Value == Resource.Machinery) continue;

                double value;
                var c = card.Value;
                if (c == Resource.Machinery) value = deficit >= 3 ? 2.5 : 0.5;
                else if (a.Need[(int)c] > me.CardCount(c)) value = 3.0;
                else if (anyDeficit > 0 && c == anyResource) value = 2.5;
                else if (anyDeficit > 0 && surplus(c) >= 0) value = 1.5;
                else value = 0.0;

                value += _rng.NextDouble() * 0.01;
                if (value > bestScore)
                {
                    bestScore = value;
                    bestSlot = i;
                }
            }

            bool deckAvailable = game.DeckCount + game.DiscardCount > 0;
            if (bestSlot >= 0 && (bestScore >= 1.5 || !deckAvailable)) return game.DrawFaceUpCard(bestSlot);
            bool bold = me.Character == CharacterKind.Insurer; // Страховщику удача почти без риска — берёт всегда
            if (UseLuck && game.CanDrawLuckCard() && (bold || deficit <= LuckDeficit || me.HandSize >= LuckHand))
                return game.DrawLuckCard();
            if (deckAvailable) return game.DrawCardFromDeck();
            return ActionResult.Fail("Нечего взять.");
        }
    }
}
