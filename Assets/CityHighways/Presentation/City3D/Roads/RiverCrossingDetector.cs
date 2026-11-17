using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Участок дороги над водой: расстояния от начала дороги (по горизонтали), From &lt; To.</summary>
    public struct WaterSpan
    {
        public float From, To;
        public float Length => To - From;
        public WaterSpan(float from, float to) { From = from; To = to; }
    }

    /// <summary>
    /// Ищет, где отрезок дороги проходит над рекой. Вдоль отрезка с шагом step проверяется «вода ли здесь»
    /// (луч вниз по LayerMask воды и/или расстояние до линии реки), а каждая граница вода/берег затем
    /// уточняется делением пополам до долей миллиметра — мост начинается ровно у кромки, без зазоров.
    /// </summary>
    public sealed class RiverCrossingDetector
    {
        readonly RiverDetection _mode;
        readonly LayerMask _mask;
        readonly float _rayHeight;
        readonly List<List<Vector3>> _rivers = new List<List<Vector3>>();
        readonly float _riverHalfWidth;

        /// <param name="rivers">Средние линии рек (для MapPolylines/Both); y не важен.</param>
        /// <param name="riverHalfWidth">Половина ширины воды для проверки по линиям.</param>
        public RiverCrossingDetector(RiverDetection mode, LayerMask waterMask, float rayHeight = 2f,
            IEnumerable<IReadOnlyList<Vector3>> rivers = null, float riverHalfWidth = 0f)
        {
            _mode = mode;
            _mask = waterMask;
            _rayHeight = Mathf.Max(0.01f, rayHeight);
            _riverHalfWidth = riverHalfWidth;
            if (rivers != null)
                foreach (var river in rivers)
                    if (river != null && river.Count >= 2) _rivers.Add(new List<Vector3>(river));
        }

        /// <summary>Вода ли под точкой.</summary>
        public bool IsWater(Vector3 point)
        {
            if (_mode != RiverDetection.MapPolylines && PhysicsWater(point)) return true;
            if (_mode != RiverDetection.Physics && MapWater(point)) return true;
            return false;
        }

        bool PhysicsWater(Vector3 point)
        {
            if (_mask.value == 0) return false;
            var origin = new Vector3(point.x, point.y + _rayHeight, point.z);
            return UnityEngine.Physics.Raycast(origin, Vector3.down, out _, _rayHeight * 2f, _mask, QueryTriggerInteraction.Collide);
        }

        bool MapWater(Vector3 point)
        {
            if (_riverHalfWidth <= 0f) return false;
            var p = new Vector2(point.x, point.z);
            float limit = _riverHalfWidth * _riverHalfWidth;
            foreach (var river in _rivers)
                for (int i = 0; i + 1 < river.Count; i++)
                {
                    var a = new Vector2(river[i].x, river[i].z);
                    var b = new Vector2(river[i + 1].x, river[i + 1].z);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                    if ((p - (a + ab * t)).sqrMagnitude < limit) return true;
                }
            return false;
        }

        /// <summary>Пересекает ли отрезок воду.</summary>
        public bool CrossesWater(Vector3 start, Vector3 end, float step) => FindSpans(start, end, step).Count > 0;

        /// <summary>Все участки над водой вдоль отрезка start → end (расстояния по горизонтали от start).</summary>
        public List<WaterSpan> FindSpans(Vector3 start, Vector3 end, float step)
        {
            var spans = new List<WaterSpan>();
            var flat = end - start;
            flat.y = 0f;
            float length = flat.magnitude;
            if (length < 1e-5f) return spans;
            // Коллайдеры, созданные в этом кадре, должны попасть в физическую сцену до лучей.
            if (_mode != RiverDetection.MapPolylines) UnityEngine.Physics.SyncTransforms();

            Vector3 At(float distance) => Vector3.Lerp(start, end, distance / length);
            int samples = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.001f, step)));
            float previous = 0f;
            bool previousWet = IsWater(start);
            float spanStart = previousWet ? 0f : -1f;
            for (int i = 1; i <= samples; i++)
            {
                float distance = length * i / samples;
                bool wet = IsWater(At(distance));
                if (wet != previousWet)
                {
                    float edge = Refine(At, previous, distance, previousWet);
                    if (wet) spanStart = edge;
                    else
                    {
                        spans.Add(new WaterSpan(spanStart, edge));
                        spanStart = -1f;
                    }
                }
                previous = distance;
                previousWet = wet;
            }
            if (spanStart >= 0f) spans.Add(new WaterSpan(spanStart, length));
            return spans;
        }

        /// <summary>Граница вода/берег между dry и wet делением пополам (12 шагов — точнее 1/4000 шага).</summary>
        float Refine(System.Func<float, Vector3> at, float from, float to, bool fromWet)
        {
            for (int k = 0; k < 12; k++)
            {
                float middle = (from + to) * 0.5f;
                if (IsWater(at(middle)) == fromWet) from = middle;
                else to = middle;
            }
            return (from + to) * 0.5f;
        }
    }
}
