using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralReplacementTests
    {
        [Test]
        public void RemovesNearestBuildingAndItsMatchingFootprint()
        {
            var plan = new CityPlanner.Plan();
            plan.Buildings.Add(new CityPlanner.Building { Center = new Vector2(100f, 200f), Seed = 1 });
            plan.Buildings.Add(new CityPlanner.Building { Center = new Vector2(120f, 200f), Seed = 2 });
            plan.Footprints.Add(new Vector4(1f, 0f, 0f, 0f));
            plan.Footprints.Add(new Vector4(2f, 0f, 0f, 0f));

            Assert.IsTrue(CityProceduralReplacementResolver.TryRemoveNearest(plan,
                CityProceduralReplacement.TargetKind.Building, new Vector2(119f, 200f), 10f));
            Assert.AreEqual(1, plan.Buildings.Count);
            Assert.AreEqual(1, plan.Buildings[0].Seed);
            Assert.AreEqual(1, plan.Footprints.Count);
            Assert.AreEqual(1f, plan.Footprints[0].x);
        }

        [Test]
        public void RemovesOnlyOneTreeAndNeverBuildings()
        {
            var plan = new CityPlanner.Plan();
            plan.Trees.Add(new CityPlanner.TreeSpot { At = new Vector2(5f, 10f), Seed = 1 });
            plan.Trees.Add(new CityPlanner.TreeSpot { At = new Vector2(6f, 10f), Seed = 2 });
            plan.Buildings.Add(new CityPlanner.Building { Center = new Vector2(5f, 10f), Seed = 3 });

            Assert.IsTrue(CityProceduralReplacementResolver.TryRemoveNearest(plan,
                CityProceduralReplacement.TargetKind.Tree, new Vector2(5f, 10f), 3f));
            Assert.AreEqual(1, plan.Trees.Count);
            Assert.AreEqual(2, plan.Trees[0].Seed);
            Assert.AreEqual(1, plan.Buildings.Count);
        }

        [Test]
        public void NoTargetInRadiusPreservesPlan()
        {
            var plan = new CityPlanner.Plan();
            plan.Buildings.Add(new CityPlanner.Building { Center = new Vector2(100f, 200f), Seed = 7 });
            Assert.IsFalse(CityProceduralReplacementResolver.TryRemoveNearest(plan,
                CityProceduralReplacement.TargetKind.Building, Vector2.zero, 5f));
            Assert.AreEqual(1, plan.Buildings.Count);
            Assert.IsFalse(CityProceduralReplacementResolver.TryRemoveNearest(null,
                CityProceduralReplacement.TargetKind.Building, Vector2.zero, 5f));
        }

        [Test]
        public void AnchorRequiresPrefabAndExactMap()
        {
            var go = new GameObject("Replacement anchor");
            var visual = new GameObject("Test visual");
            try
            {
                var anchor = go.AddComponent<CityProceduralReplacement>();
                Assert.IsFalse(anchor.AppliesTo("Москва"));

                var serialized = new SerializedObject(anchor);
                serialized.FindProperty("visualPrefab").objectReferenceValue = visual;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(anchor.AppliesTo("Москва"));
                Assert.IsFalse(anchor.AppliesTo("Амстердам"));
                anchor.enabled = false;
                Assert.IsFalse(anchor.AppliesTo("Москва"));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(visual);
            }
        }
    }
}
