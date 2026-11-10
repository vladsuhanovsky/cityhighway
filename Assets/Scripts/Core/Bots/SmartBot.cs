using System;
using System.Collections.Generic;
using System.Linq;

namespace RailGame.Core
{
    /// <summary>
    /// Бот, который играет «по-человечески»:
    /// 1) берёт задания, которые выгодно складываются в одну сеть и помещаются в запас вагонов;
    /// 2) каждый ход пересчитывает кратчайшие пути (соперники могли перекрыть дорогу);
    /// 3) занимает нужный маршрут, как только хватает карт, иначе тянет карты нужных цветов;
    /// 4) выполнив задания, берёт новые или тратит вагоны на длинные маршруты;
    /// 5) в конце партии занимает самое дорогое, что может оплатить.
    /// </summary>
    public sealed class SmartBot : IBot
    {
        // Насколько бот не любит длинные сети: очки задания минус Lambda × вагонов на него.
        const double Lambda = 0.7;
        const int TrainReserve = 2;
        const int EndgameTrains = 6;

        readonly Random _rng;

        public SmartBot(int seed = 0)
        {
            _rng = new Random(seed);
        }

        public ActionResult Act(Game game)
        {
            var move = Decide(game);
            return move == null ? ActionResult.Fail("Игра окончена.") : move.Apply(game);
        }

        /// <summary>Что бот сделал бы сейчас за игрока, чья очередь. null — игра окончена.</summary>
        public BotMove Decide(Game game)
        {
            switch (game.Phase)
            {
                case GamePhase.SetupTickets:
                {
                    var player = game.Players[game.ActingPlayerIndex];
                    var keep = ChooseTickets(game, player, game.Config.InitialTicketsMinKeep);
                    return BotMove.KeepInitial(player.Index, keep);
                }
                case GamePhase.ChoosingTickets:
                    return BotMove.Keep(ChooseTickets(game, game.CurrentPlayer, game.Config.TicketsMinKeep));
                case GamePhase.DrawingSecondCard:
                    return DrawBestCard(game, Analyze(game), isSecondCard: true);
                case GamePhase.AwaitingAction:
                    return TakeTurn(game);
                default:
                    return null;
            }
        }

        /// <summary>Маршруты, которые бот проложил бы под задания игрока (для подсветки «моего пути»).</summary>
        public static List<Route> PlannedRoutes(Game game, int playerIndex)
        {
            var player = game.Players[playerIndex];
            return new BotPlanner(game, playerIndex).BuildPlan(player.Tickets).RoutesToClaim;
        }

        // ================================================================== Выбор заданий

        List<string> ChooseTickets(Game game, PlayerState player, int minKeep)
        {
            var pending = player.PendingTickets.ToList();
            int required = Math.Min(minKeep, pending.Count);
            var planner = new BotPlanner(game, player.Index);
            int baseTrains = planner.BuildPlan(player.Tickets).TrainsNeeded;
            int budget = player.TrainsLeft - TrainReserve;

            List<Ticket> best = null;
            double bestScore = double.NegativeInfinity;
            List<Ticket> fallback = null;
            int fallbackCost = int.MaxValue;

            // Перебираем все подмножества предложенных заданий (их обычно 3, то есть 7 вариантов).
            for (int mask = 1; mask < (1 << pending.Count); mask++)
            {
                var subset = new List<Ticket>();
                for (int i = 0; i < pending.Count; i++)
                    if ((mask & (1 << i)) != 0) subset.Add(pending[i]);
                if (subset.Count < required) continue;

                var plan = planner.BuildPlan(player.Tickets.Concat(subset));
                bool reachable = subset.All(t => !plan.Unreachable.Contains(t));
                int extra = plan.TrainsNeeded - baseTrains;

                // Запасной вариант на случай, если ничего хорошего нет: минимум лишних вагонов.
                int fallbackKey = extra + (reachable ? 0 : 1000) + subset.Count;
                if (fallbackKey < fallbackCost)
                {
                    fallbackCost = fallbackKey;
                    fallback = subset;
                }

                if (!reachable || plan.TrainsNeeded > budget) continue;

                double score = subset.Sum(t => t.Points) - Lambda * extra;
                // Сеть, съедающая почти все вагоны, рискованна: можно не успеть.
                double comfortable = player.TrainsLeft * 0.75;
                if (plan.TrainsNeeded > comfortable) score -= (plan.TrainsNeeded - comfortable) * 1.5;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = subset;
                }
            }

