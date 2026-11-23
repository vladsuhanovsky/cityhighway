using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralBuildingPrefabRulesTests
    {
        GameObject root;
        GameObject configObject;
        GameObject prefab;
        CityProceduralBuildingPrefabRules config;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test generated city");
            configObject = new GameObject("Test building rules");
            prefab = new GameObject("Test decorative building");
            config = configObject.AddComponent<CityProceduralBuildingPrefabRules>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(configObject);
            Object.DestroyImmediate(prefab);
        }

        void SetRule(float probability, int maxInstances, GameObject model)
        {
            var serialized = new SerializedObject(config);
            var rules = serialized.FindProperty("rules");
            rules.arraySize = 1;
            var rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("buildingKind").enumValueIndex =
                (int)CityShapes.Kind.Block;
            rule.FindPropertyRelative("visualPrefab").objectReferenceValue = model;
            rule.FindPropertyRelative("probability").floatValue = probability;
            rule.FindPropertyRelative("prefabScale").floatValue = 1f;
            rule.FindPropertyRelative("maxInstances").intValue = maxInstances;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static CityPlanner.Plan PlanWithBuildings(int count)
        {
            var plan = new CityPlanner.Plan();
            for (int i = 0; i < count; i++)
            {
                plan.Buildings.Add(new CityPlanner.Building
                {
                    Kind = CityShapes.Kind.Block,
                    Center = new Vector2(120f + i * 20f, 200f),
                    Width = 10f,
                    Depth = 12f,
                    Height = 0.1f,
                    Seed = i + 1
                });
                plan.Footprints.Add(new Vector4(i, 0f, 0f, 0f));
            }
            return plan;
        }

        [Test]
        public void SeedAndCoordinatesSelectDeterministically()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var position = new Vector2(seed * 7f, seed * 11f);
                bool first = CityProceduralBuildingPrefabSpawner.Selected(seed, position, 0.3f);
                bool again = CityProceduralBuildingPrefabSpawner.Selected(seed, position, 0.3f);
                Assert.AreEqual(first, again);
                Assert.IsTrue(CityProceduralBuildingPrefabSpawner.Selected(seed, position, 1f));
                Assert.IsFalse(CityProceduralBuildingPrefabSpawner.Selected(seed, position, 0f));
            }
        }

        [Test]
        public void UnconfiguredRulesNeverModifyPlan()
        {
            var plan = PlanWithBuildings(4);
            Assert.AreEqual(0, CityProceduralBuildingPrefabSpawner.Apply(
                plan, "Random city", config, root.transform));
            Assert.AreEqual(4, plan.Buildings.Count);
            Assert.AreEqual(4, plan.Footprints.Count);
            Assert.AreEqual(0, root.transform.childCount);
        }

        [Test]
        public void MatchingRuleRespectsCapAndKeepsFootprintsAligned()
        {
            SetRule(1f, 2, prefab);
            var plan = PlanWithBuildings(4);
            Assert.AreEqual(2, CityProceduralBuildingPrefabSpawner.Apply(
                plan, "Random Magnate 42", config, root.transform));
            Assert.AreEqual(2, plan.Buildings.Count);
            Assert.AreEqual(2, plan.Footprints.Count);
            Assert.AreEqual(2, root.transform.childCount);
            Assert.AreEqual(3, plan.Buildings[0].Seed);
            Assert.AreEqual(2f, plan.Footprints[0].x);
        }

        [Test]
        public void RulesCanRestrictCityWithoutDependingOnRandomSeed()
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("mapName").stringValue = "Special city";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetRule(1f, 5, prefab);
            var plan = PlanWithBuildings(3);
            Assert.AreEqual(0, CityProceduralBuildingPrefabSpawner.Apply(
                plan, "Another city", config, root.transform));
            Assert.AreEqual(3, plan.Buildings.Count);
            Assert.IsTrue(config.AppliesTo("Special city"));
            Assert.IsFalse(config.AppliesTo("Another city"));
        }

        [TestCase(7)]
        [TestCase(42)]
        public void RandomMagnateMapAcceptsRulesWithoutChangingLogicalCity(int seed)
        {
            var city = CityGenerator.Generate(seed);
            var plan = CityPlanner.Build(city.Map, 0.55f);
            Assert.Greater(plan.Buildings.Count, 0);
            int beforeBuildings = plan.Buildings.Count;
            int beforeRoads = city.Map.Roads.Count;
            int beforeLocations = city.Map.Locations.Count;

            var serialized = new SerializedObject(config);
            var rules = serialized.FindProperty("rules");
            rules.arraySize = 1;
            var rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("buildingKind").enumValueIndex =
                (int)plan.Buildings[0].Kind;
            rule.FindPropertyRelative("visualPrefab").objectReferenceValue = prefab;
            rule.FindPropertyRelative("probability").floatValue = 1f;
            rule.FindPropertyRelative("prefabScale").floatValue = 1f;
            rule.FindPropertyRelative("maxInstances").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            int applied = CityProceduralBuildingPrefabSpawner.Apply(
                plan, city.Map.Name, config, root.transform);
            Assert.AreEqual(1, applied);
            Assert.AreEqual(beforeBuildings - 1, plan.Buildings.Count);
            Assert.AreEqual(1, root.transform.childCount);
            Assert.AreEqual(beforeRoads, city.Map.Roads.Count);
            Assert.AreEqual(beforeLocations, city.Map.Locations.Count);
        }

        [Test]
        public void WrongBuildingKindNeverReplacesObjects()
        {
            SetRule(1f, 10, prefab);
            var plan = PlanWithBuildings(2);
            var different = plan.Buildings[0];
            different.Kind = CityShapes.Kind.Factory;
            plan.Buildings[0] = different;
            different = plan.Buildings[1];
            different.Kind = CityShapes.Kind.Factory;
            plan.Buildings[1] = different;
            Assert.AreEqual(0, CityProceduralBuildingPrefabSpawner.Apply(
                plan, "Random Magnate 7", config, root.transform));
            Assert.AreEqual(2, plan.Buildings.Count);
        }
    }
}
