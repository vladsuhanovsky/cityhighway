using System.IO;
using System.Linq;
using NUnit.Framework;
using RailGame.Core;
using UnityEngine;

namespace RailGame.Tests
{
    public class BotPlannerTests
    {
        static Game StartGame()
        {
            var game = new Game(TestMaps.Small(), new[] { "Аня", "Борис" }, seed: 42);
            foreach (var player in game.Players)
                game.KeepInitialTickets(player.Index, player.PendingTickets.Take(2).Select(t => t.Id).ToList());
            return game;
        }

        [Test]
        public void ShortestPath_FindsCheapestRoute()
        {
            var game = StartGame();
            var planner = new BotPlanner(game, 0);

            var path = planner.ShortestPath("A", "D", null, out int cost);

            Assert.IsNotNull(path);
            Assert.AreEqual(6, cost); // A-B-C-D или A-C-D
        }

        [Test]
        public void ShortestPath_AvoidsRoutesTakenByOpponent()
        {
            var game = StartGame();
            game.Debug_SetHand(0, CardColor.Red);
            Assert.IsTrue(game.ClaimRoute("ab", CardColor.Red, 0).Success);       // Аня
            game.Debug_SetHand(1, CardColor.Blue, CardColor.Blue, CardColor.Blue);
            Assert.IsTrue(game.ClaimRoute("cd", CardColor.Blue, 0).Success);      // Борис; cd2 теперь закрыт (2 игрока)

            var planner = new BotPlanner(game, 0);
            planner.ShortestPath("A", "D", null, out int cost);

            // Свой A-B бесплатен, C-D перекрыт: дальше всего дешевле B-E-D = 2 + 4.
            Assert.AreEqual(6, cost);
        }

        [Test]
        public void Plan_SharesRoutesBetweenTickets()
        {
            var game = StartGame();
            var planner = new BotPlanner(game, 0);
            var ad = new Ticket("x1", "A", "D", 6);
            var bd = new Ticket("x2", "B", "D", 5);

            int alone = planner.BuildPlan(new[] { ad }).TrainsNeeded;
            int together = planner.BuildPlan(new[] { ad, bd }).TrainsNeeded;

            Assert.AreEqual(6, alone);
            // Отдельно B-D стоит 5 вагонов; в общей сети — не больше 2 сверху.
            Assert.LessOrEqual(together, alone + 2, "Второе задание должно почти целиком лечь на ту же сеть");
        }
    }

    public class SmartBotTests
    {
        static MapDefinition DemoMap()
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Maps/demo_map.json"));
            return JsonUtility.FromJson<MapDto>(json).ToDefinition();
        }

        [Test]
        public void SmartBots_PlayLegalGamesToTheEnd([Values(2, 3, 4, 5)] int players)
        {
            var map = DemoMap();
            for (int seed = 0; seed < 15; seed++)
            {
                var names = Enumerable.Range(1, players).Select(i => "Бот " + i).ToList();
                var config = new GameConfig { TrainsPerPlayer = 30 };
                var game = new Game(map, names, config, seed);
                var bots = Enumerable.Range(0, players).Select(i => (IBot)new SmartBot(seed * 10 + i)).ToArray();

                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bots[game.ActingPlayerIndex].Act(game);
                    Assert.IsTrue(result.Success, $"seed {seed}, шаг {steps}: {result.Error}");
                    Assert.AreEqual(config.TotalCards, game.Debug_CountAllCards());
                    Assert.Less(++steps, 5000, $"seed {seed}: партия зависла");
                }
            }
        }

        [Test]
        public void SmartBot_NoCardsInEndgame_TakesTicketsInsteadOfIllegalPass()
        {
            var game = new Game(TestMaps.Small(), new[] { "Аня", "Борис" }, seed: 7);
            foreach (var player in game.Players)
                game.KeepInitialTickets(player.Index, player.PendingTickets.Take(2).Select(t => t.Id).ToList());

            game.Debug_EmptyCardSupply();   // карт взять неоткуда
            game.Debug_SetHand(0);          // на руке пусто — ничего не оплатить
            game.Debug_SetTrains(0, 3);     // конец партии близко
            Assert.Greater(game.TicketDeckCount, 0, "В колоде заданий что-то есть, значит пас запрещён");

            var result = new SmartBot(1).Act(game);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(GamePhase.ChoosingTickets, game.Phase);
            Assert.IsTrue(new SmartBot(1).Act(game).Success, "Выбор заданий тоже должен пройти");
        }

        [Test]
        public void SmartBot_DecideDoesNotChangeTheGame()
        {
            var map = DemoMap();
            var game = new Game(map, new[] { "Аня", "Борис" }, seed: 3);
            var bot = new SmartBot(5);

            for (int step = 0; step < 40 && game.Phase != GamePhase.GameOver; step++)
            {
                var phase = game.Phase;
                int acting = game.ActingPlayerIndex;
                int hand = game.Players[acting].HandSize;

                var move = bot.Decide(game);
                Assert.IsNotNull(move);
                Assert.IsFalse(string.IsNullOrEmpty(move.Describe(game)));
                Assert.AreEqual(phase, game.Phase, "Decide не должен делать ход");
                Assert.AreEqual(acting, game.ActingPlayerIndex);
                Assert.AreEqual(hand, game.Players[acting].HandSize);

                Assert.IsTrue(move.Apply(game).Success);
            }
        }

        [Test]
        public void SmartBot_BeatsRandomBot()
        {
            var map = DemoMap();
            const int games = 40;
            int smartWins = 0;
            int ticketsCompleted = 0, ticketsTotal = 0;

            for (int seed = 0; seed < games; seed++)
            {
                // Меняем бота местами, чтобы не влияло, кто ходит первым.
                int smartSeat = seed % 2;
                var bots = new IBot[2];
                bots[smartSeat] = new SmartBot(seed);
                bots[1 - smartSeat] = new RandomBot(seed);

                var game = new Game(map, new[] { "Игрок 1", "Игрок 2" }, new GameConfig { TrainsPerPlayer = 30 }, seed);
                int steps = 0;
                while (game.Phase != GamePhase.GameOver && steps++ < 5000)
                    Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);

                var smart = game.Result.Ranking.First(s => s.PlayerIndex == smartSeat);
                var random = game.Result.Ranking.First(s => s.PlayerIndex != smartSeat);
                if (smart.Total > random.Total) smartWins++;
                ticketsCompleted += smart.CompletedTickets;
                ticketsTotal += smart.CompletedTickets + smart.FailedTickets;
            }

            Debug.Log($"Умный бот выиграл {smartWins} из {games}, выполнил заданий {ticketsCompleted} из {ticketsTotal}.");
            Assert.GreaterOrEqual(smartWins, games * 3 / 4);
            Assert.GreaterOrEqual(ticketsCompleted, ticketsTotal / 2, "Умный бот должен выполнять большинство заданий");
        }
    }
}
