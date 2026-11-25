using System.Collections;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CityHighways.PlayModeTests
{
    public class CitySessionSmokeTests
    {
        [UnityTest]
        public IEnumerator ClassicTurnSequenceSurvivesFramesAndSerialization()
        {
            var map = CityHighwaysApp.LoadMap("CityHighways/Maps/paris");
            var game = new Game(map, new[] { "A", "B" }, new GameConfig(), 42);
            var bot = new RandomBot(17);
            for (int i = 0; i < 12 && game.Phase != GamePhase.GameOver; i++)
            {
                var result = bot.Act(game);
                Assert.IsTrue(result.Success, result.Error);
                yield return null;
            }
            Assert.AreNotEqual(GamePhase.GameOver, game.Phase);
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            Assert.IsTrue(game.SameGameState(Game.Load(map, data)));
            Assert.IsTrue(game.SameGameState(Game.Replay(map, data.start, data.history)));
        }

        [UnityTest]
        public IEnumerator CoopCheckpointCanReplaceAnAttachedMapAcrossFrames()
        {
            var setup = GameSetup.CreateCoopPractice(null, "Tester");
            var map = CityHighwaysApp.LoadMap(setup.mapResourcePath);
            var game = new Game(map, setup.PlayerNames(), setup.CreateConfig(), 2026);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.themeStyleSheet = theme;
            var owner = new GameObject("CityHighways co-op smoke UI");
            owner.SetActive(false);
            var doc = owner.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            owner.SetActive(true);
            try
            {
                for (int i = 0; i < 5 && doc.rootVisualElement == null; i++) yield return null;
                Assert.IsNotNull(doc.rootVisualElement);
                var view = new MapView();
                doc.rootVisualElement.Add(view);
                view.SetGame(game);
                yield return null;
                Assert.IsNotNull(view.panel);
                var bot = new RandomBot(17);
                for (int i = 0; i < 12 && game.Phase != GamePhase.GameOver; i++)
                {
                    var result = bot.Act(game);
                    Assert.IsTrue(result.Success, result.Error);
                    yield return null;
                }
                Assert.AreNotEqual(GamePhase.GameOver, game.Phase);
                var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
                var resumed = Game.Load(map, snapshot);
                Assert.IsTrue(resumed.IsCooperative);
                Assert.IsTrue(game.SameGameState(resumed));
                Assert.AreEqual(game.Crisis, resumed.Crisis);
                view.SetGame(resumed);
                yield return null;
                Assert.IsNotNull(view.panel);
                Assert.IsEmpty(view.PathRoads);
                Assert.IsNull(view.SelectedRoadId);
                view.RemoveFromHierarchy();
                Assert.IsNull(view.panel);
            }
            finally
            {
                Object.Destroy(owner);
                Object.Destroy(settings);
                Object.Destroy(theme);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClockOnlyRenderRetainsHudControlsAcrossFrames()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.themeStyleSheet = theme;
            var owner = new GameObject("CityHighways clock smoke UI");
            owner.SetActive(false);
            var doc = owner.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            owner.SetActive(true);
            try
            {
                for (int i = 0; i < 5 && doc.rootVisualElement == null; i++) yield return null;
                Assert.IsNotNull(doc.rootVisualElement);
                var hud = new VisualElement();
                var label = new Label();
                var action = new Button { text = "Build", name = "build" };
                hud.Add(label);
                hud.Add(action);
                doc.rootVisualElement.Add(hud);
                HudTurnClock.Render(label, "Build", true, 11f, true);
                yield return null;
                Assert.IsNotNull(label.panel);
                Assert.AreEqual("Build  ·  remaining 0:11", label.text);
                HudTurnClock.Render(label, "Build", true, 10f, true);
                yield return null;
                Assert.AreEqual("Build  ·  remaining 0:10", label.text);
                Assert.AreEqual(Theme.Danger, label.style.color.value);
                Assert.AreSame(action, hud.Q<Button>("build"));
                Assert.AreEqual(2, hud.childCount);
                HudTurnClock.Render(label, "Ход", false, 0f, false);
                yield return null;
                Assert.AreEqual("Ход", label.text);
                Assert.AreEqual(Theme.Text, label.style.color.value);
                Assert.AreSame(action, hud.Q<Button>("build"));
            }
            finally
            {
                Object.Destroy(owner);
                Object.Destroy(settings);
                Object.Destroy(theme);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapViewReplacesCityAndClearsTransientState()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.themeStyleSheet = theme;
            var owner = new GameObject("CityHighways smoke UI");
            owner.SetActive(false);
            var doc = owner.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            owner.SetActive(true);
            try
            {
                for (int i = 0; i < 5 && doc.rootVisualElement == null; i++) yield return null;
                Assert.IsNotNull(doc.rootVisualElement);
                var view = new MapView();
                doc.rootVisualElement.Add(view);
                var first = CityHighwaysApp.LoadMap("CityHighways/Maps/paris");
                var second = CityHighwaysApp.LoadMap("CityHighways/Maps/berlin");
                view.SetGame(new Game(first, new[] { "A", "B" }, new GameConfig(), 42));
                yield return null;
                Assert.IsNotNull(view.panel);
                string road = first.Roads[0].Id;
                view.SelectedRoadId = road;
                view.Affordable.Add(road);
                view.BuildProgress[road] = 0.5f;
                view.PathRoads[road] = Color.red;
                view.SetGame(new Game(second, new[] { "A", "B" }, new GameConfig(), 42));
                yield return null;
                Assert.IsNull(view.SelectedRoadId);
                Assert.IsEmpty(view.Affordable);
                Assert.IsEmpty(view.BuildProgress);
                Assert.IsEmpty(view.PathRoads);
                view.RemoveFromHierarchy();
                Assert.IsNull(view.panel);
            }
            finally
            {
                Object.Destroy(owner);
                Object.Destroy(settings);
                Object.Destroy(theme);
            }
            yield return null;
        }
    }
}
