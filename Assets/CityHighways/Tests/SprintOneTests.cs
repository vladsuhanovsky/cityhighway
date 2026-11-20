using System;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Regression tests for the first infrastructure/localization sprint.</summary>
    public sealed class SprintOneTests
    {
        [Serializable]
        sealed class ReplayFixture
        {
            public GameStartInfo start;
            public GameCommand[] history;
        }

        [Test]
        public void Replay_SerializedCommands_ProduceIdenticalState()
        {
            var game = TestCities.Started(TestCities.Small());
            for (int i = 0; i < 12; i++)
            {
                var result = game.DrawCardFromDeck();
                Assert.IsTrue(result.Success, "Action " + i + ": " + result.Error);
            }
            var exported = new ReplayFixture { start = game.StartInfo, history = game.History.ToArray() };
            var json = JsonUtility.ToJson(exported);
            var imported = JsonUtility.FromJson<ReplayFixture>(json);
            var replayed = Game.Replay(TestCities.Small(), imported.start, imported.history);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
            Assert.AreEqual(game.TurnNumber, replayed.TurnNumber);
            Assert.AreEqual(game.History.Count, replayed.History.Count);
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(10)]
        [TestCase(13)]
        public void LegacySnapshotFixture_Loads(int legacyVersion)
        {
            var game = TestCities.Started(TestCities.Small());
            var legacy = LegacySaveFixtures.Snapshot(game, legacyVersion);
            var loaded = Game.Load(TestCities.Small(), legacy);
            Assert.AreEqual(game.CurrentPlayerIndex, loaded.CurrentPlayerIndex);
            Assert.AreEqual(game.Phase, loaded.Phase);
            Assert.AreEqual(game.Players.Count, loaded.Players.Count);
        }

        [Test]
        public void SemanticIds_AreDistinctAndStable()
        {
            var resources = Enum.GetValues(typeof(Resource)).Cast<Resource>().ToArray();
            Assert.AreEqual(ResourceInfo.Count, resources.Select(VisualIdentity.ResourceId).Distinct().Count());
            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(VisualIdentity.PlayerId).Distinct().Count());
            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(VisualIdentity.PlayerPattern).Distinct().Count());
        }

        [Test]
        public void EnglishCatalog_HasHomeKeys()
        {
            var asset = Resources.Load<TextAsset>("CityHighways/Localization/en");
            Assert.IsNotNull(asset);
            StringAssert.Contains("menu.home.new_game", asset.text);
            StringAssert.Contains("menu.home.language_switch", asset.text);
        }
    }
}
