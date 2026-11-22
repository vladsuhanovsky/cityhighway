using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CitySceneDecorationTests
    {
        GameObject _anchor;
        GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _anchor = new GameObject("Decoration anchor");
            _prefab = new GameObject("Visual prefab");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_anchor);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void DecorationOnlyMatchesItsExactMapName()
        {
            var component = _anchor.AddComponent<CitySceneDecoration>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("mapName").stringValue = "Москва";
            serialized.FindProperty("visualPrefab").objectReferenceValue = _prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(component.AppliesTo("Москва"));
            Assert.IsFalse(component.AppliesTo("Амстердам"));
            Assert.IsFalse(component.AppliesTo(null));
        }

        [Test]
        public void MissingPrefabOrDisabledAnchorIsIgnored()
        {
            var component = _anchor.AddComponent<CitySceneDecoration>();
            Assert.IsFalse(component.AppliesTo("Москва"));

            var serialized = new SerializedObject(component);
            serialized.FindProperty("visualPrefab").objectReferenceValue = _prefab;
            serialized.FindProperty("showInGame").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(component.AppliesTo("Москва"));
        }
    }
}
