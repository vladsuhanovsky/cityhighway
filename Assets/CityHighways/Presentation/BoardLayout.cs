using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Геометрия поля: где стоят места, как лежат участки дорог, куда попал клик.
    /// Поле может быть наклонено (2.5D): лёгкая перспектива, дальний (северный) край уже ближнего.
    /// Перспектива сохраняет прямые, поэтому дороги, клики и подписи считаются в экранных точках как раньше.
    /// </summary>
    public sealed class BoardLayout
    {
        public const float LocationRadius = 9f;
        public const float RoadThickness = 11f;
        /// <summary>Толщина торца доски под полем при полном наклоне.</summary>
        public const float SlabDepth = 16f;
        const float SegmentGap = 3f;
        const float Padding = 50f;
        /// <summary>Насколько дальний край поля уже ближнего при полном наклоне (0.14 — на 14 %).</summary>
        const float FarShrink = 0.14f;
        /// <summary>Во сколько раз сплющиваются круги на земле при полном наклоне.</summary>
        const float FullSquash = 0.72f;
        /// <summary>Отступ доски от краёв окна при полном наклоне — вокруг виден «стол».</summary>
        const float BoardInset = 6f;

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

        /// <summary>Наклон: 0 — плоская карта сверху, 1 — полный лёгкий наклон.</summary>
        public float Tilt { get; private set; }
        /// <summary>Приближение: 1 — вся карта, больше — крупнее (колесо мыши, мини-карта).</summary>
        public float Zoom { get; private set; } = 1f;
        /// <summary>Сдвиг приближённой карты в пикселях поля.</summary>
        public Vector2 Pan { get; private set; }

        public void Fit(Rect area, float tilt) => Fit(area, tilt, Zoom, Pan);

        public void Fit(Rect area, float tilt, float zoom, Vector2 pan)
        {
            tilt = Mathf.Clamp01(tilt);
            zoom = Mathf.Max(1f, zoom);
            if (area == _area && Mathf.Abs(tilt - Tilt) < 0.0001f && Mathf.Abs(zoom - Zoom) < 0.0001f
                && (pan - Pan).sqrMagnitude < 0.0001f && _positions.Count > 0) return;
            _area = area;
            Tilt = tilt;
            Zoom = zoom;
            Pan = ClampPan(pan, zoom);
            _positions.Clear();
            foreach (var location in _map.Locations) _positions[location.Id] = ToScreen(location.X, location.Y);
        }

        float Left => _area.x + Padding;
        float Right => _area.xMax - Padding;
        float Top => _area.y + Padding;
        float Bottom => _area.yMax - Padding;
        bool Flat => Tilt < 0.001f;
        /// <summary>Ширина дальнего края относительно ближнего.</summary>
        float FarWidth => 1f - FarShrink * Tilt;
        float K => 1f / FarWidth - 1f;

        /// <summary>Размер поля без наклона (для разрешения текстуры местности).</summary>
        public Vector2 InnerSize => new Vector2(Right - Left, Bottom - Top);

        /// <summary>Масштаб глубины строки карты y: 1 — ближний край, FarWidth — дальний.</summary>
        float DepthOf(float y) => Flat ? 1f : 1f / (1f + K * (1f - y));

        /// <summary>
        /// Нормализованные координаты карты (0..1) → пиксели поля. С наклоном — перспектива:
        /// ширина строки ∝ 1/z, высота на экране линейна по 1/z, поэтому прямые остаются прямыми.
        /// </summary>
        public Vector2 ToScreen(float x, float y) => View(Project(x, y));

        /// <summary>Проекция без приближения: вся карта в окне.</summary>
        Vector2 Project(float x, float y)
        {
            float width = Right - Left, height = Bottom - Top;
            if (Flat) return new Vector2(Left + x * width, Top + y * height);
            float w = DepthOf(y);
            float w0 = FarWidth;
            float ny = (w - w0) / (1f - w0);
            return new Vector2((Left + Right) / 2f + (x - 0.5f) * width * w, Top + ny * height);
        }

        Vector2 ViewCenter => _area.center;

        /// <summary>Приближение вокруг центра поля и сдвиг.</summary>
        Vector2 View(Vector2 projected) => ViewCenter + (projected - ViewCenter) * Zoom + Pan;

        /// <summary>Обратно к проекции без приближения.</summary>
        Vector2 Unview(Vector2 screen) => ViewCenter + (screen - ViewCenter - Pan) / Zoom;

        /// <summary>Сдвиг не даёт увести карту из окна: край приближённой карты не заходит внутрь поля.</summary>
        public Vector2 ClampPan(Vector2 pan, float zoom)
        {
            float maxX = (zoom - 1f) * _area.width / 2f;
            float maxY = (zoom - 1f) * _area.height / 2f;
            return new Vector2(Mathf.Clamp(pan.x, -maxX, maxX), Mathf.Clamp(pan.y, -maxY, maxY));
        }

        /// <summary>Сдвиг, при котором точка карты (x, y) окажется в центре поля при этом приближении.</summary>
        public Vector2 PanToCenter(float x, float y, float zoom) =>
            ClampPan(-(Project(x, y) - ViewCenter) * zoom, zoom);

        /// <summary>Сдвиг, при котором экранная точка anchor останется на месте после смены приближения.</summary>
        public Vector2 PanForZoomAt(Vector2 anchor, float newZoom)
        {
            var offset = anchor - ViewCenter - Pan;
            return ClampPan(anchor - ViewCenter - offset * (newZoom / Zoom), newZoom);
        }

        /// <summary>Экранная точка → координаты карты (0..1), с учётом приближения и наклона.</summary>
        public Vector2 ScreenToMap(Vector2 screen)
        {
            var p = Unview(screen);
            float width = Mathf.Max(1f, Right - Left);
            float y = MapY(p.y);
            float x = 0.5f + (p.x - (Left + Right) / 2f) / (width * DepthOf(y));
            return new Vector2(x, y);
        }

        /// <summary>Строка карты (y), которая видна на высоте экрана screenY.</summary>
        public float MapY(float screenY)
        {
            float ny = (screenY - Top) / Mathf.Max(1f, Bottom - Top);
            if (Flat) return ny;
            float w0 = FarWidth;
            float w = Mathf.Max(0.05f, w0 + ny * (1f - w0));
            return 1f - (1f / w - 1f) / K;
        }

        /// <summary>Во сколько раз предметы у этой точки экрана меньше, чем у ближнего края (перспектива).</summary>
        public float ScaleAt(Vector2 screen)
        {
            if (Flat) return 1f;
            float ny = (Unview(screen).y - Top) / Mathf.Max(1f, Bottom - Top);
            return Mathf.Clamp(FarWidth + ny * (1f - FarWidth), 0.6f, 1.15f);
        }

        /// <summary>Отношение высоты к ширине у кругов, лежащих на земле (зоны, тени, лужи света).</summary>
        public float Squash => Mathf.Lerp(1f, FullSquash, Tilt);

        /// <summary>
        /// Края доски в координатах карты (не шире запаса текстуры margin): доска помещается в окно,
        /// снизу под ней остаётся место для торца. Без наклона доска занимает всё окно.
        /// </summary>
        public void BoardBounds(float margin, out float x0, out float x1, out float y0, out float y1)
        {
            float inset = BoardInset * Tilt;
            y0 = Mathf.Max(-margin, MapY(_area.y + inset));
            y1 = Mathf.Min(1f + margin, MapY(_area.yMax - inset - SlabDepth * Tilt));
            float half = (_area.width / 2f - inset) / Mathf.Max(1f, (Right - Left) * DepthOf(y1));
            x0 = Mathf.Max(-margin, 0.5f - half);
            x1 = Mathf.Min(1f + margin, 0.5f + half);
        }

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
