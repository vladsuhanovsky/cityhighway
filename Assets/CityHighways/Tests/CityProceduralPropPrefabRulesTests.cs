using System.Collections.Generic;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralPropPrefabRulesTests
    {
        GameObject root;
        GameObject rulesObject;
        GameObject visual;
        CityProceduralPropPrefabRules config;
        Dictionary<string, GameObject> districtVisuals;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Generated 3D city");
            rulesObject = new GameObject("Prop rules");
            visual = new GameObject("Test prop prefab");
            config = rulesObject.AddComponent<CityProceduralPropPrefabRules>();
            districtVisuals = new Dictionary<string, GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rulesObject);
            Object.DestroyImmediate(visual);
        }

        void Rule(CityPlanner.PropKind kind, float probability, int max)
        {
            var serialized = new SerializedObject(config);
            var rules = serialized.FindProperty("rules");
            rules.arraySize = 1;
            var item = rules.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("propKind").enumValueIndex = (int)kind;
            item.FindPropertyRelative("visualPrefab").objectReferenceValue = visual;
            item.FindPropertyRelative("probability").floatValue = probability;
            item.FindPropertyRelative("prefabScale").floatValue = 1f;
            item.FindPropertyRelative("maxInstances").intValue = max;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static CityPlanner.Plan MakePlan(int count, CityPlanner.PropKind kind,
            string district = "Center")
        {
            var plan = new CityPlanner.Plan();
            for (int i = 0; i < count; i++)
                plan.Props.Add(new CityPlanner.Prop
                {
                    At = new Vector2(100 + i * 12, 200),
                    Seed = i + 11,
                    Kind = kind,
                    District = district,
                    Size = 0.02f,
                    Angle = 30f
                });
            return plan;
        }

        int Apply(CityPlanner.Plan plan, string map = "Random city") =>
            CityProceduralPropPrefabSpawner.Apply(plan, map, config,
                root.transform, districtVisuals, 0.004f);

        [Test]
        public void WithoutRulesPlanAndSceneRemainUnchanged()
        {
            var plan = MakePlan(4, CityPlanner.PropKind.Lamp);
            Assert.AreEqual(0, Apply(plan));
            Assert.AreEqual(4, plan.Props.Count);
            Assert.AreEqual(0, root.transform.childCount);
        }

        [Test]
        public void SelectedPropsRespectCapAndDistrictVisibilityRegistration()
        {
            Rule(CityPlanner.PropKind.Lamp, 1f, 2);
            var plan = MakePlan(5, CityPlanner.PropKind.Lamp);
            Assert.AreEqual(2, Apply(plan));
            Assert.AreEqual(3, plan.Props.Count);
            Assert.AreEqual(1, root.transform.childCount);
            Assert.IsTrue(districtVisuals.ContainsKey("Center#ruleprops"));
            Assert.AreEqual(2, districtVisuals["Center#ruleprops"].transform.childCount);
            Assert.AreEqual(13, plan.Props[0].Seed);
        }

        [Test]
        public void DefaultDistrictDoesNotRegisterVisibilityOverride()
        {
            Rule(CityPlanner.PropKind.Bush, 1f, 1);
            var plan = MakePlan(2, CityPlanner.PropKind.Bush, null);
            Assert.AreEqual(1, Apply(plan));
            Assert.IsEmpty(districtVisuals);
        }

        [Test]
        public void ProbabilityZeroCapZeroAndOtherKindDoNotReplace()
        {
            var plan = MakePlan(3, CityPlanner.PropKind.Boat);
            Rule(CityPlanner.PropKind.Boat, 0f, 3);
            Assert.AreEqual(0, Apply(plan));
            Rule(CityPlanner.PropKind.Boat, 1f, 0);
            Assert.AreEqual(0, Apply(plan));
            Rule(CityPlanner.PropKind.Lamp, 1f, 3);
            Assert.AreEqual(0, Apply(plan));
            Assert.AreEqual(3, plan.Props.Count);
        }

        [Test]
        public void RestrictedCityIsNotTouched()
        {
            Rule(CityPlanner.PropKind.Lamp, 1f, 5);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("mapName").stringValue = "Москва";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var plan = MakePlan(4, CityPlanner.PropKind.Lamp);
            Assert.AreEqual(0, Apply(plan, "Магнат 42"));
            Assert.AreEqual(4, plan.Props.Count);
        }

        [Test]
        public void BoatUsesWaterLevelAndOriginalYaw()
        {
            Rule(CityPlanner.PropKind.Boat, 1f, 1);
            var plan = MakePlan(1, CityPlanner.PropKind.Boat, null);
            var prop = plan.Props[0];
            Assert.AreEqual(1, Apply(plan));
            var instance = root.transform.GetChild(0).GetChild(0);
            var expected = CityPlanner.ToWorld(prop.At, 0.003f);
            Assert.That(instance.position.y, Is.EqualTo(expected.y).Within(0.00001f));
            Assert.That(Mathf.DeltaAngle(instance.eulerAngles.y, 30f), Is.EqualTo(0f).Within(0.0001f));
        }

        [TestCase(7)]
        [TestCase(42)]
        public void MagnateRandomCitiesPreserveLogicalMapWithPropRules(int seed)
        {
            var city = CityGenerator.Generate(seed);
            var plan = CityPlanner.Build(city.Map, 0.8f);
            Assert.Greater(plan.Props.Count, 0);
            var kind = plan.Props[0].Kind;
            int original = plan.Props.Count;
            int roads = city.Map.Roads.Count;
            int locations = city.Map.Locations.Count;
            Rule(kind, 1f, 1);
            Assert.AreEqual(1, Apply(plan, city.Map.Name));
            Assert.AreEqual(original - 1, plan.Props.Count);
            Assert.AreEqual(roads, city.Map.Roads.Count);
            Assert.AreEqual(locations, city.Map.Locations.Count);
        }
    }
}
