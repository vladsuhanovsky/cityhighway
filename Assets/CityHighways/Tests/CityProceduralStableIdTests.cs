using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralStableIdTests
    {
        [TearDown]
        public void TearDown() => CityProceduralReplacementResolver.ClearCatalog();

        [Test]
        public void IdDependsOnKindSeedAndPositionNotListIndex()
        {
            var building = new CityPlanner.Building { Seed = 17, Center = new Vector2(100f, 200f) };
            var moved = new CityPlanner.Building { Seed = 17, Center = new Vector2(101f, 200f) };
            var tree = new CityPlanner.TreeSpot { Seed = 17, At = new Vector2(100f, 200f) };

            Assert.AreEqual(CityProceduralReplacementResolver.BuildingId(building),
                CityProceduralReplacementResolver.BuildingId(building));
            Assert.AreNotEqual(CityProceduralReplacementResolver.BuildingId(building),
                CityProceduralReplacementResolver.BuildingId(moved));
            Assert.AreNotEqual(CityProceduralReplacementResolver.BuildingId(building),
                CityProceduralReplacementResolver.TreeId(tree));
        }

        [Test]
        public void ExplicitIdRemovesOnlySelectedBuildingAndFootprint()
        {
            var plan = new CityPlanner.Plan();
            var first = new CityPlanner.Building { Seed = 4, Center = new Vector2(10f, 20f) };
            var second = new CityPlanner.Building { Seed = 5, Center = new Vector2(30f, 40f) };
            plan.Buildings.Add(first);
            plan.Buildings.Add(second);
            plan.Footprints.Add(new Vector4(1f, 0f, 0f, 0f));
            plan.Footprints.Add(new Vector4(2f, 0f, 0f, 0f));

            Assert.IsTrue(CityProceduralReplacementResolver.TryRemoveById(plan,
                CityProceduralReplacement.TargetKind.Building,
                CityProceduralReplacementResolver.BuildingId(second)));
            Assert.AreEqual(1, plan.Buildings.Count);
            Assert.AreEqual(first.Seed, plan.Buildings[0].Seed);
            Assert.AreEqual(1, plan.Footprints.Count);
            Assert.AreEqual(1f, plan.Footprints[0].x);
        }

        [Test]
        public void UnknownIdDoesNotFallBackToAnotherVisual()
        {
            var plan = new CityPlanner.Plan();
            var tree = new CityPlanner.TreeSpot { Seed = 11, At = new Vector2(40f, 50f) };
            plan.Trees.Add(tree);
            Assert.IsFalse(CityProceduralReplacementResolver.TryRemoveById(plan,
                CityProceduralReplacement.TargetKind.Tree, "T:unknown"));
            Assert.AreEqual(1, plan.Trees.Count);
            Assert.IsTrue(CityProceduralReplacementResolver.TryRemoveById(plan,
                CityProceduralReplacement.TargetKind.Tree,
                CityProceduralReplacementResolver.TreeId(tree)));
            Assert.AreEqual(0, plan.Trees.Count);
        }

        [Test]
        public void CatalogFiltersCityTypeAndRadius()
        {
            var plan = new CityPlanner.Plan();
            var building = new CityPlanner.Building { Seed = 2, Center = new Vector2(50f, 60f) };
            var tree = new CityPlanner.TreeSpot { Seed = 3, At = new Vector2(50f, 60f) };
            plan.Buildings.Add(building);
            plan.Trees.Add(tree);
            CityProceduralReplacementResolver.CaptureCatalog(plan, "Москва");

            Assert.IsTrue(CityProceduralReplacementResolver.TryFindNearestId("Москва",
                CityProceduralReplacement.TargetKind.Tree, new Vector2(50f, 60f), 1f, out var id));
            Assert.AreEqual(CityProceduralReplacementResolver.TreeId(tree), id);
            Assert.IsFalse(CityProceduralReplacementResolver.TryFindNearestId("Амстердам",
                CityProceduralReplacement.TargetKind.Tree, new Vector2(50f, 60f), 1f, out _));
            Assert.IsFalse(CityProceduralReplacementResolver.TryFindNearestId("Москва",
                CityProceduralReplacement.TargetKind.Building, Vector2.zero, 1f, out _));
        }
    }
}
