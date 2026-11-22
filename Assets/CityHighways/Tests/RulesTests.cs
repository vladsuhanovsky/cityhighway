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
        public void LiveBuildAndSave_KeepCoreStateIndependentOfLanguage()
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("ru");
                var map = TestCities.Small();
                var game = TestCities.Started(map);
                var journal = new List<string>();
                game.Log += journal.Add;
                game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
                var built = game.BuildRoad("r1", Resource.Asphalt, 0);
                Assert.IsTrue(built.Success, built.Error);
                Assert.IsTrue(journal.Any(line => line.Contains("строит")),
                    "The game must store its original language-neutral log format.");

                string blocker = game.GetBuildBlocker("r1");
                StringAssert.Contains("Дорогу уже построил", blocker);
                string before = UnityEngine.JsonUtility.ToJson(game.CreateSave());

                LocalizationService.SetLanguage("en");
                string translated = LocalizationService.TranslateGameError(blocker);
                StringAssert.Contains("This road was already built by", translated);
                Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(game.CreateSave()),
                    "Switching to English must not modify a live game or its saved state.");

                var restored = Game.FromSave(map,
                    UnityEngine.JsonUtility.FromJson<GameSaveData>(before));
                Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(restored.CreateSave()),
                    "Build state must survive save/load under the other UI language.");

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual(blocker, LocalizationService.TranslateGameError(blocker));
                Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(game.CreateSave()));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [Test]
        public void LivePendingTrade_SurvivesLanguageChangeSaveLoadAndAcceptance()
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("ru");
                var map = TestCities.Small();
                var game = TestCities.Started(map);
                var journal = new List<string>();
                game.Log += journal.Add;
                game.Debug_SetHand(0, Resource.Asphalt);
                game.Debug_SetHand(1, Resource.Steel);

                var proposed = game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 1);
                Assert.IsTrue(proposed.Success, proposed.Error);
                Assert.AreEqual(GamePhase.RespondingTrade, game.Phase);
                string offer = journal.Last();
                StringAssert.Contains(" предлагает ", offer);
                Assert.IsNull(LocalizationService.TranslateTradeJournal(offer));
                string saved = UnityEngine.JsonUtility.ToJson(game.CreateSave());

                LocalizationService.SetLanguage("en");
                string display = LocalizationService.TranslateTradeJournal(offer);
                StringAssert.Contains(" offers ", display);
                StringAssert.Contains("asphalt", display);
                StringAssert.Contains("steel", display);
                Assert.AreEqual(saved, UnityEngine.JsonUtility.ToJson(game.CreateSave()));

                var restored = Game.FromSave(map,
                    UnityEngine.JsonUtility.FromJson<GameSaveData>(saved));
                Assert.AreEqual(GamePhase.RespondingTrade, restored.Phase);
                Assert.AreEqual(saved, UnityEngine.JsonUtility.ToJson(restored.CreateSave()));
                Assert.IsTrue(restored.RespondTrade(true).Success);
                Assert.AreEqual(1, restored.Players[0].CardCount(Resource.Steel));
                Assert.AreEqual(1, restored.Players[1].CardCount(Resource.Asphalt));

                LocalizationService.SetLanguage("ru");
                Assert.IsNull(LocalizationService.TranslateTradeJournal(offer));
                Assert.AreEqual(saved, UnityEngine.JsonUtility.ToJson(game.CreateSave()));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [Test]
        public void RoadSweep_RespectsProgressAndReducedMotion()
        {
            Assert.AreEqual(0f, GameFxTuning.SweepOpacity(0.5f, 0.5f, true));
            Assert.AreEqual(1f, GameFxTuning.SweepOpacity(0.5f, 0.5f, false), 0.001f);
            Assert.AreEqual(0f, GameFxTuning.SweepOpacity(0.7f, 0.5f, false));
            Assert.AreEqual(0f, GameFxTuning.SweepOpacity(0.1f, 0.9f, false));
        }

        [Test]
        public void GameTooltip_UsesViewportWidthAndRemainsReadable()
        {
            Assert.AreEqual(300f, GameFxTuning.TooltipWidth(1200f));
            Assert.AreEqual(224f, GameFxTuning.TooltipWidth(240f));
            var position = GameFxTuning.TooltipPosition(new UnityEngine.Vector2(220f, 125f),
                new UnityEngine.Vector2(240f, 180f), GameFxTuning.TooltipWidth(240f), 130f);
            Assert.GreaterOrEqual(position.x, 8f);
            Assert.GreaterOrEqual(position.y, 8f);
            Assert.LessOrEqual(position.x + GameFxTuning.TooltipWidth(240f), 240f);
        }

        [Test]
        public void ContractCarousel_LeavesVerticalScrollForTheParentPanel()
        {
            Assert.IsFalse(CardCarousel.ShouldCaptureWheel(0f, 1f, true));
            Assert.IsFalse(CardCarousel.ShouldCaptureWheel(1f, 2f, true));
            Assert.IsFalse(CardCarousel.ShouldCaptureWheel(2f, 0f, false));
            Assert.IsTrue(CardCarousel.ShouldCaptureWheel(2f, 0f, true));
        }

        [Test]
        public void ContractLayout_UsesCompactRulesWithoutChangingCardData()
        {
            Assert.IsTrue(GameFxTuning.CompactContracts(800f));
            Assert.IsFalse(GameFxTuning.CompactContracts(1200f));
            Assert.AreEqual(16f, GameFxTuning.ContractsHeaderFontSize(800f));
            Assert.AreEqual(18f, GameFxTuning.ContractsHeaderFontSize(1200f));
        }

        [Test]
        public void GamePanels_AdaptWidthsAndToastsOnCompactScreens()
        {
            Assert.IsTrue(GameFxTuning.CompactActions(600f));
            Assert.IsFalse(GameFxTuning.CompactActions(1200f));
            Assert.AreEqual(576f, GameFxTuning.SheetWidth(600f, 860f));
            Assert.AreEqual(860f, GameFxTuning.SheetWidth(1400f, 860f));
            Assert.AreEqual(568f, GameFxTuning.ToastMaxWidth(600f));
            Assert.AreEqual(76f, GameFxTuning.ToastVerticalOffset(600f));
            Assert.AreEqual(0f, GameFxTuning.ToastVerticalOffset(1200f));
        }

        [Test]
        public void MapTooltip_SupportsSmallViewportWidths()
        {
            var bounds = new UnityEngine.Vector2(200f, 170f);
            float tooltipWidth = UnityEngine.Mathf.Min(300f, UnityEngine.Mathf.Max(100f, bounds.x - 16f));
            var pos = GameFxTuning.TooltipPosition(new UnityEngine.Vector2(190f, 120f),
                bounds, tooltipWidth, 130f);
            Assert.LessOrEqual(pos.x + tooltipWidth, bounds.x);
            Assert.LessOrEqual(pos.y + 130f, bounds.y);
        }

        [Test]
        public void MapTooltip_StaysInsideAvailableBounds()
        {
            var bounds = new UnityEngine.Vector2(700f, 500f);
            var topLeft = GameFxTuning.TooltipPosition(UnityEngine.Vector2.zero, bounds, 300f, 130f);
            Assert.GreaterOrEqual(topLeft.x, 8f);
            Assert.GreaterOrEqual(topLeft.y, 8f);
            var bottomRight = GameFxTuning.TooltipPosition(new UnityEngine.Vector2(680f, 480f), bounds, 300f, 130f);
            Assert.LessOrEqual(bottomRight.x + 300f, bounds.x);
            Assert.LessOrEqual(bottomRight.y + 130f, bounds.y);
            var small = GameFxTuning.TooltipPosition(new UnityEngine.Vector2(50f, 50f),
                new UnityEngine.Vector2(240f, 140f), 300f, 130f);
            Assert.GreaterOrEqual(small.x, 8f);
            Assert.GreaterOrEqual(small.y, 8f);
        }

        [Test]
        public void HazardMarkers_PrioritizeJamsAndClosuresOverSnow()
        {
            Assert.AreEqual(0, GameFxTuning.RoadWarningPriority(false, false, false, false));
            Assert.AreEqual(1, GameFxTuning.RoadWarningPriority(false, false, false, true));
            Assert.AreEqual(2, GameFxTuning.RoadWarningPriority(false, false, true, true));
            Assert.AreEqual(3, GameFxTuning.RoadWarningPriority(false, true, true, true));
            Assert.AreEqual(4, GameFxTuning.RoadWarningPriority(true, true, true, true));
        }

        [Test]
        public void HazardMarkers_RespectReducedMotionAndHaveBoundedOpacity()
        {
            Assert.AreEqual(0.82f, GameFxTuning.RoadWarningOpacity(0f, true), 0.001f);
            foreach (float time in new[] { 0f, 0.3f, 2f, 20f })
            {
                float opacity = GameFxTuning.RoadWarningOpacity(time, false);
                Assert.GreaterOrEqual(opacity, 0.54f);
                Assert.LessOrEqual(opacity, 0.91f);
            }
        }

        [Test]
        public void ConstructionPreview_CanBeClearedOnActionTransition()
        {
            var map = new MapView();
            map.SetConstructionPreview("r1", false, false);
            Assert.AreEqual("r1", map.PreviewRoadId);
            Assert.IsFalse(map.PreviewAllowed);
            map.SetConstructionPreview(null, false, false);
            Assert.IsNull(map.PreviewRoadId);
            Assert.IsFalse(map.PreviewFree);
        }

        [Test]
        public void OwnershipMap_IsOnlyAViewMode()
        {
            var map = new MapView();
            Assert.IsFalse(map.OwnershipView);
            map.OwnershipView = true;
            Assert.IsTrue(map.OwnershipView);
            map.OwnershipView = false;
            Assert.IsFalse(map.OwnershipView);
        }

        [Test]
        public void ConstructionPreview_IsVisualOnlyAndClearsCorrectly()
        {
            var map = new MapView();
            Assert.IsNull(map.PreviewRoadId);
            map.SetConstructionPreview("r1", true, false);
            Assert.AreEqual("r1", map.PreviewRoadId);
            Assert.IsTrue(map.PreviewAllowed);
            Assert.IsFalse(map.PreviewFree);
            map.SetConstructionPreview("r1", true, true);
            Assert.IsTrue(map.PreviewFree);
            map.SetConstructionPreview(null, false, false);
            Assert.IsNull(map.PreviewRoadId);
        }

        [Test]
        public void ButtonFeedback_UsesColorWithoutMovementInReducedMotion()
        {
            Assert.AreEqual(-1.5f, GameFxTuning.ButtonHoverLift(false), 0.001f);
            Assert.AreEqual(0f, GameFxTuning.ButtonHoverLift(true), 0.001f);
            Assert.AreEqual(0.97f, GameFxTuning.ButtonPressScale(false), 0.001f);
            Assert.AreEqual(1f, GameFxTuning.ButtonPressScale(true), 0.001f);
        }

        [Test]
        public void ResourceCardFx_RespectsAvailabilityAndReducedMotion()
        {
            Assert.IsTrue(GameFxTuning.CanHighlightFaceUp(true, true, true, false));
            Assert.IsFalse(GameFxTuning.CanHighlightFaceUp(false, true, true, false));
            Assert.IsFalse(GameFxTuning.CanHighlightFaceUp(true, false, true, false));
            Assert.IsFalse(GameFxTuning.CanHighlightFaceUp(true, true, false, false));
            Assert.IsFalse(GameFxTuning.CanHighlightFaceUp(true, true, true, true));
            Assert.Less(GameFxTuning.FaceUpHoverOffset(true, false), 0f);
            Assert.AreEqual(0f, GameFxTuning.FaceUpHoverOffset(false, false));
            Assert.AreEqual(0f, GameFxTuning.FaceUpHoverOffset(true, true));
            Assert.Less(GameFxTuning.CardFlightDuration(true), GameFxTuning.CardFlightDuration(false));
            Assert.AreEqual(0f, GameFxTuning.CardArcHeight(true));
            Assert.Greater(GameFxTuning.CardArcHeight(false), 0f);
        }

        [Test]
        public void ContractAndJamFx_UseBoundedParticleBudgets()
        {
            Assert.AreEqual(24, GameFxTuning.ContractSparksPerDistrict(1, false));
            Assert.AreEqual(24, GameFxTuning.ContractSparksPerDistrict(2, false));
            Assert.AreEqual(20, GameFxTuning.ContractSparksPerDistrict(3, false));
            Assert.AreEqual(8, GameFxTuning.ContractSparksPerDistrict(20, false));
            Assert.AreEqual(0, GameFxTuning.ContractSparksPerDistrict(2, true));
            Assert.AreEqual(8, GameFxTuning.JamSparks(0, false));
            Assert.AreEqual(8, GameFxTuning.JamSparks(1, false));
            Assert.AreEqual(0, GameFxTuning.JamSparks(2, false));
            Assert.AreEqual(0, GameFxTuning.JamSparks(0, true));
            Assert.AreEqual(0, GameFxTuning.JamSparks(-1, false));
        }

        [Test]
        public void CoinFx_KeepLargeSettlementsReadableWithoutLosingValues()
        {
            Assert.IsFalse(GameFxTuning.AnimateIndividualCoins(0, false));
            Assert.IsTrue(GameFxTuning.AnimateIndividualCoins(1, false));
            Assert.IsTrue(GameFxTuning.AnimateIndividualCoins(GameFxTuning.MaxAnimatedCoinsPerRoute, false));
            Assert.IsFalse(GameFxTuning.AnimateIndividualCoins(GameFxTuning.MaxAnimatedCoinsPerRoute + 1, false));
            Assert.IsFalse(GameFxTuning.AnimateIndividualCoins(1, true));

            foreach (float k in new[] { -1f, 0f, 0.25f, 0.5f, 1f, 2f })
            {
                Assert.GreaterOrEqual(GameFxTuning.CoinScale(k), 0.25f);
                Assert.LessOrEqual(GameFxTuning.CoinScale(k), 1.01f);
            }
        }

        [Test]
        public void GameplayEffects_RespectReducedMotionAndStayWithinSafeBounds()
        {
            Assert.AreEqual(0f, GameFxTuning.ToastOpacity(0f), 0.001f);
            Assert.AreEqual(1f, GameFxTuning.ToastOpacity(0.5f), 0.001f);
            Assert.AreEqual(0f, GameFxTuning.ToastOpacity(1f), 0.001f);
            Assert.AreEqual(0f, GameFxTuning.ToastOpacity(-1f), 0.001f);
            Assert.AreEqual(0f, GameFxTuning.ToastOpacity(2f), 0.001f);
            Assert.AreEqual(GameFxTuning.ToastTop, GameFxTuning.ToastTopAt(0f, true), 0.001f);
            Assert.AreEqual(GameFxTuning.ToastTop, GameFxTuning.ToastTopAt(1f, true), 0.001f);
            Assert.Less(GameFxTuning.ToastTopAt(0f, false), GameFxTuning.ToastTopAt(1f, false));
            Assert.AreEqual(0, GameFxTuning.FireworkCount(48, true));
            Assert.AreEqual(48, GameFxTuning.FireworkCount(48, false));
            Assert.AreEqual(0f, GameFxTuning.EventGlowStrength(0.5f, true), 0.001f);
            Assert.Greater(GameFxTuning.EventGlowStrength(0.5f, false), 0.9f);
        }

        [Test]
        public void DynamicGameActionErrors_TranslateValuesWithoutChangingCoreMessages()
        {
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Influence required: 4; you have 2.",
                    LocalizationService.TranslateGameError("Нужно влияния: 4, у вас 2."));
                Assert.AreEqual("Bribes require at least 3 players.",
                    LocalizationService.TranslateGameError("Взятки — только когда игроков 3 и больше."));
                Assert.AreEqual("You need 5 steel cards (machinery can substitute for any card).",
                    LocalizationService.TranslateGameError("Нужно 5 карт(ы) «сталь» (техника подходит вместо любой карты)."));
                Assert.AreEqual("Not enough asphalt cards.",
                    LocalizationService.TranslateGameError("Не хватает карт «асфальт»."));
                Assert.AreEqual("This road requires concrete.",
                    LocalizationService.TranslateGameError("Эту дорогу строят из ресурса «бетон»."));
                Assert.AreEqual("Masha already has your pass.",
                    LocalizationService.TranslateGameError("У Masha уже есть ваш пропуск."));
                Assert.AreEqual("You already have Alex's pass.",
                    LocalizationService.TranslateGameError("У вас уже есть пропуск Alex."));
                Assert.AreEqual("You only have 0 influence.",
                    LocalizationService.TranslateGameError("У вас только 0 влияния."));
                Assert.AreEqual("Внимание: не хватило 3 новых карт.",
                    LocalizationService.TranslateGameError("Внимание: не хватило 3 новых карт."));
                Assert.AreEqual("Не хватает карт «неизвестно».",
                    LocalizationService.TranslateGameError("Не хватает карт «неизвестно»."));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Не хватает карт «сталь».",
                    LocalizationService.TranslateGameError("Не хватает карт «сталь»."));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [System.Serializable]
        public class LocalizationAuditEntry
        {
            public string key;
            public string value;
        }

        [System.Serializable]
        public class LocalizationAuditCatalog
        {
            public LocalizationAuditEntry[] entries;
        }

        [Test]
        public void MayorGoalInfluenceReward_ChangesLanguageWithoutChangingSource()
        {
            const string source = "Mira: +2 влияния (цель мэрии).";
            var previous = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Mira: +2 influence (City Hall goal).",
                    LocalizationService.TranslateProgressJournal(source));
                LocalizationService.SetLanguage("ru");
                Assert.IsNull(LocalizationService.TranslateProgressJournal(source));
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Mira: +2 influence (City Hall goal).",
                    LocalizationService.TranslateProgressJournal(source));
            }
            finally { LocalizationService.SetLanguage(previous); }
        }

        [Test]
        public void HistoricalTradeOffers_TranslateMixedCardInfluenceAndPassOffers()
        {
            var previous = LocalizationService.Language;
            const string offer = "Mira предлагает Alex: 3 × сталь и 2 влияния и пропуск по дорогам за 1 × бетон и пропуск по дорогам.";
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Mira offers Alex: 3 × steel and 2 influence and road pass for 1 × concrete and road pass.",
                    LocalizationService.TranslateTradeJournal(offer));
                Assert.AreEqual("Mira offers Alex: road pass for 2 × gravel.",
                    LocalizationService.TranslateTradeJournal("Mira предлагает Alex: пропуск по дорогам за 2 × щебень."));
                Assert.AreEqual("Mira offers Alex: nothing for 1 influence.",
                    LocalizationService.TranslateTradeJournal("Mira предлагает Alex: ничего за 1 влияния."));
                Assert.IsNull(LocalizationService.TranslateTradeJournal("Mira предлагает Alex: 2 × unknown за 1 × бетон."));
                Assert.IsNull(LocalizationService.TranslateTradeJournal("Not a trade message"));
                LocalizationService.SetLanguage("ru");
                Assert.IsNull(LocalizationService.TranslateTradeJournal(offer));
                LocalizationService.SetLanguage("en");
                Assert.IsNotNull(LocalizationService.TranslateTradeJournal(offer));
            }
            finally { LocalizationService.SetLanguage(previous); }
        }

        [Test]
        public void ScenarioAnnouncements_TranslateAllScenarioKindsAndLiveRuleValues()
        {
            var previous = LocalizationService.Language;
            try
            {
                foreach (var kind in Scenarios.All)
                {
                    if (kind == ScenarioKind.Standard) continue;
                    var config = Scenarios.Apply(new GameConfig(), kind);
                    string original = "Сценарий «" + Scenarios.Title(kind) + "»: " +
                        string.Join(" ", Scenarios.Changes(kind, config));
                    LocalizationService.SetLanguage("en");
                    string translated = ScenarioMenuLocalization.JournalAnnouncement(original, kind, config);
                    Assert.IsNotNull(translated, "Missing scenario translation: " + kind);
                    StringAssert.StartsWith("Scenario “", translated);
                    Assert.IsFalse(Regex.IsMatch(translated, "[а-яё]", RegexOptions.IgnoreCase),
                        "Untranslated scenario journal: " + kind);
                    Assert.IsNull(ScenarioMenuLocalization.JournalAnnouncement(original + " extra", kind, config));
                    LocalizationService.SetLanguage("ru");
                    Assert.IsNull(ScenarioMenuLocalization.JournalAnnouncement(original, kind, config));
                }
            }
            finally { LocalizationService.SetLanguage(previous); }
        }

        [Test]
        public void CityHallAuctionAndBriberyAccusations_AreLocalized()
        {
            var original = LocalizationService.Language;
            const string auction = "Аукцион мэрии: контракт Север — Центр (7 очк., выполнен — ещё +3). Тайные ставки влиянием.";
            try
            {
                LocalizationService.SetLanguage("en");
                System.Func<string, string> district = name =>
                    name == "Север" ? "North" : name == "Центр" ? "Center" : name;
                Assert.AreEqual("City Hall auction: contract North — Center (7 pts, +3 when completed). Secret influence bids.",
                    LocalizationService.TranslateSpecialJournal(auction, district));
                Assert.AreEqual("Vera accuses Alex of bribery: confirmed! 2 bribes exposed, penalty −6.",
                    LocalizationService.TranslateSpecialJournal("Vera обвиняет Alex во взятке — подтвердилось! Раскрыто: 2, штраф −6."));
                Assert.AreEqual("Alex accuses Vera of bribery: not confirmed. False accusation penalty: −4.",
                    LocalizationService.TranslateSpecialJournal("Alex обвиняет Vera во взятке — не подтвердилось. Ложное обвинение: −4."));
                Assert.IsNull(LocalizationService.TranslateSpecialJournal("Unrecognized auction data."));
                LocalizationService.SetLanguage("ru");
                Assert.IsNull(LocalizationService.TranslateSpecialJournal(auction, district));
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("City Hall auction: contract North — Center (7 pts, +3 when completed). Secret influence bids.",
                    LocalizationService.TranslateSpecialJournal(auction, district));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [Test]
        public void ProgressJournal_CoversRoadsInfluenceAndFinaleInBothLanguages()
        {
            var original = LocalizationService.Language;
            const string jam = "Пробка на дороге Север — Центр: дальше проезд по ней ещё +2 очк. городу. Владелец может улучшить её до магистрали.";
            const string income = "Mira: +2 влияния (контракт Север — Центр выполнен).";
            try
            {
                LocalizationService.SetLanguage("en");
                System.Func<string, string> place = x => x == "Север" ? "North" : x == "Центр" ? "Center" : x;
                Assert.AreEqual("Traffic jam on North — Center: future trips pay an extra 2 pts to the city. The owner can upgrade the road to a highway.",
                    LocalizationService.TranslateProgressJournal(jam, place));
                Assert.AreEqual("Mira pays road tolls: Alex 2, Vera 1 pts, plus 3 pts to the city for traffic jams.",
                    LocalizationService.TranslateProgressJournal("Mira платит за проезд: Alex 2, Vera 1 очк., пробка: ещё 3 городу."));
                Assert.AreEqual("Mira upgrades North — Center to a highway (+3 pts).",
                    LocalizationService.TranslateProgressJournal("Mira улучшает дорогу Север — Центр до магистрали (+3).", place));
                Assert.AreEqual("Mira spends 2 influence to refresh the market (next refresh costs 3).",
                    LocalizationService.TranslateProgressJournal("Mira тратит 2 влияния: рынок выложен заново (следующее обновление — 3)."));
                Assert.AreEqual("Mira buys a City Hall permit for 2 influence: Snowfall does not restrict this turn.",
                    LocalizationService.TranslateProgressJournal("Mira получает разрешение мэрии за 2 влияния: «Снегопад» в этот ход ему не мешает."));
                Assert.AreEqual("Mira: +2 influence (contract North — Center completed).",
                    LocalizationService.TranslateProgressJournal(income, place));
                Assert.AreEqual("Mira: +1 influence (catch-up support).",
                    LocalizationService.TranslateProgressJournal("Mira: +1 влияния (поддержка отстающих)."));
                Assert.AreEqual("Final City Hall order: build North — Center during the final round for +5 pts.",
                    LocalizationService.TranslateProgressJournal("Финальный заказ мэрии: дорога Север — Центр. Кто построит её в последнем круге, получит +5 очков.", place));
                Assert.AreEqual("Mira completes the final City Hall order: +5 pts.",
                    LocalizationService.TranslateProgressJournal("Mira выполняет финальный заказ мэрии: +5 очков."));
                Assert.AreEqual("Final audit: 2 bribes exposed (Mira −4).",
                    LocalizationService.TranslateProgressJournal("Проверка перед итогами: нашли взяток — 2 (Mira −4)."));
                Assert.AreEqual("Final audit: no bribes were found.",
                    LocalizationService.TranslateProgressJournal("Проверка перед итогами: взяток не нашли."));
                Assert.IsNull(LocalizationService.TranslateProgressJournal("Unknown action"));
                Assert.IsNull(LocalizationService.TranslateProgressJournal("Mira: +2 влияния (неизвестная причина)."));
                LocalizationService.SetLanguage("ru");
                Assert.IsNull(LocalizationService.TranslateProgressJournal(jam, place));
                LocalizationService.SetLanguage("en");
                Assert.IsNotNull(LocalizationService.TranslateProgressJournal(jam, place));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [Test]
        public void LocalizationCatalogs_HaveMatchingUniqueCompleteKeys()
        {
            var russianAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("CityHighways/Localization/ru");
            var englishAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("CityHighways/Localization/en");
            Assert.IsNotNull(russianAsset, "Russian localization asset is missing.");
            Assert.IsNotNull(englishAsset, "English localization asset is missing.");

            var russian = UnityEngine.JsonUtility.FromJson<LocalizationAuditCatalog>(russianAsset.text);
            var english = UnityEngine.JsonUtility.FromJson<LocalizationAuditCatalog>(englishAsset.text);
            Assert.IsNotNull(russian);
            Assert.IsNotNull(english);
            Assert.IsNotNull(russian.entries);
            Assert.IsNotNull(english.entries);

            var russianKeys = new HashSet<string>();
            foreach (var item in russian.entries)
            {
                Assert.IsNotNull(item);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.key), "Empty Russian key.");
                Assert.IsTrue(russianKeys.Add(item.key), "Duplicate Russian key: " + item.key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.value), "Empty Russian value: " + item.key);
            }

            var englishKeys = new HashSet<string>();
            foreach (var item in english.entries)
            {
                Assert.IsNotNull(item);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.key), "Empty English key.");
                Assert.IsTrue(englishKeys.Add(item.key), "Duplicate English key: " + item.key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.value), "Empty English value: " + item.key);
                Assert.IsFalse(Regex.IsMatch(item.value, "[а-яё]", RegexOptions.IgnoreCase),
                    "Untranslated Cyrillic characters in English localization: " + item.key);
            }

            CollectionAssert.AreEquivalent(russianKeys, englishKeys,
                "Both languages must contain exactly the same localization keys.");
        }

        [Test]
        public void AuctionTradeAndBribeJournal_RetranslateWithoutChangingNamesOrBids()
        {
            var original = LocalizationService.Language;
            const string auction = "Аукцион выигрывает Vera за 3 влияния (ставки: Vera 3, Даша 2).";
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Vera places a secret bid.",
                    LocalizationService.TranslateSpecialJournal("Vera делает тайную ставку."));
                Assert.AreEqual("Auction winner: Vera for 3 influence (bids: Vera 3, Даша 2).",
                    LocalizationService.TranslateSpecialJournal(auction));
                Assert.AreEqual("No auction bids were made; the contract returns to the bottom of the deck (bids: Vera 0).",
                    LocalizationService.TranslateSpecialJournal("На аукционе никто не поставил — контракт уходит под колоду (ставки: Vera 0)."));
                Assert.AreEqual("There were 2 bribes last round. Their authors remain unknown and can be exposed before this round ends.",
                    LocalizationService.TranslateSpecialJournal("В прошлом круге в мэрии шептались: взяток — 2. Кто — неизвестно; раскрыть можно до конца этого круга."));
                Assert.AreEqual("Аня accepts the trade.",
                    LocalizationService.TranslateSpecialJournal("Аня соглашается на сделку."));
                Assert.AreEqual("Аня rejects the trade.",
                    LocalizationService.TranslateSpecialJournal("Аня отказывается от сделки."));
                Assert.AreEqual("Аня now travels free on roads owned by Vera.",
                    LocalizationService.TranslateSpecialJournal("Аня теперь ездит по дорогам Vera бесплатно."));
                Assert.AreEqual("Vera lacks the required resources; the trade is canceled.",
                    LocalizationService.TranslateSpecialJournal("У Vera не нашлось нужного — сделки нет."));
                Assert.IsNull(LocalizationService.TranslateSpecialJournal("Unknown journal entry."));
                LocalizationService.SetLanguage("ru");
                Assert.IsNull(LocalizationService.TranslateSpecialJournal(auction));
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Auction winner: Vera for 3 influence (bids: Vera 3, Даша 2).",
                    LocalizationService.TranslateSpecialJournal(auction));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void ExistingGameErrors_RetranslateWhenLanguageChanges()
        {
            const string storedError = "Нужно 3 карт(ы) «сталь» (техника подходит вместо любой карты).";
            const string roadBlocked = "Дорогу уже построил Sasha.";
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual(storedError, LocalizationService.TranslateGameError(storedError));
                Assert.AreEqual(roadBlocked, LocalizationService.TranslateGameError(roadBlocked));

                LocalizationService.SetLanguage("en");
                Assert.AreEqual("You need 3 steel cards (machinery can substitute for any card).",
                    LocalizationService.TranslateGameError(storedError));
                Assert.AreEqual("This road was already built by Sasha.",
                    LocalizationService.TranslateGameError(roadBlocked));

                LocalizationService.SetLanguage("ru");
                Assert.AreEqual(storedError, LocalizationService.TranslateGameError(storedError));
                Assert.AreEqual(roadBlocked, LocalizationService.TranslateGameError(roadBlocked));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

        [Test]
        public void BuildAuctionAndSpecialActionBlockers_AreTranslated()
        {
            var original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("This road is already yours.",
                    LocalizationService.TranslateGameError("Эта дорога уже ваша."));
                Assert.AreEqual("This road was already built by Elena.",
                    LocalizationService.TranslateGameError("Дорогу уже построил Elena."));
                Assert.AreEqual("Roadworks: this road is closed until the end of the round.",
                    LocalizationService.TranslateGameError("Ремонт: участок закрыт до конца круга."));
                Assert.AreEqual("Not enough road pieces.",
                    LocalizationService.TranslateGameError("Не хватает участков дороги."));
                Assert.AreEqual("Free construction is limited to roads of length 4 or less.",
                    LocalizationService.TranslateGameError("Бесплатно — только дороги длиной до 4."));
                Assert.AreEqual("Contract 12 is not among those offered.",
                    LocalizationService.TranslateGameError("Контракта 12 нет среди предложенных."));
                Assert.AreEqual("Contract 12 is listed twice.",
                    LocalizationService.TranslateGameError("Контракт 12 указан дважды."));
                Assert.AreEqual("You can only upgrade your own roads.",
                    LocalizationService.TranslateGameError("Улучшать можно только свои дороги."));
                Assert.AreEqual("You can make an accusation only at the start of your turn.",
                    LocalizationService.TranslateGameError("Обвинить можно в начале своего хода."));
                Assert.AreEqual("You only have 3 influence.",
                    LocalizationService.TranslateGameError("У вас только 3 влияния."));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual("Эта дорога уже ваша.",
                    LocalizationService.TranslateGameError("Эта дорога уже ваша."));
                Assert.AreEqual("Бесплатно — только дороги длиной до 4.",
                    LocalizationService.TranslateGameError("Бесплатно — только дороги длиной до 4."));
            }
            finally
            {
                LocalizationService.SetLanguage(original);
            }
        }

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
