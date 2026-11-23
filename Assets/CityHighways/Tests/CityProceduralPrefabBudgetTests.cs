using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityHighways.Tests
{
    public sealed class CityProceduralPrefabBudgetTests
    {
        GameObject configRoot;
        GameObject prefab;
        CityProceduralPrefabBudget budget;
        Mesh mesh;

        [SetUp]
        public void SetUp()
        {
            configRoot = new GameObject("Budget config");
            budget = configRoot.AddComponent<CityProceduralPrefabBudget>();
            prefab = new GameObject("Prefab model");
            mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            };
            prefab.AddComponent<MeshFilter>().sharedMesh = mesh;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(configRoot);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void CountsActualMeshTrianglesAndMeshInstances()
        {
            CityProceduralPrefabBudget.CountGeometry(prefab, out int meshes, out long triangles);
            Assert.AreEqual(1, meshes);
            Assert.AreEqual(1L, triangles);
        }

        [Test]
        public void PureBudgetChecksPerPrefabAndTotalLimits()
        {
            Assert.IsTrue(CityProceduralPrefabBudget.Fits(100, 2, 1, 100, 150, 3, 250, 2));
            Assert.IsFalse(CityProceduralPrefabBudget.Fits(151, 2, 1, 100, 150, 3, 250, 2));
            Assert.IsFalse(CityProceduralPrefabBudget.Fits(100, 4, 1, 100, 150, 3, 250, 2));
            Assert.IsFalse(CityProceduralPrefabBudget.Fits(100, 2, 2, 100, 150, 3, 250, 2));
            Assert.IsFalse(CityProceduralPrefabBudget.Fits(100, 2, 1, 151, 150, 3, 250, 2));
            Assert.IsTrue(CityProceduralPrefabBudget.Fits(100, 2, 1, 151, 0, 0, 0, 2));
        }

        [Test]
        public void QualityLevelsHaveDifferentSharedInstanceCaps()
        {
            var serialized = new SerializedObject(budget);
            serialized.FindProperty("lowQualityInstances").intValue = 1;
            serialized.FindProperty("mediumQualityInstances").intValue = 2;
            serialized.FindProperty("highQualityInstances").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var setting in new[] { (quality: 0, allowed: 1),
                                            (quality: 1, allowed: 2),
                                            (quality: 2, allowed: 3) })
            {
                var session = budget.Begin(setting.quality);
                for (int i = 0; i < setting.allowed; i++)
                    Assert.IsTrue(session.TryReserve(prefab));
                Assert.AreEqual(setting.allowed, session.Instances);
                Assert.AreEqual((long)setting.allowed, session.Triangles);
                // Exercise the pure check to avoid an expected runtime warning in Edit Mode.
                Assert.IsFalse(CityProceduralPrefabBudget.Fits(
                    1, 1, session.Instances, session.Triangles,
                    12000, 12, 120000, setting.allowed));
            }
        }

        [Test]
        public void OneBudgetIsSharedBetweenBuildingAndTreeReplacements()
        {
            var serialized = new SerializedObject(budget);
            serialized.FindProperty("lowQualityInstances").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var buildingObject = new GameObject("Building rules");
            var treeObject = new GameObject("Tree rules");
            var generatedRoot = new GameObject("Generated root");
            try
            {
                var buildingRules = buildingObject.AddComponent<CityProceduralBuildingPrefabRules>();
                var b = new SerializedObject(buildingRules);
                var bArray = b.FindProperty("rules");
                bArray.arraySize = 1;
                var bRule = bArray.GetArrayElementAtIndex(0);
                bRule.FindPropertyRelative("buildingKind").enumValueIndex = (int)CityShapes.Kind.Block;
                bRule.FindPropertyRelative("visualPrefab").objectReferenceValue = prefab;
                bRule.FindPropertyRelative("probability").floatValue = 1f;
                bRule.FindPropertyRelative("maxInstances").intValue = 2;
                b.ApplyModifiedPropertiesWithoutUndo();

                var treeRules = treeObject.AddComponent<CityProceduralTreePrefabRules>();
                var t = new SerializedObject(treeRules);
                var tArray = t.FindProperty("rules");
                tArray.arraySize = 1;
                var tRule = tArray.GetArrayElementAtIndex(0);
                tRule.FindPropertyRelative("treeKind").enumValueIndex = (int)CityShapes.Tree.Round;
                tRule.FindPropertyRelative("visualPrefab").objectReferenceValue = prefab;
                tRule.FindPropertyRelative("probability").floatValue = 1f;
                tRule.FindPropertyRelative("maxInstances").intValue = 2;
                t.ApplyModifiedPropertiesWithoutUndo();

                var plan = new CityPlanner.Plan();
                plan.Buildings.Add(new CityPlanner.Building
                {
                    Kind = CityShapes.Kind.Block, Width = 10f, Depth = 10f,
                    Height = 0.1f, Center = new Vector2(100f, 100f), Seed = 1
                });
                plan.Footprints.Add(Vector4.zero);
                plan.Trees.Add(new CityPlanner.TreeSpot
                {
                    Kind = CityShapes.Tree.Round, Size = 0.1f,
                    At = new Vector2(110f, 110f), Seed = 2
                });

                var session = budget.Begin(0);
                Assert.AreEqual(1, CityProceduralBuildingPrefabSpawner.Apply(
                    plan, "Random Magnate", buildingRules, generatedRoot.transform, session));
                LogAssert.Expect(LogType.Warning,
                    "Procedural Prefab budget: skipping Prefab model " +
                    "(meshes=1, triangles=1, used instances=1/1).");
                Assert.AreEqual(0, CityProceduralTreePrefabSpawner.Apply(
                    plan, "Random Magnate", treeRules, generatedRoot.transform, session));
                Assert.AreEqual(1, session.Instances);
                Assert.AreEqual(0, plan.Buildings.Count);
                Assert.AreEqual(1, plan.Trees.Count);
            }
            finally
            {
                Object.DestroyImmediate(buildingObject);
                Object.DestroyImmediate(treeObject);
                Object.DestroyImmediate(generatedRoot);
            }
        }

        [Test]
        public void DisabledOrOtherCityConfigDoesNotMatch()
        {
            var serialized = new SerializedObject(budget);
            serialized.FindProperty("mapName").stringValue = "Москва";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(budget.AppliesTo("Случайный город 42"));
            Assert.IsTrue(budget.AppliesTo("Москва"));
            budget.enabled = false;
            Assert.IsFalse(budget.AppliesTo("Москва"));
        }
    }
}
