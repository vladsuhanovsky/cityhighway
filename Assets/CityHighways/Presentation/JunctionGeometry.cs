using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Геометрия перекрёстка на станции (на плоскости: X/Z мира для 3D или пиксели экрана для плоской карты).
    /// Перекрёсток = круглый «пятачок» по ширине дороги + «рукава» по направлениям дорог + скругления бордюра
    /// между соседними рукавами (дуга от кромки одной дороги к кромке другой), поэтому дороги вливаются
    /// в станцию плавно, без острых углов.
    /// </summary>
    public static class JunctionGeometry
    {
        /// <summary>Скругление между двумя соседними рукавами: угол X, точки касания T1 (рукав i) и T2 (рукав i+1).</summary>
        public struct Corner
        {
            public Vector2 X, T1, T2;
        }

        static readonly List<(float angle, Vector2 dir)> Sorted = new List<(float, Vector2)>();

        /// <summary>
        /// Направления от центра к соседям, по возрастанию угла; почти совпадающие (ближе mergeDegrees) — одно.
        /// </summary>
        public static void Directions(Vector2 center, List<Vector2> neighbours, List<Vector2> result, float mergeDegrees = 6f)
        {
            result.Clear();
            Sorted.Clear();
            foreach (var other in neighbours)
            {
                var d = other - center;
                if (d.sqrMagnitude < 1e-10f) continue;
                d.Normalize();
                Sorted.Add((Mathf.Atan2(d.y, d.x), d));
            }
            Sorted.Sort((a, b) => a.angle.CompareTo(b.angle));
            foreach (var item in Sorted)
                if (result.Count == 0 || Vector2.Angle(result[result.Count - 1], item.dir) > mergeDegrees) result.Add(item.dir);
            if (result.Count > 1 && Vector2.Angle(result[0], result[result.Count - 1]) <= mergeDegrees) result.RemoveAt(result.Count - 1);
        }

        /// <summary>
        /// Скругления между соседними рукавами (направления — по возрастанию угла). Кромка рукава i, обращённая
        /// к рукаву i+1, и кромка рукава i+1, обращённая к i, пересекаются в X — там острый внутренний угол;
        /// его скругляем дугой радиуса fillet (меньше, если рукав короче). Почти прямые проезды (&gt;165°)
        /// и почти слившиеся дороги (&lt;15°) скругления не требуют.
        /// </summary>
        public static void Corners(Vector2 center, List<Vector2> dirs, float halfWidth, float arm, float fillet, List<Corner> corners)
        {
            corners.Clear();
            int count = dirs.Count;
            if (count < 2) return;
            for (int i = 0; i < count; i++)
            {
                var di = dirs[i];
                var dj = dirs[(i + 1) % count];
                float gap = Mathf.Repeat(Mathf.Atan2(dj.y, dj.x) - Mathf.Atan2(di.y, di.x), Mathf.PI * 2f) * Mathf.Rad2Deg;
                if (gap < 15f || gap > 165f) continue;
                var ni = new Vector2(-di.y, di.x);   // к следующему рукаву (против часовой)
                var nj = new Vector2(-dj.y, dj.x);
                var p = center + ni * halfWidth;     // кромка рукава i со стороны j
                var q = center - nj * halfWidth;     // кромка рукава j со стороны i
                float denominator = Cross(di, dj);
                if (Mathf.Abs(denominator) < 1e-5f) continue;
                float s = Cross(q - p, dj) / denominator;
                float u = Cross(q - p, di) / denominator;
                if (s < 0f || u < 0f) continue;
                float t = Mathf.Min(fillet, arm - s, arm - u);
                if (t < fillet * 0.15f) continue;
                var x = p + di * s;
                corners.Add(new Corner { X = x, T1 = x + di * t, T2 = x + dj * t });
            }
        }

        /// <summary>Точка дуги скругления (квадратичная кривая Безье T1 → X → T2, почти совпадает с дугой окружности).</summary>
        public static Vector2 Curve(Corner corner, float t)
        {
            float a = 1f - t;
            return a * a * corner.T1 + 2f * a * t * corner.X + t * t * corner.T2;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
