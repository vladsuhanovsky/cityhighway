using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralCatalogTerrainTests
    {
        [TearDown]
        public void TearDown() => CityProceduralReplacementResolver.ClearCatalog();

        [Test]
        public void CatalogCapturesTerrainHeightAtBuildingAndTreeCenters()
        {
            var plan = new CityPlanner.Plan();
            var center = new Vector2(CityPlanner.FieldW * 0.5f, CityPlanner.FieldH * 0.5f);
            plan.Buildings.Add(new CityPlanner.Building { Center = center, Seed = 21, Height = 0.2f });
            plan.Trees.Add(new CityPlanner.TreeSpot { At = center, Seed = 22, Size = 0.1f });

            CityProceduralReplacementResolver.CaptureCatalog(plan, "Москва");
            var entries = CityProceduralReplacementResolver.GetCatalogSnapshot();

            Assert.AreEqual(2, entries.Length);
            float expected = CityPlanner.ToWorld(center).y;
            Assert.That(entries[0].TerrainY, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(entries[1].TerrainY, Is.EqualTo(expected).Within(0.0001f));
            Assert.AreEqual(CityProceduralReplacementResolver.BuildingId(plan.Buildings[0]), entries[0].Id);
            Assert.AreEqual(CityProceduralReplacementResolver.TreeId(plan.Trees[0]), entries[1].Id);
        }

        [Test]
        public void CatalogEntrySupportsExplicitCapturedHeight()
        {
            var entry = new CityProceduralReplacementResolver.CatalogEntry("B:1:2:3",
                CityProceduralReplacement.TargetKind.Building, Vector2.zero,
                0.2f, 0.3f, 0.4f, 20f, 0.16f);
            Assert.That(entry.TerrainY, Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(entry.Width, Is.EqualTo(0.2f).Within(0.0001f));
        }
    }
}
