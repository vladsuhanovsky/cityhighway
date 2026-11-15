using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>Обучающая партия: карта центра, упрощённые правила, партия доигрывается.</summary>
    public class TutorialTests
    {
        [Test]
        public void TutorialMap_IsValid_AndConnected()
        {
            var map = TestCities.Tutorial();
            Assert.IsEmpty(map.Validate(new GameConfig().MaxRoadLength));
            Assert.AreEqual(5, map.Districts.Count);
            Assert.AreEqual(10, map.Contracts.Count);
            Assert.IsTrue(map.Roads.Any(r => r.IsBridge), "Есть мосты через реку");

            var seen = new System.Collections.Generic.HashSet<string> { map.Locations[0].Id };
            var queue = new System.Collections.Generic.Queue<string>(seen);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var road in map.RoadsAt(at))
                {
                    var other = road.A == at ? road.B : road.A;
                    if (seen.Add(other)) queue.Enqueue(other);
                }
            }
            Assert.AreEqual(map.Locations.Count, seen.Count, "Из любого места можно доехать в любое");
        }

        [Test]
        public void TutorialSetup_SimpleRules_NoSaving()
        {
            var setup = GameSetup.CreateTutorial("Аня", true);
            Assert.IsTrue(setup.tutorial);
            Assert.AreEqual(2, setup.players.Count);
            Assert.IsFalse(setup.players[0].isBot);
            Assert.IsTrue(setup.players[1].isBot);
            Assert.AreEqual(GameSetup.TutorialMap, setup.mapResourcePath);
            var json = UnityEngine.JsonUtility.ToJson(setup);
            StringAssert.DoesNotContain("\"tutorial\":", json, "Флаг обучения не попадает в сохранение");

            var config = GameSetup.TutorialConfig();
            Assert.IsFalse(config.EventsEnabled || config.LuckEnabled || config.AuctionEnabled || config.BribesEnabled || config.TradesEnabled);
            Assert.IsTrue(config.InfluenceEnabled, "Влияние объясняется в обучении");
        }

        [Test]
        public void TutorialGame_BotsFinish()
        {
            var game = new Game(TestCities.Tutorial(), new[] { "Вы", "Наставник" }, GameSetup.TutorialConfig(), 2026,
                new[] { CharacterKind.None, CharacterKind.None });
            Assert.AreEqual(GameSetup.TutorialPieces, game.Players[0].RoadPieces);
            var bots = new IBot[] { new SmartBot(1), new SmartBot(2) };
            int steps = 0;
            for (; steps < 3000 && game.Phase != GamePhase.GameOver; steps++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.Less(game.TurnNumber, 80, "Короткая партия");
            Assert.IsTrue(game.Players.Any(p => p.PaidRouteList.Count > 0), "Контракты выполняются");
        }
    }
}
