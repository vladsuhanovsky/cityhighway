using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CitySceneQualityMetricsTests
    {
        static CitySceneQualityMetrics.Record Visual(Vector3 center, Vector3 size, int triangles = 12)
        {
            return new CitySceneQualityMetrics.Record
            {
                WorldBounds = new Bounds(center, size),
                Meshes = 1,
                Triangles = triangles
            };
        }

        [Test]
        public void SummaryCountsVisualsMeshesTrianglesAndAreaDensity()
        {
            var records = new[]
            {
                Visual(Vector3.zero, Vector3.one),
                Visual(new Vector3(3f, 0f, 0f), Vector3.one, 24)
            };
            Assert.IsTrue(CitySceneQualityMetrics.TryAnalyze(records, 4f, 2f, 0.01f, out var summary));
            Assert.AreEqual(2, summary.Visuals);
            Assert.AreEqual(2, summary.Meshes);
            Assert.AreEqual(36L, summary.Triangles);
            Assert.That(summary.FootprintArea, Is.EqualTo(8f));
            Assert.That(summary.AreaDensity, Is.EqualTo(0.25f).Within(0.000001f));
            Assert.AreEqual(0, summary.PossibleOverlaps);
        }

        [Test]
        public void OverlappingBoundsAreCountedExactlyOnce()
        {
            var records = new[]
            {
                Visual(Vector3.zero, Vector3.one * 2f),
                Visual(new Vector3(0.5f, 0f, 0f), Vector3.one * 2f),
                Visual(new Vector3(10f, 0f, 0f), Vector3.one)
            };
            Assert.IsTrue(CitySceneQualityMetrics.TryAnalyze(records, 20f, 20f, 0.01f, out var summary));
            Assert.AreEqual(1, summary.PossibleOverlaps);
        }

        [Test]
        public void TouchingBoxesAndVerticalSeparationDoNotCount()
        {
            Assert.IsFalse(CitySceneQualityMetrics.PotentialIntersection(
                new Bounds(Vector3.zero, Vector3.one),
                new Bounds(Vector3.right, Vector3.one), 0f));
            Assert.IsFalse(CitySceneQualityMetrics.PotentialIntersection(
                new Bounds(Vector3.zero, Vector3.one),
                new Bounds(new Vector3(0f, 2f, 0f), Vector3.one), 0f));
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.IsFalse(CitySceneQualityMetrics.TryAnalyze(
                new[] { Visual(Vector3.zero, Vector3.one) }, 0f, 1f, 0f, out _));
            Assert.IsFalse(CitySceneQualityMetrics.TryAnalyze(
                new[] { Visual(Vector3.zero, Vector3.zero) }, 1f, 1f, 0f, out _));
            Assert.IsFalse(CitySceneQualityMetrics.TryAnalyze(
                new[] { Visual(Vector3.zero, Vector3.one) }, 1f, float.NaN, 0f, out _));
        }
    }
}
