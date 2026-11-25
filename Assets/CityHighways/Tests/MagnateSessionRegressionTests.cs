using System;
using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class MagnateSessionRegressionTests
    {
        [TestCase(MagnateDifficulty.Easy, 2, true)]
        [TestCase(MagnateDifficulty.Normal, 3, false)]
        [TestCase(MagnateDifficulty.Hard, 2, true)]
        [TestCase(MagnateDifficulty.Expert, 3, true)]
        public void ShortBotMatchTerminatesAndRemainsDeterministic(MagnateDifficulty difficulty, int seats, bool opening)
        {
            const int seed = 42;
            var a = Create(difficulty, seats, opening, seed);
            var b = Create(difficulty, seats, opening, seed);
            var rngA = new Random(71);
            var rngB = new Random(71);
            int limit = 2 * seats * a.TotalRounds + 1;
            int steps = 0;
            bool sawPlanning = false;
            while (a.Phase != MagnatePhase.GameOver && steps < limit)
            {
                Assert.AreEqual(a.Phase, b.Phase);
                Assert.AreEqual(a.CurrentPlayerIndex, b.CurrentPlayerIndex);
                sawPlanning |= a.Phase == MagnatePhase.Plan;
                Step(a, rngA);
                Step(b, rngB);
                AssertState(a, b);
                AssertReferences(a);
                steps++;
            }
            Assert.IsTrue(sawPlanning);
            Assert.AreEqual(MagnatePhase.GameOver, a.Phase, "bounded match did not terminate");
            Assert.AreEqual(a.TotalRounds, a.Round);
            Assert.AreEqual(a.TotalRounds, a.LastReport.Round);
            Assert.AreEqual(GamePhase.GameOver, a.Board.Phase);
            // Phase completion commands must be inert after game over.
            a.FinishBuild();
            a.FinishPlanning();
            AssertState(a, b);
        }

        static LogisticsGame Create(MagnateDifficulty difficulty, int seats, bool opening, int seed)
        {
            var config = MagnateConfig.For(difficulty);
            config.Rounds = 3;
            config.TrucksPerPlayer = 2;
            config.OpeningBuildRound = opening;
            config.RandomFirstPlayer = true;
            var names = Enumerable.Range(0, seats).Select(i => "Bot " + i).ToArray();
            return new LogisticsGame(CityGenerator.Generate(seed), names,
                Enumerable.Repeat(true, seats).ToArray(), seed, config);
        }

        static void Step(LogisticsGame game, Random rng)
        {
            if (game.Phase == MagnatePhase.Build) LogisticsBot.Build(game, rng);
            else if (game.Phase == MagnatePhase.Plan) LogisticsBot.Plan(game, rng);
            else Assert.Fail("Unexpected terminal state");
        }

        static void AssertState(LogisticsGame a, LogisticsGame b)
        {
            Assert.AreEqual(a.Round, b.Round);
            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(a.CurrentPlayerIndex, b.CurrentPlayerIndex);
            CollectionAssert.AreEqual(a.TurnOrder, b.TurnOrder);
            CollectionAssert.AreEqual(a.Players.Select(p => (p.Score, p.BuildPoints, p.Influence, p.Reputation, p.Delivered)),
                b.Players.Select(p => (p.Score, p.BuildPoints, p.Influence, p.Reputation, p.Delivered)));
            CollectionAssert.AreEqual(a.Hubs.Select(h => (h.LocationId, h.Owner, h.Level)),
                b.Hubs.Select(h => (h.LocationId, h.Owner, h.Level)));
            CollectionAssert.AreEqual(a.Trucks.Select(t => (t.Id, t.Owner, t.Location, t.Cargo, t.PlannedCargo, t.Target, string.Join(",", t.Route))),
                b.Trucks.Select(t => (t.Id, t.Owner, t.Location, t.Cargo, t.PlannedCargo, t.Target, string.Join(",", t.Route))));
            CollectionAssert.AreEqual(a.AllCargo.OrderBy(c => c.Id).Select(c => (c.Id, c.Kind, c.From, c.To, c.Truck, c.SpawnRound)),
                b.AllCargo.OrderBy(c => c.Id).Select(c => (c.Id, c.Kind, c.From, c.To, c.Truck, c.SpawnRound)));
            CollectionAssert.AreEqual(a.Map.Roads.Select(r => a.Board.GetRoadOwner(r.Id)),
                b.Map.Roads.Select(r => b.Board.GetRoadOwner(r.Id)));
        }

        static void AssertReferences(LogisticsGame game)
        {
            Assert.AreEqual(game.Trucks.Count, game.Trucks.Select(t => t.Id).Distinct().Count());
            foreach (var truck in game.Trucks)
            {
                Assert.IsNotNull(game.Map.GetLocation(truck.Location));
                Assert.That(truck.Owner, Is.InRange(0, game.Players.Count - 1));
                foreach (string road in truck.Route) Assert.IsNotNull(game.Map.GetRoad(road));
                if (truck.Cargo != null)
                {
                    var cargo = game.GetCargo(truck.Cargo);
                    Assert.IsNotNull(cargo);
                    Assert.AreEqual(truck.Id, cargo.Truck);
                }
            }
            foreach (var cargo in game.AllCargo.Where(c => c.Truck >= 0))
                Assert.IsTrue(game.Trucks.Any(t => t.Id == cargo.Truck && t.Cargo == cargo.Id));
        }
    }
}
