using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Рельеф 3D-города (полноценный 3D, этап 3): высота земли в любой точке мира. Пока 3D-сцены нет, рельеф
    /// выключен и земля плоская — плоская карта и логика игры его не видят.
    ///
    /// Через Relief.At проходят BoardLayout.World (станции, дороги, камера, клики) и CityPlanner.ToWorld (дома,
    /// деревья, фонари, декали). Поэтому всё, что ставилось на «землю на высоте 0», само встаёт на холмы.
    /// </summary>
    public static class Relief
    {
        static ReliefField _active;

        public static bool Active => _active != null;

        /// <summary>Высота земли (метры сцены) в точке мира (x, z). Без рельефа — 0.</summary>
        public static float At(float x, float z) => _active != null ? _active.Height(x, z) : 0f;

        public static void Use(ReliefField field) => _active = field;

        public static void Release(ReliefField field)
        {
            if (_active == field) _active = null;
        }
    }

    /// <summary>
    /// Поле высот одной карты.
    ///   • Холмы — шум Перлина в три октавы с зерном из названия карты: случайная карта магната — новый рельеф,
    ///     готовая карта — всегда один и тот же. Высоты до ~0,3 м (дома 0,04–0,15 м), склоны до ~10°.
    ///   • Реки текут по долинам: у воды земля опускается к нулю (ниже набережных), дальше плавно поднимается.
    ///   • Дороги — прямые от станции к станции, и земля вдоль них подрезается или подсыпается до высоты
    ///     дороги (полоса ±7,5 см ровно, дальше до 26 см — переход). У воды подсыпки нет — там мост.
    ///   • Далеко за полем рельеф сходит на нет к ровной равнине горизонта.
    /// Внутри поля высоты посчитаны заранее сеткой с шагом 3,5 см (быстро для каждого кадра), снаружи —
    /// на лету (только холмы и долины).
    /// </summary>
    public sealed class ReliefField
    {
        public const float Amplitude = 0.3f;
        const float Step = 0.035f;
        const float CorridorFlat = 0.075f, CorridorBlend = 0.26f;
        const float FarStart = 20f, FarEnd = 27f;

        readonly float _waterHalf, _valley;
        readonly Vector2 _noiseOffset;
        readonly List<(Vector2 a, Vector2 b, Vector2 min, Vector2 max)> _river = new List<(Vector2, Vector2, Vector2, Vector2)>();
        readonly float _x0, _z0;
        readonly int _nx, _nz;
        readonly float[] _grid;

        /// <param name="riverLines">Сглаженные русла в мире (как у воды сцены).</param>
        /// <param name="waterHalf">Полуширина воды с набережной: ближе к руслу земля — ноль.</param>
        public ReliefField(CityMap map, IReadOnlyList<List<Vector3>> riverLines, float waterHalf)
        {
            _waterHalf = waterHalf;
            _valley = 1.3f;
            unchecked
            {
                int seed = 17;
                foreach (char c in map.Name ?? "") seed = seed * 31 + c;
                // И расположение станций: у двух случайных городов с одним названием рельеф всё равно разный.
                foreach (var location in map.Locations) seed = seed * 31 + (int)(location.X * 997f) + (int)(location.Y * 991f) * 7;
                var random = new System.Random(seed);
                _noiseOffset = new Vector2((float)random.NextDouble() * 900f + 50f, (float)random.NextDouble() * 900f + 50f);
            }
            foreach (var line in riverLines)
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    var a = new Vector2(line[i].x, line[i].z);
                    var b = new Vector2(line[i + 1].x, line[i + 1].z);
                    _river.Add((a, b, Vector2.Min(a, b), Vector2.Max(a, b)));
                }

            // Высоты станций — от холмов и долин; дороги — прямые между ними.
            var stations = new Dictionary<string, (Vector2 p, float h)>();
            foreach (var location in map.Locations)
            {
                var w = Flat(location.X, location.Y);
                stations[location.Id] = (w, Hills(w.x, w.y));
            }
            var roads = new List<(Vector2 a, Vector2 b, float ha, float hb)>();
            foreach (var road in map.Roads)
                if (stations.TryGetValue(road.A, out var sa) && stations.TryGetValue(road.B, out var sb))
                    roads.Add((sa.p, sb.p, sa.h, sb.h));

            float margin = TerrainTexture.Margin;
            var min = Flat(-margin, 1f + margin);
            var max = Flat(1f + margin, -margin);
            _x0 = min.x;
            _z0 = min.y;
            _nx = Mathf.CeilToInt((max.x - min.x) / Step) + 1;
            _nz = Mathf.CeilToInt((max.y - min.y) / Step) + 1;
            _grid = new float[_nx * _nz];
            for (int iz = 0; iz < _nz; iz++)
            for (int ix = 0; ix < _nx; ix++)
            {
                var p = new Vector2(_x0 + ix * Step, _z0 + iz * Step);
                float h = Hills(p.x, p.y);
                // Ближайшая дорога: вдоль неё земля — на высоте дороги.
                float bestWeight = 0f, roadHeight = 0f;
                foreach (var (a, b, ha, hb) in roads)
                {
                    if (p.x < Mathf.Min(a.x, b.x) - CorridorBlend || p.x > Mathf.Max(a.x, b.x) + CorridorBlend ||
                        p.y < Mathf.Min(a.y, b.y) - CorridorBlend || p.y > Mathf.Max(a.y, b.y) + CorridorBlend) continue;
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    float d = (p - (a + ab * t)).magnitude;
                    if (d >= CorridorBlend) continue;
                    float weight = 1f - Smooth(CorridorFlat, CorridorBlend, d);
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        roadHeight = Mathf.Lerp(ha, hb, t);
                    }
                }
                if (bestWeight > 0f)
                {
                    // У самой воды не подсыпаем: там мост, а земля остаётся ниже набережной.
                    float river = RiverDistance(p, _waterHalf + 0.06f);
                    bestWeight *= Smooth(_waterHalf + 0.01f, _waterHalf + 0.06f, river);
                    h = Mathf.Lerp(h, roadHeight, bestWeight);
                }
                _grid[iz * _nx + ix] = h;
            }
        }

        static Vector2 Flat(float x, float y)
        {
            var w = new Vector3((x - 0.5f) * BoardLayout.WorldWidth, 0f, (0.5f - y) * BoardLayout.WorldDepth);
            return new Vector2(w.x, w.z);
        }

        static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / Mathf.Max(1e-6f, to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Расстояние до ближайшего русла, но не больше cutoff (дальние отрезки отсекаются по рамке).</summary>
        float RiverDistance(Vector2 p, float cutoff)
        {
            float best = cutoff * cutoff;
            foreach (var (a, b, min, max) in _river)
            {
                if (p.x < min.x - cutoff || p.x > max.x + cutoff || p.y < min.y - cutoff || p.y > max.y + cutoff) continue;
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                float d = (p - (a + ab * t)).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Холмы и речные долины без дорог (высота станций и земля за полем).</summary>
        float Hills(float x, float z)
        {
            float far = 1f - Smooth(FarStart, FarEnd, Mathf.Sqrt(x * x + z * z));
            if (far <= 0f) return 0f;
            float nx = x + _noiseOffset.x, nz = z + _noiseOffset.y;
            float n = Mathf.PerlinNoise(nx * 0.18f, nz * 0.18f)
                      + 0.4f * Mathf.PerlinNoise(nx * 0.42f + 3.1f, nz * 0.42f + 7.7f)
                      + 0.15f * Mathf.PerlinNoise(nx * 0.95f + 11.3f, nz * 0.95f + 1.9f);
            n /= 1.55f;
            // Широкие низины и отчётливые холмы, а не равномерная рябь.
            float hill = Smooth(0.28f, 0.82f, n);
            float valley = Smooth(_waterHalf, _waterHalf + _valley, RiverDistance(new Vector2(x, z), _waterHalf + _valley));
            return Amplitude * hill * valley * far;
        }

        /// <summary>Высота земли в точке мира: внутри поля — по сетке (билинейно), снаружи — на лету.</summary>
        public float Height(float x, float z)
        {
            float fx = (x - _x0) / Step, fz = (z - _z0) / Step;
            if (fx < 0f || fz < 0f || fx > _nx - 1.001f || fz > _nz - 1.001f) return Hills(x, z);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            int i = iz * _nx + ix;
            float a = Mathf.Lerp(_grid[i], _grid[i + 1], tx);
            float b = Mathf.Lerp(_grid[i + _nx], _grid[i + _nx + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }
    }
}
