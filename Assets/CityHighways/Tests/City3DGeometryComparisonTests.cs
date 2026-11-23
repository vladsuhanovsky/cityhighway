using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DGeometryComparisonTests
    {
        [Test]
        public void BudgetChecksAreStrictAndDisabledAtZero()
        {
            Assert.IsFalse(City3DGeometryComparison.ExceedsBudget(200, 200));
            Assert.IsTrue(City3DGeometryComparison.ExceedsBudget(201, 200));
            Assert.IsFalse(City3DGeometryComparison.ExceedsBudget(9999, 0));
        }

        [Test]
        public void ComparisonReportsSignedChanges()
        {
            var before = new City3DGeometryAudit.Totals
            {
                Triangles = 200, Vertices = 80, Meshes = 8,
                BuildingTriangles = 100, TreeTriangles = 60,
                AuthoredTriangles = 20, OtherTriangles = 20
            };
            var after = new City3DGeometryAudit.Totals
            {
                Triangles = 170, Vertices = 100, Meshes = 7,
                BuildingTriangles = 70, TreeTriangles = 50,
                AuthoredTriangles = 30, OtherTriangles = 20
            };
            var delta = City3DGeometryComparison.Compare(before, after);
            Assert.AreEqual(-30L, delta.Triangles);
            Assert.AreEqual(20L, delta.Vertices);
            Assert.AreEqual(-1, delta.Meshes);
            Assert.AreEqual(-30L, delta.BuildingTriangles);
            Assert.AreEqual(-10L, delta.TreeTriangles);
            Assert.AreEqual(10L, delta.AuthoredTriangles);
            Assert.AreEqual(0L, delta.OtherTriangles);
        }

        [Test]
        public void ComparisonAggregatesRepeatedGroupNames()
        {
            var before = new[]
            {
                new City3DGeometryAudit.Entry { Name = "District A", Triangles = 10 },
                new City3DGeometryAudit.Entry { Name = "District A", Triangles = 20 }
            };
            var after = new[]
            {
                new City3DGeometryAudit.Entry { Name = "District A", Triangles = 15 },
                new City3DGeometryAudit.Entry { Name = "District A", Triangles = 25 }
            };
            Assert.AreEqual(10L, City3DGeometryComparison.DeltaForGroup(
                before, after, "District A"));
            Assert.AreEqual(0L, City3DGeometryComparison.DeltaForGroup(
                before, after, "District B"));
        }
    }
}
