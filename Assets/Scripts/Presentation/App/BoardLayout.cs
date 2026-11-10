using System.Collections.Generic;
using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation.App
{
    /// <summary>Геометрия поля: где стоят города, как лежат вагоны маршрутов, куда попал клик.</summary>
    public sealed class BoardLayout
    {
        public const float CityRadius = 11f;
        public const float TrackThickness = 12f;
        const float SegmentGap = 4f;
        const float ParallelOffset = 9f;
        const float Padding = 55f;

        public struct Segment
        {
            public Vector2 Center;
            public float Length;
            public float Angle;
        }

        readonly MapDefinition _map;
        readonly Dictionary<string, Vector2> _cityPositions = new Dictionary<string, Vector2>();
        Rect _area;

        public BoardLayout(MapDefinition map)
        {
            _map = map;
        }

        /// <summary>Пересчитывает позиции под размер области (дёшево, можно звать часто).</summary>
        public void Fit(Rect area)
        {
            if (area == _area && _cityPositions.Count > 0) return;
            _area = area;
            _cityPositions.Clear();
            foreach (var city in _map.Cities)
            {
                _cityPositions[city.Id] = new Vector2(
                    area.x + Padding + city.X * (area.width - 2f * Padding),
                    area.y + Padding + city.Y * (area.height - 2f * Padding));
            }
        }

        public Vector2 CityPosition(string cityId) =>
            _cityPositions.TryGetValue(cityId, out var position) ? position : Vector2.zero;

        /// <summary>Отрезок, вдоль которого лежат вагоны маршрута (без заходов в кружки городов).</summary>
        public void GetTrack(Route route, out Vector2 start, out Vector2 end)
        {
            var a = CityPosition(route.CityA);
            var b = CityPosition(route.CityB);

            // Упорядочиваем концы, чтобы оба пути двойного маршрута смещались в разные стороны.
            if (string.CompareOrdinal(route.CityA, route.CityB) > 0)
            {
                var tmp = a;
                a = b;
                b = tmp;
            }

            var direction = (b - a).normalized;
            if (route.ParallelRouteId != null)
            {
                var normal = new Vector2(-direction.y, direction.x);
                float side = string.CompareOrdinal(route.Id, route.ParallelRouteId) < 0 ? -1f : 1f;
                a += normal * ParallelOffset * side;
                b += normal * ParallelOffset * side;
            }

            start = a + direction * (CityRadius + 4f);
            end = b - direction * (CityRadius + 4f);
        }

        public List<Segment> GetSegments(Route route)
        {
            GetTrack(route, out var start, out var end);
            var delta = end - start;
            var direction = delta.normalized;
            int count = route.Length;
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

        /// <summary>Ближайший к точке маршрут, если она достаточно близко к нему.</summary>
        public Route RouteAt(Vector2 point)
        {
            if (_cityPositions.Count == 0) return null;
            Route best = null;
            float bestDistance = TrackThickness * 0.9f;
            foreach (var route in _map.Routes)
            {
                GetTrack(route, out var start, out var end);
                float distance = DistanceToSegment(point, start, end);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = route;
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
