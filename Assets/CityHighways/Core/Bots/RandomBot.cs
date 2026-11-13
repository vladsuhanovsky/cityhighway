using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Бот делает одно действие за того, чья сейчас очередь (Game.ActingPlayerIndex).</summary>
    public interface IBot
    {
        ActionResult Act(Game game);
    }

    /// <summary>
    /// Случайные разрешённые ходы. Соперник слабый, зато быстро прогоняет тысячи партий
    /// и находит ошибки в правилах. Умного бота под новые правила делаем на третьем этапе.
    /// </summary>
    public sealed class RandomBot : IBot
    {
        readonly Random _rng;

        public RandomBot(int seed)
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
                    return game.KeepInitialContracts(player.Index, player.PendingContracts.Select(c => c.Id).ToList());
                }
                case GamePhase.ChoosingContracts:
                    return game.KeepContracts(new[] { game.CurrentPlayer.PendingContracts[0].Id });
                case GamePhase.DrawingSecondCard:
                    return DrawRandomCard(game, true);
                case GamePhase.AwaitingAction:
                    return TakeTurn(game);
                case GamePhase.RespondingTrade:
                    return game.RespondTrade(_rng.Next(2) == 0);
                case GamePhase.ChoosingLuck:
                    return game.ChooseLuckCard(_rng.Next(game.PendingLuckChoice.Count));
                default:
                    return ActionResult.Fail("Игра окончена.");
            }
        }

        ActionResult TakeTurn(Game game)
        {
            var builds = new List<KeyValuePair<Road, PaymentOption>>();
            foreach (var road in game.Map.Roads)
                foreach (var option in game.GetPaymentOptions(road.Id))
                    builds.Add(new KeyValuePair<Road, PaymentOption>(road, option));

            bool canDraw = game.CanDrawAnyCard();
            bool canTakeContracts = game.ContractDeckCount > 0;
            double roll = _rng.NextDouble();

            if (builds.Count > 0 && (roll < 0.5 || !canDraw)) return Build(game, builds);
            if (canDraw && (roll < 0.95 || !canTakeContracts)) return DrawRandomCard(game, false);
            if (canTakeContracts) return game.DrawContracts();
            if (builds.Count > 0) return Build(game, builds);
            return game.Pass();
        }

        ActionResult Build(Game game, List<KeyValuePair<Road, PaymentOption>> builds)
        {
            var pick = builds[_rng.Next(builds.Count)];
            return game.BuildRoad(pick.Key.Id, pick.Value.Resource, pick.Value.Machinery);
        }

        ActionResult DrawRandomCard(Game game, bool isSecond)
        {
            var choices = new List<int>(); // -1 — колода, иначе ячейка открытого ряда
            if (game.DeckCount + game.DiscardCount > 0) choices.Add(-1);
            for (int i = 0; i < game.FaceUpCards.Count; i++)
            {
                var card = game.FaceUpCards[i];
                if (!card.HasValue) continue;
                if (isSecond && card.Value == Resource.Machinery) continue;
                choices.Add(i);
            }
            if (choices.Count == 0) return ActionResult.Fail("Нечего взять.");
            int choice = choices[_rng.Next(choices.Count)];
            return choice < 0 ? game.DrawCardFromDeck() : game.DrawFaceUpCard(choice);
        }
    }
}
