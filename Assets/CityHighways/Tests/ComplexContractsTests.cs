using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>Сложные контракты: транзит и узел. Карта — TestCities.SmallWithComplex().</summary>
    public class ComplexContractsTests
    {
        static Game Fresh() => TestCities.Started(TestCities.SmallWithComplex());

        static Contract K(Game game, string id) => game.Map.GetContract(id);

        [Test]
        public void Contract_KindsAndLegs()
        {
            var map = TestCities.SmallWithComplex();
            Assert.IsEmpty(map.Validate(new GameConfig().MaxRoadLength));

            var link = map.GetContract("k1");
            Assert.AreEqual(ContractKind.Link, link.Kind);
            Assert.IsFalse(link.IsComplex);
            Assert.AreEqual(1, link.Legs.Count);
            Assert.AreEqual(2, link.Districts.Count);

            var transit = map.GetContract("k8");
            Assert.AreEqual(1, transit.Legs.Count, "Транзит — один непрерывный путь");
            Assert.AreEqual("east", transit.Legs[0].Via);
            CollectionAssert.AreEquivalent(new[] { "north", "south", "east" }, transit.Districts);

            var hub = map.GetContract("k9");
            Assert.AreEqual(2, hub.Legs.Count);
            Assert.AreEqual("center", hub.Legs[0].From);
            Assert.AreEqual("center", hub.Legs[1].From);
            CollectionAssert.AreEquivalent(new[] { "north", "east" }, hub.Legs.Select(l => l.To).ToArray());
        }

        [Test]
        public void Transit_DirectPathDoesNotCount()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1"); // Север — Центр
            game.Debug_GiveRoad(0, "r3"); // Центр — Юг, мимо Востока

            Assert.IsTrue(game.CheckContract(0, K(game, "k1")).Completed, "Обычный Север — Юг выполнен");
            Assert.IsFalse(game.CheckContract(0, K(game, "k8")).Completed, "Транзит обязан пройти через Восток");
        }

        [Test]
        public void Transit_PathThroughVia_Completed()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1"); // n1 — c1
            game.Debug_GiveRoad(0, "r4"); // c1 — e1
            game.Debug_GiveRoad(0, "r5"); // e1 — s2

            var check = game.CheckContract(0, K(game, "k8"));
            Assert.IsTrue(check.Completed);
            CollectionAssert.AreEqual(new[] { "r1", "r4", "r5" }, check.Path.Select(r => r.Id).ToArray());
            Assert.AreEqual(0, check.Toll);
        }

        [Test]
        public void Transit_ViaReachedOnForeignRoads_PaysToll()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r1"); // своя n1 — c1
            game.Debug_GiveRoad(1, "r4"); // чужая c1 — e1
            game.Debug_GiveRoad(1, "r5"); // чужая e1 — s2

            var check = game.CheckContract(0, K(game, "k8"));
            Assert.IsTrue(check.Completed);
            Assert.AreEqual(2, check.ForeignRoads.Count);
            Assert.AreEqual(4, check.Toll);
        }

        [Test]
        public void Hub_NeedsBothLegs()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r2"); // Центр — Север

            var half = game.CheckContract(0, K(game, "k9"));
            Assert.IsFalse(half.Completed);
            Assert.AreEqual(1, half.LegsCompleted);
            Assert.AreEqual(2, half.LegsTotal);
            Assert.AreEqual(0, half.Path.Count, "Путь показывается только у выполненного контракта");

            game.Debug_GiveRoad(0, "r4"); // Центр — Восток
            var full = game.CheckContract(0, K(game, "k9"));
            Assert.IsTrue(full.Completed);
            Assert.AreEqual(2, full.LegsCompleted);
            CollectionAssert.AreEquivalent(new[] { "r2", "r4" }, full.Path.Select(r => r.Id).ToArray());
        }

        [Test]
        public void Hub_ForeignRoadSharedByLegs_PaidOnce()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r4"); // чужая e1 — c1, нужна обоим плечам
            game.Debug_GiveRoad(0, "r1"); // своя c1 — n1
            game.Debug_GiveRoad(0, "r3"); // своя c1 — s1

            var check = game.CheckContract(0, K(game, "k10"));
            Assert.IsTrue(check.Completed);
            Assert.AreEqual(1, check.ForeignRoads.Count);
            Assert.AreEqual(2, check.Toll, "Общий чужой участок оплачивается один раз");
        }

        [Test]
        public void Hub_EachLegNeedsOwnRoad()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r4"); // чужая e1 — c1
            game.Debug_GiveRoad(1, "r1"); // чужая c1 — n1
            game.Debug_GiveRoad(0, "r5"); // своя e1 — s2: плечо Восток — Юг готово

            var check = game.CheckContract(0, K(game, "k10"));
            Assert.IsFalse(check.Completed, "Плечо Восток — Север целиком чужое");
            Assert.AreEqual(1, check.LegsCompleted);
        }

        [Test]
        public void Scoring_ComplexContract_FailedLosesPoints_CompletedPaysToll()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r4");
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(0, "r3");
            var players = game.Players;
            var done = K(game, "k10");   // узел Восток — Север и Юг: выполнен через чужую r4
            var failed = K(game, "k8");  // транзит Север — Юг через Восток: до Юга через Восток не дойти
            foreach (var player in players) player.ContractList.Clear();
            players[0].ContractList.Add(done);
            players[0].ContractList.Add(failed);

            var result = Scoring.Calculate(game.Map, players, game.GetRoadOwner, game.TollFor, game.Config);
            var me = result.Ranking.First(s => s.PlayerIndex == 0);
            var other = result.Ranking.First(s => s.PlayerIndex == 1);

            Assert.AreEqual(done.Points - failed.Points, me.ContractPoints);
            Assert.AreEqual(1, me.CompletedContracts);
            Assert.AreEqual(1, me.FailedContracts);
            Assert.AreEqual(2, me.TollsPaid);
            Assert.AreEqual(2, other.TollsEarned);
        }

        [Test]
        public void Planner_Transit_RoutesThroughVia()
        {
            var game = Fresh();
            var plan = new BotPlanner(game, 0, 2.5f).BuildPlan(new[] { K(game, "k8") });

            Assert.AreEqual(1, plan.Reachable.Count);
            Assert.AreEqual(6, plan.PiecesNeeded, "Самый короткий путь через Восток — 6 участков, а не 4 напрямую");
            Assert.IsTrue(plan.RoadsToBuild.Any(r => r.Connects("e1")));
        }

        [Test]
        public void Planner_Hub_PlansBothLegs()
        {
            var game = Fresh();
            var plan = new BotPlanner(game, 0, 2.5f).BuildPlan(new[] { K(game, "k9") });

            CollectionAssert.AreEquivalent(new[] { "r2", "r4" }, plan.RoadsToBuild.Select(r => r.Id).ToArray());
            Assert.AreEqual(4, plan.PiecesNeeded);
        }

        [Test]
        public void Moscow_HasComplexContracts()
        {
            var map = TestCities.Moscow();
            var transit = map.Contracts.Where(c => c.Kind == ContractKind.Transit).ToList();
            var hubs = map.Contracts.Where(c => c.Kind == ContractKind.Hub).ToList();
            Assert.AreEqual(8, transit.Count);
            Assert.AreEqual(6, hubs.Count);
            Assert.IsTrue(transit.Concat(hubs).All(c => map.GetDistrict(c.DistrictC) != null));
            Assert.IsEmpty(map.Validate(new GameConfig().MaxRoadLength));
        }
    }
}
