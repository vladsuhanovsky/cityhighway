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
        /// <summary>Snapshot of the last live 3D plan for editor ID selection. Not persisted.</summary>
        public struct CatalogEntry
        {
            public readonly string Id;
            public readonly CityProceduralReplacement.TargetKind Kind;
            public readonly Vector2 FieldPosition;

            public CatalogEntry(string id, CityProceduralReplacement.TargetKind kind, Vector2 position)
            {
                Id = id;
                Kind = kind;
                FieldPosition = position;
            }
        }

        static readonly List<CatalogEntry> Catalog = new List<CatalogEntry>();
        static string catalogMapName;

        // Quantize positions to one-thousandth of a field pixel. Identity is independent of
        // list order, density and runtime InstanceID, but does change if placement is edited.
        static string MakeId(char prefix, int seed, Vector2 position)
        {
            return prefix + ":" + seed + ":" +
                Mathf.RoundToInt(position.x * 1000f) + ":" +
                Mathf.RoundToInt(position.y * 1000f);
        }

        public static string BuildingId(CityPlanner.Building building) =>
            MakeId('B', building.Seed, building.Center);

        public static string TreeId(CityPlanner.TreeSpot tree) =>
            MakeId('T', tree.Seed, tree.At);

        public static void CaptureCatalog(CityPlanner.Plan plan, string cityName)
        {
            Catalog.Clear();
            catalogMapName = cityName;
            if (plan == null) return;
            foreach (var building in plan.Buildings)
                Catalog.Add(new CatalogEntry(BuildingId(building),
                    CityProceduralReplacement.TargetKind.Building, building.Center));
            foreach (var tree in plan.Trees)
                Catalog.Add(new CatalogEntry(TreeId(tree),
                    CityProceduralReplacement.TargetKind.Tree, tree.At));
        }

        public static void ClearCatalog()
        {
            Catalog.Clear();
            catalogMapName = null;
        }

        /// <summary>Pick ID in Play Mode, copy it, then paste it into the anchor after Play Mode.</summary>
        public static bool TryFindNearestId(string cityName, CityProceduralReplacement.TargetKind kind,
            Vector2 fieldPoint, float radiusPixels, out string objectId)
        {
            objectId = null;
            if (!string.Equals(catalogMapName, cityName, StringComparison.Ordinal) || radiusPixels <= 0f)
                return false;

            float best = radiusPixels * radiusPixels;
            foreach (var entry in Catalog)
            {
                if (entry.Kind != kind) continue;
                float distance = (entry.FieldPosition - fieldPoint).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                objectId = entry.Id;
            }
            return objectId != null;
        }

        static void RemoveBuilding(CityPlanner.Plan plan, int index)
        {
            plan.Buildings.RemoveAt(index);
            // CityPlanner appends outskirt buildings after inner-city footprint entries.
            if (index < plan.Footprints.Count) plan.Footprints.RemoveAt(index);
        }

        public static bool TryRemoveById(CityPlanner.Plan plan,
            CityProceduralReplacement.TargetKind target, string stableObjectId)
        {
            if (plan == null || string.IsNullOrEmpty(stableObjectId)) return false;
            if (target == CityProceduralReplacement.TargetKind.Building)
            {
                for (int i = 0; i < plan.Buildings.Count; i++)
                    if (string.Equals(BuildingId(plan.Buildings[i]), stableObjectId, StringComparison.Ordinal))
                    {
                        RemoveBuilding(plan, i);
                        return true;
                    }
            }
            else if (target == CityProceduralReplacement.TargetKind.Tree)
            {
                for (int i = 0; i < plan.Trees.Count; i++)
                    if (string.Equals(TreeId(plan.Trees[i]), stableObjectId, StringComparison.Ordinal))
                    {
                        plan.Trees.RemoveAt(i);
                        return true;
                    }
            }
            return false;
        }

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
                RemoveBuilding(plan, bestIndex);
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
            CaptureCatalog(plan, cityName);
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

                bool matched = !string.IsNullOrEmpty(anchor.StableObjectId)
                    ? TryRemoveById(plan, anchor.Target, anchor.StableObjectId)
                    : TryRemoveNearest(plan, anchor.Target, center,
                        anchor.SearchRadius * CityPlanner.PixelsPerUnit);
                if (!matched)
                {
                    Debug.LogWarning("City procedural replacement: no matching " + anchor.Target +
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
