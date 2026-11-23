using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralBuildingBaseHeightTests
    {
        [TearDown]
        public void TearDown() => CityProceduralReplacementResolver.ClearCatalog();

        [Test]
        public void BuildingBaseHeightIsAtLeastCenterTerrainHeight()
        {
            var building = new CityPlanner.Building
            {
                Center = new Vector2(CityPlanner.FieldW * 0.5f, CityPlanner.FieldH * 0.5f),
                Width = 40f,
                Depth = 25f,
                Angle = 37f,
                Seed = 17
            };
            var baseY = CityPlanner.BuildingBaseHeight(building);
            Assert.That(baseY, Is.GreaterThanOrEqualTo(CityPlanner.ToWorld(building.Center).y - 0.00001f));
        }

        [Test]
        public void CatalogStoresFootprintBaseSeparatelyFromTerrainCenter()
        {
            var plan = new CityPlanner.Plan();
            var building = new CityPlanner.Building
            {
                Center = new Vector2(600f, 320f),
                Width = 35f,
                Depth = 30f,
                Height = 0.17f,
                Angle = 45f,
                Seed = 23
            };
            plan.Buildings.Add(building);
            CityProceduralReplacementResolver.CaptureCatalog(plan, "Москва");
            var snapshot = CityProceduralReplacementResolver.GetCatalogSnapshot();

            Assert.AreEqual(1, snapshot.Length);
            Assert.AreEqual(CityProceduralReplacementResolver.BuildingId(building), snapshot[0].Id);
            Assert.That(snapshot[0].TerrainY,
                Is.EqualTo(CityPlanner.ToWorld(building.Center).y).Within(0.00001f));
            Assert.That(snapshot[0].BuildingBaseY,
                Is.EqualTo(CityPlanner.BuildingBaseHeight(building)).Within(0.00001f));
        }

        [Test]
        public void TreesRetainCenterBasedTerrainHeight()
        {
            var plan = new CityPlanner.Plan();
            var tree = new CityPlanner.TreeSpot { At = new Vector2(650f, 300f), Size = 0.08f, Seed = 9 };
            plan.Trees.Add(tree);
            CityProceduralReplacementResolver.CaptureCatalog(plan, "Москва");
            var snapshot = CityProceduralReplacementResolver.GetCatalogSnapshot();

            Assert.AreEqual(1, snapshot.Length);
            Assert.That(snapshot[0].TerrainY, Is.EqualTo(CityPlanner.ToWorld(tree.At).y).Within(0.00001f));
            Assert.AreEqual(CityProceduralReplacement.TargetKind.Tree, snapshot[0].Kind);
        }
    }
}
