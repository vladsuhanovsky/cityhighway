using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralCatalogTests
    {
        [TearDown]
        public void TearDown() => CityProceduralReplacementResolver.ClearCatalog();

        [Test]
        public void SnapshotIsCopyAndPreservesKindAndFieldCoordinates()
        {
            var plan = new CityPlanner.Plan();
            var building = new CityPlanner.Building { Seed = 100, Center = new Vector2(55f, 66f) };
            var tree = new CityPlanner.TreeSpot { Seed = 200, At = new Vector2(77f, 88f) };
            plan.Buildings.Add(building);
            plan.Trees.Add(tree);

            CityProceduralReplacementResolver.CaptureCatalog(plan, "Москва");
            var entries = CityProceduralReplacementResolver.GetCatalogSnapshot();

            Assert.AreEqual("Москва", CityProceduralReplacementResolver.CurrentCatalogMapName);
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(CityProceduralReplacementResolver.BuildingId(building), entries[0].Id);
            Assert.AreEqual(CityProceduralReplacement.TargetKind.Building, entries[0].Kind);
            Assert.AreEqual(new Vector2(55f, 66f), entries[0].FieldPosition);
            Assert.AreEqual(CityProceduralReplacementResolver.TreeId(tree), entries[1].Id);
            Assert.AreEqual(CityProceduralReplacement.TargetKind.Tree, entries[1].Kind);
            Assert.AreEqual(new Vector2(77f, 88f), entries[1].FieldPosition);

            entries[0] = default;
            Assert.AreEqual(2, CityProceduralReplacementResolver.GetCatalogSnapshot().Length);
            Assert.AreEqual(CityProceduralReplacementResolver.BuildingId(building),
                CityProceduralReplacementResolver.GetCatalogSnapshot()[0].Id);
        }

        [Test]
        public void ClearedCatalogHasNoLiveMapOrItems()
        {
            var plan = new CityPlanner.Plan();
            plan.Buildings.Add(new CityPlanner.Building { Seed = 1, Center = Vector2.one });
            CityProceduralReplacementResolver.CaptureCatalog(plan, "Чунцин");
            CityProceduralReplacementResolver.ClearCatalog();
            Assert.IsNull(CityProceduralReplacementResolver.CurrentCatalogMapName);
            Assert.IsEmpty(CityProceduralReplacementResolver.GetCatalogSnapshot());
        }
    }
}
