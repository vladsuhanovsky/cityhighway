using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class RulesTests
    {
        static Game Started(int players = 2, GameConfig config = null) =>
            TestCities.Started(TestCities.Small(), players, config);

        [Test]
        public void GameActionErrors_UseEnglishForKnownCoreFailures()
        {
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("You cannot build right now.",
                    LocalizationService.TranslateLegacy("Сейчас нельзя строить."));
                Assert.AreEqual("Not enough machinery cards.",
                    LocalizationService.TranslateLegacy("Не хватает техники."));
                Assert.AreEqual("The deck is empty.",
                    LocalizationService.TranslateLegacy("Колода пуста."));
                Assert.AreEqual("Unknown failure", LocalizationService.TranslateLegacy("Unknown failure"));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Сейчас нельзя строить.",
                    LocalizationService.TranslateLegacy("Сейчас нельзя строить."));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void RulesGlossary_RebuildsForSelectedLanguage()
        {
            var original = LocalizationService.Language;
            try
            {
                var config = new GameConfig();
                LocalizationService.SetLanguage("en");
                var english = RulesGlossary.All(config);
                Assert.AreEqual(19, english.Count);
                foreach (var term in english)
                {
                    Assert.IsFalse(Regex.IsMatch(term.Title + term.Definition + term.Section, "[а-яё]", RegexOptions.IgnoreCase),
                        "Untranslated English glossary term: " + term.Id);
                }
                Assert.AreEqual("Contract", RulesGlossary.Find(config, "contract").Title);
                StringAssert.Contains("<link=\"contract\">",
                    RulesGlossary.Linkify(config, "Complete the contract.", new HashSet<string>()));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Контракт", RulesGlossary.Find(config, "contract").Title);
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Contract", RulesGlossary.Find(config, "contract").Title);
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void DisplaySettings_LocalizedOptionsFollowLanguage()
        {
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Low-end PC", DisplaySettings.PresetTitle(DisplaySettings.Preset.LowEnd));
                Assert.AreEqual("Medium", DisplaySettings.City3DQualityNames[1]);
                Assert.AreEqual("Slow", DisplaySettings.SpeedNames[0]);
                StringAssert.Contains("3D city", DisplaySettings.PresetHint(DisplaySettings.Preset.Cinematic));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Для слабого ПК", DisplaySettings.PresetTitle(DisplaySettings.Preset.LowEnd));
                Assert.AreEqual("Среднее", DisplaySettings.City3DQualityNames[1]);
                Assert.AreEqual("Медленно", DisplaySettings.SpeedNames[0]);
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void RulesSandboxes_TitlesFollowSelectedLanguage()
        {
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                foreach (RuleSandbox.Kind kind in System.Enum.GetValues(typeof(RuleSandbox.Kind)))
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(RuleSandbox.Title(kind), "[а-яё]",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                        "Untranslated sandbox title: " + kind);
                Assert.AreEqual("Sandbox: contracts and tolls", RuleSandbox.Title(RuleSandbox.Kind.Toll));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Песочница: контракт и проезд", RuleSandbox.Title(RuleSandbox.Kind.Toll));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void ModeTabs_TranslateTitlesAndDescriptionsForAllGameModes()
        {
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                foreach (GameMode mode in System.Enum.GetValues(typeof(GameMode)))
                {
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
                        ModeTabs.Title(mode) + ModeTabs.Tagline(mode), "[а-яё]",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                }
                Assert.AreEqual("Logistics Magnate", ModeTabs.Title(GameMode.Magnate));
                Assert.AreEqual("Competitive", ModeTabs.Title(GameMode.Classic));
                Assert.AreEqual("Cooperative", ModeTabs.Title(GameMode.Coop));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Логистический магнат", ModeTabs.Title(GameMode.Magnate));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void MagnateLocationNames_AreTranslatedOnlyForEnglishDisplay()
        {
            var initial = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Airport-West", MagnateLocationText.Name("Аэропорт-Запад"));
                Assert.AreEqual("Central Station", MagnateLocationText.Name("Главный вокзал"));
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
                    MagnateLocationText.Name("Красноозёрск"), "[а-яё]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Аэропорт-Запад", MagnateLocationText.Name("Аэропорт-Запад"));
            }
            finally
            {
                LocalizationService.SetLanguage(initial);
            }
        }

        [Test]
        public void SavedMagnateStats_AreMatchedAcrossLanguageSwitches()
        {
            var statsView = typeof(MenuStatsPage).Assembly.GetType("CityHighways.Presentation.StatsView");
            Assert.IsNotNull(statsView);
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var rankIndex = statsView.GetMethod("RankIndex", flags);
            var difficultyIndex = statsView.GetMethod("DifficultyIndex", flags);
            var displayRank = statsView.GetMethod("DisplayRank", flags);
            var displayDifficulty = statsView.GetMethod("DisplayDifficulty", flags);
            Assert.IsNotNull(rankIndex);
            Assert.IsNotNull(difficultyIndex);
            Assert.IsNotNull(displayRank);
            Assert.IsNotNull(displayDifficulty);

            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual(0, rankIndex.Invoke(null, new object[] { "Новичок" }));
                Assert.AreEqual(4, rankIndex.Invoke(null, new object[] { "Легенда" }));
                Assert.AreEqual(2, rankIndex.Invoke(null, new object[] { "Pro" }));
                Assert.AreEqual(0, difficultyIndex.Invoke(null, new object[] { "Лёгкая" }));
                Assert.AreEqual(1, difficultyIndex.Invoke(null, new object[] { "Обычная" }));
                Assert.AreEqual("Easy", displayDifficulty.Invoke(null, new object[] { "Лёгкая" }));
                Assert.AreEqual("Legend", displayRank.Invoke(null, new object[] { "Легенда" }));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Обычная", displayDifficulty.Invoke(null, new object[] { "Normal" }));
                Assert.AreEqual("Профи", displayRank.Invoke(null, new object[] { "Pro" }));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void ChangingUiLanguage_DoesNotMutateGameSaveOrRestore()
        {
            string original = LocalizationService.Language;
            try
            {
                var map = TestCities.Small();
                var game = new Game(map, new[] { "Player One", "Player Two" }, seed: 42);

                LocalizationService.SetLanguage("ru");
                string russianSave = UnityEngine.JsonUtility.ToJson(game.CreateSave());

                LocalizationService.SetLanguage("en");
                string englishSave = UnityEngine.JsonUtility.ToJson(game.CreateSave());
                Assert.AreEqual(russianSave, englishSave,
                    "Changing the UI language must not modify persisted game state.");

                var restored = Game.FromSave(map,
                    UnityEngine.JsonUtility.FromJson<GameSaveData>(russianSave));
                Assert.AreEqual(englishSave,
                    UnityEngine.JsonUtility.ToJson(restored.CreateSave()),
                    "A Russian-language save must round-trip without modifying the game state under English UI.");

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual(russianSave, UnityEngine.JsonUtility.ToJson(restored.CreateSave()));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void ArchivedMagnateEvents_TranslateWithoutActiveGameEvent()
        {
            var method = typeof(MagnateView).GetMethod("ArchivedEventMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method);
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                string[] history =
                {
                    "Событие: ДТП. Фура сломалась на дороге Север — Юг: дорога закрыта на этот раунд — фуры объезжают.",
                    "Событие: Ремонт дороги. Дорога Центр — Восток закрыта на ремонт на 2 раунда.",
                    "Событие: Нехватка топлива. Все фуры проезжают на 2 участка меньше в этот раунд.",
                    "Событие: Таможенная проверка. Опасные и ценные грузы в пути платят 2 влияния — иначе стоят раунд.",
                    "Событие: Штормовое предупреждение. Мосты пропускают на одну фуру меньше, тяжеловесы и крупногабарит по мостам не едут.",
                    "Событие: Фестиваль. Улицы Центра перекрыты на этот раунд."
                };
                foreach (string log in history)
                {
                    string result = method.Invoke(null, new object[] { log }) as string;
                    Assert.IsNotNull(result, "Unrecognized archived event: " + log);
                    Assert.IsFalse(Regex.IsMatch(result, "[а-яё]", RegexOptions.IgnoreCase),
                        "Untranslated archived event: " + log);
                }
                Assert.IsNull(method.Invoke(null, new object[] { "Unrecognized event format" }));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void CooperativeOrderTimer_LocalizesAfterSwitchingLanguage()
        {
            var method = typeof(CoopPanel).GetMethod("RoundLabel",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var initial = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("final round", method.Invoke(null, new object[] { 1 }));
                Assert.AreEqual("3 rounds", method.Invoke(null, new object[] { 3 }));
                Assert.AreEqual("5 rounds", method.Invoke(null, new object[] { 5 }));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("последний круг", method.Invoke(null, new object[] { 1 }));
                Assert.AreEqual("3 круга", method.Invoke(null, new object[] { 3 }));
                Assert.AreEqual("5 кругов", method.Invoke(null, new object[] { 5 }));
            }
            finally
            {
                LocalizationService.SetLanguage(initial);
            }
        }

        [Test]
        public void TradeSummary_UsesSelectedLanguageWithoutChangingOffer()
        {
            var method = typeof(GameView).GetMethod("TradeSide",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            string original = LocalizationService.Language;
            var args = new object[] { 2, Resource.Concrete, 3, true };
            try
            {
                LocalizationService.SetLanguage("en");
                string english = (string)method.Invoke(null, args);
                StringAssert.Contains("influence", english);
                StringAssert.Contains("road pass", english);
                Assert.IsFalse(Regex.IsMatch(english, "[а-яё]", RegexOptions.IgnoreCase));

                LocalizationService.SetLanguage("ru");
                string russian = (string)method.Invoke(null, args);
                StringAssert.Contains("влияния", russian);
                StringAssert.Contains("пропуск", russian);
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void RoadKinds_InGameTooltips_FollowSelectedLanguage()
        {
            var method = typeof(GameView).GetMethod("RoadKindLabel",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            string initial = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                foreach (Resource value in new[] { Resource.Asphalt, Resource.Concrete, Resource.Steel, Resource.Gravel })
                {
                    var title = (string)method.Invoke(null, new object[] { (Resource?)value });
                    Assert.IsFalse(Regex.IsMatch(title, "[а-яё]", RegexOptions.IgnoreCase), value.ToString());
                }
                Assert.AreEqual("Bridge", method.Invoke(null, new object[] { (Resource?)Resource.Steel }));
                Assert.AreEqual("Street (any resource)", method.Invoke(null, new object[] { null }));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("мост", method.Invoke(null, new object[] { (Resource?)Resource.Steel }));
            }
            finally
            {
                LocalizationService.SetLanguage(initial);
            }
        }

        [Test]
        public void MayorGoals_DisplayTextUsesSelectedLanguage()
        {
            var nameMethod = typeof(GameView).GetMethod("MayorGoalName",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var descriptionMethod = typeof(GameView).GetMethod("MayorGoalDescription",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(nameMethod);
            Assert.IsNotNull(descriptionMethod);
            var goal = new MayorGoal("test-bridge", "Мосты для города", GoalKind.Bridges, 2, 5);
            var map = TestCities.Small();
            var initial = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Bridge Builder", nameMethod.Invoke(null, new object[] { goal }));
                Assert.AreEqual("Build 2 bridges across the river",
                    descriptionMethod.Invoke(null, new object[] { goal, map }));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Мосты для города", nameMethod.Invoke(null, new object[] { goal }));
                Assert.AreEqual(goal.Describe(map),
                    descriptionMethod.Invoke(null, new object[] { goal, map }));
            }
            finally
            {
                LocalizationService.SetLanguage(initial);
            }
        }

        [Test]
        public void ConstructionPayment_UsesSelectedLanguage()
        {
            var method = typeof(GameView).GetMethod("PaymentLabel",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var option = new PaymentOption(Resource.Machinery, 2);
            var initial = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                StringAssert.Contains("Machinery", (string)method.Invoke(null, new object[] { 2, option }));
                LocalizationService.SetLanguage("ru");
                StringAssert.Contains("техника", (string)method.Invoke(null, new object[] { 2, option }));
            }
            finally
            {
                LocalizationService.SetLanguage(initial);
            }
        }

        [Test]
        public void LuckCardTitlesAndDescriptions_TranslateForAllKinds()
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var title = typeof(GameView).GetMethod("LuckCardTitle", flags);
            var description = typeof(GameView).GetMethod("LuckCardDescription", flags);
            Assert.IsNotNull(title);
            Assert.IsNotNull(description);
            var config = new GameConfig();
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                foreach (LuckCardKind kind in System.Enum.GetValues(typeof(LuckCardKind)))
                {
                    string name = (string)title.Invoke(null, new object[] { kind });
                    string effect = (string)description.Invoke(null, new object[] { kind, config });
                    Assert.IsFalse(Regex.IsMatch(name + effect, "[а-яё]", RegexOptions.IgnoreCase),
                        "English luck card not translated: " + kind);
                }
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Грант мэрии", title.Invoke(null, new object[] { LuckCardKind.Grant }));
                Assert.AreEqual(LuckInfo.Describe(LuckCardKind.Fine, config),
                    description.Invoke(null, new object[] { LuckCardKind.Fine, config }));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void FirstGameTutorial_AllEnglishStepsAreTranslated()
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var titlesField = typeof(GameView).GetField("EnglishFirstGameTutorialTitles", flags);
            var textsField = typeof(GameView).GetField("EnglishFirstGameTutorialTexts", flags);
            Assert.IsNotNull(titlesField);
            Assert.IsNotNull(textsField);
            var titles = titlesField.GetValue(null) as string[];
            var texts = textsField.GetValue(null) as string[];
            Assert.IsNotNull(titles);
            Assert.IsNotNull(texts);
            Assert.AreEqual(9, titles.Length);
            Assert.AreEqual(titles.Length, texts.Length);
            for (int i = 0; i < titles.Length; i++)
            {
                Assert.IsNotEmpty(titles[i], "Missing tutorial title at index " + i);
                Assert.IsNotEmpty(texts[i], "Missing tutorial text at index " + i);
                Assert.IsFalse(Regex.IsMatch(titles[i] + texts[i], "[а-яё]", RegexOptions.IgnoreCase),
                    "Russian text remains in first-game tutorial step " + i);
            }
        }

        [Test]
        public void ClassicJournal_RecognizesEveryResourceAndRoadType()
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var resources = typeof(GameView).GetMethod("EnglishResourceName", flags);
            var roads = typeof(GameView).GetMethod("EnglishRoadType", flags);
            Assert.IsNotNull(resources);
            Assert.IsNotNull(roads);
            foreach (var word in new[] { "асфальт", "бетон", "сталь", "щебень", "техника" })
            {
                string result = resources.Invoke(null, new object[] { word }) as string;
                Assert.IsFalse(Regex.IsMatch(result, "[а-яё]", RegexOptions.IgnoreCase), word);
            }
            foreach (var word in new[] { "улица", "проспект", "мост", "загородная дорога", "улица (любой ресурс)" })
            {
                string result = roads.Invoke(null, new object[] { word }) as string;
                Assert.IsFalse(Regex.IsMatch(result, "[а-яё]", RegexOptions.IgnoreCase), word);
            }
            Assert.AreEqual("country road", roads.Invoke(null, new object[] { "загородная дорога" }));
        }

        [Test]
        public void CooperativeJournal_TranslatesCityOrdersAndCrisisWithoutChangingNames()
        {
            var translate = typeof(GameView).GetMethod("CoopJournalText",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(translate);
            var examples = new[]
            {
                "Новый заказ города: Север — Юг — 7 очк., срок 3 кругов.",
                "Заказ города выполнен: Север — Юг (+7 очк. — Alice). Кризис 3 → 1. Выполнено 2 из 5.",
                "Заказ города просрочен: Север — Юг. Кризис +2 → 4 из 6.",
                "Город спасён: выполнено 5 заказов из 5, кризис 1 из 6.",
                "Город встал: кризис дошёл до 6. Выполнено заказов: 2 из 5.",
                "Участки кончились раньше, чем город получил свои заказы: выполнено 2 из 5."
            };
            foreach (string entry in examples)
            {
                string translated = (string)translate.Invoke(null, new object[] { entry });
                Assert.IsNotNull(translated, "Unknown Cooperative message: " + entry);
                // Map place names may remain Cyrillic when a map has no English alias.
                Assert.IsFalse(translated.Contains("Заказ города") || translated.Contains("Кризис") ||
                    translated.Contains("Город спасён") || translated.Contains("Участки кончились"),
                    "Cooperative message was not translated: " + entry);
            }
            string completed = (string)translate.Invoke(null, new object[] { examples[1] });
            StringAssert.Contains("Alice", completed);
            Assert.IsNull(translate.Invoke(null, new object[] { "Unknown format" }));
        }

        [Test]
        public void LuckJournal_AllKnownOutcomesAreTranslated()
        {
            var translate = typeof(GameView).GetMethod("LuckResultText",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(translate);
            foreach (string outcome in new[]
            {
                "страховка: вместо дебафа −2 очко.", "+3 влияния.", "+6 очков.",
                "+2 карт ресурсов.", "выполнено контрактов: 2, +4 очков.",
                "−3 очка.", "−1 карта с руки.", "рука пуста, красть нечего.",
                "невыполненных контрактов нет, −3 очка."
            })
            {
                string result = (string)translate.Invoke(null, new object[] { outcome });
                Assert.IsFalse(Regex.IsMatch(result, "[а-яё]", RegexOptions.IgnoreCase),
                    "Luck outcome was not translated: " + outcome);
            }
        }

        [Test]
        public void CityEvents_AllSevenTitlesTranslateOnLanguageSwitch()
        {
            var method = typeof(GameView).GetMethod("CityEventTitle",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            string initial = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                foreach (CityEventKind kind in System.Enum.GetValues(typeof(CityEventKind)))
                {
                    string label = (string)method.Invoke(null, new object[] { kind });
                    Assert.IsFalse(Regex.IsMatch(label, "[а-яё]", RegexOptions.IgnoreCase),
                        "City event title was not translated: " + kind);
                }
                Assert.AreEqual("Rush Hour", method.Invoke(null, new object[] { CityEventKind.RushHour }));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Час пик", method.Invoke(null, new object[] { CityEventKind.RushHour }));
            }
            finally
            {
                LocalizationService.SetLanguage(initial);
            }
        }

        [Test]
        public void ArchivedCityEvents_TranslateWithoutCurrentEvent()
        {
            var method = typeof(GameView).GetMethod("ArchivedCityEventText",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var source = new[]
            {
                "Событие: Час пик. Дороги, построенные в этот круг, станут загруженными магистралями: проезд по ним — 4 очк. вместо 2.",
                "Событие: Ремонт. Свободных участков для ремонта не нашлось.",
                "Событие: Субсидия мэрии. Мосты в этот круг дешевле на 1 карт(у) стали (но не меньше одной).",
                "Событие: Парад. Район «Центр» перекрыт: до конца круга нельзя строить дороги, которые его касаются.",
                "Событие: Снегопад. Щебёночные дороги в этот круг на 2 карт(у) дороже.",
                "Событие: Инвестор. Каждая дорога, построенная в этот круг, приносит ещё +3 очка.",
                "Событие: Новый район. На окраине открылся район «Восток»: новые места, дороги и контракты."
            };
            foreach (var log in source)
            {
                string translated = method.Invoke(null, new object[] { log }) as string;
                Assert.IsNotNull(translated, "Unrecognized event: " + log);
                StringAssert.StartsWith("Event:", translated);
                Assert.IsFalse(translated.Contains("Событие:"), "Untranslated event prefix: " + log);
            }
            Assert.IsNull(method.Invoke(null, new object[] { "Событие: неизвестный формат." }));
        }

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
