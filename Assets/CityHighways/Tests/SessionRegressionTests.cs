using System;
using System.IO;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Bounded integration tests of the production setup, rules,
    /// file storage and replay path; never touch PlayerPrefs or live save slots.</summary>
    public class SessionRegressionTests
    {
        const string Owner = "local:abcdefabcdefabcdefabcdefabcdefab";

        [TestCase("moscow", 2, ScenarioKind.Standard)]
        [TestCase("amsterdam", 3, ScenarioKind.RepairYear)]
        [TestCase("chongqing", 4, ScenarioKind.SupplySeason)]
        [TestCase("paris", 5, ScenarioKind.Winter)]
        [TestCase("berlin", 2, ScenarioKind.Elections)]
        public void ClassicSessionContinuesIdenticallyAfterCheckpoint(string city, int seats, ScenarioKind scenario)
        {
            var setup = MakeSetup(city, seats, scenario);
            ExerciseCheckpoint(setup, 42 + seats);
        }

        [TestCase(CoopDifficultyKind.Easy)]
        [TestCase(CoopDifficultyKind.Normal)]
        [TestCase(CoopDifficultyKind.Hard)]
        public void CoopSessionContinuesIdenticallyAfterCheckpoint(CoopDifficultyKind difficulty)
        {
            var setup = GameSetup.CreateCoopPractice(null, "Tester");
            setup.coopDifficulty = (int)difficulty;
            ExerciseCheckpoint(setup, 2026 + (int)difficulty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        public void CampaignMissionCheckpointUsesActualMissionSetup(int index)
        {
            var mission = CampaignCatalog.All[index];
            var setup = mission.CreateSetup("Tester", Owner, false);
            Assert.IsTrue(mission.Matches(setup));
            ExerciseCheckpoint(setup, mission.Seed);
        }

        static GameSetup MakeSetup(string city, int seats, ScenarioKind scenario)
        {
            var setup = new GameSetup { magnate = false, sound = false,
                mapResourcePath = "CityHighways/Maps/" + city, scenario = (int)scenario };
            for (int i = 0; i < seats; i++)
                setup.players.Add(new PlayerSlot("Seat " + i, i != 0) { character = 0 });
            return setup;
        }

        static void ExerciseCheckpoint(GameSetup setup, int seed)
        {
            var map = CityHighwaysApp.LoadMap(setup.mapResourcePath);
            var game = new Game(map, setup.PlayerNames(), setup.CreateConfig(), seed,
                setup.ResolveCharacters(new System.Random(seed ^ 0x5bd1e995)));
            var initialBot = new RandomBot(seed ^ 19);
            for (int i = 0; i < 12 && game.Phase != GamePhase.GameOver; i++)
                AssertSuccess(initialBot.Act(game), seed, i);

            Assert.AreNotEqual(GamePhase.GameOver, game.Phase, "Checkpoint must be in an active match");
            Assert.Greater(game.CreateSave().history.Length, 0);
            string dir = Path.Combine(Path.GetTempPath(), "city-session-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "checkpoint.json");
            Directory.CreateDirectory(dir);
            try
            {
                var file = new SaveFile { setup = setup, game = game.CreateSave(), playSeconds = 12f };
                SaveFileStorage.Write(path, file);
                string owner = setup.IsCampaign ? setup.campaignOwner : null;
                var restored = SaveFileStorage.Read(path, owner);
                Assert.IsNotNull(restored);
                Assert.AreEqual(setup.mapResourcePath, restored.setup.mapResourcePath);
                Assert.AreEqual(setup.campaignMission, restored.setup.campaignMission);
                Assert.AreEqual(setup.cooperative, restored.setup.cooperative);
                Assert.AreEqual(setup.scenario, restored.setup.scenario);
                Assert.AreEqual(setup.coopDifficulty, restored.setup.coopDifficulty);
                if (setup.IsCampaign)
                    Assert.IsTrue(CampaignCatalog.Find(setup.campaignMission).Matches(restored.setup));

                var resumed = Game.Load(map, restored.game);
                var replayed = Game.Replay(map, restored.game.start, restored.game.history);
                AssertState(game, resumed);
                AssertState(game, replayed);

                var originalBot = new RandomBot(seed ^ 71);
                var resumedBot = new RandomBot(seed ^ 71);
                var replayBot = new RandomBot(seed ^ 71);
                for (int i = 0; i < 12 && game.Phase != GamePhase.GameOver; i++)
                {
                    AssertSuccess(originalBot.Act(game), seed, i);
                    AssertSuccess(resumedBot.Act(resumed), seed, i);
                    AssertSuccess(replayBot.Act(replayed), seed, i);
                    AssertState(game, resumed);
                    AssertState(game, replayed);
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        static void AssertSuccess(ActionResult result, int seed, int step) =>
            Assert.IsTrue(result.Success, "seed " + seed + ", action " + step + ": " + result.Error);

        static void AssertState(Game expected, Game actual)
        {
            Assert.IsTrue(expected.SameGameState(actual), "Core comparison failed");
            var a = expected.CreateSave(); var b = actual.CreateSave();
            Assert.AreEqual(a.turnNumber, b.turnNumber);
            Assert.AreEqual(a.coopCrisis, b.coopCrisis);
            Assert.AreEqual(a.coopDone, b.coopDone);
            Assert.AreEqual(a.coopFailed, b.coopFailed);
            Assert.AreEqual(a.coopRounds, b.coopRounds);
            CollectionAssert.AreEqual(a.coopOrders, b.coopOrders);
            CollectionAssert.AreEqual(a.coopRoundsLeft, b.coopRoundsLeft);
            Assert.AreEqual(a.currentEvent, b.currentEvent);
            Assert.AreEqual(a.eventNumber, b.eventNumber);
            Assert.AreEqual(a.finalTurnsLeft, b.finalTurnsLeft);
            Assert.AreEqual(a.finalOrderWinner, b.finalOrderWinner);
            Assert.AreEqual(a.history.Length, b.history.Length);
            Assert.AreEqual(expected.Result == null, actual.Result == null);
            if (expected.Result != null)
            {
                Assert.AreEqual(expected.Result.TeamWon, actual.Result.TeamWon);
                CollectionAssert.AreEqual(expected.Result.Ranking.Select(p => p.PlayerIndex),
                    actual.Result.Ranking.Select(p => p.PlayerIndex));
            }
        }
    }
}
