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

        [Serializable]
        sealed class TranslationEntry
        {
            public string key;
            public string value;
        }

        [Serializable]
        sealed class TranslationCatalog
        {
            public TranslationEntry[] entries;
        }

        static TranslationCatalog LoadCatalog(string language)
        {
            var asset = Resources.Load<TextAsset>("CityHighways/Localization/" + language);
            Assert.IsNotNull(asset, "Missing translation catalog: " + language);
            var table = JsonUtility.FromJson<TranslationCatalog>(asset.text);
            Assert.IsNotNull(table, "Cannot parse translation catalog: " + language);
            Assert.IsNotNull(table.entries, "Missing entries in: " + language);
            return table;
        }

        [Test]
        public void TranslationCatalogs_HaveMatchingUniqueNonEmptyKeys()
        {
            var english = LoadCatalog("en").entries;
            var russian = LoadCatalog("ru").entries;
            foreach (var item in english.Concat(russian))
            {
                Assert.IsNotNull(item);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.key), "Empty translation key");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.value), "Empty translation value for " + item.key);
            }
            Assert.AreEqual(english.Length, english.Select(item => item.key).Distinct().Count(), "Duplicate en keys");
            Assert.AreEqual(russian.Length, russian.Select(item => item.key).Distinct().Count(), "Duplicate ru keys");
            CollectionAssert.AreEquivalent(english.Select(item => item.key).ToArray(),
                russian.Select(item => item.key).ToArray(), "Translation catalogs have different keys");
        }

        [Test]
        public void EnglishCatalog_HasInGameSettingsKeys()
        {
            var english = LoadCatalog("en").entries.Select(entry => entry.key).ToArray();
            var required = new[]
            {
                "common.enabled", "common.disabled", "game.settings.sound",
                "game.settings.volume", "game.settings.board_tilt", "game.settings.city_3d",
                "game.settings.camera_angle", "game.settings.minimap", "game.settings.display_hint"
            };
            foreach (var key in required)
                CollectionAssert.Contains(english, key);
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
