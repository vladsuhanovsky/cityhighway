using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class BotPlannerTests
    {
        static Game Fresh() => TestCities.Started(TestCities.Small());

        [Test]
        public void Planner_UsesForeignBridge_WhenCheaperThanDetour()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r3"); // чужой мост Центр — Юг

            var path = new BotPlanner(game, 0, 2.5f).ShortestPath(game.Map.GetContract("k1"), null);

            CollectionAssert.AreEquivalent(new[] { "r2", "r3" }, path.Select(r => r.Id).ToArray(),
                "Построить 1 участок и заплатить за мост дешевле, чем объезд в 6 участков");
        }

        [Test]
        public void Planner_BuildsDetour_WhenTollIsExpensive()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r3");

            var path = new BotPlanner(game, 0, 10f).ShortestPath(game.Map.GetContract("k1"), null);

            Assert.IsFalse(path.Any(r => r.Id == "r3"), "При дорогом проезде бот строит объезд");
            Assert.AreEqual(6, path.Sum(r => r.Length));
        }

        [Test]
        public void Planner_PathAlwaysHasOwnOrNewRoad()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r1");
            game.Debug_GiveRoad(1, "r3"); // путь n1-c1-s1 целиком чужой — так контракт не засчитают

            var path = new BotPlanner(game, 0, 2.5f).ShortestPath(game.Map.GetContract("k1"), null);

            Assert.IsTrue(path.Any(r => game.GetRoadOwner(r.Id) != 1), "В пути должна быть своя (или будущая своя) дорога");
            Assert.LessOrEqual(path.Count(r => game.GetRoadOwner(r.Id) == 1), game.Config.MaxForeignRoadsPerContract);
        }

        [Test]
        public void Plan_CountsRoadsToBuildAndTolls()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r3");

            var plan = new BotPlanner(game, 0, 2.5f).BuildPlan(new[] { game.Map.GetContract("k1") });

            Assert.AreEqual(1, plan.PiecesNeeded);
            Assert.AreEqual(game.Config.TollPerRoad, plan.TollsExpected);
            Assert.AreEqual("r2", plan.RoadsToBuild.Single().Id);
        }
    }

    public class SmartBotTests
    {
        [Test]
        public void SmartBots_PlayLegalGamesToTheEnd([Values(2, 3, 4, 5)] int players)
        {
            var map = TestCities.Moscow();
            int completed = 0, total = 0, tolls = 0;
            for (int seed = 0; seed < 15; seed++)
            {
                var game = new Game(map, Enumerable.Range(1, players).Select(i => "Бот " + i).ToList(), null, seed);
                var bots = Enumerable.Range(0, players).Select(i => (IBot)new SmartBot(seed * 10 + i)).ToArray();

                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bots[game.ActingPlayerIndex].Act(game);
                    Assert.IsTrue(result.Success, $"seed {seed}, шаг {steps}: {result.Error}");
                    Assert.AreEqual(game.Config.TotalCards, game.Debug_CountAllCards());
                    Assert.Less(++steps, 5000, $"seed {seed}: партия зависла");
                }
                completed += game.Result.Ranking.Sum(s => s.CompletedContracts);
                total += game.Result.Ranking.Sum(s => s.CompletedContracts + s.FailedContracts);
                tolls += game.Result.Ranking.Sum(s => s.TollsPaid);
            }
            Debug.Log($"{players} умных бота: выполнено контрактов {completed} из {total}, плата за проезд {tolls}.");
            Assert.GreaterOrEqual(completed, total * 8 / 10, "Умные боты должны выполнять большинство контрактов");
        }

        [Test]
        public void SmartBot_BeatsRandomBot()
        {
            var map = TestCities.Moscow();
            const int games = 40;
            int wins = 0, completed = 0, total = 0;

            for (int seed = 0; seed < games; seed++)
            {
                int smartSeat = seed % 2;
                var bots = new IBot[2];
                bots[smartSeat] = new SmartBot(seed);
                bots[1 - smartSeat] = new RandomBot(seed);

                var game = new Game(map, new[] { "Игрок 1", "Игрок 2" }, null, seed);
                int steps = 0;
                while (game.Phase != GamePhase.GameOver && steps++ < 5000)
                    Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);

                var smart = game.Result.Ranking.First(s => s.PlayerIndex == smartSeat);
                var random = game.Result.Ranking.First(s => s.PlayerIndex != smartSeat);
                if (smart.Total > random.Total) wins++;
                completed += smart.CompletedContracts;
                total += smart.CompletedContracts + smart.FailedContracts;
            }

            Debug.Log($"Умный бот выиграл {wins} из {games}, выполнил контрактов {completed} из {total}.");
            Assert.GreaterOrEqual(wins, games * 85 / 100);
        }
    }
}
