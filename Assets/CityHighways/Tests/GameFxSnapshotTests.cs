using System;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class GameFxSnapshotTests
    {
        [Test]
        public void CaptureIsDetachedAndDoesNotChangeCoreState()
        {
            var game = TestCities.Started(TestCities.Small());
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            string saved = JsonUtility.ToJson(game.CreateSave());
            var before = GameFxSnapshot.Capture(game);
            Assert.AreEqual(saved, JsonUtility.ToJson(game.CreateSave()));
            Assert.AreEqual(2, before.Hands[0, (int)Resource.Asphalt]);
            Assert.IsFalse(before.Owners.ContainsKey("r1"));

            var built = game.BuildRoad("r1", Resource.Asphalt, 0);
            Assert.IsTrue(built.Success, built.Error);
            string builtSave = JsonUtility.ToJson(game.CreateSave());
            var after = GameFxSnapshot.Capture(game);
            Assert.AreEqual(builtSave, JsonUtility.ToJson(game.CreateSave()));
            Assert.AreEqual(0, after.Owners["r1"]);
            Assert.IsFalse(before.Owners.ContainsKey("r1"));
            Assert.AreEqual(2, before.Hands[0, (int)Resource.Asphalt]);
            Assert.AreEqual(0, after.Hands[0, (int)Resource.Asphalt]);
            Assert.AreNotSame(before.FaceUp, after.FaceUp);
        }

        [Test]
        public void FlightsConsumeChangedFaceUpSlotsOnlyOnceAndKeepOriginalOrder()
        {
            var before = new int[2, ResourceInfo.Count];
            var now = new int[2, ResourceInfo.Count];
            now[0, (int)Resource.Asphalt] = 2;
            now[1, (int)Resource.Asphalt] = 1;
            var oldFace = new Resource?[] { Resource.Asphalt, Resource.Asphalt, Resource.Asphalt };
            var newFace = new Resource?[] { Resource.Steel, Resource.Gravel, Resource.Asphalt };
            var flights = GameFxSnapshot.PlanCardFlights(before, now, oldFace, newFace).ToArray();
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, flights.Select(f => f.Player));
            CollectionAssert.AreEqual(new[] { 0, 1, -1 }, flights.Select(f => f.FaceUpSlot));
            Assert.IsTrue(flights.All(f => f.Resource == Resource.Asphalt));
            Assert.AreEqual(0f, flights[0].Delay, 1e-6f);
            Assert.AreEqual(0.15f, flights[1].Delay, 1e-6f);
            Assert.AreEqual(0.30f, flights[2].Delay, 1e-6f);
            Assert.AreEqual(Resource.Asphalt, oldFace[0]);
        }

        [Test]
        public void CardLossesAndUnchangedHandsDoNotAnimate()
        {
            var before = new int[1, ResourceInfo.Count];
            var now = new int[1, ResourceInfo.Count];
            before[0, (int)Resource.Steel] = 3;
            now[0, (int)Resource.Steel] = 1;
            Assert.IsEmpty(GameFxSnapshot.PlanCardFlights(before, now, new Resource?[0], new Resource?[0]).ToArray());
        }

        [Test]
        public void DifferentPlayerCountsDoNotProduceInvalidFlightTargets()
        {
            Assert.IsEmpty(GameFxSnapshot.PlanCardFlights(new int[1, ResourceInfo.Count],
                new int[2, ResourceInfo.Count], new Resource?[0], new Resource?[0]).ToArray());
        }
    }

    public class GameHudReadoutTests
    {
        [TestCase("ru")]
        [TestCase("en")]
        public void StatusPriorityAndCorePhaseRemainLocalized(string language)
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage(language);
                var game = TestCities.Started(TestCities.Small());
                int acting = game.ActingPlayerIndex;
                Assert.AreEqual(LocalizationService.Get("game.ui.092"), GameHudReadout.Instruction(game, -1, true, true, true, true));
                Assert.AreEqual(LocalizationService.Get("game.ui.039"), GameHudReadout.Instruction(game, acting, true, true, true, true));
                Assert.AreEqual(LocalizationService.Get("game.ui.093"), GameHudReadout.Instruction(game, acting, false, true, true, true));
                StringAssert.Contains(game.Players[acting].Name, GameHudReadout.Instruction(game, acting, false, false, true, true));
                Assert.AreEqual(LocalizationService.Get("game.ui.094"), GameHudReadout.Instruction(game, acting, false, false, false, true));
                Assert.AreEqual(LocalizationService.Get("game.ui.097"), GameHudReadout.Instruction(game, acting, false, false, false, false));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void PlayerReadoutUsesVisibleScoreAndPublicHandCount(string language)
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage(language);
                var game = TestCities.Started(TestCities.Small());
                var player = game.Players[0];
                var other = game.Players[1];
                string saved = JsonUtility.ToJson(game.CreateSave());
                var row = GameHudReadout.Player(game, player, " [tag]");
                StringAssert.Contains(player.Name, row.Name);
                StringAssert.EndsWith(" [tag]", row.Name);
                Assert.AreEqual(game.VisibleScore(player), row.Score);
                Assert.AreEqual(player.RoadPieces / (float)Math.Max(1, game.Config.RoadPiecesFor(game.Players.Count)), row.PiecesRatio, 1e-6f);
                StringAssert.Contains(player.HandSize + (language == "en" ? " cards" : " карт"), row.Stats);
                Assert.AreEqual(player.Index == game.ActingPlayerIndex, row.Active);
                Assert.AreEqual(other.Index == game.ActingPlayerIndex, GameHudReadout.Player(game, other, "").Active);
                Assert.AreEqual(saved, JsonUtility.ToJson(game.CreateSave()));
            }
            finally { LocalizationService.SetLanguage(original); }
        }
    }
}
