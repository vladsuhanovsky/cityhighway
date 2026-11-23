using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Verify the shared flat-map conversion used by the editor-only placement helper.</summary>
    public sealed class CityProceduralAnchorPlacementTests
    {
        [Test]
        public void FieldCenterMapsToWorldOriginAndPreservesHeight()
        {
            var position = CityPlanner.ToFlatWorld(
                new Vector2(CityPlanner.FieldW * 0.5f, CityPlanner.FieldH * 0.5f), 0.42f);
            Assert.That(position.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(0.42f).Within(0.0001f));
        }

        [Test]
        public void FieldCornersMapToWorldBoundsWithoutFlippingX()
        {
            var topLeft = CityPlanner.ToFlatWorld(Vector2.zero, 0.31f);
            var bottomRight = CityPlanner.ToFlatWorld(
                new Vector2(CityPlanner.FieldW, CityPlanner.FieldH), 0.31f);

            Assert.That(topLeft.x, Is.EqualTo(-BoardLayout.WorldWidth * 0.5f).Within(0.0001f));
            Assert.That(topLeft.z, Is.EqualTo(BoardLayout.WorldDepth * 0.5f).Within(0.0001f));
            Assert.That(bottomRight.x, Is.EqualTo(BoardLayout.WorldWidth * 0.5f).Within(0.0001f));
            Assert.That(bottomRight.z, Is.EqualTo(-BoardLayout.WorldDepth * 0.5f).Within(0.0001f));
            Assert.That(topLeft.y, Is.EqualTo(0.31f).Within(0.0001f));
            Assert.That(bottomRight.y, Is.EqualTo(0.31f).Within(0.0001f));
        }

        [Test]
        public void FlatCoordinateMappingDoesNotDependOnSceneTransforms()
        {
            var field = new Vector2(321f, 456f);
            var first = CityPlanner.ToFlatWorld(field, 0f);
            var second = CityPlanner.ToFlatWorld(field, 0.7f);
            Assert.That(first.x, Is.EqualTo(second.x).Within(0.0001f));
            Assert.That(first.z, Is.EqualTo(second.z).Within(0.0001f));
            Assert.That(second.y, Is.EqualTo(0.7f).Within(0.0001f));
        }
    }
}
