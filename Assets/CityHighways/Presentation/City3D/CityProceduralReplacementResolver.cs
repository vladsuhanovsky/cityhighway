using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Matches authored anchors to the nearest as-yet-unmatched procedural visual.
    /// Removes it from the render plan before procedural meshes are assembled.
    /// </summary>
    public static class CityProceduralReplacementResolver
    {
        /// <summary>
        /// Removes exactly one closest target within range. Pure plan operation; no scene writes.
        /// Failed matches leave the plan untouched. Buildings keep footprint indices synchronized.
        /// </summary>
        public static bool TryRemoveNearest(CityPlanner.Plan plan, CityProceduralReplacement.TargetKind target,
            Vector2 centerInFieldPixels, float searchRadiusInPixels)
        {
            if (plan == null || searchRadiusInPixels <= 0f) return false;
            float bestSquared = searchRadiusInPixels * searchRadiusInPixels;
            int bestIndex = -1;

            if (target == CityProceduralReplacement.TargetKind.Building)
            {
                for (int i = 0; i < plan.Buildings.Count; i++)
                {
                    float distance = (plan.Buildings[i].Center - centerInFieldPixels).sqrMagnitude;
                    if (distance >= bestSquared) continue;
                    bestSquared = distance;
                    bestIndex = i;
                }
                if (bestIndex < 0) return false;
                plan.Buildings.RemoveAt(bestIndex);
                // CityPlanner inserts the footprint for each inner-city building in the same order.
                // Outskirts buildings are appended afterwards and do not have footprints.
                if (bestIndex < plan.Footprints.Count) plan.Footprints.RemoveAt(bestIndex);
            }
            else if (target == CityProceduralReplacement.TargetKind.Tree)
            {
                for (int i = 0; i < plan.Trees.Count; i++)
                {
                    float distance = (plan.Trees[i].At - centerInFieldPixels).sqrMagnitude;
                    if (distance >= bestSquared) continue;
                    bestSquared = distance;
                    bestIndex = i;
                }
                if (bestIndex < 0) return false;
                plan.Trees.RemoveAt(bestIndex);
            }
            else return false;

            return true;
        }

        public static int Apply(CityPlanner.Plan plan, string cityName, Transform runtimeRoot)
        {
            if (plan == null || runtimeRoot == null) return 0;
            var anchors = new List<CityProceduralReplacement>(
                UnityEngine.Object.FindObjectsByType<CityProceduralReplacement>(FindObjectsInactive.Exclude));
            // Stable order for conflicting replacement markers, independent of Unity enumeration order.
            anchors.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
                if (c != 0) return c;
                c = a.transform.position.x.CompareTo(b.transform.position.x);
                if (c != 0) return c;
                return a.transform.position.z.CompareTo(b.transform.position.z);
            });

            int replaced = 0;
            foreach (var anchor in anchors)
            {
                if (!anchor.AppliesTo(cityName)) continue;
                Vector3 world = anchor.transform.position;
                var center = new Vector2(
                    (world.x / BoardLayout.WorldWidth + 0.5f) * CityPlanner.FieldW,
                    (0.5f - world.z / BoardLayout.WorldDepth) * CityPlanner.FieldH);

                if (!TryRemoveNearest(plan, anchor.Target, center,
                        anchor.SearchRadius * CityPlanner.PixelsPerUnit))
                {
                    Debug.LogWarning("City procedural replacement: no nearby " + anchor.Target +
                                     " for " + anchor.gameObject.name + " on " + cityName);
                    continue;
                }

                var instance = UnityEngine.Object.Instantiate(anchor.VisualPrefab, runtimeRoot);
                instance.name = anchor.gameObject.name + " (replacement)";
                instance.transform.SetPositionAndRotation(world, anchor.transform.rotation);
                instance.transform.localScale = anchor.transform.lossyScale;
                replaced++;
            }
            return replaced;
        }
    }
}
