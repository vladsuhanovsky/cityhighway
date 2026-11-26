using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Optional scene-authored limits shared across all automatic Prefab rule categories.</summary>
    [DisallowMultipleComponent]
    public sealed class CityProceduralPrefabBudget : MonoBehaviour
    {
        [SerializeField, Tooltip("Empty applies to all cities including random Magnate seeds.")]
        string mapName = "";
        [SerializeField, Min(0)] int maxTrianglesPerPrefab = 12000;
        [SerializeField, Min(0)] int maxMeshesPerPrefab = 12;
        [SerializeField, Min(0)] int maxTotalTriangles = 120000;
        [SerializeField, Min(0)] int lowQualityInstances = 8;
        [SerializeField, Min(0)] int mediumQualityInstances = 24;
        [SerializeField, Min(0)] int highQualityInstances = 48;

        public bool AppliesTo(string cityName) =>
            isActiveAndEnabled && (string.IsNullOrEmpty(mapName) ||
                string.Equals(mapName, cityName, StringComparison.Ordinal));

        public static CityProceduralPrefabBudget FindForMap(string cityName)
        {
            CityProceduralPrefabBudget found = null;
            foreach (var candidate in FindObjectsByType<CityProceduralPrefabBudget>(
                FindObjectsInactive.Exclude))
            {
                if (!candidate.AppliesTo(cityName)) continue;
                if (found != null)
                {
                    Debug.LogWarning("Multiple procedural Prefab budgets for " + cityName +
                        "; using unlimited legacy rule behavior.");
                    return null;
                }
                found = candidate;
            }
            return found;
        }

        public Session Begin(int quality) => new Session(this, Mathf.Clamp(quality, 0, 2));

        /// <summary>Read-only mesh statistics from Prefab assets, without instantiation.</summary>
        public static void CountGeometry(GameObject prefab, out int meshes, out long triangles)
        {
            meshes = 0;
            triangles = 0;
            if (prefab == null) return;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) Accumulate(filter.sharedMesh, ref meshes, ref triangles);
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null) Accumulate(renderer.sharedMesh, ref meshes, ref triangles);
        }

        static void Accumulate(Mesh mesh, ref int meshes, ref long triangles)
        {
            meshes++;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    triangles += (long)mesh.GetIndexCount(i) / 3L;
        }

        public static bool Fits(long triangles, int meshes, int instances,
            long usedTriangles, int maxTrianglesPerPrefab, int maxMeshesPerPrefab,
            long maxTotalTriangles, int maxInstances)
        {
            if (triangles < 0 || meshes < 0 || instances < 0 || usedTriangles < 0)
                return false;
            return (maxTrianglesPerPrefab <= 0 || triangles <= maxTrianglesPerPrefab) &&
                   (maxMeshesPerPrefab <= 0 || meshes <= maxMeshesPerPrefab) &&
                   (maxTotalTriangles <= 0 ||
                    (usedTriangles <= maxTotalTriangles &&
                     triangles <= maxTotalTriangles - usedTriangles)) &&
                   instances < maxInstances;
        }

        /// <summary>
        /// A single shared session per generated city, across building, tree and prop rules.
        /// A rejected Prefab leaves the original procedural geometry untouched.
        /// </summary>
        public sealed class Session
        {
            readonly CityProceduralPrefabBudget settings;
            readonly int instanceLimit;
            readonly Dictionary<GameObject, (int meshes, long triangles)> cached =
                new Dictionary<GameObject, (int, long)>();
            readonly HashSet<GameObject> warned = new HashSet<GameObject>();
            public int Instances { get; private set; }
            public long Triangles { get; private set; }

            internal Session(CityProceduralPrefabBudget budget, int quality)
            {
                settings = budget;
                instanceLimit = quality == 0 ? Mathf.Max(0, budget.lowQualityInstances)
                    : quality == 1 ? Mathf.Max(0, budget.mediumQualityInstances)
                    : Mathf.Max(0, budget.highQualityInstances);
            }

            public bool TryReserve(GameObject prefab)
            {
                if (prefab == null) return false;
                if (!cached.TryGetValue(prefab, out var cost))
                {
                    CountGeometry(prefab, out int meshes, out long triangles);
                    cost = (meshes, triangles);
                    cached.Add(prefab, cost);
                }
                if (!Fits(cost.triangles, cost.meshes, Instances, Triangles,
                    settings.maxTrianglesPerPrefab, settings.maxMeshesPerPrefab,
                    settings.maxTotalTriangles, instanceLimit))
                {
                    if (warned.Add(prefab))
                        Debug.LogWarning("Procedural Prefab budget: skipping " + prefab.name +
                            " (meshes=" + cost.meshes + ", triangles=" + cost.triangles +
                            ", used instances=" + Instances + "/" + instanceLimit + ").");
                    return false;
                }
                Instances++;
                Triangles += cost.triangles;
                return true;
            }
        }
    }
}
