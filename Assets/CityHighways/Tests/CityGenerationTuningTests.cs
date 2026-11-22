using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityHighways.Tests
{
    public sealed class CityGenerationTuningTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Tuning test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void DefaultsPreserveOriginalProceduralDensity()
        {
            var tuning = _root.AddComponent<CityGenerationTuning>();

            Assert.AreEqual(1f, tuning.BuildingDensity);
            Assert.AreEqual(1f, tuning.TreeDensity);
            Assert.IsTrue(tuning.Matches("Москва"));
            Assert.IsFalse(tuning.Matches("Амстердам"));
        }

        [Test]
        public void OverridesClampAndMatchOnlyExactActiveMap()
        {
            var tuning = _root.AddComponent<CityGenerationTuning>();
            var serialized = new SerializedObject(tuning);
            serialized.FindProperty("mapName").stringValue = "Чунцин";
            serialized.FindProperty("buildingDensity").floatValue = 10f;
            serialized.FindProperty("treeDensity").floatValue = -2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(1.5f, tuning.BuildingDensity);
            Assert.AreEqual(0.5f, tuning.TreeDensity);
            Assert.IsTrue(tuning.Matches("Чунцин"));
            Assert.IsFalse(tuning.Matches("Москва"));
            tuning.enabled = false;
            Assert.IsFalse(tuning.Matches("Чунцин"));
        }

        [Test]
        public void DuplicateConfigurationsFallBackToOriginal()
        {
            var tuning = _root.AddComponent<CityGenerationTuning>();
            var other = new GameObject("Duplicate tuning");
            try
            {
                other.AddComponent<CityGenerationTuning>();
                LogAssert.Expect(LogType.Warning,
                    "Multiple CityGenerationTuning components for Москва; ignoring overrides and using original density.");
                Assert.IsNull(CityGenerationTuning.FindForMap("Москва"));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
