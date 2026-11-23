using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Optional visual Prefab rules for generated lamps, bushes and boats.</summary>
    [DisallowMultipleComponent]
    public sealed class CityProceduralPropPrefabRules : MonoBehaviour
    {
        [Serializable]
        public sealed class Rule
        {
            public CityPlanner.PropKind propKind;
            public GameObject visualPrefab;
            [Range(0f, 1f)] public float probability = 0.02f;
            [Range(0.05f, 5f)] public float prefabScale = 1f;
            [Min(0)] public int maxInstances = 8;
        }

        [SerializeField, Tooltip("Empty: applies to all cities, including random Logistic Magnate maps.")]
        string mapName = "";
        [SerializeField] Rule[] rules = new Rule[0];

        public Rule[] Rules => rules;

        public bool AppliesTo(string cityName) =>
            isActiveAndEnabled && (string.IsNullOrEmpty(mapName) ||
                string.Equals(mapName, cityName, StringComparison.Ordinal));

        public static CityProceduralPropPrefabRules FindForMap(string cityName)
        {
            CityProceduralPropPrefabRules found = null;
            foreach (var candidate in FindObjectsByType<CityProceduralPropPrefabRules>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!candidate.AppliesTo(cityName)) continue;
                if (found != null)
                {
                    Debug.LogWarning("Multiple procedural prop Prefab rule sets for " +
                        cityName + "; preserving ordinary procedural props.");
                    return null;
                }
                found = candidate;
            }
            return found;
        }
    }
}
