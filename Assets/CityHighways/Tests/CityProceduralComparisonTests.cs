using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralComparisonTests
    {
        [Test]
        public void EqualVolumesReportUnityRatiosAndNoOffset()
        {
            Assert.IsTrue(CityProceduralFitMath.TryCompare(
                new Vector3(0.5f, 0.2f, 0.3f),
                new Vector3(0.5f, 0.2f, 0.3f),
                Vector3.zero, Vector3.zero, out var ratios, out var offset));
            Assert.That(ratios, Is.EqualTo(Vector3.one));
            Assert.That(offset, Is.EqualTo(0f));
        }

        [Test]
        public void ComparisonReportsIndependentAxesAndWorldOffset()
        {
            Assert.IsTrue(CityProceduralFitMath.TryCompare(
                new Vector3(0.8f, 0.3f, 0.2f),
                new Vector3(0.4f, 0.6f, 0.4f),
                Vector3.zero, new Vector3(0f, 3f, 4f),
                out var ratios, out var offset));
            Assert.That(ratios.x, Is.EqualTo(2f).Within(0.000001f));
            Assert.That(ratios.y, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(ratios.z, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(offset, Is.EqualTo(5f).Within(0.000001f));
        }

        [Test]
        public void InvalidDimensionsOrPositionReturnNoComparison()
        {
            Assert.IsFalse(CityProceduralFitMath.TryCompare(
                Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, out _, out _));
            Assert.IsFalse(CityProceduralFitMath.TryCompare(
                Vector3.one, new Vector3(0f, 1f, 1f), Vector3.zero, Vector3.zero, out _, out _));
            Assert.IsFalse(CityProceduralFitMath.TryCompare(
                Vector3.one, Vector3.one, new Vector3(float.NaN, 0f, 0f),
                Vector3.zero, out _, out _));
        }
    }
}
