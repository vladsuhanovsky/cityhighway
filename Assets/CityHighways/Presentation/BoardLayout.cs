using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Геометрия поля: где стоят места, как лежат участки дорог, куда попал клик.</summary>
    public sealed class BoardLayout
    {
        public const float LocationRadius = 9f;
        public const float RoadThickness = 11f;
        const float SegmentGap = 3f;
        const float Padding = 50f;

        public struct Segment
        {
            public Vector2 Center;
            public float Length;
            public float Angle;
        }

        readonly CityMap _map;
        readonly Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
        Rect _area;

        public BoardLayout(CityMap map)
        {
            _map = map;
        }

        public Rect Area => _area;

        public void Fit(Rect area)
        {
            if (area == _area && _positions.Count > 0) return;
            _area = area;
            _positions.Clear();
            foreach (var location in _map.Locations) _positions[location.Id] = ToScreen(location.X, location.Y);
        }

        /// <summary>Нормализованные координаты карты (0..1) → пиксели поля.</summary>
        public Vector2 ToScreen(float x, float y) => new Vector2(
            _area.x + Padding + x * (_area.width - 2f * Padding),
            _area.y + Padding + y * (_area.height - 2f * Padding));

        public Vector2 LocationPosition(string locationId) =>
            _positions.TryGetValue(locationId, out var p) ? p : Vector2.zero;

        public void GetTrack(Road road, out Vector2 start, out Vector2 end)
        {
            var a = LocationPosition(road.A);
            var b = LocationPosition(road.B);
            var direction = (b - a).normalized;
            start = a + direction * (LocationRadius + 3f);
            end = b - direction * (LocationRadius + 3f);
        }

        public List<Segment> GetSegments(Road road)
        {
            GetTrack(road, out var start, out var end);
            var delta = end - start;
            var direction = delta.normalized;
            int count = road.Length;
            float length = Mathf.Max(4f, (delta.magnitude - SegmentGap * (count - 1)) / count);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            var segments = new List<Segment>(count);
            for (int i = 0; i < count; i++)
            {
                segments.Add(new Segment
                {
                    Center = start + direction * (length / 2f + i * (length + SegmentGap)),
                    Length = length,
                    Angle = angle
                });
            }
            return segments;
        }

        public Road RoadAt(Vector2 point)
        {
            if (_positions.Count == 0) return null;
            Road best = null;
            float bestDistance = RoadThickness * 0.9f;
            foreach (var road in _map.Roads)
            {
                GetTrack(road, out var start, out var end);
                float distance = DistanceToSegment(point, start, end);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = road;
                }
            }
            return best;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 0.0001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
