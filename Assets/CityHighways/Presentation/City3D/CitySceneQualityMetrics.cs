using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Pure approximate analysis of authored 3D visual bounds.
    /// Does not inspect runtime generated meshes or issue actual physics queries.
    /// </summary>
    public static class CitySceneQualityMetrics
    {
        public struct Record
        {
            public Bounds WorldBounds;
            public int Meshes;
            public long Triangles;
        }

        public struct Summary
        {
            public int Visuals;
            public int Meshes;
            public long Triangles;
            public float FootprintArea;
            public float AreaDensity;
            public int PossibleOverlaps;
        }

        public static bool TryAnalyze(IReadOnlyList<Record> records, float areaWidth, float areaDepth,
            float minimumOverlap, out Summary result)
        {
            result = default;
            if (records == null || !Finite(areaWidth) || !Finite(areaDepth) ||
                areaWidth <= 0f || areaDepth <= 0f || !Finite(minimumOverlap) ||
                minimumOverlap < 0f) return false;

            float area = areaWidth * areaDepth;
            if (!Finite(area) || area <= 0f) return false;

            result.Visuals = records.Count;
            result.FootprintArea = area;
            result.AreaDensity = records.Count / area;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Meshes < 0 || records[i].Triangles < 0 ||
                    !Valid(records[i].WorldBounds)) return false;
                result.Meshes += records[i].Meshes;
                result.Triangles += records[i].Triangles;
            }

            // Sweep sorted X bounds: avoids testing pairs that cannot overlap.
            var sorted = new List<Bounds>(records.Count);
            foreach (var record in records) sorted.Add(record.WorldBounds);
            sorted.Sort((a, b) => a.min.x.CompareTo(b.min.x));
            for (int i = 0; i < sorted.Count; i++)
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (sorted[j].min.x >= sorted[i].max.x - minimumOverlap) break;
                    if (PotentialIntersection(sorted[i], sorted[j], minimumOverlap))
                        result.PossibleOverlaps++;
                }
            return Finite(result.AreaDensity);
        }

        /// <summary>Strict X/Y/Z AABB intersection; touch-only volumes do not count.</summary>
        public static bool PotentialIntersection(Bounds first, Bounds second, float minOverlap)
        {
            if (!Valid(first) || !Valid(second) || !Finite(minOverlap) || minOverlap < 0f)
                return false;
            return Mathf.Min(first.max.x, second.max.x) -
                   Mathf.Max(first.min.x, second.min.x) > minOverlap &&
                   Mathf.Min(first.max.y, second.max.y) -
                   Mathf.Max(first.min.y, second.min.y) > minOverlap &&
                   Mathf.Min(first.max.z, second.max.z) -
                   Mathf.Max(first.min.z, second.min.z) > minOverlap;
        }

        static bool Valid(Bounds bounds) =>
            Finite(bounds.center.x) && Finite(bounds.center.y) && Finite(bounds.center.z) &&
            Finite(bounds.size.x) && Finite(bounds.size.y) && Finite(bounds.size.z) &&
            bounds.size.x > 0f && bounds.size.y > 0f && bounds.size.z > 0f;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