            var chosen = best ?? fallback ?? pending.Take(required).ToList();
            return chosen.Select(t => t.Id).ToList();
        }

        // ================================================================== Анализ позиции

        sealed class Analysis
        {
            public PlayerState Me;
            public List<Route> Desired;
            public bool HasTicketGoals;
            public readonly Dictionary<CardColor, int> Need = new Dictionary<CardColor, int>();
            public int LongestGrayNeed;
        }

        Analysis Analyze(Game game)
        {
            var me = game.CurrentPlayer;
            var plan = new BotPlanner(game, me.Index).BuildPlan(me.Tickets);
            var analysis = new Analysis { Me = me };

            analysis.Desired = plan.RoutesToClaim.Where(r => r.Length <= me.TrainsLeft).ToList();
            analysis.HasTicketGoals = analysis.Desired.Count > 0;

            if (!analysis.HasTicketGoals)
            {
                // Целей по заданиям нет — копим на самые длинные свободные маршруты (они дают больше очков).
                analysis.Desired = game.Map.Routes
                    .Where(r => r.Length <= me.TrainsLeft && game.IsRouteOpenFor(me.Index, r.Id))
                    .OrderByDescending(r => r.Length)
                    .Take(3)
                    .ToList();
            }

            foreach (var route in analysis.Desired)
            {
                if (route.Color.HasValue)
                {
                    analysis.Need.TryGetValue(route.Color.Value, out var n);
                    analysis.Need[route.Color.Value] = n + route.Length;
                }
                else
                {
                    analysis.LongestGrayNeed = Math.Max(analysis.LongestGrayNeed, route.Length);
                }
            }
            return analysis;
        }

        static int NeedOf(Analysis a, CardColor color) => a.Need.TryGetValue(color, out var n) ? n : 0;

        static int Surplus(Analysis a, CardColor color) => a.Me.CardCount(color) - NeedOf(a, color);

        // ================================================================== Ход

        BotMove TakeTurn(Game game)
        {
            var a = Analyze(game);
            var me = a.Me;
            bool endgame = game.IsFinalRound || game.Players.Any(p => p.TrainsLeft <= EndgameTrains);

            // 1. Можем оплатить маршрут из плана — занимаем, самый длинный первым.
            var affordableDesired = Affordable(game, a.Desired);
            if (a.HasTicketGoals && affordableDesired.Count > 0)
                return Claim(game, a, affordableDesired.OrderByDescending(x => x.Key.Length).First());

            // 2. Конец партии: занимаем самое дорогое, что можем.
            if (endgame)
            {
                var anyAffordable = Affordable(game, game.Map.Routes);
                if (anyAffordable.Count > 0)
                    return Claim(game, a, anyAffordable.OrderByDescending(x => x.Key.Length).First());
            }

            // 3. Все задания выполнены (или безнадёжны).
            if (!a.HasTicketGoals)
            {
                bool calmGame = !game.Players.Any(p => p.TrainsLeft <= 12);
                if (game.TicketDeckCount > 0 && me.TrainsLeft >= 14 && calmGame && !game.IsFinalRound)
                    return BotMove.Tickets();

                if (affordableDesired.Count > 0)
                {
                    var longest = affordableDesired.OrderByDescending(x => x.Key.Length).First();
                    if (longest.Key.Length >= 3 || me.HandSize >= 10) return Claim(game, a, longest);
                }
            }

            // 4. Копим карты.
            if (game.CanDrawAnyCard()) return DrawBestCard(game, a, isSecondCard: false);

            // 5. Карт нет совсем: занимаем что угодно, иначе берём задания, иначе пас.
            // Задания берём даже в конце партии: пока колода заданий не пуста, пропускать ход нельзя.
            // При выборе бот оставит самое дешёвое (лучше всего — уже выполненное) задание.
            var fallback = Affordable(game, game.Map.Routes);
            if (fallback.Count > 0) return Claim(game, a, fallback.OrderByDescending(x => x.Key.Length).First());
            if (game.TicketDeckCount > 0) return BotMove.Tickets();
            return BotMove.PassTurn();
        }

