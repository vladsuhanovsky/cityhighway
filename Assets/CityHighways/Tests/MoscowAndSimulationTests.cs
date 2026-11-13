using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class MoscowMapTests
    {
        [Test]
        public void Moscow_IsValid()
        {
            var map = TestCities.Moscow();
            var errors = map.Validate(new GameConfig().MaxRoadLength);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(17, map.Districts.Count);
            Assert.AreEqual(3, map.Districts.Count(d => d.IsExpansion), "Зеленоград, Некрасовка, Троицк");
            Assert.AreEqual(66, map.Locations.Count);
            Assert.GreaterOrEqual(map.Contracts.Count, 30);
            Assert.Greater(map.River.Count, 5, "Москва-река нужна для отрисовки");
        }

        [Test]
        public void Moscow_AllLocationsConnected()
        {
            var map = TestCities.Moscow();
            var first = map.Locations[0].Id;
            var seen = new HashSet<string> { first };
            var stack = new Stack<string>();
            stack.Push(first);
            while (stack.Count > 0)
            {
                var location = stack.Pop();
                foreach (var road in map.RoadsAt(location))
                {
                    var next = road.OtherEnd(location);
                    if (seen.Add(next)) stack.Push(next);
                }
            }
            Assert.AreEqual(map.Locations.Count, seen.Count);
        }

        [Test]
        public void Moscow_BridgesAreSteel()
        {
            var map = TestCities.Moscow();
            var bridges = map.Roads.Where(r => r.IsBridge).ToList();
            Assert.GreaterOrEqual(bridges.Count, 10, "Через Москву-реку должно быть много мостов");
        }

        [Test]
        public void Moscow_DeckMatchesDemand()
        {
            // Карт каждого ресурса должно хватать хотя бы на четверть дорог этого типа (за партию строится меньше половины карты).
            var map = TestCities.Moscow();
            var config = new GameConfig();
            for (int r = 0; r < ResourceInfo.Building.Length; r++)
            {
                var resource = ResourceInfo.Building[r];
                // Считаем дороги, открытые с начала партии (районы-расширения открываются не сразу).
                int demand = map.Roads.Where(x => x.Resource == resource
                                                  && !map.GetDistrict(map.DistrictOf(x.A)).IsExpansion
                                                  && !map.GetDistrict(map.DistrictOf(x.B)).IsExpansion).Sum(x => x.Length);
                Assert.GreaterOrEqual(config.CardsPerResource[r] * 4, demand, ResourceInfo.RussianName(resource));
            }
        }
    }

    /// <summary>Сотни случайных партий: не зависают, не теряют карты, корректно заканчиваются.</summary>
    public class SimulationTests
    {
        [Test]
        public void RandomGamesOnMoscow_AlwaysFinish([Values(2, 3, 4, 5)] int players)
        {
            var map = TestCities.Moscow();
            int completedTotal = 0, contractsTotal = 0, tolls = 0;

            for (int seed = 0; seed < 30; seed++)
            {
                var names = Enumerable.Range(1, players).Select(i => "Бот " + i).ToList();
                var game = new Game(map, names, null, seed);
                var bot = new RandomBot(seed);

                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bot.Act(game);
                    Assert.IsTrue(result.Success, $"seed {seed}, шаг {steps}: {result.Error}");
                    Assert.AreEqual(game.Config.TotalCards, game.Debug_CountAllCards(), $"seed {seed}: карты потерялись");
                    Assert.Less(++steps, 5000, $"seed {seed}: партия зависла");
                }

                Assert.AreEqual(players, game.Result.Ranking.Count);
                Assert.IsNotEmpty(game.Result.Winners);
                // Плата за проезд только перекладывает очки: сколько заплатили, столько и получили.
                Assert.AreEqual(game.Result.Ranking.Sum(s => s.TollsPaid), game.Result.Ranking.Sum(s => s.TollsEarned));

                completedTotal += game.Result.Ranking.Sum(s => s.CompletedContracts);
                contractsTotal += game.Result.Ranking.Sum(s => s.CompletedContracts + s.FailedContracts);
                tolls += game.Result.Ranking.Sum(s => s.TollsPaid);
            }
            Debug.Log($"{players} игрока(ов): выполнено контрактов {completedTotal} из {contractsTotal}, плата за проезд всего {tolls}.");
        }
    }

    public class SaveTests
    {
        [Test]
        public void SaveAndLoad_RestoresSameState([Values(0, 10, 60)] int steps)
        {
            var map = TestCities.Moscow();
            var game = new Game(map, new[] { "Аня", "Борис", "Вера" }, null, 5);
            var bot = new RandomBot(5);
            for (int i = 0; i < steps && game.Phase != GamePhase.GameOver; i++)
                Assert.IsTrue(bot.Act(game).Success);
            if (game.Phase == GamePhase.GameOver) Assert.Ignore("Партия закончилась раньше");

            string json = JsonUtility.ToJson(game.CreateSave());
            var restored = Game.FromSave(map, JsonUtility.FromJson<GameSaveData>(json));

            Assert.AreEqual(json, JsonUtility.ToJson(restored.CreateSave()));
            Assert.AreEqual(restored.Config.TotalCards, restored.Debug_CountAllCards());

            int guard = 0;
            while (restored.Phase != GamePhase.GameOver)
            {
                Assert.IsTrue(bot.Act(restored).Success);
                Assert.Less(++guard, 5000);
            }
        }

        [Test]
        public void Load_WithUnknownRoad_Throws()
        {
            var map = TestCities.Moscow();
            var save = new Game(map, new[] { "Аня", "Борис" }, null, 1).CreateSave();
            save.players[0].roads = new[] { "нет-такой" };
            Assert.Throws<System.FormatException>(() => Game.FromSave(map, save));
        }
    }
}
