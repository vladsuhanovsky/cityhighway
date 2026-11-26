using System;
using System.Text.RegularExpressions;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class CityPrefabLookupCompatibilityTests
    {
        [TestCase(typeof(CityProceduralBuildingPrefabRules))]
        [TestCase(typeof(CityProceduralTreePrefabRules))]
        [TestCase(typeof(CityProceduralPropPrefabRules))]
        [TestCase(typeof(CityProceduralPrefabBudget))]
        public void LookupExcludesInactiveAndDisabledAndRejectsAmbiguousMatches(Type type)
        {
            string city = "Lookup regression " + Guid.NewGuid().ToString("N");
            var first = new GameObject("First lookup configuration");
            var second = new GameObject("Second lookup configuration");
            try
            {
                var a = Add(first, type, city);
                var b = Add(second, type, city);
                second.SetActive(false);
                Assert.AreSame(a, Find(type, city));
                second.SetActive(true);
                b.enabled = false;
                Assert.AreSame(a, Find(type, city));
                b.enabled = true;
                LogAssert.Expect(LogType.Warning, new Regex("Multiple procedural .*" + Regex.Escape(city)));
                Assert.IsNull(Find(type, city));
                first.SetActive(false);
                Assert.AreSame(b, Find(type, city));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        static Behaviour Add(GameObject owner, Type type, string city)
        {
            var component = (Behaviour)owner.AddComponent(type);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("mapName").stringValue = city;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        static Behaviour Find(Type type, string city)
        {
            if (type == typeof(CityProceduralBuildingPrefabRules)) return CityProceduralBuildingPrefabRules.FindForMap(city);
            if (type == typeof(CityProceduralTreePrefabRules)) return CityProceduralTreePrefabRules.FindForMap(city);
            if (type == typeof(CityProceduralPropPrefabRules)) return CityProceduralPropPrefabRules.FindForMap(city);
            if (type == typeof(CityProceduralPrefabBudget)) return CityProceduralPrefabBudget.FindForMap(city);
            throw new ArgumentException("Unknown configuration type.");
        }
    }
}