        static List<KeyValuePair<Route, IReadOnlyList<ClaimOption>>> Affordable(Game game, IEnumerable<Route> routes)
        {
            var result = new List<KeyValuePair<Route, IReadOnlyList<ClaimOption>>>();
            foreach (var route in routes)
            {
                var options = game.GetClaimOptions(route.Id);
                if (options.Count > 0) result.Add(new KeyValuePair<Route, IReadOnlyList<ClaimOption>>(route, options));
            }
            return result;
        }

        /// <summary>Платим так, чтобы потратить меньше локомотивов и не трогать нужные дальше цвета.</summary>
        static BotMove Claim(Game game, Analysis a, KeyValuePair<Route, IReadOnlyList<ClaimOption>> choice)
        {
            var route = choice.Key;
            var option = choice.Value
                .OrderBy(o => o.Color == CardColor.Locomotive ? 1 : 0)
                .ThenBy(o => o.Locomotives)
                .ThenByDescending(o => o.Color == CardColor.Locomotive ? 0 : SurplusAfter(a, route, o))
                .First();
            return BotMove.Claim(route.Id, option.Color, option.Locomotives);
        }

        static int SurplusAfter(Analysis a, Route route, ClaimOption option)
        {
            int spent = route.Length - option.Locomotives;
            int needWithoutThis = NeedOf(a, option.Color) - (route.Color == option.Color ? route.Length : 0);
            return a.Me.CardCount(option.Color) - spent - needWithoutThis;
        }

        // ================================================================== Карты

        BotMove DrawBestCard(Game game, Analysis a, bool isSecondCard)
        {
            int totalDeficit = CardColors.Regular.Sum(c => Math.Max(0, NeedOf(a, c) - a.Me.CardCount(c)));

            // Серые маршруты удобнее всего оплачивать цветом, которого больше всего «лишнего».
            CardColor grayColor = CardColors.Regular.OrderByDescending(c => Surplus(a, c)).First();
            int grayDeficit = a.LongestGrayNeed > 0 ? Math.Max(0, a.LongestGrayNeed - Math.Max(0, Surplus(a, grayColor))) : 0;
            totalDeficit += grayDeficit;

            int bestSlot = -1;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < game.FaceUpCards.Count; i++)
            {
                var card = game.FaceUpCards[i];
                if (!card.HasValue) continue;
                if (isSecondCard && card.Value == CardColor.Locomotive) continue;

                double score = CardValue(a, card.Value, totalDeficit, grayColor, grayDeficit) + _rng.NextDouble() * 0.01;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = i;
                }
            }

            bool deckAvailable = game.DeckCount + game.DiscardCount > 0;
            const double deckValue = 1.0; // вслепую — в среднем «так себе» карта

            if (bestSlot >= 0 && (bestScore >= 1.5 || !deckAvailable)) return BotMove.FaceUp(bestSlot);
            // Брать нечего — такого не бывает (игра не даёт перейти ко второй карте), но ход из колоды
            // хотя бы вернёт понятную ошибку, а не исключение.
            return BotMove.Deck();
        }

        static double CardValue(Analysis a, CardColor color, int totalDeficit, CardColor grayColor, int grayDeficit)
        {
            // Открытый локомотив первой картой заканчивает добор, поэтому он стоит как «одна очень хорошая карта».
            if (color == CardColor.Locomotive) return totalDeficit >= 3 ? 2.5 : 0.5;
            if (NeedOf(a, color) > a.Me.CardCount(color)) return 3.0;
            if (grayDeficit > 0 && color == grayColor) return 2.5;
            if (grayDeficit > 0 && Surplus(a, color) >= 0) return 1.5;
            return 0.0;
        }
    }
}
