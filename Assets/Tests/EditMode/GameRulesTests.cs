using System.Linq;
using NUnit.Framework;
using RailGame.Core;

namespace RailGame.Tests
{
    public class GameRulesTests
    {
        /// <summary>Партия после выбора стартовых заданий: каждый оставил по 2, ходит игрок 0.</summary>
        static Game StartGame(int players = 2, GameConfig config = null)
        {
            var names = Enumerable.Range(1, players).Select(i => "Игрок " + i).ToList();
            var game = new Game(TestMaps.Small(), names, config, seed: 42);
            foreach (var player in game.Players)
            {
                if (player.PendingTickets.Count == 0) continue;
                var keep = player.PendingTickets.Take(2).Select(t => t.Id).ToList();
                Assert.IsTrue(game.KeepInitialTickets(player.Index, keep).Success);
            }
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            return game;
        }

        [Test]
        public void Setup_DealsCardsAndTickets()
        {
            var game = new Game(TestMaps.Small(), new[] { "Аня", "Борис" }, seed: 1);

            Assert.AreEqual(GamePhase.SetupTickets, game.Phase);
            foreach (var player in game.Players)
            {
                Assert.AreEqual(4, player.HandSize);
                Assert.AreEqual(3, player.PendingTickets.Count);
                Assert.AreEqual(45, player.TrainsLeft);
            }
            Assert.AreEqual(5, game.FaceUpCards.Count(c => c.HasValue));
        }

        [Test]
        public void Setup_MustKeepAtLeastTwoTickets()
        {
            var game = new Game(TestMaps.Small(), new[] { "Аня", "Борис" }, seed: 1);
            var pending = game.Players[0].PendingTickets;

            Assert.IsFalse(game.KeepInitialTickets(0, new[] { pending[0].Id }).Success);
            Assert.IsTrue(game.KeepInitialTickets(0, new[] { pending[0].Id, pending[1].Id }).Success);
            Assert.AreEqual(GamePhase.SetupTickets, game.Phase, "Второй игрок ещё не выбрал задания");

            var pending2 = game.Players[1].PendingTickets.Select(t => t.Id).ToList();
            Assert.IsTrue(game.KeepInitialTickets(1, pending2).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            Assert.AreEqual(0, game.CurrentPlayerIndex);
        }

        [Test]
        public void ClaimRoute_WithExactCards_Succeeds()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Red);

