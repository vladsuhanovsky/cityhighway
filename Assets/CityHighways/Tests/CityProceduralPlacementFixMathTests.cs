using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralPlacementFixMathTests
    {
        [Test]
        public void AlignHorizontalPreservesAuthoredY()
        {
            var aligned = CityProceduralPlacementFixMath.AlignHorizontal(
                new Vector3(3f, 0.42f, 2f), new Vector3(-1f, 9f, -2f));
            Assert.AreEqual(new Vector3(-1f, 0.42f, -2f), aligned);
        }

        [Test]
        public void RaiseAboveGroundUsesLowestMeshCornerRatherThanAnchorPivot()
        {
            Assert.IsTrue(CityProceduralPlacementFixMath.TryRaiseAboveGround(
                0.2f, -0.03f, 0.02f, out var correctedY));
            Assert.That(correctedY, Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void GroundCorrectionNeverLowersExistingModel()
        {
            Assert.IsTrue(CityProceduralPlacementFixMath.TryRaiseAboveGround(
                0.2f, 0.3f, 0.1f, out var correctedY));
            Assert.That(correctedY, Is.EqualTo(0.2f).Within(0.000001f));
        }

        [Test]
        public void InvalidGroundSampleIsRejectedWithoutChangingY()
        {
            Assert.IsFalse(CityProceduralPlacementFixMath.TryRaiseAboveGround(
                0.25f, 0f, float.NaN, out var correctedY));
            Assert.AreEqual(0.25f, correctedY);
            Assert.IsFalse(CityProceduralPlacementFixMath.TryRaiseAboveGround(
                0.25f, float.PositiveInfinity, 0f, out _));
        }
    }
}
