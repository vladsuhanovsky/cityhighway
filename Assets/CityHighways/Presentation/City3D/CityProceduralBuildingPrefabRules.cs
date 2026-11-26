using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Optional seed-independent rules for procedurally placing building Prefabs.
    /// Add one enabled component to a scene; with no component existing visuals are unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityProceduralBuildingPrefabRules : MonoBehaviour
    {
        [Serializable]
        public sealed class Rule
        {
            public CityShapes.Kind buildingKind;
            public GameObject visualPrefab;
            [Range(0f, 1f)] public float probability = 0.05f;
            [Range(0.05f, 5f)] public float prefabScale = 1f;
            [Min(0)] public int maxInstances = 12;
        }

        [SerializeField, Tooltip("Leave empty to apply on every generated city, including random Magnate maps.")]
        string mapName = "";
        [SerializeField] Rule[] rules = new Rule[0];

        public Rule[] Rules => rules;
        public bool AppliesTo(string cityName) =>
            isActiveAndEnabled && (string.IsNullOrEmpty(mapName) ||
                string.Equals(mapName, cityName, StringComparison.Ordinal));

        /// <summary>Ambiguous configurations never change the procedural plan.</summary>
        public static CityProceduralBuildingPrefabRules FindForMap(string cityName)
        {
            CityProceduralBuildingPrefabRules found = null;
            foreach (var candidate in FindObjectsByType<CityProceduralBuildingPrefabRules>(
                FindObjectsInactive.Exclude))
            {
                if (!candidate.AppliesTo(cityName)) continue;
                if (found != null)
                {
                    Debug.LogWarning("Multiple procedural building Prefab rule sets for " +
                        cityName + "; preserving original procedural buildings.");
                    return null;
                }
                found = candidate;
            }
            return found;
        }
    }
}
