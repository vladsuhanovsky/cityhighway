using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralTreePrefabRulesTests
    {
        GameObject root;
        GameObject rulesObject;
        GameObject visual;
        CityProceduralTreePrefabRules config;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Generated 3D city root");
            rulesObject = new GameObject("Tree rules");
            visual = new GameObject("Tree art");
            config = rulesObject.AddComponent<CityProceduralTreePrefabRules>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rulesObject);
            Object.DestroyImmediate(visual);
        }

        void Rule(CityShapes.Tree kind, float probability, int maximum)
        {
            var serialized = new SerializedObject(config);
            var array = serialized.FindProperty("rules");
            array.arraySize = 1;
            var entry = array.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("treeKind").enumValueIndex = (int)kind;
            entry.FindPropertyRelative("visualPrefab").objectReferenceValue = visual;
            entry.FindPropertyRelative("probability").floatValue = probability;
            entry.FindPropertyRelative("prefabScale").floatValue = 1f;
            entry.FindPropertyRelative("maxInstances").intValue = maximum;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static CityPlanner.Plan Trees(int count, int cardOnlyFrom)
        {
            var plan = new CityPlanner.Plan();
            for (int i = 0; i < count; i++)
                plan.Trees.Add(new CityPlanner.TreeSpot
                {
                    Kind = CityShapes.Tree.Round,
                    Seed = i + 13,
                    At = new Vector2(i * 12f + 100f, 200f),
                    Size = 0.12f,
                    CardOnly = i >= cardOnlyFrom
                });
            return plan;
        }

        [Test]
        public void MissingRulesLeaveAllTreesUnchanged()
        {
            var plan = Trees(5, 3);
            Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                plan, "Random", config, root.transform));
            Assert.AreEqual(5, plan.Trees.Count);
            Assert.AreEqual(0, root.transform.childCount);
        }

        [Test]
        public void CapsAndDistantBillboardsArePreserved()
        {
            Rule(CityShapes.Tree.Round, 1f, 2);
            var plan = Trees(5, 3);
            Assert.AreEqual(2, CityProceduralTreePrefabSpawner.Apply(
                plan, "Random Magnate 42", config, root.transform));
            Assert.AreEqual(3, plan.Trees.Count);
            Assert.AreEqual(2, root.transform.childCount);
            Assert.AreEqual(2, plan.Trees.FindAll(t => t.CardOnly).Count);
            Assert.AreEqual(15, plan.Trees[0].Seed);
        }

        [Test]
        public void FarOnlyTreesNeverSpawnPrefabs()
        {
            Rule(CityShapes.Tree.Round, 1f, 100);
            var plan = Trees(3, 0);
            Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                plan, "Random", config, root.transform));
            Assert.AreEqual(3, plan.Trees.Count);
        }

        [Test]
        public void WrongMapOrTreeKindIsNoOp()
        {
            Rule(CityShapes.Tree.Spruce, 1f, 3);
            var plan = Trees(3, 3);
            Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                plan, "Random", config, root.transform));
            var serialized = new SerializedObject(config);
            serialized.FindProperty("mapName").stringValue = "Only this city";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Rule(CityShapes.Tree.Round, 1f, 3);
            Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                plan, "Another city", config, root.transform));
            Assert.AreEqual(3, plan.Trees.Count);
        }

        [Test]
        public void ProbabilityAndZeroCapDisablePrefabSpawn()
        {
            Rule(CityShapes.Tree.Round, 0f, 10);
            var plan = Trees(3, 3);
            Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                plan, "Any", config, root.transform));
            Rule(CityShapes.Tree.Round, 1f, 0);
            Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                plan, "Any", config, root.transform));
        }

        [TestCase(7)]
        [TestCase(42)]
        public void RandomMagnateMapSupportsTreeRulesWithoutChangingLogicalMap(int seed)
        {
            var city = CityGenerator.Generate(seed);
            var plan = CityPlanner.Build(city.Map, 0.55f);
            Assert.Greater(plan.Trees.Count, 0);
            var kind = plan.Trees.Find(t => !t.CardOnly).Kind;
            Rule(kind, 1f, 1);
            var before = plan.Trees.Count;
            var roads = city.Map.Roads.Count;
            var locations = city.Map.Locations.Count;
            int replaced = CityProceduralTreePrefabSpawner.Apply(
                plan, city.Map.Name, config, root.transform);
            Assert.AreEqual(1, replaced);
            Assert.AreEqual(before - 1, plan.Trees.Count);
            Assert.AreEqual(roads, city.Map.Roads.Count);
            Assert.AreEqual(locations, city.Map.Locations.Count);
        }
    }
}
