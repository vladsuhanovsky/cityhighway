using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>Контракты, платный проезд и бонус сети. Карта — TestCities.Small().</summary>
    public class ScoringTests
    {
        static Game Fresh(GameConfig config = null) => TestCities.Started(TestCities.Small(), 2, config);

        static Contract K(Game game, string id) => game.Map.GetContract(id);

        [Test]
        public void Contract_OwnRoadsOnly_CompletedWithoutToll()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(0, "r3");

            var check = game.CheckContract(0, K(game, "k1")); // Север — Юг

            Assert.IsTrue(check.Completed);
            Assert.AreEqual(0, check.ForeignRoads.Count);
            Assert.AreEqual(2, check.Path.Count);
        }

        [Test]
        public void Contract_ThroughForeignRoad_CompletedWithToll()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1");   // своя: Север — Центр
            game.Debug_GiveRoad(1, "r3");   // чужой мост: Центр — Юг

            var check = game.CheckContract(0, K(game, "k1"));

            Assert.IsTrue(check.Completed);
            Assert.AreEqual(1, check.ForeignRoads.Count);
            Assert.AreEqual("r3", check.ForeignRoads[0].Id);
        }

        [Test]
        public void Contract_NeedsAtLeastOneOwnRoad()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r1");
            game.Debug_GiveRoad(1, "r3");   // весь путь чужой
            game.Debug_GiveRoad(0, "r6");   // своя дорога есть, но не помогает дойти с Севера

            // Путь n1-c1-s1 чужой целиком; через r6 можно дойти до s2, но это уже район Юг — дорога не на пути.
            // Значит, контракт засчитывается: путь n1 -r1- c1 -r3- s1 -r6(своя)- s2 содержит свою дорогу и 2 чужие.
            var check = game.CheckContract(0, K(game, "k1"));
            Assert.IsTrue(check.Completed, "Своя дорога в конце пути тоже считается");
            Assert.AreEqual(2, check.ForeignRoads.Count);

            // А без своей дороги — не засчитывается, даже если чужих всего две.
            var other = Fresh();
            other.Debug_GiveRoad(1, "r1");
            other.Debug_GiveRoad(1, "r3");
            Assert.IsFalse(other.CheckContract(0, K(other, "k1")).Completed);
        }

        [Test]
        public void Contract_TooManyForeignRoads_NotCompleted()
        {
            var config = new GameConfig { MaxForeignRoadsPerContract = 1 };
            var game = Fresh(config);
            game.Debug_GiveRoad(0, "r2");   // своя: Север — Центр
            game.Debug_GiveRoad(1, "r4");   // чужая: Центр — Восток
            game.Debug_GiveRoad(1, "r5");   // чужая: Восток — Юг

            Assert.IsFalse(game.CheckContract(0, K(game, "k1")).Completed, "Нужно 2 чужих участка, а можно 1");

            var relaxed = Fresh();
            relaxed.Debug_GiveRoad(0, "r2");
            relaxed.Debug_GiveRoad(1, "r4");
            relaxed.Debug_GiveRoad(1, "r5");
            Assert.IsTrue(relaxed.CheckContract(0, K(relaxed, "k1")).Completed, "По умолчанию можно 2 чужих участка");
        }

        [Test]
        public void Contract_PrefersPathWithFewerForeignRoads()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(0, "r4");
            game.Debug_GiveRoad(0, "r5");
            game.Debug_GiveRoad(1, "r3"); // короче, но чужой

            var check = game.CheckContract(0, K(game, "k1"));

            Assert.IsTrue(check.Completed);
            Assert.AreEqual(0, check.ForeignRoads.Count, "Лучше длинный путь по своим, чем платить");
        }

        [Test]
        public void Contract_UnbuiltRoadsDoNotCount()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1");
            Assert.IsFalse(game.CheckContract(0, K(game, "k1")).Completed);
        }

        [Test]
        public void FinalScore_TollsMovePointsBetweenPlayers()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(1, "r3");
            game.Debug_GiveContract(0, "k1");
            game.Debug_Finish();

            var p0 = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            var p1 = game.Result.Ranking.First(s => s.PlayerIndex == 1);
            Assert.AreEqual(5, p0.ContractPoints);
            Assert.AreEqual(2, p0.TollsPaid);
            Assert.AreEqual(2, p1.TollsEarned);
        }

        [Test]
        public void FinalScore_FailedContractSubtractsPoints()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_GiveContract(0, "k2");
            game.Debug_Finish();

            var p0 = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            Assert.AreEqual(-6, p0.ContractPoints);
            Assert.AreEqual(1, p0.FailedContracts);
        }

        [Test]
        public void NetworkBonus_GoesToWidestNetwork()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_GiveRoad(0, "r1");   // Север — Центр
            game.Debug_GiveRoad(0, "r3");   // Центр — Юг: 3 района
            game.Debug_GiveRoad(1, "r5");   // Восток — Юг: 2 района
            game.Debug_Finish();

            var p0 = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            var p1 = game.Result.Ranking.First(s => s.PlayerIndex == 1);
            Assert.AreEqual(3, p0.NetworkDistricts);
            Assert.AreEqual(2, p1.NetworkDistricts);
            Assert.AreEqual(10, p0.NetworkBonus);
            Assert.AreEqual(0, p1.NetworkBonus);
        }

        [Test]
        public void NetworkDistricts_CountsOnlyLargestConnectedPart()
        {
            var map = TestCities.Small();
            var roads = new[] { map.GetRoad("r1"), map.GetRoad("r5") }; // две отдельные части по 2 района
            Assert.AreEqual(2, Scoring.NetworkDistricts(map, roads));
        }

        /// <summary>Убирает стартовые контракты, чтобы тест считал только свои.</summary>
        static void ClearContracts(Game game)
        {
            foreach (var player in game.Players) player.ContractList.Clear();
        }
    }
}
