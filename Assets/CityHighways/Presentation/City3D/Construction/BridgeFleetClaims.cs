using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Presentation-only owner baseline for existing map bridges. Only an observed
    /// unowned-to-owned transition after initial sync is eligible for the cosmetic effect.</summary>
    public sealed class BridgeFleetClaims
    {
        readonly Dictionary<string, int?> owners = new Dictionary<string, int?>();

        public bool Observe(string id, int? owner, bool animated, out bool changed)
        {
            bool known = owners.TryGetValue(id, out var previous);
            owners[id] = owner;
            changed = known && previous != owner;
            return animated && changed && !previous.HasValue && owner.HasValue;
        }

        public void Clear() => owners.Clear();

        /// <summary>Choose the midpoint of the longest usable bridge deck piece, not a ramp
        /// or the riverbed. Reject invalid or very short pieces rather than guess.</summary>
        public static bool TryDeckPosition(BuiltRoad road, out Vector3 position)
        {
            position = default;
            if (road == null || !road.HasBridge || road.Length < .12f ||
                float.IsNaN(road.Length) || float.IsInfinity(road.Length)) return false;
            float best = .12f;
            bool found = false;
            foreach (var piece in road.Pieces)
            {
                if (piece.Kind != RoadPieceKind.Bridge || piece.From < 0f || piece.To > road.Length ||
                    float.IsNaN(piece.From) || float.IsNaN(piece.To)) continue;
                float length = piece.To - piece.From;
                if (length < best || float.IsNaN(length)) continue;
                var point = road.PointAt((piece.From + piece.To) * .5f) +
                    Vector3.up * (piece.FromHeight * .5f + piece.ToHeight * .5f);
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
                    float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z)) continue;
                position = point;
                best = length;
                found = true;
            }
            return found;
        }
    }
}
