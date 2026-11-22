using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Editor-only map layout preview stored on a scene object. Never changes authoritative game state.
    /// The authoring tool writes marker positions back to the existing map JSON explicitly.
    /// </summary>
    [ExecuteAlways]
    public sealed class MapLayoutPreview : MonoBehaviour
    {
        [Serializable]
        public struct RoadLink
        {
            public string from;
            public string to;
        }

        [SerializeField] TextAsset sourceMap;
        [SerializeField] List<RoadLink> roads = new List<RoadLink>();

        public TextAsset SourceMap => sourceMap;

#if UNITY_EDITOR
        public void Configure(TextAsset source, List<RoadLink> links)
        {
            sourceMap = source;
            roads = links;
        }

        void OnDrawGizmos()
        {
            var markers = GetComponentsInChildren<MapLocationMarker>(true);
            var positions = new Dictionary<string, Vector3>();
            foreach (var marker in markers)
                if (!string.IsNullOrEmpty(marker.Id) && !positions.ContainsKey(marker.Id))
                    positions.Add(marker.Id, marker.transform.position);

            Gizmos.color = new Color(0.45f, 0.7f, 0.9f, 0.65f);
            foreach (var road in roads)
                if (positions.TryGetValue(road.from, out var a) && positions.TryGetValue(road.to, out var b))
                    Gizmos.DrawLine(a, b);
        }
#endif
    }

    /// <summary>One draggable visual marker, identified by the stable map location ID.</summary>
    public sealed class MapLocationMarker : MonoBehaviour
    {
        [SerializeField] string locationId;
        public string Id => locationId;

#if UNITY_EDITOR
        public void SetId(string id) => locationId = id;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.25f, 0.85f);
            Gizmos.DrawSphere(transform.position, 0.045f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.10f);
        }
#endif
    }
}
