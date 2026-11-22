using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Replaces one nearby procedural visual instance (building or tree) with an authored Prefab.
    /// Does not change CityMap, rules, roads, saves or replay data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityProceduralReplacement : MonoBehaviour
    {
        public enum TargetKind { Building, Tree }

        [SerializeField, Tooltip("Exact CityMap.Name for this replacement.")]
        string mapName = "Москва";

        [SerializeField] TargetKind target = TargetKind.Building;
        [SerializeField, Min(0.05f), Tooltip("Maximum horizontal distance to the procedural object's center, in Unity world units.")]
        float searchRadius = 0.5f;

        [SerializeField, Tooltip("Prefab spawned in place of the nearest matching procedural visual.")]
        GameObject visualPrefab;

        [SerializeField] bool showInGame = true;

        public TargetKind Target => target;
        public float SearchRadius => Mathf.Max(0.05f, searchRadius);
        public GameObject VisualPrefab => visualPrefab;
        public string MapName => mapName;

        public bool AppliesTo(string cityName) =>
            isActiveAndEnabled && showInGame && visualPrefab != null &&
            !string.IsNullOrEmpty(mapName) &&
            string.Equals(mapName, cityName, StringComparison.Ordinal);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.65f, 0.15f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, SearchRadius);
        }
#endif
    }
}
