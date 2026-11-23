using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralAnchorPlacementTests
    {
        [Test]
        public void FieldCenterConvertsToWorldOriginAndPreservesHeight()
        {
            var position = CityProceduralAnchorPlacement.WorldPosition(
                new Vector2(CityPlanner.FieldW * 0.5f, CityPlanner.FieldH * 0.5f), 0.42f);
            Assert.That(position.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(0.42f).Within(0.0001f));
        }

        [Test]
        public void PlaceMovesOnlyHorizontalCoordinatesByDefault()
        {
            var root = new GameObject("Placement test");
            try
            {
                var anchor = root.AddComponent<CityProceduralReplacement>();
                root.transform.position = new Vector3(2f, 0.31f, -2f);
                root.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                root.transform.localScale = new Vector3(2f, 1f, 3f);

                CityProceduralAnchorPlacement.Place(anchor,
                    new Vector2(CityPlanner.FieldW * 0.5f, CityPlanner.FieldH * 0.5f), 80f, false);

                Assert.That(root.transform.position.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(root.transform.position.z, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(root.transform.position.y, Is.EqualTo(0.31f).Within(0.0001f));
                Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(10f, 20f, 30f)),
                    Is.LessThan(0.001f));
                Assert.AreEqual(new Vector3(2f, 1f, 3f), root.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlaceOptionMatchesBuildingYawWithoutResettingScale()
        {
            var root = new GameObject("Rotation test");
            try
            {
                var anchor = root.AddComponent<CityProceduralReplacement>();
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = new Vector3(0.5f, 1.5f, 0.75f);
                CityProceduralAnchorPlacement.Place(anchor, Vector2.zero, 90f, true);
                Assert.That(Mathf.DeltaAngle(90f, root.transform.eulerAngles.y), Is.EqualTo(0f).Within(0.001f));
                Assert.AreEqual(new Vector3(0.5f, 1.5f, 0.75f), root.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
