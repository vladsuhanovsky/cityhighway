using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// An authored circular clearance zone for procedural visuals. This never modifies game
    /// locations, roads, rule data or serialized saves. Place anchors in Scene View on X/Z.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityGenerationExclusionZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Exact CityMap.Name. This zone applies to one city only.")]
        string mapName = "Москва";

        [SerializeField, Min(0.05f), Tooltip("Clearance radius in Unity world units.")]
        float radius = 0.5f;

        [SerializeField] bool excludeBuildings = true;
        [SerializeField] bool excludeTrees = true;

        public string MapName => mapName;
        public float Radius => Mathf.Max(0.05f, radius);
        public bool ExcludeBuildings => excludeBuildings;
        public bool ExcludeTrees => excludeTrees;

        public bool AppliesTo(string cityName) =>
            isActiveAndEnabled && !string.IsNullOrEmpty(mapName)
            && string.Equals(mapName, cityName, StringComparison.Ordinal);

        /// <summary>Convert scene-space X/Z into CityPlanner's field pixel coordinates.</summary>
        public CityPlanner.ExclusionZone ToPlannerZone()
        {
            Vector3 world = transform.position;
            return new CityPlanner.ExclusionZone(
                new Vector2(
                    (world.x / BoardLayout.WorldWidth + 0.5f) * CityPlanner.FieldW,
                    (0.5f - world.z / BoardLayout.WorldDepth) * CityPlanner.FieldH),
                Radius * CityPlanner.PixelsPerUnit, excludeBuildings, excludeTrees);
        }

        public static List<CityPlanner.ExclusionZone> Collect(string cityName)
        {
            var zones = new List<CityPlanner.ExclusionZone>();
            foreach (var zone in FindObjectsByType<CityGenerationExclusionZone>(FindObjectsInactive.Exclude))
                if (zone.AppliesTo(cityName) && (zone.ExcludeBuildings || zone.ExcludeTrees))
                    zones.Add(zone.ToPlannerZone());
            return zones;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.15f);
            Gizmos.DrawSphere(transform.position, Radius);
        }
#endif
    }
}
