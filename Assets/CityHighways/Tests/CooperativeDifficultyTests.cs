using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class CooperativeDifficultyTests
    {
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(99)]
        public void LegacyAndInvalidDifficultyUseOriginalRules(int value)
        {
            var expected = new GameConfig().MakeCooperative();
            var actual = CooperativeDifficulty.Apply(new GameConfig(), CooperativeDifficulty.Normalize(value));
            Assert.AreEqual(JsonUtility.ToJson(expected), JsonUtility.ToJson(actual));
        }

        [TestCase(1, 6, 15, 0, 4, 1, 3)]
        [TestCase(0, 4, 12, 1, 3, 2, 4)]
        [TestCase(2, 3, 10, 2, 2, 3, 5)]
        public void PresetsSetConcreteRulesAndPreserveScenario(int value, int rounds, int limit, int start, int interval, int penalty, int target)
        {
            var original = Scenarios.Apply(new GameConfig { RoadPiecesOverride = 30 }, ScenarioKind.Winter);
            var config = CooperativeDifficulty.Apply(original, CooperativeDifficulty.Normalize(value));
            Assert.IsTrue(config.Cooperative);
            Assert.IsFalse(config.BribesEnabled || config.AuctionEnabled || config.PassesEnabled);
            Assert.AreEqual(rounds, config.CoopOrderRoundsBase);
            Assert.AreEqual(limit, config.CoopCrisisMax);
            Assert.AreEqual(start, config.CoopCrisisStart);
            Assert.AreEqual(interval, config.CoopCrisisEveryRounds);
            Assert.AreEqual(penalty, config.CoopCrisisPerFailedOrder);
            Assert.AreEqual(target, config.CoopOrdersToWin(2));
            Assert.IsTrue(config.PermanentSnow);
            Assert.AreEqual(30, config.RoadPiecesOverride);
            Assert.IsFalse(original.Cooperative);
            Assert.AreEqual(4, original.CoopOrderRoundsBase);
            Assert.AreNotSame(original.CardsPerResource, config.CardsPerResource);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void DifficultyMonotonicallyChangesTargetAndTime(int seats)
        {
            var easy = CooperativeDifficulty.Apply(null, CoopDifficultyKind.Easy);
            var normal = CooperativeDifficulty.Apply(null, CoopDifficultyKind.Normal);
            var hard = CooperativeDifficulty.Apply(null, CoopDifficultyKind.Hard);
            Assert.Less(easy.CoopOrdersToWin(seats), normal.CoopOrdersToWin(seats));
            Assert.Less(normal.CoopOrdersToWin(seats), hard.CoopOrdersToWin(seats));
            Assert.Greater(easy.CoopOrderRoundsFor(10, seats), normal.CoopOrderRoundsFor(10, seats));
            Assert.Greater(normal.CoopOrderRoundsFor(10, seats), hard.CoopOrderRoundsFor(10, seats));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SetupAndAuthoritativeGameConfigRoundTrip(int difficulty)
        {
            var setup = new GameSetup { magnate = false, cooperative = true, coopDifficulty = difficulty };
            var restored = JsonUtility.FromJson<GameSetup>(JsonUtility.ToJson(setup));
            var config = restored.CreateConfig();
            var map = TestCities.Moscow();
            var game = new Game(map, new[] { "A", "B" }, config, 42);
            var wire = NetCodec.RoundTrip(game.StartInfo);
            var replayed = Game.Replay(map, wire, game.History);
            Assert.AreEqual(difficulty, wire.config.CoopDifficulty);
            Assert.AreEqual(game.OrdersTarget, replayed.OrdersTarget);
            Assert.AreEqual(game.Crisis, replayed.Crisis);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
            var file = new ReplayFile { mode = "coop", start = wire };
            Assert.AreEqual(difficulty, (int)file.Difficulty);
            restored.cooperative = false;
            Assert.IsFalse(restored.CreateConfig().Cooperative);
            Assert.AreEqual(0, restored.CreateConfig().CoopDifficulty);
        }

        [Test]
        public void LocalRecordRetentionSeparatesDifficulty()
        {
            var entries = new[] {
                new Replays.Entry { id = "normal", mode = "coop", author = "A", mapPath = "map", authorScore = 20 },
                new Replays.Entry { id = "easy", mode = "coop", author = "A", mapPath = "map", authorScore = 99, coopDifficulty = 1 },
                new Replays.Entry { id = "hard", mode = "coop", author = "A", mapPath = "map", authorScore = 10, coopDifficulty = 2 },
                new Replays.Entry { id = "lower", mode = "coop", author = "A", mapPath = "map", authorScore = 1 }
            };
            CollectionAssert.AreEquivalent(new[] { "normal", "easy", "hard" }, ReplayRetention.Select(entries, 3).Select(e => e.id));
        }

        [Test]
        public void CycleAndLabelsUseThreeLevels()
        {
            Assert.AreEqual(CoopDifficultyKind.Hard, CooperativeDifficulty.Next(0));
            Assert.AreEqual(CoopDifficultyKind.Easy, CooperativeDifficulty.Next(2));
            Assert.AreEqual(CoopDifficultyKind.Normal, CooperativeDifficulty.Next(1));
            string old = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Лёгкая", CoopDifficultyText.Title(1));
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Hard", CoopDifficultyText.Title(2));
                StringAssert.Contains("Local records only", CoopDifficultyText.Hint(2));
            }
            finally { LocalizationService.SetLanguage(old); }
        }
    }
}
