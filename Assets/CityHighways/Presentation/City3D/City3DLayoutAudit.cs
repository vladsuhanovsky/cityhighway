using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public sealed class City3DLayoutIssue
    {
        public string code, id;
        public float measured, limit;
    }

    [Serializable]
    public sealed class City3DHubFootprint
    {
        public string id;
        public PointDto[] corners;
    }

    [Serializable]
    public sealed class City3DLayoutReport
    {
        public float waterHalfWidth, bankHalfWidth, roadHalfWidth;
        public RiverDto[] preparedRivers;
        public City3DHubFootprint[] hubFootprints;
        public City3DLayoutIssue[] warnings;
    }

    /// <summary>XZ footprint diagnostics, not a mesh or rendered-image assertion.
    /// Uses the renderer's prepared lines, junction directions, crosswalk bounds
    /// and hub placement algorithm. No scene creation or mutation.</summary>
    public static class City3DLayoutAudit
    {
        static Vector2 Xz(Vector3 p) => new Vector2(p.x, p.z);
        static PointDto MapPoint(Vector2 p) => new PointDto
        {
            x = p.x / BoardLayout.WorldWidth + 0.5f,
            y = 0.5f - p.y / BoardLayout.WorldDepth
        };

        public static City3DLayoutReport Capture(CityMap map, IEnumerable<string> hubIds)
        {
            var lines = City3DScene.PreparedRiverLines(map);
            var segments = new List<(Vector2 a, Vector2 b)>();
            foreach (var line in lines)
                for (int i = 1; i < line.Count; i++) segments.Add((Xz(line[i - 1]), Xz(line[i])));

            float water = City3DScene.RiverWaterHalfWidth, bank = City3DScene.RiverBankHalfWidth;
            float halfRoad = City3DScene.RoadWidth / 2f;
            var warnings = new List<City3DLayoutIssue>();
            void Warn(string code, string id, float measured, float limit) =>
                warnings.Add(new City3DLayoutIssue { code = code, id = id, measured = measured, limit = limit });

            var sites = HubPlacement.Plan(map, hubIds, bank);
            var footprints = new List<City3DHubFootprint>();
            foreach (var pair in sites.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var site = pair.Value;
                var corners = new[] {
                    site.Origin - site.Right * HubPlacement.HalfX + site.Forward * HubPlacement.ZMin,
                    site.Origin + site.Right * HubPlacement.HalfX + site.Forward * HubPlacement.ZMin,
                    site.Origin + site.Right * HubPlacement.HalfX + site.Forward * HubPlacement.ZMax,
                    site.Origin - site.Right * HubPlacement.HalfX + site.Forward * HubPlacement.ZMax };
                footprints.Add(new City3DHubFootprint { id = pair.Key, corners = corners.Select(MapPoint).ToArray() });
                float gap = float.PositiveInfinity;
                foreach (var seg in segments)
                    gap = Mathf.Min(gap, PolygonSegmentDistance(corners, seg.a, seg.b));
                if (gap < bank) Warn("hub-bank-overlap", pair.Key, gap, bank);
                var location = map.GetLocation(pair.Key);
                Vector2 marker = HubPlacement.World(location.X, location.Y);
                float markerGap = float.PositiveInfinity;
                foreach (var seg in segments) markerGap = Mathf.Min(markerGap, PointSegmentDistance(marker, seg.a, seg.b));
                if (markerGap < bank) Warn("hub-marker-bank-overlap", pair.Key, markerGap, bank);
            }

            foreach (var road in map.Roads)
            {
                var a = map.GetLocation(road.A); var b = map.GetLocation(road.B);
                if (a == null || b == null) continue;
                Vector2 from = HubPlacement.World(a.X, a.Y), to = HubPlacement.World(b.X, b.Y);
                float gap = float.PositiveInfinity;
                foreach (var seg in segments) gap = Mathf.Min(gap, SegmentDistance(from, to, seg.a, seg.b));
                if (!road.IsBridge && gap < water + halfRoad)
                    Warn("road-water-clearance", road.Id, gap, water + halfRoad);
                if (road.IsBridge && (segments.Count == 0 || gap > water))
                    Warn("bridge-without-water", road.Id, segments.Count == 0 ? -1f : gap, water);
                float length = Vector2.Distance(from, to);
                if (length < 2f * BoardLayout.JunctionArm)
                    Warn("short-road-junction-overlap", road.Id, length, 2f * BoardLayout.JunctionArm);
            }

            var neighbors = new List<Vector2>();
            var directions = new List<Vector2>();
            foreach (var location in map.Locations)
            {
                Vector2 center = HubPlacement.World(location.X, location.Y);
                neighbors.Clear();
                foreach (var road in map.RoadsAt(location.Id))
                {
                    var other = map.GetLocation(road.OtherEnd(location.Id));
                    if (other != null) neighbors.Add(HubPlacement.World(other.X, other.Y));
                }
                JunctionGeometry.Directions(center, neighbors, directions);
                for (int i = 0; i < directions.Count; i++)
                    for (int j = i + 1; j < directions.Count; j++)
                        if (CrosswalkEnvelopesOverlap(directions[i], directions[j], halfRoad,
                            City3DScene.CrosswalkNear, City3DScene.CrosswalkFar))
                            Warn("crosswalk-envelope-overlap", location.Id, Vector2.Angle(directions[i], directions[j]), 0f);
            }

            return new City3DLayoutReport { waterHalfWidth = water, bankHalfWidth = bank, roadHalfWidth = halfRoad,
                preparedRivers = lines.Select(l => new RiverDto { points = l.Select(p => MapPoint(Xz(p))).ToArray() }).ToArray(),
                hubFootprints = footprints.ToArray(), warnings = warnings.ToArray() };
        }

        public static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a;
            float t = d.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
            return Vector2.Distance(p, a + d * t);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 u = b - a, v = d - c;
            float denominator = Cross(u, v);
            if (Mathf.Abs(denominator) > 1e-10f)
            {
                float t = Cross(c - a, v) / denominator, s = Cross(c - a, u) / denominator;
                if (t >= 0f && t <= 1f && s >= 0f && s <= 1f) return 0f;
            }
            return Mathf.Min(Mathf.Min(PointSegmentDistance(a, c, d), PointSegmentDistance(b, c, d)),
                Mathf.Min(PointSegmentDistance(c, a, b), PointSegmentDistance(d, a, b)));
        }

        static bool Contains(Vector2[] corners, Vector2 p)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < corners.Length; i++)
            {
                float c = Cross(corners[(i + 1) % corners.Length] - corners[i], p - corners[i]);
                pos |= c > 1e-7f; neg |= c < -1e-7f;
            }
            return !(pos && neg);
        }

        public static float PolygonSegmentDistance(Vector2[] corners, Vector2 a, Vector2 b)
        {
            if (Contains(corners, a) || Contains(corners, b)) return 0f;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < corners.Length; i++)
                distance = Mathf.Min(distance, SegmentDistance(a, b, corners[i], corners[(i + 1) % corners.Length]));
            return distance;
        }

        public static bool CrosswalkEnvelopesOverlap(Vector2 a, Vector2 b, float halfWidth, float near, float far) =>
            JunctionCrosswalkLayout.Overlap(a, b, halfWidth, near, far);

    }
}
