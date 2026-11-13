using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class RulesTests
    {
        static Game Started(int players = 2, GameConfig config = null) =>
            TestCities.Started(TestCities.Small(), players, config);

        [Test]
        public void Setup_DealsCardsContractsAndPieces()
        {
            var game = new Game(TestCities.Small(), new[] { "Аня", "Борис" }, seed: 1);

            Assert.AreEqual(GamePhase.SetupContracts, game.Phase);
            foreach (var player in game.Players)
            {
                Assert.AreEqual(4, player.HandSize);
                Assert.AreEqual(3, player.PendingContracts.Count);
                Assert.AreEqual(35, player.RoadPieces, "У двоих игроков по 35 участков");
            }
            Assert.AreEqual(5, game.FaceUpCards.Count(c => c.HasValue));
            Assert.AreEqual(game.Config.TotalCards, game.Debug_CountAllCards());
        }

        [Test]
        public void RoadPieces_DependOnPlayerCount()
        {
            var config = new GameConfig();
            Assert.AreEqual(35, config.RoadPiecesFor(2));
            Assert.AreEqual(25, config.RoadPiecesFor(5));
            config.RoadPiecesOverride = 12;
            Assert.AreEqual(12, config.RoadPiecesFor(4));
        }

        [Test]
        public void Setup_MustKeepAtLeastTwoContracts()
        {
            var game = new Game(TestCities.Small(), new[] { "Аня", "Борис" }, seed: 1);
            var pending = game.Players[0].PendingContracts;

            Assert.IsFalse(game.KeepInitialContracts(0, new[] { pending[0].Id }).Success);
            Assert.IsTrue(game.KeepInitialContracts(0, new[] { pending[0].Id, pending[1].Id }).Success);
            Assert.AreEqual(GamePhase.SetupContracts, game.Phase);
            Assert.IsTrue(game.KeepInitialContracts(1, game.Players[1].PendingContracts.Select(c => c.Id).ToList()).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
        }

        [Test]
        public void Build_WithExactResource_Succeeds()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);

            var result = game.BuildRoad("r1", Resource.Asphalt, 0);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, game.GetRoadOwner("r1"));
            Assert.AreEqual(33, game.Players[0].RoadPieces);
            Assert.AreEqual(2, game.Players[0].RoadPoints);
            Assert.AreEqual(0, game.Players[0].HandSize);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void Build_WrongResource_FailsAndChangesNothing()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete);

            Assert.IsFalse(game.BuildRoad("r1", Resource.Concrete, 0).Success);
            Assert.IsNull(game.GetRoadOwner("r1"));
            Assert.AreEqual(2, game.Players[0].HandSize);
            Assert.AreEqual(0, game.CurrentPlayerIndex);
        }

        [Test]
        public void Build_BridgeNeedsSteel()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            StringAssert.Contains("сталь", game.GetBuildBlocker("r3"));

            game.Debug_SetHand(0, Resource.Steel, Resource.Steel);
            Assert.IsNull(game.GetBuildBlocker("r3"));
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success);
        }

        [Test]
        public void Build_AnyRoad_AcceptsAnySingleResource()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Gravel);
            Assert.IsTrue(game.BuildRoad("r2", Resource.Gravel, 0).Success);
        }

        [Test]
        public void Build_MachineryReplacesMissingCards()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Concrete, Resource.Machinery, Resource.Machinery);

            Assert.IsTrue(game.BuildRoad("r4", Resource.Concrete, 2).Success);
            Assert.AreEqual(0, game.Players[0].HandSize);
        }

        [Test]
        public void Build_OnlyMachinery()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Machinery, Resource.Machinery);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Machinery, 2).Success);
        }

        [Test]
        public void Build_NotEnoughPieces_Fails()
        {
            var game = Started();
            game.Debug_SetPieces(0, 3);
            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete, Resource.Concrete, Resource.Concrete);

            Assert.IsFalse(game.BuildRoad("r7", Resource.Concrete, 0).Success);
            StringAssert.Contains("участков", game.GetBuildBlocker("r7"));
        }

        [Test]
        public void Build_RoadOfAnotherPlayer_Fails()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);

            game.Debug_SetHand(1, Resource.Asphalt, Resource.Asphalt);
            Assert.IsFalse(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            StringAssert.Contains("Игрок 1", game.GetBuildBlocker("r1"));
        }

        [Test]
        public void PaymentOptions_ListAllWaysToPay()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Steel, Resource.Machinery);

            var anyRoad = game.GetPaymentOptions("r6"); // асфальт, длина 1
            Assert.AreEqual(2, anyRoad.Count, "Асфальтом или одной техникой");

            var bridge = game.GetPaymentOptions("r3"); // сталь, длина 2
            Assert.AreEqual(1, bridge.Count);
            Assert.AreEqual(Resource.Steel, bridge[0].Resource);
            Assert.AreEqual(1, bridge[0].Machinery);
        }

        [Test]
        public void FaceUpMachinery_AsFirstCard_EndsTurn()
        {
            var game = Started();
            game.Debug_SetFaceUp(Resource.Machinery, Resource.Asphalt, Resource.Steel, Resource.Gravel, Resource.Concrete);

            Assert.IsTrue(game.DrawFaceUpCard(0).Success);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void FaceUpMachinery_AsSecondCard_IsForbidden()
        {
            var game = Started();
            game.Debug_SetFaceUp(Resource.Asphalt, Resource.Machinery, Resource.Steel, Resource.Gravel, Resource.Concrete);

            Assert.IsTrue(game.DrawFaceUpCard(0).Success);
            Assert.AreEqual(GamePhase.DrawingSecondCard, game.Phase);
            Assert.IsFalse(game.DrawFaceUpCard(1).Success);
        }

        [Test]
        public void Build_NotAllowedAfterFirstCard()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsFalse(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
        }

        [Test]
        public void DrawContracts_MustKeepAtLeastOne()
        {
            var game = Started();
            Assert.AreEqual(2, game.ContractDeckCount, "6 контрактов, раздали 6, каждый вернул один");

            Assert.IsTrue(game.DrawContracts().Success);
            var pending = game.Players[0].PendingContracts;
            Assert.AreEqual(2, pending.Count);
            Assert.IsFalse(game.KeepContracts(new string[0]).Success);
            Assert.IsTrue(game.KeepContracts(new[] { pending[0].Id }).Success);
            Assert.AreEqual(3, game.Players[0].Contracts.Count);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void FinalRound_EveryoneGetsOneMoreTurn()
        {
            var game = Started();
            game.Debug_SetPieces(0, 3);
            game.Debug_SetHand(0, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r6", Resource.Asphalt, 0).Success); // осталось 2
            Assert.IsTrue(game.IsFinalRound);

            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreNotEqual(GamePhase.GameOver, game.Phase);

            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.IsNotNull(game.Result);
        }

        [Test]
        public void Pass_OnlyWhenNothingElseIsPossible()
        {
            var game = Started();
            Assert.IsFalse(game.Pass().Success);

            game.Debug_EmptyCardSupply();
            game.Debug_SetHand(0);
            Assert.Greater(game.ContractDeckCount, 0);
            Assert.IsFalse(game.Pass().Success, "Пока есть контракты, пасовать нельзя");
        }
    }
}
