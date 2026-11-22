using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Optional per-city visual density overrides authored in the Unity startup scene.
    /// Only affects procedural buildings and trees; gameplay map data stays untouched.
    /// Defaults to 1x, preserving historical generation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityGenerationTuning : MonoBehaviour
    {
        [SerializeField, Tooltip("Exact CityMap.Name, e.g. Москва or Амстердам.")]
        string mapName = "Москва";

        [SerializeField, Range(0.5f, 1.5f), Tooltip("Additional building density multiplier. 1 = original behavior.")]
        float buildingDensity = 1f;

        [SerializeField, Range(0.5f, 1.5f), Tooltip("Additional tree density multiplier. 1 = original behavior.")]
        float treeDensity = 1f;

        public string MapName => mapName;
        public float BuildingDensity => Mathf.Clamp(buildingDensity, 0.5f, 1.5f);
        public float TreeDensity => Mathf.Clamp(treeDensity, 0.5f, 1.5f);

        public bool Matches(string cityName) =>
            isActiveAndEnabled && !string.IsNullOrEmpty(mapName) &&
            string.Equals(mapName, cityName, StringComparison.Ordinal);

        /// <summary>Find one active config for the current map; duplicate configs are ignored safely.</summary>
        public static CityGenerationTuning FindForMap(string cityName)
        {
            CityGenerationTuning found = null;
            foreach (var config in FindObjectsByType<CityGenerationTuning>(FindObjectsInactive.Exclude))
            {
                if (!config.Matches(cityName)) continue;
                if (found != null)
                {
                    Debug.LogWarning("Multiple CityGenerationTuning components for " + cityName +
                                     "; ignoring overrides and using original density.");
                    return null;
                }
                found = config;
            }
            return found;
        }
    }
}
