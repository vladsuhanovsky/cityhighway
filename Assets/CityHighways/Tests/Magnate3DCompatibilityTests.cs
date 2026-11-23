using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>
    /// Shared 3D planner regression coverage for procedural Logistic Magnate cities.
    /// The 3D rendering optimizations must not depend on fixed map names or one seed.
    /// </summary>
    public sealed class Magnate3DCompatibilityTests
    {
        [TestCase(1)]
        [TestCase(42)]
        [TestCase(2026)]
        public void GeneratedMagnateCityBuilds3DPlanAtEveryQuality(int seed)
        {
            var city = CityGenerator.Generate(seed);
            Assert.IsNotNull(city.Map);
            Assert.IsNotEmpty(city.Map.Name);

            foreach (float density in new[] { 0.55f, 0.8f, 1f })
            {
                var plan = CityPlanner.Build(city.Map, density);
                Assert.IsNotNull(plan, "seed " + seed + ", density " + density);
                Assert.IsTrue(plan.Buildings.Count > 0,
                    "No 3D buildings for seed " + seed + ", density " + density);
                Assert.IsTrue(plan.Trees.Count > 0,
                    "No 3D trees for seed " + seed + ", density " + density);
                Assert.AreEqual(plan.Buildings.Count,
                    plan.Buildings.Select(CityProceduralReplacementResolver.BuildingId).Distinct().Count(),
                    "Duplicate building stable IDs for seed " + seed);
                Assert.AreEqual(plan.Trees.Count,
                    plan.Trees.Select(CityProceduralReplacementResolver.TreeId).Distinct().Count(),
                    "Duplicate tree stable IDs for seed " + seed);
            }
        }

        [Test]
        public void MagnateTutorialCityBuildsShared3DPlan()
        {
            var tutorial = CityGenerator.GenerateTutorial();
            var plan = CityPlanner.Build(tutorial.Map, 0.8f);
            Assert.IsNotNull(plan);
            Assert.IsTrue(plan.Buildings.Count > 0);
            Assert.IsTrue(plan.Trees.Count > 0);
        }
    }
}
