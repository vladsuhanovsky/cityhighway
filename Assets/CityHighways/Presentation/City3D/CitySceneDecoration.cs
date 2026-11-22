using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Artist-authored visual addition to a procedural city. Keep this component on an empty
    /// GameObject in the startup scene and position it in Scene View. It does not change roads,
    /// location IDs, contracts, or game state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CitySceneDecoration : MonoBehaviour
    {
        [SerializeField, Tooltip("Exact CityMap.Name (for example Москва or Амстердам).")]
        string mapName = "Москва";

        [SerializeField, Tooltip("Visual prefab rendered only while this city is active in 3D.")]
        GameObject visualPrefab;

        [SerializeField, Tooltip("Disable to keep the anchor in the scene without displaying it in game.")]
        bool showInGame = true;

        public string MapName => mapName;
        public GameObject VisualPrefab => visualPrefab;
        public bool ShowInGame => showInGame;

        public bool AppliesTo(string currentMapName)
        {
            return showInGame && visualPrefab != null
                && !string.IsNullOrEmpty(mapName)
                && string.Equals(mapName, currentMapName, StringComparison.Ordinal);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.65f, 0.75f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.15f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.22f);
        }
#endif
    }
}
