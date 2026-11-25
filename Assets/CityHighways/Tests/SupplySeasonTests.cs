using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class SupplySeasonTests
    {
        [Test]
        public void ScenarioIsOptInAndKeepsLegacyEnumValues()
        {
            Assert.AreEqual(4, (int)ScenarioKind.Elections);
            Assert.AreEqual(6, (int)CityEventKind.Investor);
            foreach (var kind in Scenarios.All.Where(k => k != ScenarioKind.SupplySeason))
            {
                var legacy = Scenarios.Apply(new GameConfig(), kind);
                Assert.AreEqual(0, legacy.AsphaltDeliveryEvents);
                Assert.AreEqual(0, legacy.ConcreteShortageEvents);
            }
            var config = Scenarios.Apply(new GameConfig(), ScenarioKind.SupplySeason);
            Assert.AreEqual(2, config.AsphaltDeliveryEvents);
            Assert.AreEqual(2, config.ConcreteShortageEvents);
            Assert.AreEqual(ScenarioKind.SupplySeason, Scenarios.Of(config));
        }

        [Test]
        public void AsphaltDeliveryChangesOnlyAsphaltAndNeverMakesRoadFree()
        {
            var game = TestCities.Started(TestCities.Small());
            game.Debug_StartEvent(CityEventKind.AsphaltDelivery);
            Assert.AreEqual(1, game.BuildCost(game.Map.GetRoad("r1")));
            Assert.AreEqual(1, game.BuildCost(game.Map.GetRoad("r6")));
            Assert.AreEqual(3, game.BuildCost(game.Map.GetRoad("r4")));
            Assert.AreEqual(2, game.BuildCost(game.Map.GetRoad("r3")));
            game.Debug_SetHand(0, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            Assert.AreEqual(33, game.Players[0].RoadPieces);
        }

        [Test]
        public void ShortageAndPermitUseSamePaymentRules()
        {
            var game = TestCities.Started(TestCities.Small());
            game.Debug_StartEvent(CityEventKind.ConcreteShortage);
            var road = game.Map.GetRoad("r4");
            Assert.AreEqual(4, game.BuildCost(road));
            Assert.AreEqual(2, game.BuildCost(game.Map.GetRoad("r1")));
            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete, Resource.Concrete);
            Assert.IsFalse(game.BuildRoad("r4", Resource.Concrete, 0).Success);
            game.Debug_SetInfluence(0, 5);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.Permit).Success);
            Assert.AreEqual(3, game.BuildCost(road));
            Assert.IsTrue(game.BuildRoad("r4", Resource.Concrete, 0).Success);
        }

        [TestCase(CityEventKind.AsphaltDelivery, 0)]
        [TestCase(CityEventKind.ConcreteShortage, 1)]
        public void CoopCrisisOnlyIncreasesForShortage(CityEventKind kind, int delta)
        {
            var game = TestCities.Started(TestCities.Moscow(), config: new GameConfig().MakeCooperative());
            int before = game.Crisis;
            game.Debug_StartEvent(kind);
            Assert.AreEqual(before + delta, game.Crisis);
        }

        [TestCase(CityEventKind.AsphaltDelivery)]
        [TestCase(CityEventKind.ConcreteShortage)]
        public void ActiveEventSurvivesSnapshotFallback(CityEventKind kind)
        {
            var map = TestCities.Small();
            var game = TestCities.Started(map);
            game.Debug_StartEvent(kind);
            var restored = Game.FromSave(map, game.CreateSave());
            Assert.AreEqual(kind, restored.CurrentEvent.Kind);
            Assert.AreEqual(game.BuildCost(map.GetRoad("r1")), restored.BuildCost(map.GetRoad("r1")));
            Assert.AreEqual(game.BuildCost(map.GetRoad("r4")), restored.BuildCost(map.GetRoad("r4")));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SeededSupplySeasonReplaysWithIdenticalRulesAndEvents(int seed)
        {
            var config = Scenarios.Apply(new GameConfig(), ScenarioKind.SupplySeason);
            config.EventChance = 1f;
            var map = TestCities.Moscow();
            var game = new Game(map, new[] { "A", "B" }, config, seed);
            var bot = new RandomBot(seed);
            for (int i = 0; i < 35 && game.Phase != GamePhase.GameOver; i++)
                Assert.IsTrue(bot.Act(game).Success);
            var replay = Game.Replay(map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replay.CreateSave()));
        }

        [TestCase("Поставка асфальта", "Asphalt Delivery", CityEventKind.AsphaltDelivery)]
        [TestCase("Дефицит бетона", "Concrete Shortage", CityEventKind.ConcreteShortage)]
        public void ArchivedEventAndScenarioTextTranslate(string russian, string english, CityEventKind kind)
        {
            string old = LocalizationService.Language;
            try
            {
                var game = TestCities.Started(TestCities.Small());
                var evt = new CityEvent(kind);
                Assert.AreEqual(russian, evt.Title);
                string message = "Событие: " + evt.Title + ". " + evt.Describe(game);
                LocalizationService.SetLanguage("en");
                StringAssert.StartsWith("Event: " + english, GameView.FormatJournalMessage(game, message));
                Assert.AreEqual("Supply Season", ScenarioMenuLocalization.Title(ScenarioKind.SupplySeason));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Сезон поставок", ScenarioMenuLocalization.Title(ScenarioKind.SupplySeason));
            }
            finally { LocalizationService.SetLanguage(old); }
        }
    }
}
