using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Где у станции встаёт комплекс хаба (магнат). Раньше — в самом широком промежутке между дорогами этой
    /// станции, на фиксированном расстоянии; чужие дороги, реки и соседние станции не учитывались, и комплекс
    /// с бетонной плитой под ним часто ложился на дорогу. Теперь перебираются направления и расстояния, и
    /// выбирается место с запасом до всех дорог, рек, станций и уже поставленных хабов. Считается один раз при
    /// сборке сцены — до рельефа: под выбранным местом рельеф делается ровным, и плита больше не нужна.
    /// </summary>
    public static class HubPlacement
    {
        /// <summary>Пятно комплекса в его системе: X — поперёк, Z — от станции (−Z смотрит на станцию).</summary>
        public const float HalfX = 0.2f, ZMin = -0.13f, ZMax = 0.14f;
        /// <summary>Желательный запас от оси дороги до края пятна (полоса дороги и её ровная обочина).</summary>
        const float RoadClearance = 0.11f;
        const float StationClearance = 0.08f;
        /// <summary>Расстояния от станции до центра комплекса: сначала ближние, дальше — если рядом тесно.</summary>
        static readonly float[] Distances = { 0.25f, 0.29f, 0.33f, 0.37f, 0.41f, 0.45f };

        public struct Site
        {
            /// <summary>Центр комплекса в мире (x, z).</summary>
            public Vector2 Origin;
            /// <summary>Направление от станции к комплексу, радианы (atan2 по z, x).</summary>
            public float Direction;
            public float Yaw => -Direction * Mathf.Rad2Deg + 90f;
            public Vector2 Forward => new Vector2(Mathf.Cos(Direction), Mathf.Sin(Direction));
            public Vector2 Right => new Vector2(Mathf.Sin(Direction), -Mathf.Cos(Direction));

            /// <summary>Точка мира в системе комплекса (x — поперёк, y — вдоль от станции).</summary>
            public Vector2 ToLocal(Vector2 world)
            {
                var d = world - Origin;
                return new Vector2(Vector2.Dot(d, Right), Vector2.Dot(d, Forward));
            }

            /// <summary>Расстояние от точки мира до пятна (0 — внутри).</summary>
            public float DistanceTo(Vector2 world, float grow = 0f)
            {
                var l = ToLocal(world);
                float dx = Mathf.Max(0f, Mathf.Abs(l.x) - HalfX - grow);
                float dz = Mathf.Max(0f, Mathf.Max(ZMin - grow - l.y, l.y - ZMax - grow));
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            public IEnumerable<Vector2> Outline()
            {
                for (int i = 0; i <= 4; i++)
                {
                    float x = Mathf.Lerp(-HalfX, HalfX, i / 4f);
                    yield return Origin + Right * x + Forward * ZMin;
                    yield return Origin + Right * x + Forward * ZMax;
                }
                for (int i = 1; i < 4; i++)
                {
                    float z = Mathf.Lerp(ZMin, ZMax, i / 4f);
                    yield return Origin + Right * -HalfX + Forward * z;
                    yield return Origin + Right * HalfX + Forward * z;
                }
            }
        }

        public static Vector2 World(float x, float y) =>
            new Vector2((x - 0.5f) * BoardLayout.WorldWidth, (0.5f - y) * BoardLayout.WorldDepth);

        /// <summary>Места для всех хабов по порядку: каждый следующий обходит уже поставленные.</summary>
        /// <param name="riverHalf">Полуширина воды с набережной.</param>
        public static Dictionary<string, Site> Plan(CityMap map, IEnumerable<string> hubIds, float riverHalf)
        {
            var result = new Dictionary<string, Site>();
            if (hubIds == null) return result;
            var roads = new List<(Vector2 a, Vector2 b)>();
            foreach (var road in map.Roads)
            {
                var la = map.GetLocation(road.A);
                var lb = map.GetLocation(road.B);
                if (la != null && lb != null) roads.Add((World(la.X, la.Y), World(lb.X, lb.Y)));
            }
            var river = new List<(Vector2 a, Vector2 b)>();
            foreach (var line in map.Rivers)
                for (int i = 0; i + 1 < line.Count; i++)
                    river.Add((World(line[i].X, line[i].Y), World(line[i + 1].X, line[i + 1].Y)));
            foreach (var id in hubIds)
            {
                if (result.ContainsKey(id)) continue;
                var location = map.GetLocation(id);
                if (location == null) continue;
                result[id] = Find(map, location, roads, river, riverHalf, result.Values);
            }
            return result;
        }

        static Site Find(CityMap map, Location location, List<(Vector2 a, Vector2 b)> roads, List<(Vector2 a, Vector2 b)> river,
            float riverHalf, IEnumerable<Site> placed)
        {
            var center = World(location.X, location.Y);
            var others = new List<Site>(placed);
            float gap = WidestGap(map, location, center);
            Site best = default;
            float bestScore = float.MinValue;
            const int Directions = 48;
            foreach (float distance in Distances)
            for (int k = 0; k < Directions; k++)
            {
                float direction = gap + Mathf.PI * 2f * k / Directions;
                var site = new Site { Direction = direction };
                site.Origin = center + site.Forward * distance;
                float clearance = Clearance(site, map, location, roads, river, riverHalf, others);
                // Запас важнее всего (до нужного), потом — ближе к станции и ближе к самому широкому промежутку.
                float turn = Mathf.Abs(Mathf.DeltaAngle(0f, (direction - gap) * Mathf.Rad2Deg)) / 180f;
                float score = Mathf.Min(clearance, RoadClearance) * 100f - (distance - Distances[0]) * 6f - turn * 0.6f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = site;
                }
            }
            return best;
        }

        /// <summary>Наименьший запас до препятствий (отрицательный — пятно на препятствии).</summary>
        static float Clearance(Site site, CityMap map, Location own, List<(Vector2 a, Vector2 b)> roads,
            List<(Vector2 a, Vector2 b)> river, float riverHalf, List<Site> others)
        {
            float worst = float.MaxValue;
            foreach (var (a, b) in roads) worst = Mathf.Min(worst, SegmentDistance(site, a, b));
            foreach (var (a, b) in river) worst = Mathf.Min(worst, SegmentDistance(site, a, b) - riverHalf + RoadClearance - 0.05f);
            foreach (var location in map.Locations)
            {
                if (location.Id == own.Id) continue;
                worst = Mathf.Min(worst, site.DistanceTo(World(location.X, location.Y)) - StationClearance + RoadClearance);
            }
            foreach (var other in others)
            {
                foreach (var p in other.Outline()) worst = Mathf.Min(worst, site.DistanceTo(p) + RoadClearance - 0.06f);
                foreach (var p in site.Outline()) worst = Mathf.Min(worst, other.DistanceTo(p) + RoadClearance - 0.06f);
            }
            return worst;
        }

        /// <summary>Расстояние от отрезка до пятна: отрезок проходится с шагом 1 см там, где он рядом с комплексом.</summary>
        static float SegmentDistance(Site site, Vector2 a, Vector2 b)
        {
            const float Near = 0.7f;
            var ab = b - a;
            float length = ab.magnitude;
            if (length < 1e-5f) return site.DistanceTo(a);
            var dir = ab / length;
            // Ближайшая к центру точка отрезка; дальше 0,7 м + половина диагонали — заведомо далеко.
            float t0 = Mathf.Clamp(Vector2.Dot(site.Origin - a, dir), 0f, length);
            if ((a + dir * t0 - site.Origin).magnitude > Near) return Near;
            float from = Mathf.Max(0f, t0 - Near), to = Mathf.Min(length, t0 + Near);
            float best = float.MaxValue;
            for (float t = from; t <= to + 1e-4f; t += 0.01f)
                best = Mathf.Min(best, site.DistanceTo(a + dir * Mathf.Min(t, to)));
            return best;
        }

        static float WidestGap(CityMap map, Location location, Vector2 center)
        {
            var angles = new List<float>();
            foreach (var road in map.RoadsAt(location.Id))
            {
                var other = map.GetLocation(road.A == location.Id ? road.B : road.A);
                if (other == null) continue;
                var w = World(other.X, other.Y) - center;
                angles.Add(Mathf.Atan2(w.y, w.x));
            }
            if (angles.Count == 0) return 0f;
            angles.Sort();
            float direction = 0f, bestGap = -1f;
            for (int i = 0; i < angles.Count; i++)
            {
                float a = angles[i], b = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Mathf.PI * 2f;
                if (b - a > bestGap) { bestGap = b - a; direction = (a + b) / 2f; }
            }
            return direction;
        }
    }
}
