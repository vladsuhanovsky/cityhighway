using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralBatchFixMathTests
    {
        [Test]
        public void CombinedFixAlignsShrinksAndRaisesWithoutMutatingSource()
        {
            var before = new Vector3(2f, 0.1f, -1f);
            var originalScale = new Vector3(2f, 2f, 2f);
            Assert.IsTrue(CityProceduralBatchFixMath.TryPropose(
                before, originalScale, new Vector3(0f, 9f, 0f),
                0.5f, -0.3f, 0.2f, true, true, true,
                out var position, out var scale));
            Assert.That(position.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(position.z, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(position.y, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.AreEqual(Vector3.one, scale);
            Assert.AreEqual(new Vector3(2f, 0.1f, -1f), before);
        }

        [Test]
        public void BulkFitDoesNotEnlargeSmallerModels()
        {
            Assert.IsTrue(CityProceduralBatchFixMath.TryPropose(
                Vector3.zero, Vector3.one, Vector3.zero, 2f, 0f, 0f,
                false, true, false, out _, out var scale));
            Assert.AreEqual(Vector3.one, scale);
        }

        [Test]
        public void OffsetOnlyPreservesHeightAndScale()
        {
            Assert.IsTrue(CityProceduralBatchFixMath.TryPropose(
                new Vector3(2f, 1f, 2f), new Vector3(3f, 1f, 2f),
                new Vector3(-2f, 7f, 4f), 1f, 0f, 0f,
                true, false, false, out var position, out var scale));
            Assert.AreEqual(new Vector3(-2f, 1f, 4f), position);
            Assert.AreEqual(new Vector3(3f, 1f, 2f), scale);
        }

        [Test]
        public void BadNumbersRejectProposal()
        {
            Assert.IsFalse(CityProceduralBatchFixMath.TryPropose(
                Vector3.zero, Vector3.one, Vector3.zero, float.NaN, 0f, 0f,
                true, true, false, out _, out _));
            Assert.IsFalse(CityProceduralBatchFixMath.TryPropose(
                Vector3.zero, Vector3.one, Vector3.zero, 1f, float.NaN, 0f,
                true, false, true, out _, out _));
        }
    }
}
