using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Новые карты (Амстердам, Чунцин): корректны, рисуются, на них доигрываются партии.</summary>
    public class NewCitiesTests
    {
        static CityMap Load(string file)
        {
            var path = Path.Combine(Application.dataPath, "CityHighways/Resources/CityHighways/Maps/" + file + ".json");
            return JsonUtility.FromJson<CityMapDto>(File.ReadAllText(path)).ToMap();
        }

        public static CityMap Amsterdam() => Load("amsterdam");
        public static CityMap Chongqing() => Load("chongqing");

        [Test]
        public void Amsterdam_IsValid()
        {
            var map = Amsterdam();
            var errors = map.Validate(new GameConfig().MaxRoadLength);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual("Амстердам", map.Name);
            Assert.AreEqual(3, map.Rivers.Count, "Эй, Амстел и Нордхолландский канал");
            Assert.IsNull(map.Ring, "кольцевая A10 убрана с карты");
            Assert.Less(map.PiecesScale, 1f);
            Assert.AreEqual(3, map.Districts.Count(d => d.IsExpansion));
            Assert.GreaterOrEqual(map.Roads.Count(r => r.IsBridge), 8);
            Assert.IsNotNull(map.GetDistrict("center"), "для «Парада»");
            Assert.IsNotNull(map.GetDistrict("airports"), "для значка аэропорта");
        }

        [Test]
        public void Chongqing_IsValid()
        {
            var map = Chongqing();
            var errors = map.Validate(new GameConfig().MaxRoadLength);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual("Чунцин", map.Name);
            Assert.AreEqual(2, map.Rivers.Count, "Янцзы и Цзялинцзян");
            Assert.IsNull(map.Ring);
            Assert.GreaterOrEqual(map.Roads.Count(r => r.IsBridge), 8, "город мостов");
            Assert.IsNotNull(map.GetDistrict("center"));
            Assert.IsNotNull(map.GetDistrict("airports"));
        }

        [Test]
        public void Moscow_KeepsOneRiverAndMkad()
        {
            var map = TestCities.Moscow();
            Assert.AreEqual(1, map.Rivers.Count);
            Assert.AreEqual("МКАД", map.Ring?.Name);
            Assert.AreEqual(1f, map.PiecesScale);
        }

        [Test]
        public void CompactMaps_GiveFewerPieces()
        {
            var moscow = new Game(TestCities.Moscow(), new[] { "А", "Б", "В" }, null, 1);
            var amsterdam = new Game(Amsterdam(), new[] { "А", "Б", "В" }, null, 1);
            Assert.Less(amsterdam.Players[0].RoadPieces, moscow.Players[0].RoadPieces);
            // Заданное вручную число участков не масштабируется.
            var fixedPieces = new Game(Chongqing(), new[] { "А", "Б" }, new GameConfig { RoadPiecesOverride = 30 }, 1);
            Assert.AreEqual(30, fixedPieces.Players[0].RoadPieces);
        }

        [Test]
        public void RandomGamesOnNewCities_AlwaysFinish([Values("amsterdam", "chongqing")] string city, [Values(2, 3, 5)] int players)
        {
            var map = Load(city);
            for (int seed = 0; seed < 8; seed++)
            {
                var names = Enumerable.Range(1, players).Select(i => "Бот " + i).ToList();
                var game = new Game(map, names, null, seed);
                var bot = new RandomBot(seed);
                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bot.Act(game);
                    Assert.IsTrue(result.Success, $"{city} seed {seed}, шаг {steps}: {result.Error}");
                    Assert.Less(++steps, 5000, $"{city} seed {seed}: партия зависла");
                }
                Assert.IsNotEmpty(game.Result.Winners);
            }
        }
    }

    /// <summary>Кооператив «Город против всех».</summary>
    public class CooperativeTests
    {
        static GameConfig Coop() => new GameConfig().MakeCooperative();

        [Test]
        public void Coop_StartsWithOrdersAndCrisis()
        {
            var game = TestCities.Started(TestCities.Moscow(), 3, Coop());
            Assert.IsTrue(game.IsCooperative);
            Assert.AreEqual(game.Config.CoopOpenOrders, game.CityOrders.Count);
            Assert.AreEqual(game.Config.CoopCrisisStart, game.Crisis);
            Assert.AreEqual(game.Config.CoopOrdersToWin(3), game.OrdersTarget);
            Assert.IsFalse(game.Config.BribesEnabled);
            Assert.IsFalse(game.Config.AuctionEnabled);
            // Заказы — не те контракты, что уже на руках.
            var inHands = new HashSet<string>(game.Players.SelectMany(p => p.Contracts).Select(c => c.Id));
            Assert.IsFalse(game.CityOrders.Any(o => inHands.Contains(o.Contract.Id)));
        }

        [Test]
        public void Coop_CrisisAtLimit_LosesForEveryone()
        {
            var game = TestCities.Started(TestCities.Moscow(), 2, Coop());
            game.Debug_SetCrisis(8);
            game.Debug_SetOrderRounds(1); // в начале следующего круга все заказы просрочатся
            var bot = new RandomBot(3);
            int guard = 0;
            while (game.Phase != GamePhase.GameOver && guard++ < 200) Assert.IsTrue(bot.Act(game).Success);

            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.IsTrue(game.Result.Cooperative);
            Assert.IsFalse(game.Result.TeamWon);
            Assert.IsEmpty(game.Result.Winners);
            Assert.GreaterOrEqual(game.Crisis, game.CrisisMax);
            StringAssert.Contains("кризис", game.CoopOutcome);
        }

        [Test]
        public void Coop_SmartBotTeams_FinishWithoutTolls([Values(2, 3, 4)] int players)
        {
            int won = 0;
            for (int seed = 0; seed < 4; seed++)
            {
                var names = Enumerable.Range(1, players).Select(i => "Бот " + i).ToList();
                var game = new Game(TestCities.Moscow(), names, Coop(), seed);
                var bot = new SmartBot(seed);
                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bot.Act(game);
                    Assert.IsTrue(result.Success, $"seed {seed}, шаг {steps}: {result.Error}");
                    Assert.Less(++steps, 6000, $"seed {seed}: партия зависла");
                }
                Assert.IsTrue(game.Result.Cooperative);
                // Победили все или никто.
                Assert.IsTrue(game.Result.Winners.Count == 0 || game.Result.Winners.Count == players);
                Assert.AreEqual(0, game.Result.Ranking.Sum(s => s.TollsPaid), "союзники ездят бесплатно");
                Assert.IsNotNull(game.CoopOutcome);
                if (game.Result.TeamWon) won++;
            }
            Debug.Log($"Кооператив, {players} бота(ов): побед {won} из 4.");
        }

        [Test]
        public void Coop_SaveAndLoad_KeepsOrdersAndCrisis()
        {
            var map = TestCities.Moscow();
            var game = new Game(map, new[] { "Аня", "Борис" }, Coop(), 9);
            var bot = new SmartBot(9);
            for (int i = 0; i < 40 && game.Phase != GamePhase.GameOver; i++) Assert.IsTrue(bot.Act(game).Success);
            if (game.Phase == GamePhase.GameOver) Assert.Ignore("Партия закончилась раньше");

            var json = JsonUtility.ToJson(game.CreateSave());
            var restored = Game.FromSave(map, JsonUtility.FromJson<GameSaveData>(json));
            Assert.IsTrue(restored.IsCooperative);
            Assert.AreEqual(game.Crisis, restored.Crisis);
            Assert.AreEqual(game.OrdersDone, restored.OrdersDone);
            CollectionAssert.AreEqual(game.CityOrders.Select(o => o.Contract.Id + ":" + o.RoundsLeft).ToList(),
                restored.CityOrders.Select(o => o.Contract.Id + ":" + o.RoundsLeft).ToList());
        }
    }
}
