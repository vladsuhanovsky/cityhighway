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
                && (pan - Pan).sqrMagnitude < 0.0001f && _positions.Count > 0 && !_perspectiveDirty) return;
            _area = area;
            Tilt = tilt;
            Zoom = zoom;
            Pan = ClampPan(pan, zoom);
            _perspectiveDirty = false;
            if (Perspective) FitCamera();
            _positions.Clear();
            foreach (var location in _map.Locations) _positions[location.Id] = ToScreen(location.X, location.Y);
        }

        // ================================================================== Настоящая перспектива (3D-город)
        //
        // В 3D-режиме карта лежит на земле 3D-сцены: x → мировой X, y → мировой −Z (север дальше от зрителя).
        // Камера смотрит на центр поля, наклонена от вертикали на CameraTilt градусов (0 — вид сверху).
        // Расстояние подбирается так, чтобы поле с отступом Padding поместилось в окно; затем картинка
        // центрируется. Те же матрицы получает 3D-камера (CameraProjection), поэтому дороги, места и клики
        // интерфейса точно лежат на 3D-земле. Приближение и сдвиг — как раньше, в экранных пикселях поверх проекции.

        /// <summary>Мир 3D-сцены: ширина и глубина поля в метрах сцены (пропорции карты 1400 × 780).</summary>
        public const float WorldWidth = 14f, WorldDepth = 7.8f;
        /// <summary>Вертикальный угол обзора камеры: небольшой — перспектива мягкая, дальние дороги не мельчат.</summary>
        public const float FieldOfView = 28f;

        bool _perspectiveDirty;
        Matrix4x4 _viewProj = Matrix4x4.identity, _invViewProj = Matrix4x4.identity, _proj = Matrix4x4.identity;

        /// <summary>Включена ли настоящая перспектива (3D-город под интерфейсом).</summary>
        public bool Perspective { get; private set; }
        /// <summary>Наклон камеры от вертикали при полном наклоне (Tilt = 1), градусы.</summary>
        public float MaxCameraTilt { get; private set; } = 38f;
        /// <summary>Текущий наклон камеры: MaxCameraTilt × Tilt.</summary>
        public float CameraTilt => MaxCameraTilt * Tilt;
        public Vector3 CameraPosition { get; private set; }
        public Quaternion CameraRotation { get; private set; } = Quaternion.identity;
        public float CameraNear { get; private set; } = 0.1f;
        public float CameraFar { get; private set; } = 200f;

        public void SetPerspective(bool on, float maxTiltDegrees)
        {
            maxTiltDegrees = Mathf.Clamp(maxTiltDegrees, 0f, 60f);
            if (on == Perspective && Mathf.Abs(maxTiltDegrees - MaxCameraTilt) < 0.01f) return;
            Perspective = on;
            MaxCameraTilt = maxTiltDegrees;
            _perspectiveDirty = true;
        }

        public static Vector3 World(float x, float y) => new Vector3((x - 0.5f) * WorldWidth, 0f, (0.5f - y) * WorldDepth);
        public static Vector2 MapOf(Vector3 world) => new Vector2(world.x / WorldWidth + 0.5f, 0.5f - world.z / WorldDepth);

        void FitCamera()
        {
            float aspect = Mathf.Max(0.2f, _area.width / Mathf.Max(1f, _area.height));
            float alpha = CameraTilt * Mathf.Deg2Rad;
            var toEye = new Vector3(0f, Mathf.Cos(alpha), -Mathf.Sin(alpha));
            var up = new Vector3(0f, Mathf.Sin(alpha), Mathf.Cos(alpha));
            var rotation = Quaternion.LookRotation(-toEye, up);
            float padX = Mathf.Clamp(2f * Padding / Mathf.Max(1f, _area.width), 0f, 0.5f);
            float padY = Mathf.Clamp(2f * Padding / Mathf.Max(1f, _area.height), 0f, 0.5f);
            var corners = new[] { World(0f, 0f), World(1f, 0f), World(0f, 1f), World(1f, 1f) };

            // Ближе камера — крупнее поле: ищем самое близкое расстояние, при котором поле ещё помещается.
            float lo = 2f, hi = 400f;
            Matrix4x4 viewProj = Matrix4x4.identity, proj = Matrix4x4.identity;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
            for (int iteration = 0; iteration < 40; iteration++)
            {
                float distance = (lo + hi) / 2f;
                Build(distance, out viewProj, out proj);
                Bounds(viewProj, out minX, out maxX, out minY, out maxY);
                bool fits = maxX - minX <= 2f * (1f - padX) && maxY - minY <= 2f * (1f - padY);
                if (fits) hi = distance;
                else lo = distance;
            }
            float best = hi;
            Build(best, out viewProj, out proj);
            Bounds(viewProj, out minX, out maxX, out minY, out maxY);
            // Центрируем картинку в окне (ближний край у перспективы шире, без сдвига поле «съезжает» вниз).
            var center = Matrix4x4.identity;
            center.m03 = -(minX + maxX) / 2f;
            center.m13 = -(minY + maxY) / 2f;
            _proj = center * proj;
            Build(best, out var viewOnly, out _, false);
            _viewProj = _proj * viewOnly;
            _invViewProj = _viewProj.inverse;
            CameraPosition = toEye * best;
            CameraRotation = rotation;

            void Build(float distance, out Matrix4x4 vp, out Matrix4x4 p, bool withProjection = true)
            {
                var eye = toEye * distance;
                var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, rotation, Vector3.one).inverse;
                CameraNear = Mathf.Max(0.05f, distance * 0.05f);
                CameraFar = distance * 4f + 20f;
                p = Matrix4x4.Perspective(FieldOfView, aspect, CameraNear, CameraFar);
                vp = withProjection ? p * view : view;
            }

            void Bounds(Matrix4x4 vp, out float x0, out float x1, out float y0, out float y1)
            {
                x0 = y0 = float.MaxValue;
                x1 = y1 = float.MinValue;
                foreach (var corner in corners)
                {
                    var clip = vp * new Vector4(corner.x, corner.y, corner.z, 1f);
                    float w = Mathf.Max(0.0001f, clip.w);
                    float nx = clip.x / w, ny = clip.y / w;
                    x0 = Mathf.Min(x0, nx); x1 = Mathf.Max(x1, nx);
                    y0 = Mathf.Min(y0, ny); y1 = Mathf.Max(y1, ny);
                }
            }
        }

        /// <summary>Мировая точка → пиксели поля без приближения.</summary>
        Vector2 ProjectWorld(Vector3 world)
        {
            var clip = _viewProj * new Vector4(world.x, world.y, world.z, 1f);
            float w = Mathf.Max(0.0001f, clip.w);
            float nx = clip.x / w, ny = clip.y / w;
            return new Vector2(_area.x + (nx + 1f) / 2f * _area.width, _area.y + (1f - ny) / 2f * _area.height);
        }

        /// <summary>Мировая точка (например, крыша здания) → пиксели поля с приближением.</summary>
        public Vector2 WorldToScreen(Vector3 world) => View(ProjectWorld(world));

        /// <summary>Пиксели поля без приближения → точка на земле (координаты карты).</summary>
        Vector2 ProjectedToMap(Vector2 projected)
        {
            float u = (projected.x - _area.x) / Mathf.Max(1f, _area.width) * 2f - 1f;
            float v = 1f - (projected.y - _area.y) / Mathf.Max(1f, _area.height) * 2f;
            var near = _invViewProj * new Vector4(u, v, -1f, 1f);
            var far = _invViewProj * new Vector4(u, v, 1f, 1f);
            var a = (Vector3)near / near.w;
            var b = (Vector3)far / far.w;
            float denominator = b.y - a.y;
            float t = Mathf.Abs(denominator) < 1e-6f ? 0f : -a.y / denominator;
            return MapOf(a + (b - a) * t);
        }

        /// <summary>
        /// Матрица проекции для 3D-камеры на весь экран: та же перспектива, что у ToScreen, плюс приближение, сдвиг
        /// и положение поля на панели (offset — левый верхний угол поля в пикселях панели, panelSize — размер панели).
        /// </summary>
        public Matrix4x4 CameraProjection(Vector2 panelSize, Vector2 offset)
        {
            float panelW = Mathf.Max(1f, panelSize.x), panelH = Mathf.Max(1f, panelSize.y);
            float ScreenX(float u)
            {
                float px = _area.x + (u + 1f) / 2f * _area.width;
                float viewed = ViewCenter.x + (px - ViewCenter.x) * Zoom + Pan.x + offset.x;
                return viewed / panelW * 2f - 1f;
            }
            float ScreenY(float v)
            {
                float py = _area.y + (1f - v) / 2f * _area.height;
                float viewed = ViewCenter.y + (py - ViewCenter.y) * Zoom + Pan.y + offset.y;
                return 1f - viewed / panelH * 2f;
            }
            var m = Matrix4x4.identity;
            m.m00 = (ScreenX(1f) - ScreenX(-1f)) / 2f;
            m.m03 = (ScreenX(1f) + ScreenX(-1f)) / 2f;
            m.m11 = (ScreenY(1f) - ScreenY(-1f)) / 2f;
            m.m13 = (ScreenY(1f) + ScreenY(-1f)) / 2f;
            return m * _proj;
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
            if (Perspective) return ProjectWorld(World(x, y));
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
            if (Perspective) return ProjectedToMap(Unview(screen));
            var p = Unview(screen);
            float width = Mathf.Max(1f, Right - Left);
            float y = MapY(p.y);
            float x = 0.5f + (p.x - (Left + Right) / 2f) / (width * DepthOf(y));
            return new Vector2(x, y);
        }

        /// <summary>Строка карты (y), которая видна на высоте экрана screenY.</summary>
        public float MapY(float screenY)
        {
            if (Perspective) return ProjectedToMap(new Vector2(_area.center.x, screenY)).y;
            float ny = (screenY - Top) / Mathf.Max(1f, Bottom - Top);
            if (Flat) return ny;
            float w0 = FarWidth;
            float w = Mathf.Max(0.05f, w0 + ny * (1f - w0));
            return 1f - (1f / w - 1f) / K;
        }

        /// <summary>Во сколько раз предметы у этой точки экрана меньше, чем у ближнего края (перспектива).</summary>
        public float ScaleAt(Vector2 screen)
        {
            if (Perspective)
            {
                // Длина малого шага по земле здесь и у ближнего края по центру.
                var here = ProjectedToMap(Unview(screen));
                float Step(Vector2 m) => (ProjectWorld(World(m.x, m.y) + Vector3.right * 0.2f) - ProjectWorld(World(m.x, m.y))).magnitude;
                float near = Step(new Vector2(0.5f, 1f));
                return Mathf.Clamp(Step(here) / Mathf.Max(0.001f, near), 0.45f, 1.2f);
            }
            if (Flat) return 1f;
            float ny = (Unview(screen).y - Top) / Mathf.Max(1f, Bottom - Top);
            return Mathf.Clamp(FarWidth + ny * (1f - FarWidth), 0.6f, 1.15f);
        }

        /// <summary>Отношение высоты к ширине у кругов, лежащих на земле (зоны, тени, лужи света).</summary>
        public float Squash
        {
            get
            {
                if (!Perspective) return Mathf.Lerp(1f, FullSquash, Tilt);
                // Отношение высоты к ширине у маленького круга в центре поля.
                var c = World(0.5f, 0.5f);
                float dx = (ProjectWorld(c + Vector3.right * 0.2f) - ProjectWorld(c)).magnitude;
                float dz = (ProjectWorld(c + Vector3.forward * 0.2f) - ProjectWorld(c)).magnitude;
                return Mathf.Clamp(dz / Mathf.Max(0.001f, dx), 0.3f, 1f);
            }
        }

        /// <summary>
        /// Края доски в координатах карты (не шире запаса текстуры margin): доска помещается в окно,
        /// снизу под ней остаётся место для торца. Без наклона доска занимает всё окно.
        /// </summary>
        public void BoardBounds(float margin, out float x0, out float x1, out float y0, out float y1)
        {
            if (Perspective)
            {
                x0 = y0 = -margin;
                x1 = y1 = 1f + margin;
                return;
            }
            float inset = BoardInset * Tilt;
            y0 = Mathf.Max(-margin, MapY(_area.y + inset));
            y1 = Mathf.Min(1f + margin, MapY(_area.yMax - inset - SlabDepth * Tilt));
            float half = (_area.width / 2f - inset) / Mathf.Max(1f, (Right - Left) * DepthOf(y1));
            x0 = Mathf.Max(-margin, 0.5f - half);
            x1 = Mathf.Min(1f + margin, 0.5f + half);
        }

        public Vector2 LocationPosition(string locationId) =>
            _positions.TryGetValue(locationId, out var p) ? p : Vector2.zero;

        /// <summary>
        /// Длина «рукава» перекрёстка станции в 3D (метры сцены): 3D-перекрёсток со скруглениями занимает её,
        /// а дорога интерфейса начинается у края рукава — станция видна как перекрёсток.
        /// </summary>
        public const float JunctionArm = 0.15f;

        public void GetTrack(Road road, out Vector2 start, out Vector2 end)
        {
            if (Perspective)
            {
                var la = _map.GetLocation(road.A);
                var lb = _map.GetLocation(road.B);
                if (la != null && lb != null)
                {
                    var wa = World(la.X, la.Y);
                    var wb = World(lb.X, lb.Y);
                    var along = wb - wa;
                    float length = along.magnitude;
                    if (length > JunctionArm * 2.5f)
                    {
                        along /= length;
                        start = WorldToScreen(wa + along * JunctionArm);
                        end = WorldToScreen(wb - along * JunctionArm);
                        return;
                    }
                }
            }
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
