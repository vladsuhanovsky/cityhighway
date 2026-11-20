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
        public void NotificationCatalogs_HaveMatchingFormatArguments()
        {
            var russian = LoadCatalog("ru").entries.ToDictionary(entry => entry.key, entry => entry.value);
            var english = LoadCatalog("en").entries.ToDictionary(entry => entry.key, entry => entry.value);
            foreach (var key in russian.Keys.Where(key => key.StartsWith("game.notice.") || key.StartsWith("game.tooltip.")))
            {
                Assert.IsTrue(english.ContainsKey(key), "Missing English text for " + key);
                var arguments = new object[] { "A", "B", "C" };
                Assert.DoesNotThrow(() => string.Format(russian[key], arguments), "Invalid Russian format: " + key);
                Assert.DoesNotThrow(() => string.Format(english[key], arguments), "Invalid English format: " + key);
            }
        }

        [Test]
        public void SharedUiControls_TranslateOnlyUnambiguousLegacyText()
        {
            string previous = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Settings", LocalizationService.TranslateLegacy("Настройки"));
                Assert.AreEqual("Players", LocalizationService.TranslateLegacy("Игроки"));
                // "Карта" means both a map and a card; it must not be translated implicitly.
                Assert.AreEqual("Карта", LocalizationService.TranslateLegacy("Карта"));
            }
            finally
            {
                LocalizationService.SetLanguage(previous);
            }
        }

        [Test]
        public void EnglishContent_UsesTranslatedTitlesWithoutChangingStableIds()
        {
            string previous = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                var achievement = Achievements.All.First(a => a.Id == "first_contract");
                Assert.AreEqual("First Contract", achievement.Title);
                Assert.AreEqual("Complete a contract.", achievement.Description);
                var challenge = Achievements.DailyTasks.First(a => a.Id == "d_bridges2");
                Assert.AreEqual("Build 2 bridges", challenge.Title);
                Assert.AreEqual("Star", Profiles.Item("emblem_star").DisplayName);
                Assert.AreEqual("Foreman's Crate", Containers.Kinds.First(k => k.Id == "box_foreman").DisplayName);
                Assert.AreEqual("Safety Vest", CharacterCatalog.Item("top_vest").DisplayName);
                Assert.AreEqual("Bridge Builder", CharacterLocalization.Title(CharacterKind.Bridger));
                Assert.AreEqual("Rare", Rarity.Title(Rarity.Level.Rare));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Первый подряд", achievement.Title);
                Assert.AreEqual("Звезда", Profiles.Item("emblem_star").DisplayName);
                Assert.AreEqual("Сигнальный жилет", CharacterCatalog.Item("top_vest").DisplayName);
            }
            finally
            {
                LocalizationService.SetLanguage(previous);
            }
        }

        [Test]
        public void GameRuleContent_HasTranslationsForEveryCargoKind()
        {
            var russian = LoadCatalog("ru").entries.ToDictionary(x => x.key, x => x.value);
            var english = LoadCatalog("en").entries.ToDictionary(x => x.key, x => x.value);
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
                foreach (string suffix in new[] { ".name", ".rule" })
                {
                    string key = "rules.magnate.cargo." + kind + suffix;
                    Assert.IsTrue(russian.ContainsKey(key), "Missing RU cargo text: " + key);
                    Assert.IsTrue(english.ContainsKey(key), "Missing EN cargo text: " + key);
                }
            foreach (string key in new[] { "rules.coop.meter", "rules.coop.orders", "rules.coop.deadlines",
                                           "rules.coop.crisis", "rules.coop.win", "rules.coop.lose" })
                Assert.IsTrue(english.ContainsKey(key), "Missing co-op rules: " + key);
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
