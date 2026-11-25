using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Geometry for road details, sampled only from the final RoadBuilder piece plan.</summary>
    public static class RoadMarkingGeometry
    {
        public const float DashLength = 0.03f, DashGap = 0.025f, DashHalfWidth = 0.0018f, MarkLift = 0.0004f;

        public struct Quad
        {
            public Vector3 A, B, C, D;
        }

        public static Vector3 Across(BuiltRoad road)
        {
            var line = road.End - road.Start;
            return new Vector3(-line.z, 0f, line.x).normalized;
        }

        /// <summary>Surface height of this exact piece, including its ramp and the road's own slope.</summary>
        public static Vector3 SurfaceAt(BuiltRoad road, RoadPiece piece, float distance)
        {
            float t = piece.To - piece.From > 1e-5f ? Mathf.Clamp01((distance - piece.From) / (piece.To - piece.From)) : 0f;
            return road.PointAt(distance) + Vector3.up * Mathf.Lerp(piece.FromHeight, piece.ToHeight, t);
        }

        /// <summary>Emit one upward-facing strip per intersected piece. Never bridge a gap or a change of slope.</summary>
        public static IEnumerable<Quad> Strips(BuiltRoad road, float from, float to, float inner, float outer, float lift)
        {
            if (road == null || road.Length < 1e-5f || to <= from || outer <= inner) yield break;
            var across = Across(road);
            if (across.sqrMagnitude < 0.5f) yield break;
            var right = across * inner + Vector3.up * lift;
            var left = across * outer + Vector3.up * lift;
            foreach (var piece in road.Pieces)
            {
                float a = Mathf.Max(0f, Mathf.Max(from, piece.From));
                float b = Mathf.Min(road.Length, Mathf.Min(to, piece.To));
                if (b - a < 1e-5f) continue;
                var start = SurfaceAt(road, piece, a);
                var end = SurfaceAt(road, piece, b);
                yield return new Quad { A = start + left, B = end + left, C = end + right, D = start + right };
            }
        }

        /// <summary>Uniform dash pattern in horizontal world distance, inside the already-trimmed road.
        /// Bridges retain their original no-center-marking style, even if discovered by water detection.</summary>
        public static IEnumerable<Quad> LaneMarks(BuiltRoad road)
        {
            if (road == null || road.HasBridge) yield break;
            foreach (var piece in road.Pieces)
                if (piece.Kind != RoadPieceKind.Road) yield break;
            for (int i = 0; ; i++)
            {
                float from = DashGap + i * (DashLength + DashGap);
                float to = from + DashLength;
                if (to >= road.Length - DashGap * 0.5f) yield break;
                foreach (var quad in Strips(road, from, to, -DashHalfWidth, DashHalfWidth, MarkLift))
                    yield return quad;
            }
        }
    }
}
