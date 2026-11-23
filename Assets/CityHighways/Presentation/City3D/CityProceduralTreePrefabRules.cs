using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Optional seed-independent tree Prefab rules. Far-only card trees remain procedural.
    /// No component = unchanged tree generation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityProceduralTreePrefabRules : MonoBehaviour
    {
        [Serializable]
        public sealed class Rule
        {
            public CityShapes.Tree treeKind;
            public GameObject visualPrefab;
            [Range(0f, 1f)] public float probability = 0.02f;
            [Range(0.05f, 5f)] public float prefabScale = 1f;
            [Min(0)] public int maxInstances = 8;
        }

        [SerializeField, Tooltip("Empty = all cities, including randomly generated Logistic Magnate maps.")]
        string mapName = "";
        [SerializeField] Rule[] rules = new Rule[0];

        public Rule[] Rules => rules;

        public bool AppliesTo(string cityName) =>
            isActiveAndEnabled &&
            (string.IsNullOrEmpty(mapName) ||
             string.Equals(mapName, cityName, StringComparison.Ordinal));

        /// <summary>Ambiguous matching rule sets fall back to ordinary procedural trees.</summary>
        public static CityProceduralTreePrefabRules FindForMap(string cityName)
        {
            CityProceduralTreePrefabRules found = null;
            foreach (var candidate in FindObjectsByType<CityProceduralTreePrefabRules>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!candidate.AppliesTo(cityName)) continue;
                if (found != null)
                {
                    Debug.LogWarning("Multiple procedural tree Prefab rule sets for " +
                        cityName + "; preserving ordinary procedural trees.");
                    return null;
                }
                found = candidate;
            }
            return found;
        }
    }
}
