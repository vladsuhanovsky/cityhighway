using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Optional bridge from scene-authored decorations to the runtime 3D city.
    /// Spawned objects live under the generated city root and are destroyed with it.
    /// </summary>
    internal static class SceneDecorationSpawner
    {
        internal static int Spawn(string mapName, Transform cityRoot)
        {
            if (cityRoot == null) return 0;

            var anchors = Object.FindObjectsByType<CitySceneDecoration>(FindObjectsInactive.Exclude);
            int count = 0;
            foreach (var anchor in anchors)
            {
                if (!anchor.isActiveAndEnabled || !anchor.AppliesTo(mapName)) continue;

                var visual = Object.Instantiate(anchor.VisualPrefab, cityRoot);
                visual.name = anchor.gameObject.name + " (authored)";
                // Scene View authored world coordinates are the source of truth.
                visual.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
                visual.transform.localScale = anchor.transform.lossyScale;
                count++;
            }
            return count;
        }
    }
}
