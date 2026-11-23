using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Read-only snapshot of actual generated 3D mesh instances during Play Mode.
    /// District buildings and tree chunks are batched; individual trees/buildings have no meshes.
    /// </summary>
    public static class City3DGeometryAudit
    {
        public enum Group { Buildings, Trees, Replacements, Decorations, Other }

        public struct Entry
        {
            public string Name;
            public Group Kind;
            public int MeshCount;
            public long Triangles;
            public long Vertices;
        }

        public struct Totals
        {
            public int Meshes;
            public long Triangles;
            public long Vertices;
            public int Buildings;
            public int Trees;
            public int Other;
            public long BuildingTriangles;
            public long TreeTriangles;
            public long AuthoredTriangles;
            public long OtherTriangles;
        }

        public static Group Classify(string topLevelName)
        {
            if (string.IsNullOrEmpty(topLevelName)) return Group.Other;
            if (topLevelName.StartsWith("District ", StringComparison.Ordinal)) return Group.Buildings;
            if (topLevelName.StartsWith("Trees ", StringComparison.Ordinal)) return Group.Trees;
            if (topLevelName.EndsWith(" (replacement)", StringComparison.Ordinal)) return Group.Replacements;
            if (topLevelName.EndsWith(" (authored)", StringComparison.Ordinal)) return Group.Decorations;
            return Group.Other;
        }

        /// <summary>Enumerate existing MeshFilters and SkinnedMeshRenderers without mesh copying.</summary>
        public static Entry[] Capture(Transform generatedRoot, out Totals totals)
        {
            totals = default;
            if (generatedRoot == null) return new Entry[0];
            var groups = new Dictionary<Transform, Entry>();
            foreach (var filter in generatedRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Add(filter.transform, filter.sharedMesh, generatedRoot, groups);
            }
            foreach (var skin in generatedRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                Add(skin.transform, skin.sharedMesh, generatedRoot, groups);
            }
            var entries = new List<Entry>(groups.Values);
            entries.Sort((a, b) =>
            {
                int byTriangles = b.Triangles.CompareTo(a.Triangles);
                return byTriangles != 0 ? byTriangles : string.CompareOrdinal(a.Name, b.Name);
            });
            foreach (var item in entries)
            {
                totals.Meshes += item.MeshCount;
                totals.Triangles += item.Triangles;
                totals.Vertices += item.Vertices;
                switch (item.Kind)
                {
                    case Group.Buildings:
                        totals.Buildings++;
                        totals.BuildingTriangles += item.Triangles;
                        break;
                    case Group.Trees:
                        totals.Trees++;
                        totals.TreeTriangles += item.Triangles;
                        break;
                    case Group.Replacements:
                    case Group.Decorations:
                        totals.Other++;
                        totals.AuthoredTriangles += item.Triangles;
                        break;
                    default:
                        totals.Other++;
                        totals.OtherTriangles += item.Triangles;
                        break;
                }
            }
            return entries.ToArray();
        }

        static void Add(Transform transform, Mesh mesh, Transform root, Dictionary<Transform, Entry> groups)
        {
            var top = transform;
            while (top.parent != null && top.parent != root)
                top = top.parent;
            if (!groups.TryGetValue(top, out var entry))
                entry = new Entry { Name = top.name, Kind = Classify(top.name) };
            entry.MeshCount++;
            entry.Vertices += mesh.vertexCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    entry.Triangles += (long)mesh.GetIndexCount(i) / 3L;
            groups[top] = entry;
        }
    }
}
