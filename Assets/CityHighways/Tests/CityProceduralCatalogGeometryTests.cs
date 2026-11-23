using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralCatalogGeometryTests
    {
        [TearDown]
        public void TearDown() => CityProceduralReplacementResolver.ClearCatalog();

        [Test]
        public void BuildingCatalogDimensionsAreConvertedToWorldUnits()
        {
            var plan = new CityPlanner.Plan();
            var building = new CityPlanner.Building
            {
                Center = new Vector2(120f, 230f),
                Width = 45f,
                Depth = 25f,
                Height = 0.18f,
                Angle = 35f,
                Seed = 42
            };
            plan.Buildings.Add(building);
            CityProceduralReplacementResolver.CaptureCatalog(plan, "Москва");

            var entry = CityProceduralReplacementResolver.GetCatalogSnapshot()[0];
            Assert.AreEqual(CityProceduralReplacementResolver.BuildingId(building), entry.Id);
            Assert.That(entry.Width, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(entry.Depth, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(entry.Height, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(entry.Angle, Is.EqualTo(35f).Within(0.0001f));
        }

        [Test]
        public void TreeCatalogIncludesSizeAndKeepsOriginalStableId()
        {
            var plan = new CityPlanner.Plan();
            var tree = new CityPlanner.TreeSpot
            {
                At = new Vector2(40f, 50f), Size = 0.09f, Seed = 11
            };
            plan.Trees.Add(tree);
            CityProceduralReplacementResolver.CaptureCatalog(plan, "Чунцин");

            var entry = CityProceduralReplacementResolver.GetCatalogSnapshot()[0];
            Assert.AreEqual(CityProceduralReplacementResolver.TreeId(tree), entry.Id);
            Assert.That(entry.Width, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(entry.Height, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.AreEqual(CityProceduralReplacement.TargetKind.Tree, entry.Kind);
        }

        [Test]
        public void EmptyCatalogRemainsEmpty()
        {
            CityProceduralReplacementResolver.CaptureCatalog(new CityPlanner.Plan(), "Амстердам");
            Assert.IsEmpty(CityProceduralReplacementResolver.GetCatalogSnapshot());
        }
    }
}