            var result = game.ClaimRoute("ab", CardColor.Red, 0);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, game.GetRouteOwner("ab"));
            Assert.AreEqual(44, game.Players[0].TrainsLeft);
            Assert.AreEqual(1, game.Players[0].RoutePoints);
            Assert.AreEqual(0, game.Players[0].HandSize);
            Assert.AreEqual(1, game.CurrentPlayerIndex, "Ход переходит к следующему игроку");
        }

        [Test]
        public void ClaimRoute_WrongColor_FailsAndChangesNothing()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Blue);

            var result = game.ClaimRoute("ab", CardColor.Blue, 0);

            Assert.IsFalse(result.Success);
            Assert.IsNull(game.GetRouteOwner("ab"));
            Assert.AreEqual(1, game.Players[0].HandSize);
            Assert.AreEqual(0, game.CurrentPlayerIndex);
        }

        [Test]
        public void ClaimRoute_GrayRoute_AcceptsAnySingleColor()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Green, CardColor.Green);

            Assert.IsTrue(game.ClaimRoute("bc", CardColor.Green, 0).Success);
        }

        [Test]
        public void ClaimRoute_GrayRoute_RejectsMixedColors()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Green, CardColor.Red);

            Assert.IsFalse(game.ClaimRoute("bc", CardColor.Green, 0).Success);
        }

        [Test]
        public void ClaimRoute_LocomotivesReplaceMissingCards()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Blue, CardColor.Blue, CardColor.Locomotive);

            Assert.IsTrue(game.ClaimRoute("cd", CardColor.Blue, 1).Success);
            Assert.AreEqual(0, game.Players[0].HandSize);
        }

        [Test]
        public void ClaimRoute_NotEnoughTrains_Fails()
        {
            var game = StartGame();
            game.Debug_SetTrains(0, 2);
            game.Debug_SetHand(0, CardColor.Blue, CardColor.Blue, CardColor.Blue);

            Assert.IsFalse(game.ClaimRoute("cd", CardColor.Blue, 0).Success);
        }

        [Test]
        public void ParallelRoute_ClosedForTwoPlayers()
        {
            var game = StartGame(players: 2);
            game.Debug_SetHand(0, CardColor.Blue, CardColor.Blue, CardColor.Blue);
            Assert.IsTrue(game.ClaimRoute("cd", CardColor.Blue, 0).Success);

            game.Debug_SetHand(1, CardColor.Green, CardColor.Green, CardColor.Green);
            Assert.IsFalse(game.ClaimRoute("cd2", CardColor.Green, 0).Success);
        }

        [Test]
        public void ParallelRoute_OpenForFourPlayers()
        {
            var game = StartGame(players: 4);
            game.Debug_SetHand(0, CardColor.Blue, CardColor.Blue, CardColor.Blue);
            Assert.IsTrue(game.ClaimRoute("cd", CardColor.Blue, 0).Success);

            game.Debug_SetHand(1, CardColor.Green, CardColor.Green, CardColor.Green);
            Assert.IsTrue(game.ClaimRoute("cd2", CardColor.Green, 0).Success);
        }

        [Test]
        public void ClaimOptions_ListAllWaysToPay()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Red, CardColor.Blue, CardColor.Locomotive);

            var options = game.GetClaimOptions("bc"); // серый, длина 2

            Assert.AreEqual(2, options.Count);
            Assert.IsTrue(options.Any(o => o.Color == CardColor.Red && o.Locomotives == 1));
            Assert.IsTrue(options.Any(o => o.Color == CardColor.Blue && o.Locomotives == 1));
            Assert.AreEqual(0, game.GetClaimOptions("cd").Count, "На синий маршрут длины 3 карт не хватает");
        }

        [Test]
        public void DrawCards_TwoFromDeck_EndsTurn()
        {
            var game = StartGame();
            int handBefore = game.Players[0].HandSize;

            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreEqual(GamePhase.DrawingSecondCard, game.Phase);
            Assert.IsTrue(game.DrawCardFromDeck().Success);

            Assert.AreEqual(handBefore + 2, game.Players[0].HandSize);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void FaceUpLocomotive_AsFirstCard_EndsTurn()
        {
            var game = StartGame();
            game.Debug_SetFaceUp(CardColor.Locomotive, CardColor.Red, CardColor.Blue, CardColor.Green, CardColor.Yellow);

            Assert.IsTrue(game.DrawFaceUpCard(0).Success);

            Assert.GreaterOrEqual(game.Players[0].CardCount(CardColor.Locomotive), 1);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void FaceUpLocomotive_AsSecondCard_IsForbidden()
        {
            var game = StartGame();
            game.Debug_SetFaceUp(CardColor.Red, CardColor.Locomotive, CardColor.Blue, CardColor.Green, CardColor.Yellow);

            Assert.IsTrue(game.DrawFaceUpCard(0).Success);
            Assert.AreEqual(GamePhase.DrawingSecondCard, game.Phase);

            Assert.IsFalse(game.DrawFaceUpCard(1).Success);
            Assert.AreEqual(GamePhase.DrawingSecondCard, game.Phase);
        }

        [Test]
        public void ClaimRoute_NotAllowedAfterFirstCard()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Red);
            Assert.IsTrue(game.DrawCardFromDeck().Success);

            Assert.IsFalse(game.ClaimRoute("ab", CardColor.Red, 0).Success);
        }

        [Test]
        public void DrawTickets_MustKeepAtLeastOne()
        {
            var game = StartGame();
            Assert.AreEqual(2, game.TicketDeckCount, "6 заданий, раздали 6, каждый вернул по одному");

            Assert.IsTrue(game.DrawTickets().Success);
            Assert.AreEqual(GamePhase.ChoosingTickets, game.Phase);
            var pending = game.Players[0].PendingTickets;
            Assert.AreEqual(2, pending.Count);

            Assert.IsFalse(game.KeepTickets(new string[0]).Success);
            Assert.IsTrue(game.KeepTickets(new[] { pending[0].Id }).Success);

            Assert.AreEqual(3, game.Players[0].Tickets.Count);
            Assert.AreEqual(1, game.TicketDeckCount);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void FinalRound_EveryoneGetsOneMoreTurn()
        {
            var game = StartGame();
            game.Debug_SetTrains(0, 3);
            game.Debug_SetHand(0, CardColor.Red);

            Assert.IsTrue(game.ClaimRoute("ab", CardColor.Red, 0).Success); // осталось 2 вагона
            Assert.IsTrue(game.IsFinalRound);

            // Последний ход игрока 1.
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreNotEqual(GamePhase.GameOver, game.Phase);

            // Последний ход игрока 0.
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);

            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.IsNotNull(game.Result);
            Assert.AreEqual(2, game.Result.Ranking.Count);
        }

        [Test]
        public void ClaimBlocker_ExplainsWhyRouteIsUnavailable()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Red);

            Assert.IsNull(game.GetClaimBlocker("ab"), "Красный маршрут длины 1 можно занять");
            StringAssert.Contains("синий", game.GetClaimBlocker("cd"));

            Assert.IsTrue(game.ClaimRoute("ab", CardColor.Red, 0).Success);
            StringAssert.Contains("занят", game.GetClaimBlocker("ab"));
        }

        [Test]
        public void Events_ClaimRaisesRouteClaimedThenTurnStarted()
        {
            var game = StartGame();
            var events = new System.Collections.Generic.List<GameEvent>();
            game.EventRaised += events.Add;
            game.Debug_SetHand(0, CardColor.Red);

            Assert.IsTrue(game.ClaimRoute("ab", CardColor.Red, 0).Success);

            var kinds = events.Select(e => e.Kind).ToList();
            Assert.AreEqual(GameEventKind.RouteClaimed, kinds.First());
            Assert.AreEqual("ab", events[0].RouteId);
            Assert.AreEqual(0, events[0].Player);
            Assert.AreEqual(GameEventKind.TurnStarted, kinds.Last());
            Assert.AreEqual(1, events.Last().Player);
        }

        [Test]
        public void Events_FaceUpDrawReportsSlotAndCard()
        {
            var game = StartGame();
            game.Debug_SetFaceUp(CardColor.Blue, CardColor.Red, CardColor.Red, CardColor.Green, CardColor.Yellow);
            GameEvent seen = null;
            game.EventRaised += e => { if (e.Kind == GameEventKind.CardTakenFaceUp) seen = e; };

            Assert.IsTrue(game.DrawFaceUpCard(0).Success);

            Assert.IsNotNull(seen);
            Assert.AreEqual(0, seen.Slot);
            Assert.AreEqual(CardColor.Blue, seen.Card);
        }

        [Test]
        public void RoutePointsTable()
        {
            var config = new GameConfig();
            Assert.AreEqual(1, config.PointsForRouteLength(1));
            Assert.AreEqual(4, config.PointsForRouteLength(3));
            Assert.AreEqual(15, config.PointsForRouteLength(6));
            Assert.AreEqual(21, config.PointsForRouteLength(8));
        }
    }
}
