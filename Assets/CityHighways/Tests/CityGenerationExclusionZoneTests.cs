using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityGenerationExclusionZoneTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Clearance test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void CircleBlocksOnlyEnabledVisualCategories()
        {
            var zone = new CityPlanner.ExclusionZone(new Vector2(100f, 200f), 50f, true, false);
            Assert.IsTrue(zone.Contains(new Vector2(110f, 200f), true));
            Assert.IsFalse(zone.Contains(new Vector2(110f, 200f), false));
            Assert.IsFalse(zone.Contains(new Vector2(200f, 200f), true));
            Assert.IsTrue(zone.Contains(new Vector2(160f, 200f), true, 15f));
        }

        [Test]
        public void EditorAnchorConvertsWorldCoordinatesAndSelectsCity()
        {
            var component = _go.AddComponent<CityGenerationExclusionZone>();
            _go.transform.position = Vector3.zero;

            var zone = component.ToPlannerZone();
            Assert.That(zone.Center.x, Is.EqualTo(CityPlanner.FieldW * 0.5f).Within(0.001f));
            Assert.That(zone.Center.y, Is.EqualTo(CityPlanner.FieldH * 0.5f).Within(0.001f));
            Assert.That(zone.Radius, Is.EqualTo(50f).Within(0.001f));
            Assert.IsTrue(component.AppliesTo("Москва"));
            Assert.IsFalse(component.AppliesTo("Амстердам"));
            component.enabled = false;
            Assert.IsFalse(component.AppliesTo("Москва"));
        }

        [Test]
        public void NoActiveCategoryProducesNoExclusion()
        {
            var component = _go.AddComponent<CityGenerationExclusionZone>();
            var data = new SerializedObject(component);
            data.FindProperty("excludeBuildings").boolValue = false;
            data.FindProperty("excludeTrees").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();

            var zone = component.ToPlannerZone();
            Assert.IsFalse(zone.Contains(zone.Center, true));
            Assert.IsFalse(zone.Contains(zone.Center, false));
        }
    }
}
