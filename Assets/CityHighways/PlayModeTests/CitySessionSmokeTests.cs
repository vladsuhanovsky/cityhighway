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
