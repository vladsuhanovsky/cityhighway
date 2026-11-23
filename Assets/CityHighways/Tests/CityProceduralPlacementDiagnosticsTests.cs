using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralPlacementDiagnosticsTests
    {
        static CityProceduralPlacementDiagnostics.Result Evaluate(Vector3 source, Vector3 target,
            Vector3 pivot, float bottom, float ground)
        {
            Assert.IsTrue(CityProceduralPlacementDiagnostics.TryEvaluate(
                source, target, pivot, Vector3.zero, bottom, ground, out var result));
            return result;
        }

        [Test]
        public void WellFittedModelHasNoWarnings()
        {
            var result = Evaluate(new Vector3(0.3f, 0.5f, 0.3f),
                new Vector3(0.4f, 0.6f, 0.4f), Vector3.zero, 0f, 0f);
            Assert.IsFalse(result.Oversized);
            Assert.IsFalse(result.Offset);
            Assert.IsFalse(result.BelowGround);
        }

        [Test]
        public void OversizeUsesLargestDimensionRatio()
        {
            var result = Evaluate(new Vector3(0.6f, 0.2f, 0.2f),
                new Vector3(0.4f, 0.4f, 0.4f), Vector3.zero, 0f, 0f);
            Assert.IsTrue(result.Oversized);
            Assert.That(result.MaximumSizeRatio, Is.EqualTo(1.5f).Within(0.00001f));
        }

        [Test]
        public void HorizontalDisplacementIgnoresPivotHeight()
        {
            var result = Evaluate(new Vector3(0.2f, 0.2f, 0.2f),
                new Vector3(0.4f, 0.4f, 0.4f), new Vector3(0.2f, 9f, 0f), 0f, 0f);
            Assert.IsTrue(result.Offset);
            Assert.That(result.HorizontalOffset, Is.EqualTo(0.2f).Within(0.00001f));
        }

        [Test]
        public void LowestMeshPointBelowGroundTriggersWarning()
        {
            var result = Evaluate(Vector3.one, Vector3.one, Vector3.zero, -0.05f, 0f);
            Assert.IsTrue(result.BelowGround);
            Assert.That(result.GroundPenetration, Is.EqualTo(0.05f).Within(0.00001f));
        }

        [Test]
        public void MissingOrInvalidMeasurementsDoNotGenerateFalseDiagnostics()
        {
            Assert.IsFalse(CityProceduralPlacementDiagnostics.TryEvaluate(
                Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, 0f, 0f, out _));
            Assert.IsFalse(CityProceduralPlacementDiagnostics.TryEvaluate(
                Vector3.one, Vector3.one, Vector3.zero, Vector3.zero, float.NaN, 0f, out _));
        }
    }
}
