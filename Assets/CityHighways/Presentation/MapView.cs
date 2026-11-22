using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Карта города, нарисованная кодом (Painter2D): зоны районов, сетка улиц, парки,
    /// река, МКАД, дороги разных типов и места. Подписи — обычные Label поверх.
    /// </summary>
    public sealed class MapView : VisualElement
    {
        // Дневная местность в духе MegaMek: трава, кварталы с крышами, леса (TerrainTexture), сверху — дороги.
        static readonly Color Land = TerrainTexture.GrassDark;
        static readonly Color Water = new Color(0.16f, 0.36f, 0.60f);
        static readonly Color WaterLight = new Color(0.22f, 0.44f, 0.70f);
        static readonly Color Ink = new Color(0.90f, 0.92f, 0.96f);
        static readonly Color LabelBack = new Color(0.07f, 0.09f, 0.06f, 0.80f);
        static readonly Color Glow = new Color(1f, 0.80f, 0.10f);
        /// <summary>Цвета районов контракта: первый — жёлтый, второй — красный.</summary>
        public static readonly Color FromColor = new Color(1f, 0.80f, 0.10f);
        public static readonly Color ToColor = new Color(0.94f, 0.22f, 0.26f);
        /// <summary>Третий район сложного контракта («через» у транзита, второе плечо у узла).</summary>
        public static readonly Color ThirdColor = new Color(0.72f, 0.48f, 1f);
        static readonly Color Dimmer = new Color(0.04f, 0.06f, 0.03f, 0.55f);
        /// <summary>«Стол» вокруг наклонённой доски.</summary>
        static readonly Color Table = new Color(0.07f, 0.075f, 0.09f);

        public event Action<Road, Vector2> Hovered;
        public event Action<Road, Vector2> RoadClicked;
        /// <summary>Курсор над местом (null — ушёл с места). Для подсказок по элементам карты.</summary>
        public event Action<Location, Vector2> LocationHovered;
        /// <summary>Приближение или сдвиг карты изменились (мини-карта перерисовывает рамку обзора).</summary>
        public event Action ViewChanged;

        /// <summary>
        /// Наибольшее приближение (×8; было ×4). Приближение — масштаб картинки камеры (BoardLayout.View), сама камера
        /// не подлетает ближе: ничего не обрезается ближней плоскостью, значки и подписи остаются своего размера.
        /// </summary>
        public const float MaxZoom = 8f;
        float _zoom = 1f, _zoomTarget = 1f;
        Vector2 _pan, _panTarget;
        bool _panning;
        Vector2 _panStartPointer, _panStartOffset;
        int _panPointer = -1;

        // ---------------------------------------------------------------- Свободная камера (3D)
        // Поворот вокруг вертикали (yaw) и наклон от вертикали (pitch), только при 3D-городе. По умолчанию
        // (DisplaySettings.leftDragRotates) левая кнопка — крутить и наклонять (клик без перетаскивания — выбор дороги
        // или станции), правая/средняя — тянуть приближенную карту. Иначе — как раньше: правая крутит, левая тянет.
        // Колесо — приближать, Q/E — поворот, R/F — наклон, Home — север сверху. Карта крутится вокруг центра экрана.
        public const float MinPitch = 12f;
        float _yaw, _yawTarget;
        float _pitch = -1f, _pitchTarget = -1f;   // −1 — наклон из настроек (CameraTiltDegrees)
        bool _rotating;
        int _rotatePointer = -1;
        // Левая кнопка нажата, но ещё не ясно, клик это или поворот: сдвиг больше DragThreshold — поворот.
        bool _leftPending;
        int _leftPointer = -1;
        Vector2 _leftStart;
        const float DragThreshold = 6f;

        static bool LeftRotates => DisplaySettings.Active == null || DisplaySettings.Active.leftDragRotates;
        Vector2 _rotateStartPointer;
        float _rotateStartYaw, _rotateStartPitch;

        /// <summary>Камеру можно вращать: под картой живёт 3D-город.</summary>
        public bool CanRotate => _use3D;
        /// <summary>Поворот камеры, градусы (0 — север сверху).</summary>
        public float Yaw => _yaw;
        /// <summary>Наклон камеры от вертикали, градусы.</summary>
        public float Pitch => _pitch < 0f ? DefaultPitch : _pitch;
        float DefaultPitch => Mathf.Clamp(CameraTiltDegrees, 20f, 50f);
        float PitchTarget => _pitchTarget < 0f ? DefaultPitch : _pitchTarget;
        Location _hoverLocation;

        public Road HoverRoad { get; private set; }
        /// <summary>Дороги, которые текущий игрок может построить прямо сейчас (мягко пульсируют).</summary>
        public readonly HashSet<string> Affordable = new HashSet<string>();
        /// <summary>Подсвеченный контракт: первый район (жёлтый) и второй (красный); null — подсветки нет.</summary>
        public string HighlightFrom { get; private set; }
        /// <summary>
        /// Свет по маршруту выполненного контракта: дороги RoutePath (по порядку) загораются одна за другой,
        /// RouteProgress 0..1 — где «голова» света; в конце свет гаснет.
        /// </summary>
        public IReadOnlyList<string> RoutePath { get; set; }
        public float RouteProgress { get; set; }
        public Color RouteColor { get; set; } = Color.white;

        /// <summary>Середина дороги в координатах окна (для летящих монеток).</summary>
        public Vector2 RoadWorldCenter(string roadId)
        {
            var road = _game?.Map.GetRoad(roadId);
            if (road == null) return worldBound.center;
            var mid = (LocationPosition(road.A) + LocationPosition(road.B)) * 0.5f;
            return this.LocalToWorld(mid);
        }

        /// <summary>Дорога, которую подсказывает наставник обучения (голубая пульсирующая рамка); null — нет.</summary>
        public string HintRoad { get; set; }
        public string HighlightTo { get; private set; }
        public string HighlightThird { get; private set; }
        public bool HasHighlight => HighlightFrom != null;
        /// <summary>Дороги пути контракта и цвет обводки (свои — зелёным, чужие — оранжевым).</summary>
        public readonly Dictionary<string, Color> PathRoads = new Dictionary<string, Color>();
        /// <summary>Прогресс анимации строительства 0..1 для только что построенных дорог.</summary>
        public readonly Dictionary<string, float> BuildProgress = new Dictionary<string, float>();
        public float PulseTime;
        public bool Interactive = true;
        /// <summary>Наклонять поле (2.5D). Берётся из настроек экрана при старте партии.</summary>
        public bool TiltEnabled = true;

        /// <summary>
        /// Под картой живёт 3D-город (City3DScene): местность, река, доска и виньетка не рисуются —
        /// их заменяет сцена, а интерфейс остаётся прозрачным слоем дорог, мест и подписей поверх неё.
        /// </summary>
        public bool Use3D
        {
            get => _use3D;
            set
            {
                if (_use3D == value) return;
                _use3D = value;
                if (!value) { _yaw = _yawTarget = 0f; _rotating = false; _leftPending = false; }
                Relayout();
                _waterLayer?.MarkDirtyRepaint();
                _roadUnderLayer?.MarkDirtyRepaint();
                ViewChanged?.Invoke();   // мини-карта показывает или прячет кнопки камеры
            }
        }
        bool _use3D;
        /// <summary>
        /// Дороги, которые интерфейс сейчас не рисует: в 3D пока идёт стройка (рабочие, экскаватор, каток),
        /// дорогу показывает сама 3D-сцена, а нарисованная поверх дорога закрыла бы технику.
        /// </summary>
        public Func<string, bool> RoadHidden;

        bool RoadVisible(Road road) => _game.IsRoadOpen(road.Id) && (RoadHidden == null || !RoadHidden(road.Id)) && !IsFreeHidden(road);

        /// <summary>Клик по станции (только если на событие кто-то подписан — иначе клик идёт дороге под ней).</summary>
        public event Action<Location, Vector2> LocationClicked;
        /// <summary>Дополнительный рисунок поверх карты (фуры и грузы магната) — в координатах карты.</summary>
        public Action<Painter2D> Overlay;
        /// <summary>Цвет городских (общих) дорог магната — владелец за пределами списка игроков.</summary>
        public static readonly Color PublicRoadColor = new Color(0.70f, 0.72f, 0.76f);

        // ---------------------------------------------------------------- Режим магната: спокойная карта

        /// <summary>
        /// Спокойные дороги (магнат): одно покрытие для всех дорог (асфальт, у мостов — настил), без разметки
        /// по ресурсам, доступные для покупки — тонкий светлый контур без пульсирующего золотого ореола.
        /// </summary>
        public bool CalmRoads;
        /// <summary>
        /// Дороги лежат на наклонённой земле 3D-города (магнат): участки — проекции прямоугольников мира,
        /// дальний край уже ближнего. Без 3D ни на что не влияет.
        /// </summary>
        public bool GroundRoads
        {
            get => _groundRoads;
            set
            {
                _groundRoads = value;
                if (_layout != null) _layout.GroundQuads = value;
            }
        }
        bool _groundRoads;
        /// <summary>
        /// Магнат: городские дороги, построенные с начала партии, — просто асфальт, без бордюров и свечения
        /// (их цвет — PublicRoadColor).
        /// </summary>
        public bool PlainPublicRoads;
        /// <summary>Магнат: ничьи участки выглядят как дороги «из любых материалов» в соревновании (брусчатка и светлый контур), без ореола.</summary>
        public bool DashedFreeRoads;
        /// <summary>Прозрачность свободных (никому не принадлежащих) дорог: 1 — как обычно, 0 — не рисуются.</summary>
        public float FreeRoadAlpha = 1f;
        /// <summary>
        /// Фокус под курсором (пиксели экрана; 0 — выключен): дороги и места дальше радиуса плавно тускнеют
        /// до FocusMinAlpha, рядом с курсором — яркие. Пути (PathRoads) и места из FocusExempt не тускнеют.
        /// </summary>
        public float FocusRadius;
        public const float FocusMinAlpha = 0.28f;
        /// <summary>Места, которые всегда яркие (хабы магната).</summary>
        public Func<string, bool> FocusExempt;
        /// <summary>Размер фишек мест (1 — как в классике); зона клика растёт вместе с ним и ещё на 10 px.</summary>
        public float LocationScale = 1f;
        /// <summary>Порты (район «port») — с якорем внутри, как аэропорты и вокзалы со своими значками.</summary>
        public bool HubIcons;
        /// <summary>
        /// Плашка на середине дороги (магнат: цена в очках стройки); null — без плашки. Опрашивается каждый кадр,
        /// подпись меняется, только когда изменился текст. RoadBadgeMuted — плашка серая (например, не хватает очков).
        /// </summary>
        public Func<Road, string> RoadBadge;
        public Func<Road, bool> RoadBadgeMuted;
        /// <summary>Цвет плашки дороги (null — обычная золотистая), например цвет игрока у плашки способности.</summary>
        public Func<Road, Color?> RoadBadgeColor;
        /// <summary>
        /// Подсветка дорог, на которые действует способность владельца (мосты Мостовика, дороги Сборщика пошлин):
        /// мягкий пульсирующий ореол его цвета. null — нет подсветки.
        /// </summary>
        public Func<Road, Color?> AbilityGlow;
        readonly Dictionary<string, Label> _roadBadges = new Dictionary<string, Label>();
        readonly Dictionary<string, string> _roadBadgeState = new Dictionary<string, string>();
        VisualElement _badgeLayer;
        static readonly Color BadgeBack = new Color(0.98f, 0.80f, 0.30f, 0.95f);
        static readonly Color BadgeMutedBack = new Color(0.20f, 0.22f, 0.27f, 0.88f);

        Vector2 _pointer;
        bool _pointerInside;
        float _focusLevel;   // 0..1 — фокус проявляется и гаснет плавно
        float _fade = 1f;    // множитель прозрачности текущей рисуемой дороги или места
        bool _showLocationLabels = true, _showDistrictLabels = true;
        readonly Dictionary<string, float> _labelFade = new Dictionary<string, float>();

        /// <summary>Слои подписей: названия мест и районов.</summary>
        public void SetLabelLayers(bool locations, bool districts)
        {
            _showLocationLabels = locations;
            _showDistrictLabels = districts;
            if (_game == null) return;
            foreach (var pair in _locationLabels) Ui.Show(pair.Value, locations && _game.IsLocationOpen(pair.Key));
            foreach (var pair in _districtLabels) Ui.Show(pair.Value, districts && _game.IsDistrictOpen(pair.Key));
        }

        Color Fade(Color c) => _fade >= 0.999f ? c : new Color(c.r, c.g, c.b, c.a * _fade);

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        float FocusFade(float distance)
        {
            if (_focusLevel <= 0.001f || FocusRadius <= 0f) return 1f;
            float k = Mathf.Clamp01((distance - FocusRadius) / FocusRadius);
            k = k * k * (3f - 2f * k);
            return 1f - _focusLevel * k * (1f - FocusMinAlpha);
        }

        bool IsFreeHidden(Road road) =>
            FreeRoadAlpha <= 0.01f && !_game.GetRoadOwner(road.Id).HasValue && !PathRoads.ContainsKey(road.Id);

        /// <summary>Прозрачность дороги: свободная — FreeRoadAlpha, дальше от курсора — тусклее; путь и выбранная — ярко.</summary>
        float RoadFade(Road road)
        {
            if (PathRoads.ContainsKey(road.Id) || IsSelected(road)) return 1f;
            float alpha = _game.GetRoadOwner(road.Id).HasValue ? 1f : FreeRoadAlpha;
            if (_focusLevel > 0.001f)
                alpha *= FocusFade(DistanceToSegment(_pointer, _layout.LocationPosition(road.A), _layout.LocationPosition(road.B)));
            return alpha;
        }

        float LocationFade(Location location)
        {
            if (_focusLevel <= 0.001f || (FocusExempt != null && FocusExempt(location.Id))) return 1f;
            return FocusFade(Vector2.Distance(_pointer, _layout.LocationPosition(location.Id)));
        }

        static readonly Color CalmRoadColor = new Color(0.80f, 0.82f, 0.86f);

        /// <summary>Городская (общая) дорога магната: владелец за пределами списка игроков.</summary>
        bool IsPublicRoad(int? owner) => owner.HasValue && _game != null && owner.Value >= _game.Players.Count;

        Color OwnerColor(int owner) =>
            _game != null && owner >= _game.Players.Count ? PublicRoadColor : Palette.PlayerColor(owner);

        /// <summary>Точка карты на экране (для фур магната).</summary>
        public Vector2 ScreenPositionOf(string locationId) => _layout.LocationPosition(locationId);

        /// <summary>Пересчитать раскладку поля (например, после смены угла камеры).</summary>
        public void RefreshLayout() => Relayout();
        /// <summary>Наклон камеры 3D-города от вертикали, градусы (30–45).</summary>
        public float CameraTiltDegrees = 38f;
        /// <summary>Раскладка поля — 3D-сцена берёт из неё ту же камеру.</summary>
        public BoardLayout Layout => _layout;
        /// <summary>Текущий свет времени суток — для солнца, луны, окон и тумана 3D-города.</summary>
        public CityLight Light => new CityLight
        {
            Hour = _hour, Daylight = _daylight, Night = _night, Golden = _golden,
            Morning = _morning, Twilight = _twilight, Lamps = _lamps, SunX = _sunX,
        };
        /// <summary>Дорога, выбранная для стройки или улучшения (открыто окно оплаты), — в светлом контуре.</summary>
        public string SelectedRoadId { get; set; }
        /// <summary>Temporary construction ghost, calculated from current action legality.</summary>
        public string PreviewRoadId { get; private set; }
        public bool PreviewAllowed { get; private set; }
        public bool PreviewFree { get; private set; }
        public void SetConstructionPreview(string roadId, bool allowed, bool free)
        {
            if (PreviewRoadId == roadId && PreviewAllowed == allowed && PreviewFree == free) return;
            PreviewRoadId = roadId;
            PreviewAllowed = allowed;
            PreviewFree = free;
            Repaint();
        }


        float _tiltProgress;   // 0..1, наклон плавно появляется в начале партии
        float _hoverFade;      // контур наведённой дороги проявляется за доли секунды
        float _dimLevel;       // затемнение карты при подсветке контракта, 0..1
        readonly Dictionary<string, float> _districtLit = new Dictionary<string, float>();
        readonly Dictionary<string, Color> _districtLitColor = new Dictionary<string, Color>();
        Label _clockLabel;
        int _clockShown = -1;

        Game _game;
        BoardLayout _layout;
        readonly Dictionary<string, int> _districtIndex = new Dictionary<string, int>();
        readonly VisualElement _labels;
        readonly Dictionary<string, VisualElement> _locationLabels = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, VisualElement> _districtLabels = new Dictionary<string, VisualElement>();
        VisualElement _ringLabel;
        int _openLocations = -1;

        // Слои: неподвижный фон (перерисовывается только при смене размера или открытии района),
        // живой слой (дороги, места, машинки, салют — каждый кадр) и подписи.
        readonly VisualElement _terrainLayer;     // местность: трава, кварталы, леса (одна текстура)
        readonly VisualElement _staticLayer;
        readonly VisualElement _waterLayer;       // текстурная вода, течёт
        readonly VisualElement _roadUnderLayer;   // свечение под дорогами
        readonly VisualElement _roadTextureLayer; // текстуры покрытий
        readonly VisualElement _dynamicLayer;     // бордюры, разметка, места, машинки, эффекты
        readonly TexturedQuads _quads = new TexturedQuads();
        readonly TexturedQuads _terrainQuads = new TexturedQuads();

        public MapView()
        {
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;

            _terrainLayer = Layer(DrawTerrain);
            _terrainLayer.style.backgroundColor = Land;

            _staticLayer = Ui.Box();
            Ui.Fill(_staticLayer);
            _staticLayer.pickingMode = PickingMode.Ignore;
            _staticLayer.generateVisualContent += DrawStatic;
            Add(_staticLayer);

            _waterLayer = Layer(DrawWater);
            _roadUnderLayer = Layer(DrawRoadsUnder);
            _roadTextureLayer = Layer(DrawRoadTextures);

            _dynamicLayer = Ui.Box();
            Ui.Fill(_dynamicLayer);
            _dynamicLayer.pickingMode = PickingMode.Ignore;
            _dynamicLayer.generateVisualContent += Draw;
            Add(_dynamicLayer);

            _labels = Ui.Box();
            Ui.Fill(_labels);
            Add(_labels);

            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            RegisterCallback<PointerMoveEvent>(e =>
            {
                Vector2 position = e.localPosition;
                _pointer = position;
                _pointerInside = true;
                if (_leftPending && e.pointerId == _leftPointer && (position - _leftStart).magnitude > DragThreshold)
                {
                    // Потянули — это поворот, а не клик.
                    _leftPending = false;
                    StartRotate(e.pointerId, _leftStart, position);
                }
                if (_rotating && e.pointerId == _rotatePointer)
                {
                    // Вправо-влево — поворот, вверх-вниз — наклон. Без задержки, как перетаскивание карты.
                    var delta = position - _rotateStartPointer;
                    // Мышь задаёт цель, камера догоняет её за несколько кадров: события мыши приходят неровно
                    // (то два за кадр, то ни одного), и прямой поворот давал «перескоки».
                    var controls = DisplaySettings.Active;
                    float speed = controls?.CameraSpeedFactor ?? 1f;
                    float tiltSign = controls != null && controls.invertTilt ? -1f : 1f;
                    SetAngles(_rotateStartYaw - delta.x * 0.35f * speed, _rotateStartPitch - delta.y * 0.22f * speed * tiltSign,
                        controls != null && !controls.smoothCamera);
                    return;
                }
                if (_panning && e.pointerId == _panPointer)
                {
                    // Перетаскивание карты: сдвиг идёт за курсором без задержки.
                    _panTarget = _layout.ClampPan(_panStartOffset + (position - _panStartPointer), _zoomTarget);
                    _pan = _panTarget;
                    Relayout();
                    ViewChanged?.Invoke();
                    return;
                }
                var road = Interactive && _game != null ? OpenRoadAt(position) : null;
                SetHover(road, position);
                SetHoverLocation(road == null && _game != null ? LocationAt(position) : null, position);
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _pointerInside = false;
                SetHover(null, Vector2.zero);
                SetHoverLocation(null, Vector2.zero);
            });
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (_game == null) return;
                Vector2 position = e.localPosition;
                bool leftRotates = LeftRotates && CanRotate;
                // Левая кнопка (если она крутит поле): ждём — клик это или перетаскивание. Клик сработает при отпускании.
                if (e.button == 0 && leftRotates)
                {
                    _leftPending = true;
                    _leftPointer = e.pointerId;
                    _leftStart = position;
                    this.CapturePointer(e.pointerId);
                    return;
                }
                // Правая кнопка крутит поле, если левая этого не делает (или карту не приближали — тянуть нечего).
                if (e.button == 1 && CanRotate && (!leftRotates || _zoomTarget <= 1.001f))
                {
                    StartRotate(e.pointerId, position, position);
                    return;
                }
                if (e.button == 0 && Click(position)) return;
                // Пусто под курсором (или правая/средняя кнопка) — тянем карту, если она приближена.
                if (_zoomTarget > 1.001f)
                {
                    _panning = true;
                    _panPointer = e.pointerId;
                    _panStartPointer = position;
                    _panStartOffset = _panTarget;
                    this.CapturePointer(e.pointerId);
                    SetHover(null, position);
                    SetHoverLocation(null, position);
                }
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (_leftPending && e.pointerId == _leftPointer)
                {
                    // Отпустили, не сдвинув, — обычный клик по станции или дороге.
                    _leftPending = false;
                    if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
                    Click(e.localPosition);
                    return;
                }
                if (_rotating && e.pointerId == _rotatePointer)
                {
                    _rotating = false;
                    if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
                    return;
                }
                if (!_panning || e.pointerId != _panPointer) return;
                _panning = false;
                if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            });
            // Колесо мыши — приближение к точке под курсором.
            RegisterCallback<WheelEvent>(e =>
            {
                if (_game == null || Mathf.Abs(e.delta.y) < 0.01f) return;
                float step = DisplaySettings.Active?.ZoomStep ?? 1.2f;
                ZoomAt(e.localMousePosition, e.delta.y > 0f ? 1f / step : step);
                e.StopPropagation();
            });
        }

        // ================================================================== Обзор: приближение и сдвиг

        public float Zoom => _zoomTarget;

        /// <summary>Начать поворот камеры: start — где нажали (от него считается сдвиг), now — где курсор сейчас.</summary>
        void StartRotate(int pointer, Vector2 start, Vector2 now)
        {
            _rotating = true;
            _rotatePointer = pointer;
            _rotateStartPointer = start;
            _rotateStartYaw = _yawTarget;
            _rotateStartPitch = PitchTarget;
            if (!this.HasPointerCapture(pointer)) this.CapturePointer(pointer);
            SetHover(null, now);
            SetHoverLocation(null, now);
        }

        /// <summary>Клик левой кнопкой: станция (магнат — «ехать сюда») раньше дороги под ней. true — что-то выбрано.</summary>
        bool Click(Vector2 position)
        {
            if (_game == null || !Interactive) return false;
            if (LocationClicked != null)
            {
                var clicked = LocationAt(position);
                if (clicked != null)
                {
                    LocationClicked(clicked, position);
                    return true;
                }
            }
            var road = OpenRoadAt(position);
            if (road == null) return false;
            RoadClicked?.Invoke(road, position);
            return true;
        }

        /// <summary>Приблизить (factor больше 1) или отдалить так, чтобы точка anchor осталась под курсором.</summary>
        public void ZoomAt(Vector2 anchor, float factor)
        {
            if (_layout == null) return;
            float zoom = Mathf.Clamp(_zoomTarget * factor, 1f, MaxZoom);
            var center = _layout.Area.center;
            var offset = anchor - center - _panTarget;
            _panTarget = _layout.ClampPan(anchor - center - offset * (zoom / _zoomTarget), zoom);
            _zoomTarget = zoom;
            ViewChanged?.Invoke();
        }

        /// <summary>Приблизить или отдалить вокруг центра поля (кнопки на мини-карте).</summary>
        public void ZoomBy(float factor)
        {
            if (_layout != null) ZoomAt(_layout.Area.center, factor);
        }

        /// <summary>Куда на экране смотрит север карты (единичный вектор, y вниз); без поворота — вверх.</summary>
        public Vector2 NorthDirection()
        {
            if (_layout == null || !_use3D) return Vector2.down;
            var a = _layout.ToScreen(0.5f, 0.5f);
            var b = _layout.ToScreen(0.5f, 0.42f);
            var d = b - a;
            return d.sqrMagnitude < 1e-4f ? Vector2.down : d.normalized;
        }

        /// <summary>Повернуть камеру на degrees (плавно).</summary>
        public void RotateBy(float degrees)
        {
            if (!CanRotate) return;
            SetAngles(_yawTarget + degrees, PitchTarget, false);
        }

        /// <summary>Наклонить камеру: больше — ближе к горизонту, меньше — ближе к виду сверху (плавно).</summary>
        public void TiltBy(float degrees)
        {
            if (!CanRotate) return;
            SetAngles(_yawTarget, PitchTarget + degrees, false);
        }

        /// <summary>Север сверху и наклон из настроек.</summary>
        public void ResetCamera()
        {
            // Ближайший «север»: из 350° — вперёд на 10°, а не назад на 350°.
            SetAngles(_yaw + Mathf.DeltaAngle(_yaw, 0f), DefaultPitch, false);
            _pitchTarget = -1f;
        }

        /// <summary>Новые углы камеры; immediate — сразу (клавиши Q/E, R/F — они и так меняют угол каждый кадр), иначе плавно догоняют (мышь, кнопки).</summary>
        void SetAngles(float yaw, float pitch, bool immediate)
        {
            _yawTarget = yaw;
            _pitchTarget = Mathf.Clamp(pitch, MinPitch, BoardLayout.MaxPitch);
            if (immediate) ApplyAngles(_yawTarget, _pitchTarget);
            ViewChanged?.Invoke();
        }

        /// <summary>Поставить углы и пересчитать раскладку так, чтобы точка в центре экрана осталась в центре.</summary>
        void ApplyAngles(float yaw, float pitch)
        {
            if (_layout == null) { _yaw = yaw; _pitch = pitch; return; }
            var center = _layout.ScreenToMap(_layout.Area.center);
            _yaw = yaw;
            _pitch = pitch;
            Relayout();
            if (_zoom > 1.001f)
            {
                _pan = _panTarget = _layout.PanToCenter(center.x, center.y, _zoom);
                Relayout();
            }
        }

        /// <summary>Клавиши камеры: Q/E — поворот, R/F — наклон, Home — север сверху.</summary>
        void CameraKeys(float dt)
        {
            if (!CanRotate || panel == null) return;
            if (panel.focusController?.focusedElement is TextField) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            float turn = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            float lean = (keyboard.rKey.isPressed ? 1f : 0f) - (keyboard.fKey.isPressed ? 1f : 0f);
            if (Mathf.Abs(turn) > 0f || Mathf.Abs(lean) > 0f)
            {
                float speed = DisplaySettings.Active?.CameraSpeedFactor ?? 1f;
                SetAngles(_yawTarget + turn * 90f * speed * dt, PitchTarget + lean * 40f * speed * dt, true);
            }
            if (keyboard.homeKey.wasPressedThisFrame) ResetCamera();
        }

        /// <summary>Показать всю карту.</summary>
        public void ResetView()
        {
            _zoomTarget = 1f;
            _panTarget = Vector2.zero;
            ViewChanged?.Invoke();
        }

        /// <summary>Поставить точку карты (координаты 0..1) в центр поля; без приближения — сначала приблизить.</summary>
        public void CenterOn(Vector2 mapPoint)
        {
            if (_layout == null) return;
            if (_zoomTarget < 1.5f) _zoomTarget = 1.8f;
            _panTarget = _layout.PanToCenter(mapPoint.x, mapPoint.y, _zoomTarget);
            ViewChanged?.Invoke();
        }

        /// <summary>Углы видимой части поля в координатах карты (0..1): левый верхний, правый верхний, правый нижний, левый нижний.</summary>
        public void ViewportCorners(Vector2[] corners)
        {
            if (_layout == null || corners == null || corners.Length < 4) return;
            var r = _layout.Area;
            corners[0] = _layout.ScreenToMap(new Vector2(r.xMin, r.yMin));
            corners[1] = _layout.ScreenToMap(new Vector2(r.xMax, r.yMin));
            corners[2] = _layout.ScreenToMap(new Vector2(r.xMax, r.yMax));
            corners[3] = _layout.ScreenToMap(new Vector2(r.xMin, r.yMax));
        }

        /// <summary>Открытое место под курсором (в радиусе фишки с запасом).</summary>
        Location LocationAt(Vector2 position)
        {
            if (_layout == null) return null;
            Location best = null;
            float bestDistance = BoardLayout.LocationRadius * LocationScale + (LocationScale > 1.01f ? 10f : 6f);
            foreach (var location in _game.Map.Locations)
            {
                if (!_game.IsLocationOpen(location.Id)) continue;
                float distance = Vector2.Distance(position, _layout.LocationPosition(location.Id));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = location;
                }
            }
            return best;
        }

        void SetHoverLocation(Location location, Vector2 position)
        {
            if (location == null && _hoverLocation == null) return;
            _hoverLocation = location;
            LocationHovered?.Invoke(location, position);
        }

        /// <summary>
        /// Подменить партию на той же карте, не сбрасывая камеру (повтор: перемотка назад пересобирает партию).
        /// Если карта другая — обычный SetGame.
        /// </summary>
        public void SwapGame(Game game)
        {
            if (game == null) return;
            if (_game == null || _game.Map != game.Map)
            {
                SetGame(game);
                return;
            }
            _game = game;
            Affordable.Clear();
            PathRoads.Clear();
            BuildProgress.Clear();
            HoverRoad = null;
            SetConstructionPreview(null, false, false);
            RefreshVisibility();
            Repaint();
        }

        public void SetGame(Game game)
        {
            _game = game;
            _layout = new BoardLayout(game.Map) { GroundQuads = _groundRoads };
            Affordable.Clear();
            HighlightFrom = null;
            HighlightTo = null;
            HighlightThird = null;
            PathRoads.Clear();
            BuildProgress.Clear();
            HoverRoad = null;
            SelectedRoadId = null;
            SetConstructionPreview(null, false, false);
            _tiltProgress = 0f;
            _zoom = _zoomTarget = 1f;
            _pan = _panTarget = Vector2.zero;
            _panning = false;
            _rotating = false;
            _leftPending = false;
            _yaw = _yawTarget = 0f;
            _pitch = _pitchTarget = -1f;
            _dimLevel = 0f;
            _districtLit.Clear();
            _districtLitColor.Clear();
            _hour = HourTarget();
            UpdateLight();

            _districtIndex.Clear();
            for (int i = 0; i < game.Map.Districts.Count; i++) _districtIndex[game.Map.Districts[i].Id] = i;

            _labels.Clear();
            _locationLabels.Clear();
            _districtLabels.Clear();
            _roadBadges.Clear();
            _roadBadgeState.Clear();
            // Плашки дорог — под подписями мест (слой добавлен первым).
            _badgeLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_badgeLayer);
            _labels.Add(_badgeLayer);

            foreach (var district in game.Map.Districts)
            {
                // Разбросанные по карте районы (аэропорты по разным окраинам, вокзалы вокруг центра) без подписи:
                // подпись встала бы посередине — там, где этого района нет.
                var members = game.Map.LocationsIn(district.Id);
                if (members.Count > 1)
                {
                    float mx = members.Average(l => l.X), my = members.Average(l => l.Y);
                    float spread = members.Max(l => Mathf.Sqrt(Mathf.Pow((l.X - mx) * 1400f, 2f) + Mathf.Pow((l.Y - my) * 780f, 2f)));
                    if (spread > 230f) continue;
                }
                var label = Ui.Text(MagnateLocationText.Name(district.Name).ToUpperInvariant(), 14f, DistrictLabelColor, true);
                label.style.letterSpacing = 3f;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                // Обводка: подпись района читается и поверх дорог, и поверх светлых крыш.
                label.style.unityTextOutlineWidth = 0.9f;
                label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.75f);
                SmoothChanges(label);
                var holder = Holder(240f);
                holder.Add(label);
                _labels.Add(holder);
                _districtLabels[district.Id] = holder;
            }

            foreach (var location in game.Map.Locations)
            {
                var label = Ui.Text(MagnateLocationText.Name(location.Name), 12f, Ink, true);
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.unityTextOutlineWidth = 0.4f;
                label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.8f);
                label.style.backgroundColor = LabelBack;
                Ui.Radius(label, 4f);
                Ui.Padding(label, 1f, 5f);
                var holder = Holder(200f);
                holder.Add(label);
                _labels.Add(holder);
                _locationLabels[location.Id] = holder;
            }

            if (game.Map.Ring != null)
            {
                var ring = Ui.Text(MagnateLocationText.Name(game.Map.Ring.Name), 12f, new Color(1f, 0.72f, 0.35f, 0.85f), true);
                ring.style.backgroundColor = LabelBack;
                Ui.Padding(ring, 0f, 4f);
                _ringLabel = Holder(80f);
                _ringLabel.Add(ring);
                _labels.Add(_ringLabel);
            }

            BuildTitle(MagnateLocationText.Name(game.Map.Name));
            Relayout();
        }

        public Vector2 LocationPosition(string locationId) => _layout.LocationPosition(locationId);

        /// <summary>Дорога под курсором, если её район уже открыт.</summary>
        Road OpenRoadAt(Vector2 position)
        {
            var road = _layout.RoadAt(position);
            return road != null && _game.IsRoadOpen(road.Id) && !IsFreeHidden(road) ? road : null;
        }

        /// <summary>Спрятать подписи ещё не открытых районов (вызывается при каждом обновлении экрана).</summary>
        public void RefreshVisibility()
        {
            if (_game == null) return;
            foreach (var pair in _locationLabels) Ui.Show(pair.Value, _showLocationLabels && _game.IsLocationOpen(pair.Key));
            foreach (var pair in _districtLabels) Ui.Show(pair.Value, _showDistrictLabels && _game.IsDistrictOpen(pair.Key));
            // Открылся новый район — фон (зоны, парки) тоже меняется.
            int open = _game.Map.Locations.Count(l => _game.IsLocationOpen(l.Id));
            if (open != _openLocations)
            {
                _openLocations = open;
                _staticLayer.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Подсветить районы контракта: первый жёлтым, второй красным, третий (у сложных) фиолетовым;
        /// остальная карта затемняется. Путь (PathRoads) рисуется поверх затемнения.
        /// </summary>
        public void SetContractHighlight(string fromDistrict, string toDistrict, string thirdDistrict = null)
        {
            HighlightFrom = fromDistrict;
            HighlightTo = toDistrict;
            HighlightThird = thirdDistrict;
            UpdateLabelDimming();
            Repaint();
        }

        public void ClearContractHighlight()
        {
            HighlightFrom = null;
            HighlightTo = null;
            HighlightThird = null;
            PathRoads.Clear();
            UpdateLabelDimming();
            Repaint();
        }

        bool IsHighlighted(string districtId) =>
            HasHighlight && (districtId == HighlightFrom || districtId == HighlightTo || (HighlightThird != null && districtId == HighlightThird));

        Color HighlightColor(string districtId) =>
            districtId == HighlightFrom ? FromColor : districtId == HighlightThird ? ThirdColor : ToColor;

        /// <summary>Подписи мест тускнеют вместе с фишками (меняем стиль, только если прозрачность заметно сдвинулась).</summary>
        void UpdateLabelFocus()
        {
            foreach (var location in _game.Map.Locations)
            {
                if (!_locationLabels.TryGetValue(location.Id, out var holder)) continue;
                float fade = LocationFade(location);
                if (_labelFade.TryGetValue(location.Id, out var shown) && Mathf.Abs(shown - fade) < 0.03f) continue;
                _labelFade[location.Id] = fade;
                holder.style.opacity = fade;
            }
        }

        void UpdateLabelDimming()
        {
            if (_game == null) return;
            foreach (var pair in _locationLabels)
                pair.Value.style.opacity = !HasHighlight || IsHighlighted(_game.Map.DistrictOf(pair.Key)) ? 1f : 0.3f;
            foreach (var pair in _districtLabels)
            {
                bool lit = IsHighlighted(pair.Key);
                pair.Value.style.opacity = !HasHighlight || lit ? 1f : 0.3f;
                // Подпись подсвеченного района — его цветом, чтобы было видно, где «жёлтый», а где «красный».
                if (pair.Value.childCount > 0 && pair.Value[0] is Label label)
                    label.style.color = lit ? HighlightColor(pair.Key) : DistrictLabelColor;
            }
        }

        static Color Darker(Color c) => new Color(c.r * 0.75f, c.g * 0.75f, c.b * 0.75f, 1f);
        static readonly Color DistrictLabelColor = new Color(1f, 1f, 1f, 0.62f);

        /// <summary>Перерисовать живой слой (дороги, места, машинки). Фон перерисовывается сам при смене размера.</summary>
        public void Repaint()
        {
            _waterLayer.MarkDirtyRepaint();
            _roadUnderLayer.MarkDirtyRepaint();
            _roadTextureLayer.MarkDirtyRepaint();
            _dynamicLayer.MarkDirtyRepaint();
        }

        /// <summary>Кадр: время для анимаций, наклон, свет, плавная подсветка — и перерисовка живых слоёв.</summary>
        public void Tick(float deltaTime)
        {
            PulseTime += deltaTime;
            if (_game != null)
            {
                float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
                Animate(dt);
            }
            Repaint();
        }

        void Animate(float dt)
        {
            UpdateAtmosphere(dt);
            UpdateRoadBadges();
            _focusLevel = Mathf.MoveTowards(_focusLevel, FocusRadius > 0f && _pointerInside && !_panning ? 1f : 0f, dt / 0.25f);
            if (FocusRadius > 0f || _labelFade.Count > 0)
            {
                UpdateLabelFocus();
                if (FocusRadius <= 0f && _focusLevel <= 0.001f) _labelFade.Clear();
            }

            // Наклон поля появляется плавно в начале партии (и уходит, если его выключили).
            float tiltTarget = TiltEnabled ? 1f : 0f;
            if (Mathf.Abs(_tiltProgress - tiltTarget) > 0.0001f)
            {
                _tiltProgress = Mathf.MoveTowards(_tiltProgress, tiltTarget, dt / 1.4f);
                Relayout();
            }

            // Подсветка районов контракта: затемнение и каждый район проявляются и гаснут плавно,
            // при переходе с контракта на контракт старые районы гаснут, новые разгораются.
            _dimLevel = Mathf.MoveTowards(_dimLevel, HasHighlight ? 1f : 0f, dt / 0.25f);
            foreach (var district in _game.Map.Districts)
            {
                bool on = IsHighlighted(district.Id);
                _districtLit.TryGetValue(district.Id, out var level);
                if (on)
                {
                    var target = HighlightColor(district.Id);
                    _districtLitColor[district.Id] = level > 0.01f && _districtLitColor.TryGetValue(district.Id, out var old)
                        ? Color.Lerp(old, target, Mathf.Clamp01(dt * 12f))
                        : target;
                }
                _districtLit[district.Id] = Mathf.MoveTowards(level, on ? 1f : 0f, dt / 0.22f);
            }

            _hoverFade = Mathf.MoveTowards(_hoverFade, 1f, dt / 0.15f);
            UpdateClock();

            // Углы камеры догоняют цель плавно (кнопки, «север сверху»).
            CameraKeys(dt);
            float pitchTarget = PitchTarget;
            if (_use3D && (Mathf.Abs(Mathf.DeltaAngle(_yaw, _yawTarget)) > 0.05f || Mathf.Abs(Pitch - pitchTarget) > 0.05f))
            {
                // Мышью — быстрее (камера идёт почти вплотную за курсором), кнопками и «севером» — мягче.
                float k = 1f - Mathf.Exp(-Mathf.Min(dt, 0.05f) * (_rotating ? 16f : 10f));
                float yaw = Mathf.Abs(_yawTarget - _yaw) < 0.06f ? _yawTarget : Mathf.Lerp(_yaw, _yawTarget, k);
                float pitch = Mathf.Abs(pitchTarget - Pitch) < 0.06f ? pitchTarget : Mathf.Lerp(Pitch, pitchTarget, k);
                ApplyAngles(yaw, pitch);
                if (_pitchTarget < 0f && Mathf.Abs(pitch - pitchTarget) < 0.06f) _pitch = -1f;
                ViewChanged?.Invoke();
            }

            // Приближение и сдвиг догоняют цель плавно (колесо, кнопки, клик по мини-карте).
            if (Mathf.Abs(_zoom - _zoomTarget) > 0.0005f || (_pan - _panTarget).sqrMagnitude > 0.05f)
            {
                float k = 1f - Mathf.Exp(-dt * 12f);
                _zoom = Mathf.Abs(_zoom - _zoomTarget) < 0.001f ? _zoomTarget : Mathf.Lerp(_zoom, _zoomTarget, k);
                _pan = (_pan - _panTarget).sqrMagnitude < 0.25f ? _panTarget : Vector2.Lerp(_pan, _panTarget, k);
                Relayout();
                ViewChanged?.Invoke();
            }
        }

        float DistrictLit(string districtId) => _districtLit.TryGetValue(districtId, out var v) ? v : 0f;

        Color DistrictLitColor(string districtId) =>
            _districtLitColor.TryGetValue(districtId, out var c) ? c : FromColor;

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Плавный переход: 0 при x = from, 1 при x = to (from может быть больше to).</summary>
        static float Smooth(float from, float to, float x) => Smooth01(Mathf.InverseLerp(from, to, x));

        /// <summary>Подъём с лёгким «перелётом» в конце — фишки выбранного района подпрыгивают.</summary>
        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        VisualElement Layer(Action<MeshGenerationContext> draw)
        {
            var layer = Ui.Box();
            Ui.Fill(layer);
            layer.pickingMode = PickingMode.Ignore;
            layer.generateVisualContent += draw;
            Add(layer);
            return layer;
        }

        bool CanDraw => _game != null && !float.IsNaN(contentRect.width) && contentRect.width >= 50f;

        static VisualElement Holder(float width)
        {
            var holder = Ui.Column();
            holder.style.position = Position.Absolute;
            holder.style.width = width;
            holder.style.alignItems = Align.Center;
            SmoothChanges(holder);
            return holder;
        }

        /// <summary>Прозрачность и цвет подписи меняются плавно (подсветка районов контракта).</summary>
        static void SmoothChanges(VisualElement e)
        {
            e.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName>
            {
                new StylePropertyName("opacity"),
                new StylePropertyName("color")
            });
            e.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0.25f, TimeUnit.Second) });
            e.style.transitionTimingFunction = new StyleList<EasingFunction>(new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut) });
        }

        void BuildTitle(string city)
        {
            // Табличка в стиле дорожного указателя.
            var sign = Ui.Column();
            sign.style.position = Position.Absolute;
            sign.style.left = 24f;
            sign.style.top = 22f;
            sign.style.backgroundColor = new Color(0.10f, 0.42f, 0.30f);
            Ui.Border(sign, 3f, Color.white);
            Ui.Radius(sign, 8f);
            Ui.Padding(sign, 8f, 16f);
            // Логотип игры — в верхней панели экрана; на табличке — город и время суток.
            var title = Ui.Text(city, 20f, Color.white, true);
            Fonts.Apply(title, Fonts.Weight.Logo);
            title.style.whiteSpace = WhiteSpace.NoWrap;
            sign.Add(title);
            // Время суток на поле: идёт вместе с партией, к финалу — вечер и ночь.
            _clockLabel = Ui.Text("", 12f, new Color(1f, 1f, 1f, 0.75f));
            _clockLabel.style.marginTop = 2f;
            _clockShown = -1;
            sign.Add(_clockLabel);
            UpdateClock();
            _labels.Add(sign);
        }

        void Relayout()
        {
            if (_game == null) return;
            var rect = contentRect;
            if (float.IsNaN(rect.width) || rect.width < 50f || rect.height < 50f) return;
            float tilt = Smooth01(_tiltProgress);
            _layout.SetPerspective(_use3D, _use3D ? Mathf.Clamp(Pitch, MinPitch, BoardLayout.MaxPitch) : DefaultPitch, _use3D ? _yaw : 0f);
            _layout.Fit(new Rect(0f, 0f, rect.width, rect.height), tilt, _zoom, _pan);
            _pan = _layout.Pan;
            _terrainLayer.style.backgroundColor = _use3D ? Color.clear : Color.Lerp(Land, Table, tilt);

            foreach (var pair in _locationLabels)
            {
                var p = _layout.LocationPosition(pair.Key);
                pair.Value.style.left = p.x - 100f;
                pair.Value.style.top = p.y + BoardLayout.LocationRadius * LocationScale + 4f;
            }

            foreach (var pair in _districtLabels)
            {
                var c = DistrictCenter(pair.Key);
                pair.Value.style.left = c.x - 120f;
                pair.Value.style.top = c.y - 48f;
            }

            PositionRoadBadges();

            if (_ringLabel != null && _game.Map.Ring != null)
            {
                var top = RingPoint(-Mathf.PI / 2f);
                _ringLabel.style.left = top.x - 40f;
                _ringLabel.style.top = top.y - 9f;
            }
            _terrainLayer.MarkDirtyRepaint();
            _staticLayer.MarkDirtyRepaint();
            Repaint();
        }

        /// <summary>Плашки дорог: создать, показать или спрятать по RoadBadge; прозрачность — как у дороги (фокус).</summary>
        void UpdateRoadBadges()
        {
            if (_badgeLayer == null) return;
            if (RoadBadge == null && _roadBadges.Count == 0) return;
            foreach (var road in _game.Map.Roads)
            {
                string text = RoadBadge != null && RoadVisible(road) ? RoadBadge(road) : null;
                bool muted = text != null && RoadBadgeMuted != null && RoadBadgeMuted(road);
                string state = text == null ? null : (muted ? "m" : "s") + text;
                _roadBadgeState.TryGetValue(road.Id, out var shown);
                _roadBadges.TryGetValue(road.Id, out var badge);
                if (state != shown)
                {
                    _roadBadgeState[road.Id] = state;
                    if (state == null)
                    {
                        if (badge != null) Ui.Show(badge, false);
                    }
                    else
                    {
                        if (badge == null)
                        {
                            badge = Ui.Text("", 11f, Color.black, true);
                            badge.pickingMode = PickingMode.Ignore;
                            badge.style.position = Position.Absolute;
                            badge.style.minWidth = 18f;
                            badge.style.height = 16f;
                            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                            Ui.Padding(badge, 0f, 4f);
                            Ui.Radius(badge, 8f);
                            _roadBadges[road.Id] = badge;
                            _badgeLayer.Add(badge);
                            PositionRoadBadge(road, badge);
                        }
                        badge.text = text;
                        var custom = !muted && RoadBadgeColor != null ? RoadBadgeColor(road) : null;
                        badge.style.backgroundColor = muted ? BadgeMutedBack : custom ?? BadgeBack;
                        // На цвете игрока — тёмный или белый текст, смотря по яркости.
                        bool lightBack = !custom.HasValue || custom.Value.r * 0.3f + custom.Value.g * 0.59f + custom.Value.b * 0.11f > 0.55f;
                        badge.style.color = muted ? new Color(0.75f, 0.77f, 0.8f) : lightBack ? new Color(0.12f, 0.10f, 0.06f) : Color.white;
                        Ui.Border(badge, 1f, muted ? new Color(1f, 1f, 1f, 0.15f) : new Color(0f, 0f, 0f, 0.45f));
                        Ui.Show(badge, true);
                    }
                }
                if (badge != null && state != null)
                {
                    float fade = RoadFade(road);
                    if (Mathf.Abs(badge.resolvedStyle.opacity - fade) > 0.03f) badge.style.opacity = fade;
                }
            }
        }

        void PositionRoadBadges()
        {
            if (_game == null) return;
            foreach (var pair in _roadBadges)
            {
                var road = _game.Map.GetRoad(pair.Key);
                if (road != null) PositionRoadBadge(road, pair.Value);
            }
        }

        void PositionRoadBadge(Road road, Label badge)
        {
            _layout.GetTrack(road, out var start, out var end);
            var mid = (start + end) * 0.5f;
            // Центр плашки — на середине дороги, какой бы ширины ни была подпись.
            badge.style.left = mid.x;
            badge.style.top = mid.y;
            badge.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
        }

        Vector2 DistrictCenter(string districtId)
        {
            var locations = _game.Map.LocationsIn(districtId);
            if (locations.Count == 0) return Vector2.zero;
            var sum = Vector2.zero;
            foreach (var l in locations) sum += _layout.LocationPosition(l.Id);
            return sum / locations.Count;
        }

        /// <summary>
        /// Точка на кольцевой дороге карты (МКАД — эллипс вокруг 0.5, 0.5 с полуосями 0.40 × 0.49, A10 — вокруг центра
        /// Амстердама). Эллипс задаёт JSON карты (ring).
        /// </summary>
        Vector2 RingPoint(float angle)
        {
            var ring = _game.Map.Ring;
            if (ring == null) return _layout.ToScreen(0.5f, 0.5f);
            return _layout.ToScreen(ring.CX + Mathf.Cos(angle) * ring.RX, ring.CY + Mathf.Sin(angle) * ring.RY);
        }

        void SetHover(Road road, Vector2 position)
        {
            if (road != HoverRoad)
            {
                HoverRoad = road;
                // Контур проявляется заново только у новой дороги (уже выбранная не мигает).
                if (road != null && road.Id != SelectedRoadId) _hoverFade = 0f;
                Repaint();
            }
            Hovered?.Invoke(road, position);
        }

        // ================================================================== Рисование

        /// <summary>
        /// Местность — одна запечённая текстура на всю доску. С наклоном доска — трапеция, и текстура
        /// кладётся сеткой мелких четырёхугольников: так перспектива не ломает рисунок по диагонали.
        /// </summary>
        void DrawTerrain(MeshGenerationContext context)
        {
            if (!CanDraw || _use3D) return;
            const float m = TerrainTexture.Margin;
            const float span = 1f + 2f * m;
            var inner = _layout.InnerSize;
            var texture = TerrainTexture.For(_game.Map, inner.x, inner.y, _layout.Area.height);
            _layout.BoardBounds(m, out float x0, out float x1, out float y0, out float y1);
            int cells = _layout.Tilt > 0.001f ? 14 : 1;
            Vector2 Uv(float x, float y) => new Vector2((x + m) / span, 1f - (y + m) / span);

            _terrainQuads.Clear();
            for (int row = 0; row < cells; row++)
            {
                float ya = Mathf.Lerp(y0, y1, row / (float)cells);
                float yb = Mathf.Lerp(y0, y1, (row + 1) / (float)cells);
                for (int col = 0; col < cells; col++)
                {
                    float xa = Mathf.Lerp(x0, x1, col / (float)cells);
                    float xb = Mathf.Lerp(x0, x1, (col + 1) / (float)cells);
                    _terrainQuads.Add(texture,
                        _layout.ToScreen(xa, ya), _layout.ToScreen(xb, ya), _layout.ToScreen(xb, yb), _layout.ToScreen(xa, yb),
                        Uv(xa, ya), Uv(xb, ya), Uv(xb, yb), Uv(xa, yb), Color.white);
                }
            }
            _terrainQuads.Flush(context);
        }

        /// <summary>Доска на столе: торец под ближним краем, тень на столе и тёмная кромка по контуру.</summary>
        void DrawBoard(Painter2D p)
        {
            float tilt = _layout.Tilt;
            if (tilt < 0.01f) return;
            _layout.BoardBounds(TerrainTexture.Margin, out float x0, out float x1, out float y0, out float y1);
            var tl = _layout.ToScreen(x0, y0);
            var tr = _layout.ToScreen(x1, y0);
            var br = _layout.ToScreen(x1, y1);
            var bl = _layout.ToScreen(x0, y1);
            float depth = BoardLayout.SlabDepth * tilt;
            var down = new Vector2(0f, depth);

            // Мягкая тень доски на столе.
            for (int i = 1; i <= 3; i++)
            {
                p.fillColor = new Color(0f, 0f, 0f, 0.14f * tilt);
                var spread = new Vector2(3f * i, 0f);
                PolyFill(p, bl - spread + down, br + spread + down, br + spread * 1.5f + down + new Vector2(0f, 4f * i),
                    bl - spread * 1.5f + down + new Vector2(0f, 4f * i));
            }
            // Торец: светлее вверху, темнее внизу — доска толщиной в пару сантиметров.
            var mid = new Vector2(0f, depth * 0.45f);
            p.fillColor = new Color(0.33f, 0.25f, 0.16f);
            PolyFill(p, bl, br, br + mid, bl + mid);
            p.fillColor = new Color(0.21f, 0.15f, 0.10f);
            PolyFill(p, bl + mid, br + mid, br + down, bl + down);
            // Кромка по контуру и блик на переднем ребре.
            p.strokeColor = new Color(0f, 0f, 0f, 0.5f * tilt);
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(tl);
            p.LineTo(tr);
            p.LineTo(br);
            p.LineTo(bl);
            p.ClosePath();
            p.Stroke();
            p.strokeColor = new Color(1f, 0.95f, 0.85f, 0.22f * tilt);
            p.lineWidth = 1f;
            Line(p, bl + new Vector2(0f, 0.5f), br + new Vector2(0f, 0.5f));
        }

        static void PolyFill(Painter2D p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.LineTo(c);
            p.LineTo(d);
            p.ClosePath();
            p.Fill();
        }

        /// <summary>Неподвижный слой над местностью: зоны районов, набережные, МКАД, лёгкая виньетка.</summary>
        void DrawStatic(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (_game == null || float.IsNaN(rect.width) || rect.width < 50f) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            var area = new Rect(0f, 0f, rect.width, rect.height);

            if (_use3D)
            {
                // Земля, набережные и виньетка — в 3D-сцене; поверх неё — только МКАД (она читается как дорога).
                DrawRing(p);
                return;
            }
            DrawBoard(p);
            DrawDistrictZones(p);
            DrawRiver(p);
            DrawRing(p);
            DrawVignette(p, area);
        }

        /// <summary>Живой слой: блики на реке, дороги, места, машинки, подсветка контракта, салют, вспышка события.</summary>
        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (_game == null || float.IsNaN(rect.width) || rect.width < 50f) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            var area = new Rect(0f, 0f, rect.width, rect.height);
            float dt = Mathf.Clamp(PulseTime - _lastDrawTime, 0f, 0.1f);
            _lastDrawTime = PulseTime;

            DrawAtmosphere(p, area);
            foreach (var road in _game.Map.Roads)
                if (RoadVisible(road)) DrawRoadOver(p, road);
            DrawConstructionPreview(p);
            DrawRoadWarnings(p);
            DrawCars(p);
            DrawStreetLights(p);
            foreach (var location in _game.Map.Locations)
                if (_game.IsLocationOpen(location.Id)) DrawLocation(p, location);
            DrawCones(p);
            DrawSnowfall(p, area);
            Overlay?.Invoke(p);

            if (_dimLevel > 0.001f) DrawContractHighlight(p, area);
            DrawFireworks(p, dt);
            DrawFlash(p, area);
        }

        /// <summary>Ghost road overlay is a visual-only preview, never modifies ownership.</summary>
        void DrawConstructionPreview(Painter2D p)
        {
            if (string.IsNullOrEmpty(PreviewRoadId)) return;
            var road = _game.Map.GetRoad(PreviewRoadId);
            if (road == null || _game.GetRoadOwner(road.Id).HasValue || !RoadVisible(road)) return;
            Color color = PreviewAllowed
                ? (PreviewFree ? new Color(0.40f, 0.88f, 1f) : new Color(0.44f, 0.96f, 0.64f))
                : new Color(1f, 0.39f, 0.35f);
            var segments = _layout.GetSegments(road);
            float alpha = Ui.ReducedMotion ? 0.75f : 0.68f + 0.15f * Mathf.Sin(PulseTime * 3f);
            p.lineCap = LineCap.Round;
            foreach (var segment in segments)
            {
                p.strokeColor = new Color(color.r, color.g, color.b, 0.16f * alpha);
                p.lineWidth = 15f;
                SegmentPath(p, segment, 3.5f, Vector2.zero);
                p.Stroke();
                p.strokeColor = new Color(color.r, color.g, color.b, alpha);
                p.lineWidth = 2.5f;
                SegmentPath(p, segment, 3.5f, Vector2.zero);
                p.Stroke();
            }
        }

        /// <summary>Compact warning signs for traffic, roadworks, parades and snow.
        /// The simulation already owns those states; this paints only their meaning.</summary>
        void DrawRoadWarnings(Painter2D p)
        {
            int signs = 0;
            foreach (var road in _game.Map.Roads)
            {
                if (signs >= 28) break; // avoid flooding dense maps
                if (!RoadVisible(road)) continue;
                Color color;
                if (_game.IsJammed(road.Id)) color = new Color(1f, 0.35f, 0.25f);
                else if (_game.IsUnderRepair(road.Id) || _game.IsScheduledRepair(road.Id))
                    color = new Color(1f, 0.68f, 0.22f);
                else if (_game.IsParadeClosed(road.Id)) color = new Color(0.80f, 0.49f, 1f);
                else if (road.Resource == Resource.Gravel && _game.CurrentEvent?.Kind == CityEventKind.Snow)
                    color = new Color(0.68f, 0.86f, 1f);
                else continue;

                var segments = _layout.GetSegments(road);
                if (segments.Count == 0) continue;
                var center = segments[segments.Count / 2].Center + new Vector2(0f, -10f);
                float opacity = GameFxTuning.RoadWarningOpacity(PulseTime, Ui.ReducedMotion);
                // Dark rim keeps the marker legible over bright 3D terrain.
                p.fillColor = new Color(0.07f, 0.08f, 0.12f, 0.85f);
                CirclePath(p, center, 7.5f);
                p.Fill();
                p.fillColor = new Color(color.r, color.g, color.b, opacity);
                CirclePath(p, center, 5.5f);
                p.Fill();
                p.strokeColor = new Color(0.08f, 0.09f, 0.12f, 0.95f);
                p.lineWidth = 1.7f;
                Line(p, center + new Vector2(0f, -2.6f), center + new Vector2(0f, 1f));
                p.Stroke();
                p.fillColor = new Color(0.08f, 0.09f, 0.12f, 0.95f);
                CirclePath(p, center + new Vector2(0f, 3f), 0.85f);
                p.Fill();
                signs++;
            }
        }

        float _lastDrawTime;

        // ================================================================== Атмосфера и свет
        // Время суток идёт вместе с партией: утро в начале, день, золотой час к «скоро финал», ночь
        // в последнем круге. От часа зависят направление и сила солнца (свет с одной стороны поля,
        // тени от фишек и конусов), цвет неба и фонари. Снег и конусы на ремонте — как раньше.
        // Все уровни меняются плавно, а не скачком.

        float _hour;       // 6.5 — утро … 22.5 — ночь
        float _daylight;   // 0..1 — солнце над горизонтом
        float _night;      // 0..1 — ночь
        float _golden;     // 0..1 — золотой час перед закатом
        float _morning;    // 0..1 — розовое утро
        float _twilight;   // 0..1 — синие сумерки после заката
        float _lamps;      // 0..1 — фонари включены
        float _sunX;       // +1 — солнце справа (утро, восток), −1 — слева (вечер, запад)
        Vector2 _shadowDir; // куда падают тени (на пиксель высоты предмета)
        float _snowCover;  // 0..1 — снег на земле
        float _snowfall;   // 0..1 — идёт ли снег

        /// <summary>Час на поле по ходу партии: чем меньше участков осталось у лидера, тем позже.</summary>
        float HourTarget()
        {
            if (_game == null) return 9f;
            if (_game.Phase == GamePhase.GameOver) return 22.5f;
            if (_game.Phase == GamePhase.SetupContracts) return 6.5f;
            int start = _game.Config.RoadPiecesFor(_game.Players.Count);
            int least = _game.Players.Count == 0 ? start : _game.Players.Min(pl => pl.RoadPieces);
            float span = Mathf.Max(1f, start - _game.Config.FinalWarningPieces);
            float hour = Mathf.Lerp(6.5f, 18.5f, Mathf.Clamp01((start - least) / span));
            if (_game.FinalWarned) hour = Mathf.Max(hour, 19.5f);
            if (_game.IsFinalRound) hour = Mathf.Max(hour, 21.8f);
            return hour;
        }

        void UpdateAtmosphere(float dt)
        {
            _hour = Mathf.MoveTowards(_hour, HourTarget(), dt * 1.2f); // час игры — меньше секунды
            UpdateLight();

            bool snowing = _game.CurrentEvent != null && _game.CurrentEvent.Kind == CityEventKind.Snow;
            bool winter = _game.Scenario == ScenarioKind.Winter;
            float coverTarget = winter ? (snowing ? 1f : 0.75f) : snowing ? 0.6f : 0f;
            float fallTarget = snowing ? 1f : winter ? 0.35f : 0f;
            float step = dt * 0.6f; // за пару секунд
            _snowCover = Mathf.MoveTowards(_snowCover, coverTarget, step);
            _snowfall = Mathf.MoveTowards(_snowfall, fallTarget, step);
        }

        /// <summary>Свет от часа: восход в 5, закат в 21 (летняя Москва), высота солнца — синусоида.</summary>
        void UpdateLight()
        {
            float phase = Mathf.PI * (_hour - 5f) / 16f;
            float altitude = Mathf.Sin(phase);
            bool afternoon = _hour > 13f;
            _daylight = Smooth(-0.05f, 0.30f, altitude);
            _night = Smooth(0.12f, -0.15f, altitude);
            _golden = afternoon ? Smooth(0.45f, 0.10f, altitude) * (1f - _night) : 0f;
            _morning = afternoon ? 0f : Smooth(0.42f, 0.12f, altitude);
            _twilight = afternoon ? Smooth(0.15f, -0.02f, altitude) * (1f - _night) : 0f;
            _lamps = afternoon ? Smooth(0.28f, 0.02f, altitude) : Smooth(0.10f, -0.05f, altitude);
            _sunX = Mathf.Cos(phase);
            // Низкое солнце — длинные тени; падают от солнца и чуть «к зрителю».
            float length = Mathf.Lerp(2.4f, 0.8f, Mathf.Clamp01(altitude));
            _shadowDir = new Vector2(-_sunX, 0.45f).normalized * length;
        }

        /// <summary>Часы на табличке: «14:20 · день». Обновляются раз в 10 игровых минут.</summary>
        void UpdateClock()
        {
            if (_clockLabel == null) return;
            int tenMinutes = Mathf.FloorToInt(_hour * 6f);
            if (tenMinutes == _clockShown) return;
            _clockShown = tenMinutes;
            int hours = (tenMinutes / 6) % 24;
            int minutes = tenMinutes % 6 * 10;
            string part = LocalizationService.Language == "en"
                ? (_hour < 11f ? "morning" : _hour < 17f ? "day" : _hour < 21.3f ? "evening" : "night")
                : (_hour < 11f ? "утро" : _hour < 17f ? "день" : _hour < 21.3f ? "вечер" : "ночь");
            _clockLabel.text = $"{hours:00}:{minutes:00} · {part}";
        }

        void DrawAtmosphere(Painter2D p, Rect area)
        {
            if (_snowCover > 0.01f)
            {
                p.fillColor = new Color(0.93f, 0.96f, 1f, 0.20f * _snowCover);
                RectPath(p, area);
                p.Fill();
            }

            // В 3D свет, сумерки и туман дают солнце и луна сцены — плоские заливки не нужны.
            if (_use3D) return;

            // Направленный свет: сторона к солнцу чуть теплее и светлее, дальняя — в лёгкой тени.
            // Слои-полосы накладываются друг на друга, поэтому переход плавный, без ступенек.
            if (_daylight > 0.01f && Mathf.Abs(_sunX) > 0.05f)
            {
                const int bands = 14;
                float strength = Mathf.Abs(_sunX) * _daylight;
                for (int i = 0; i < bands; i++)
                {
                    float from = 0.5f + 0.5f * i / bands; // от середины к краю
                    float lit = from * area.width;
                    var warm = new Color(1f, 0.93f, 0.76f, 0.10f * strength / bands);
                    var shade = new Color(0.04f, 0.06f, 0.14f, 0.12f * strength / bands);
                    var sunSide = _sunX > 0f
                        ? new Rect(area.x + lit, area.y, area.width - lit, area.height)
                        : new Rect(area.x, area.y, area.width - lit, area.height);
                    var shadeSide = _sunX > 0f
                        ? new Rect(area.x, area.y, area.width - lit, area.height)
                        : new Rect(area.x + lit, area.y, area.width - lit, area.height);
                    p.fillColor = warm;
                    RectPath(p, sunSide);
                    p.Fill();
                    p.fillColor = shade;
                    RectPath(p, shadeSide);
                    p.Fill();
                }
            }

            Wash(p, area, new Color(1f, 0.70f, 0.62f), 0.10f * _morning);   // розовое утро
            Wash(p, area, new Color(1f, 0.50f, 0.18f), 0.15f * _golden);    // золотой час
            Wash(p, area, new Color(0.32f, 0.18f, 0.45f), 0.14f * _twilight); // сиреневые сумерки
            Wash(p, area, new Color(0.04f, 0.06f, 0.18f), 0.45f * _night);  // ночь
        }

        static void Wash(Painter2D p, Rect area, Color color, float alpha)
        {
            if (alpha < 0.005f) return;
            color.a = alpha;
            p.fillColor = color;
            RectPath(p, area);
            p.Fill();
        }

        /// <summary>Фонари у мест: тёплые лужи света на земле, когда темнеет.</summary>
        void DrawStreetLights(Painter2D p)
        {
            if (_lamps < 0.02f) return;
            float radius = Mathf.Max(18f, _layout.Area.height * 0.03f);
            foreach (var location in _game.Map.Locations)
            {
                if (!_game.IsLocationOpen(location.Id)) continue;
                var center = _layout.LocationPosition(location.Id);
                float flicker = 0.92f + 0.08f * Mathf.Sin(PulseTime * 3f + location.Id.GetHashCode() % 7);
                for (int ring = 3; ring >= 1; ring--)
                {
                    p.fillColor = new Color(1f, 0.80f, 0.45f, 0.10f * _lamps * flicker);
                    GroundEllipse(p, center, radius * ring * 0.8f);
                    p.Fill();
                }
            }
        }

        /// <summary>Дорожные конусы на закрытых свободных участках (ремонт, «Год ремонта», парад).</summary>
        void DrawCones(Painter2D p)
        {
            // Тень конуса ложится от солнца; ночью — короткая и бледная.
            var shadow = new Vector2(1.5f, 0f) + _shadowDir * 3f * _daylight;
            float shadowAlpha = 0.15f + 0.20f * _daylight;
            foreach (var road in _game.Map.Roads)
            {
                if (_game.GetRoadOwner(road.Id).HasValue || !_game.IsRoadOpen(road.Id)) continue;
                if (!_game.IsUnderRepair(road.Id) && !_game.IsParadeClosed(road.Id)) continue;
                s_roadWidthScale = RoadWidthScale(road);
                var segments = _layout.GetSegments(road);
                if (segments.Count == 0) continue;
                // Конусы по краям первого и последнего участка — «въезд закрыт».
                foreach (var segment in new[] { segments[0], segments[segments.Count - 1] })
                {
                    float rad = segment.Angle * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    var normal = new Vector2(-dir.y, dir.x);
                    float side = BoardLayout.RoadThickness * s_roadWidthScale * 0.9f;
                    DrawCone(p, segment.Center + normal * side, shadow, shadowAlpha);
                    DrawCone(p, segment.Center - normal * side, shadow, shadowAlpha);
                }
            }
        }

        static void DrawCone(Painter2D p, Vector2 at, Vector2 shadow, float shadowAlpha)
        {
            const float h = 11f, w = 8f;
            p.fillColor = new Color(0f, 0f, 0f, shadowAlpha);
            EllipsePath(p, at + new Vector2(0f, h * 0.45f) + shadow, w * 0.6f, w * 0.4f);
            p.Fill();
            p.fillColor = new Color(1f, 0.45f, 0.08f);
            p.BeginPath();
            p.MoveTo(at + new Vector2(0f, -h * 0.55f));
            p.LineTo(at + new Vector2(w / 2f, h * 0.45f));
            p.LineTo(at + new Vector2(-w / 2f, h * 0.45f));
            p.ClosePath();
            p.Fill();
            p.strokeColor = Color.white;
            p.lineWidth = 1.6f;
            Line(p, at + new Vector2(-w * 0.24f, 0f), at + new Vector2(w * 0.24f, 0f));
        }

        /// <summary>Снегопад: хлопья падают с покачиванием; положение — от времени, без хранения частиц.</summary>
        void DrawSnowfall(Painter2D p, Rect area)
        {
            if (_snowfall < 0.01f) return;
            int count = Mathf.RoundToInt(180f * _snowfall);
            for (int i = 0; i < count; i++)
            {
                float seed = i * 12.9898f;
                float rx = Frac(Mathf.Sin(seed) * 43758.5453f);
                float ry = Frac(Mathf.Sin(seed * 1.7f) * 24634.6345f);
                float speed = 0.05f + 0.06f * Frac(rx * 7.3f);
                float y = Frac(ry + PulseTime * speed) * (area.height + 20f) - 10f;
                float x = rx * area.width + Mathf.Sin(PulseTime * (0.6f + rx) + i) * 12f;
                float size = 1.1f + 1.6f * Frac(ry * 5.1f);
                p.fillColor = new Color(1f, 1f, 1f, (0.45f + 0.4f * Frac(rx * 3.7f)) * _snowfall);
                CirclePath(p, new Vector2(x, y), size);
                p.Fill();
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>Затемнение всей карты и поверх него — два района контракта и путь между ними.</summary>
        void DrawContractHighlight(Painter2D p, Rect area)
        {
            // Затемнение и районы проявляются плавно (уровни ведёт Animate).
            var dim = Dimmer;
            dim.a *= Smooth01(_dimLevel);
            p.fillColor = dim;
            RectPath(p, area);
            p.Fill();

            float pulse = 0.5f + 0.5f * Mathf.Sin(PulseTime * 4f);
            float radius = Mathf.Max(32f, _layout.Area.height * 0.045f);
            foreach (var location in _game.Map.Locations)
            {
                float lit = DistrictLit(location.DistrictId);
                if (lit < 0.01f || !_game.IsLocationOpen(location.Id)) continue;
                var color = DistrictLitColor(location.DistrictId);
                color.a = (0.30f + 0.12f * pulse) * lit;
                p.fillColor = color;
                // Пятно района «распускается» из места с лёгким перелётом.
                GroundEllipse(p, _layout.LocationPosition(location.Id), radius * (0.55f + 0.45f * EaseOutBack(lit)));
                p.Fill();
            }

            foreach (var road in _game.Map.Roads)
                if (PathRoads.ContainsKey(road.Id) && RoadVisible(road))
                {
                    DrawRoadFlat(p, road);
                    DrawRoadOver(p, road);
                }

            foreach (var location in _game.Map.Locations)
                if (DistrictLit(location.DistrictId) > 0.01f && _game.IsLocationOpen(location.Id)) DrawLocation(p, location);
        }

        void DrawDistrictZones(Painter2D p)
        {
            // Каждый район — мягкое цветное пятно вокруг его мест.
            float radius = Mathf.Max(32f, _layout.Area.height * 0.045f); // мест много — зоны поменьше
            foreach (var location in _game.Map.Locations)
            {
                if (!_game.IsLocationOpen(location.Id)) continue;
                int index = _districtIndex.TryGetValue(location.DistrictId, out var i) ? i : 0;
                var color = Palette.DistrictColor(index);
                var center = _layout.LocationPosition(location.Id);
                // На траве и крышах — полупрозрачная заливка и чёткий контур цвета района.
                // Зона лежит на земле: с наклоном она — эллипс, а вдали меньше.
                float rx = radius * _layout.ScaleAt(center);
                float ry = rx * _layout.Squash;
                p.fillColor = new Color(color.r, color.g, color.b, 0.16f);
                EllipsePath(p, center, rx, ry);
                p.Fill();
                p.strokeColor = new Color(color.r, color.g, color.b, 0.70f);
                p.lineWidth = 2f;
                EllipsePath(p, center, rx, ry);
                p.Stroke();
            }
        }

        void DrawRiver(Painter2D p)
        {
            // Рек может быть несколько (Чунцин — Янцзы и Цзялинцзян): у каждой своя набережная.
            foreach (var points in _game.Map.Rivers)
            {
                if (points.Count < 2) continue;
                var screen = points.Select(pt => _layout.ToScreen(pt.X, pt.Y)).ToList();

                // Набережные: тёмный край и светлый гранит; сама вода — текстурой поверх.
                p.strokeColor = new Color(0.40f, 0.39f, 0.35f, 0.9f);
                p.lineWidth = 30f;
                SmoothPath(p, screen);
                p.Stroke();
                // Сама вода — текстурой в отдельном слое (DrawWater); здесь гранитная кромка набережной.
                p.strokeColor = new Color(0.64f, 0.62f, 0.56f, 1f);
                p.lineWidth = 27f;
                SmoothPath(p, screen);
                p.Stroke();
            }
        }

        void DrawRing(Painter2D p)
        {
            if (_game.Map.Ring == null) return;
            // МКАД днём: асфальтовая лента со светлыми кромками и жёлтым пунктиром посередине.
            const int pieces = 320;
            var points = new List<Vector2>();
            for (int i = 0; i <= pieces; i++) points.Add(RingPoint(i * 2f * Mathf.PI / pieces));
            p.strokeColor = new Color(0.85f, 0.85f, 0.82f, 1f);
            p.lineWidth = 11f;
            PolyPath(p, points);
            p.Stroke();
            p.strokeColor = new Color(0.27f, 0.28f, 0.30f, 1f);
            p.lineWidth = 9f;
            PolyPath(p, points);
            p.Stroke();
            p.strokeColor = new Color(0.95f, 0.85f, 0.40f, 0.9f);
            p.lineWidth = 1f;
            for (int i = 0; i + 2 <= pieces; i += 4) Line(p, points[i], points[i + 2]);
        }

        /// <summary>Сколько участков дороги уже лежит (с учётом анимации укладки).</summary>
        int BuiltSegments(Road road, int count)
        {
            if (!_game.GetRoadOwner(road.Id).HasValue) return 0;
            if (BuildProgress.TryGetValue(road.Id, out var progress))
                return Mathf.Clamp(Mathf.FloorToInt(progress * count + 0.001f), 0, count);
            return count;
        }

        static Vector2 Dir(BoardLayout.Segment segment)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        // ---------------------------------------------------------------- Состояние дороги

        /// <summary>
        /// Визуальная иерархия дорог: построенные; активные — можно строить прямо сейчас (яркие, со свечением);
        /// заблокированные — закрыты или сейчас не по карману (приглушены); обычные — когда никто из людей не выбирает.
        /// </summary>
        enum RoadLook { Built, Active, Idle, Blocked }

        RoadLook LookOf(Road road, int? owner)
        {
            if (owner.HasValue) return RoadLook.Built;
            if (_game.IsUnderRepair(road.Id) || _game.IsParadeClosed(road.Id)) return RoadLook.Blocked;
            if (Affordable.Count == 0) return RoadLook.Idle;
            return Affordable.Contains(road.Id) ? RoadLook.Active : RoadLook.Blocked;
        }

        /// <summary>Наведённая или выбранная для стройки дорога — в светлом контуре.</summary>
        bool IsSelected(Road road) => road == HoverRoad || (SelectedRoadId != null && road.Id == SelectedRoadId);

        // ---------------------------------------------------------------- Проход 1: свечение под дорогой

        void DrawRoadsUnder(MeshGenerationContext context)
        {
            if (!CanDraw) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            if (!_use3D) DrawJunctions(p);   // в 3D перекрёстки строит сама сцена
            foreach (var road in _game.Map.Roads)
                if (RoadVisible(road)) DrawRoadUnder(p, road);
        }

        /// <summary>Высота дороги над городом (в единицах мира): обычная 0.1, магистраль 0.2, контракт/активная 0.3.</summary>
        float RoadLift(Road road, RoadLook look, int? owner)
        {
            if (PathRoads.ContainsKey(road.Id) || look == RoadLook.Active) return 0.3f;
            if (owner.HasValue && _game.IsRushHourRoad(road.Id)) return 0.2f;
            return 0.1f;
        }

        /// <summary>
        /// Множитель ширины дороги на экране для текущей рисуемой дороги. В 3D дорога на экране ровно такой же
        /// ширины, как 3D-дорога и мост под ней (City3DScene.RoadWidth метров), при любом зуме и наклоне;
        /// без 3D — 1 (прежние 11 px).
        /// </summary>
        static float s_roadWidthScale = 1f;

        readonly List<Vector2> _junctionNeighbours = new List<Vector2>();
        readonly List<Vector2> _junctionDirs = new List<Vector2>();
        readonly List<JunctionGeometry.Corner> _junctionCorners = new List<JunctionGeometry.Corner>();
        static readonly Color JunctionColor = new Color(0.19f, 0.20f, 0.22f, 0.92f);

        /// <summary>
        /// Плоская карта: каждая станция — перекрёсток. Под фишкой станции — асфальтовый пятачок, рукава по
        /// открытым дорогам до начала дороги (с нахлёстом) и скругления бордюра между соседними рукавами.
        /// </summary>
        void DrawJunctions(Painter2D p)
        {
            float half = BoardLayout.RoadThickness / 2f;
            float arm = BoardLayout.LocationRadius + 5f;     // дорога начинается в LocationRadius + 3
            const float fillet = 7f;
            p.fillColor = JunctionColor;
            foreach (var location in _game.Map.Locations)
            {
                if (!_game.IsLocationOpen(location.Id)) continue;
                var center = _layout.LocationPosition(location.Id);
                _junctionNeighbours.Clear();
                foreach (var road in _game.Map.Roads)
                {
                    if (!_game.IsRoadOpen(road.Id)) continue;
                    string other = road.A == location.Id ? road.B : road.B == location.Id ? road.A : null;
                    if (other != null) _junctionNeighbours.Add(_layout.LocationPosition(other));
                }
                JunctionGeometry.Directions(center, _junctionNeighbours, _junctionDirs);
                if (_junctionDirs.Count == 0) continue;

                CirclePath(p, center, half);
                p.Fill();
                foreach (var d in _junctionDirs)
                {
                    var n = new Vector2(-d.y, d.x);
                    p.BeginPath();
                    p.MoveTo(center - n * half);
                    p.LineTo(center + n * half);
                    p.LineTo(center + n * half + d * arm);
                    p.LineTo(center - n * half + d * arm);
                    p.ClosePath();
                    p.Fill();
                }
                JunctionGeometry.Corners(center, _junctionDirs, half, arm, fillet, _junctionCorners);
                foreach (var corner in _junctionCorners)
                {
                    p.BeginPath();
                    p.MoveTo(corner.X);
                    p.LineTo(corner.T1);
                    for (int j = 1; j <= 6; j++) p.LineTo(JunctionGeometry.Curve(corner, j / 6f));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }

        float RoadWidthScale(Road road)
        {
            if (!_use3D) return 1f;
            var la = _game.Map.GetLocation(road.A);
            var lb = _game.Map.GetLocation(road.B);
            if (la == null || lb == null) return 1f;
            var a = BoardLayout.World(la.X, la.Y);
            var b = BoardLayout.World(lb.X, lb.Y);
            var along = b - a;
            along.y = 0f;
            if (along.sqrMagnitude < 1e-6f) return 1f;
            along.Normalize();
            var across = new Vector3(-along.z, 0f, along.x);
            var mid = (a + b) * 0.5f;
            float perMeter = (_layout.WorldToScreen(mid + across * 0.5f) - _layout.WorldToScreen(mid - across * 0.5f)).magnitude;
            return Mathf.Clamp(perMeter * City3DScene.RoadWidth / BoardLayout.RoadThickness, 0.4f, 3.5f);
        }

        void DrawRoadUnder(Painter2D p, Road road)
        {
            _fade = RoadFade(road);
            s_roadWidthScale = RoadWidthScale(road);
            int? owner = _game.GetRoadOwner(road.Id);
            var segments = _layout.GetSegments(road);
            int built = BuiltSegments(road, segments.Count);
            var look = LookOf(road, owner);
            float tilt = _layout.Tilt;

            // 3D: построенные дороги — 3D-объекты сцены со своими тенями и торцами; плоский слой их не рисует.
            if (_use3D) { }
            // 2.5D: у уложенного участка есть толщина — тёмный торец чуть ниже покрытия.
            else if (built > 0 && tilt > 0.01f)
            {
                p.fillColor = Fade(new Color(0.04f, 0.04f, 0.05f, 0.85f));
                for (int i = 0; i < built; i++)
                {
                    SegmentPath(p, segments[i], 0.6f, new Vector2(0f, 2.4f * tilt));
                    p.Fill();
                }
            }

            // Активная дорога: двойное свечение — широкий мягкий ореол и яркая сердцевина, пульсируют.
            if (look == RoadLook.Active && !PathRoads.ContainsKey(road.Id) && !CalmRoads)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(PulseTime * 3.2f);
                p.strokeColor = Fade(new Color(Glow.r, Glow.g, Glow.b, 0.10f + 0.10f * pulse));
                p.lineWidth = 16f;
                foreach (var segment in segments)
                {
                    SegmentPath(p, segment, 1.5f, Vector2.zero);
                    p.Stroke();
                }
            }

            // Обводка пути контракта, выбранная дорога, доступные дороги.
            Color? glow = null;
            float glowWidth = 0f;
            if (PathRoads.TryGetValue(road.Id, out var pathColor))
            {
                glow = pathColor;
                glowWidth = 9f;
            }
            else if (IsSelected(road))
            {
                // Контур рисуется поверх (проход 3), здесь — мягкий ореол под ним.
                glow = new Color(1f, 1f, 1f, 0.30f * _hoverFade);
                glowWidth = 12f;
            }
            else if (DashedFreeRoads && !owner.HasValue)
            {
                // Пунктирные (ещё не построенные) дороги — без ореола: ни белого, ни жёлтого.
            }
            else if (look == RoadLook.Active && CalmRoads)
            {
                glow = new Color(1f, 1f, 1f, 0.10f);
                glowWidth = 5f;
            }
            else if (look == RoadLook.Active)
            {
                glow = new Color(Glow.r, Glow.g, Glow.b, 0.50f + 0.25f * Mathf.Sin(PulseTime * 3.2f));
                glowWidth = 6f;
            }
            // Способность владельца действует на эту дорогу (мост Мостовика, дорога Сборщика пошлин) — его цветом.
            else if (owner.HasValue && AbilityGlow != null && AbilityGlow(road) is Color ability)
            {
                glow = new Color(ability.r, ability.g, ability.b, 0.40f + 0.22f * Mathf.Sin(PulseTime * 2.6f));
                glowWidth = road.IsBridge ? 13f : 9f;
            }
            // Субсидия мэрии: свободные мосты подсвечены голубым.
            else if (look == RoadLook.Idle && road.IsBridge && _game.CurrentEvent != null && _game.CurrentEvent.Kind == CityEventKind.Subsidy)
            {
                glow = new Color(0.45f, 0.78f, 1f, 0.45f + 0.25f * Mathf.Sin(PulseTime * 4f));
                glowWidth = 7f;
            }
            if (glow.HasValue)
            {
                p.strokeColor = Fade(glow.Value);
                p.lineWidth = glowWidth;
                foreach (var segment in segments)
                {
                    SegmentPath(p, segment, 1.5f, Vector2.zero);
                    p.Stroke();
                }
            }

            // Свет по маршруту выполненного контракта: волна идёт от начала пути к концу.
            if (RoutePath != null)
            {
                int index = -1;
                for (int i = 0; i < RoutePath.Count; i++)
                    if (RoutePath[i] == road.Id) { index = i; break; }
                if (index >= 0)
                {
                    float head = RouteProgress * (RoutePath.Count + 1.5f);
                    float lit = Mathf.Clamp01(head - index);
                    float fade = RouteProgress > 0.75f ? (1f - RouteProgress) / 0.25f : 1f;
                    float alpha = lit * fade;
                    if (alpha > 0.01f)
                    {
                        int count = Mathf.Clamp(Mathf.CeilToInt(lit * segments.Count), 0, segments.Count);
                        p.strokeColor = Fade(new Color(RouteColor.r, RouteColor.g, RouteColor.b, 0.85f * alpha));
                        p.lineWidth = 12f;
                        for (int i = 0; i < count; i++)
                        {
                            SegmentPath(p, segments[i], 1.5f, Vector2.zero);
                            p.Stroke();
                        }
                        p.strokeColor = Fade(new Color(1f, 1f, 1f, 0.9f * alpha));
                        p.lineWidth = 3.5f;
                        for (int i = 0; i < count; i++)
                        {
                            SegmentPath(p, segments[i], 1.5f, Vector2.zero);
                            p.Stroke();
                        }
                    }
                }
            }

            // Построенные участки слегка светятся цветом компании (ночью — сильнее: город в огнях).
            // Городские дороги магната — без свечения: это просто асфальт. В 3D цвет компании — у бордюров сцены.
            if (owner.HasValue && !_use3D && !(PlainPublicRoads && IsPublicRoad(owner)))
            {
                var c = OwnerColor(owner.Value);
                p.strokeColor = Fade(CalmRoads ? new Color(c.r, c.g, c.b, 0.16f + 0.14f * _night) : new Color(c.r, c.g, c.b, 0.30f + 0.22f * _night));
                p.lineWidth = 7f;
                for (int i = 0; i < built; i++)
                {
                    SegmentPath(p, segments[i], 2.5f, Vector2.zero);
                    p.Stroke();
                }
            }
            _fade = 1f;
        }

        // ---------------------------------------------------------------- Проход 2: текстуры покрытий

        void DrawRoadTextures(MeshGenerationContext context)
        {
            if (!CanDraw) return;
            _quads.Clear();
            foreach (var road in _game.Map.Roads)
            {
                if (!RoadVisible(road)) continue;
                var texture = CalmRoads ? (road.IsBridge ? RoadTextures.SteelDeck : RoadTextures.Asphalt) : RoadTextures.For(road);
                s_roadWidthScale = RoadWidthScale(road);
                var segments = _layout.GetSegments(road);
                int built = BuiltSegments(road, segments.Count);
                var look = LookOf(road, _game.GetRoadOwner(road.Id));
                // Свободный участок — проступающее «будущее» покрытие: у активной дороги ярче,
                // у заблокированной — бледное и серое.
                // Магнат: свободные участки — как дороги «из любых материалов» в соревновании (брусчатка с контуром).
                var freeTexture = DashedFreeRoads ? RoadTextures.Cobble : texture;
                var free = CalmRoads && !DashedFreeRoads
                    ? new Color(0.82f, 0.84f, 0.88f, look == RoadLook.Active ? 0.5f : look == RoadLook.Blocked ? 0.22f : 0.38f)
                    : look == RoadLook.Active ? new Color(1f, 1f, 1f, 0.85f)
                    : look == RoadLook.Blocked ? new Color(0.70f, 0.70f, 0.74f, 0.25f)
                    : new Color(1f, 1f, 1f, 0.55f);
                float fade = RoadFade(road);
                // Городская дорога магната — чуть темнее своих: фон, а не акцент.
                var builtTint = PlainPublicRoads && IsPublicRoad(_game.GetRoadOwner(road.Id))
                    ? new Color(0.86f, 0.86f, 0.88f, fade)
                    : new Color(1f, 1f, 1f, fade);
                free.a *= fade;
                int count = segments.Count;
                // В 3D построенные участки — асфальт сцены; плоский слой кладёт только «будущее» покрытие.
                int first = _use3D ? built : 0;
                for (int i = first; i < count; i++)
                    AddSegmentTexture(i < built ? texture : freeTexture, segments[i], i < built ? builtTint : free);
            }
            _quads.Flush(context);
        }

        /// <summary>Участок — несколько квадратных «плиток» текстуры по длине (без растяжения).</summary>
        void AddSegmentTexture(Texture2D texture, BoardLayout.Segment segment, Color tint)
        {
            if (segment.Quad)
            {
                // Участок на земле: плитки делят трапецию по длине, каждая — свой четырёхугольник с UV 0..1.
                float width = ((segment.Q0 - segment.Q3).magnitude + (segment.Q1 - segment.Q2).magnitude) * 0.5f;
                int count = Mathf.Max(1, Mathf.RoundToInt(segment.Length / Mathf.Max(2f, width)));
                for (int k = 0; k < count; k++)
                {
                    float t0 = k / (float)count, t1 = (k + 1) / (float)count;
                    _quads.Add(texture,
                        Vector2.Lerp(segment.Q0, segment.Q1, t0), Vector2.Lerp(segment.Q0, segment.Q1, t1),
                        Vector2.Lerp(segment.Q3, segment.Q2, t1), Vector2.Lerp(segment.Q3, segment.Q2, t0),
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), tint);
                }
                return;
            }
            var dir = Dir(segment);
            float thickness = BoardLayout.RoadThickness * s_roadWidthScale;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(segment.Length / thickness));
            float tileLength = segment.Length / tiles;
            var start = segment.Center - dir * segment.Length / 2f;
            for (int k = 0; k < tiles; k++)
            {
                var center = start + dir * (tileLength * (k + 0.5f));
                _quads.AddStrip(texture, center, dir, tileLength, thickness, 0f, 1f, tint);
            }
        }

        // ---------------------------------------------------------------- Проход 3: детали поверх

        void DrawRoadOver(Painter2D p, Road road)
        {
            _fade = RoadFade(road);
            s_roadWidthScale = RoadWidthScale(road);
            int? owner = _game.GetRoadOwner(road.Id);
            var segments = _layout.GetSegments(road);
            int built = BuiltSegments(road, segments.Count);
            var baseColor = CalmRoads ? CalmRoadColor : NightRoadColor(road);
            var ownerColor = owner.HasValue ? OwnerColor(owner.Value) : baseColor;
            bool isBridge = road.IsBridge;
            // В 3D мост той же ширины, что дорога: рамки и перила не выходят за полотно.
            bool wideBridge = isBridge && !_use3D;
            float half = BoardLayout.RoadThickness * s_roadWidthScale / 2f;
            var look = LookOf(road, owner);
            bool selected = IsSelected(road);

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var dir = Dir(segment);
                var normal = new Vector2(-dir.y, dir.x);
                var a = segment.Center - dir * segment.Length / 2f;
                var b = segment.Center + dir * segment.Length / 2f;
                bool isBuilt = i < built;

                if (isBuilt && _use3D)
                {
                    // 3D: бордюры, разметка и машины — в сцене. Здесь остаются только рамки (магистраль, пробка, выбор).
                }
                else if (isBuilt)
                {
                    // Бордюры цвета компании по обеим сторонам — так видно, чья дорога.
                    // У городских дорог магната бордюров нет: они не подсвечиваются.
                    if (!(PlainPublicRoads && IsPublicRoad(owner)))
                    {
                        p.strokeColor = Fade(ownerColor);
                        p.lineWidth = 2.4f;
                        Line(p, SidePoint(segment, 0f, 1f), SidePoint(segment, 1f, 1f));
                        Line(p, SidePoint(segment, 0f, -1f), SidePoint(segment, 1f, -1f));
                    }
                    // Магистраль: двойная разделительная полоса и бегущий пунктир по обеим полосам движения.
                    if (_game.IsRushHourRoad(road.Id)) DrawHighwayMarks(p, a, b, dir, normal, half, i);
                    // Спокойные дороги: одна неяркая белая разметка у всех.
                    else if (CalmRoads) DrawLaneMark(p, segment, Fade(new Color(1f, 1f, 1f, PlainPublicRoads && IsPublicRoad(owner) ? 0.30f : 0.42f)));
                    // Разметка: асфальт — белый пунктир, бетон — жёлтая осевая.
                    else if (road.Resource == Resource.Asphalt) DrawLaneMark(p, segment, Fade(new Color(1f, 1f, 1f, 0.85f)));
                    else if (road.Resource == Resource.Concrete) DrawLaneMark(p, segment, Fade(new Color(1f, 0.82f, 0.25f, 0.85f)));
                }
                else
                {
                    // Свободный участок — неоновая обводка цвета ресурса: у активной — яркая и толще,
                    // у заблокированной — серая и тонкая.
                    {
                        // Магнат: свободный участок — как дорога «из любых материалов» в соревновании: светлый контур.
                        var freeColor = DashedFreeRoads ? AnyRoadColor : baseColor;
                        if (CalmRoads && !DashedFreeRoads)
                        {
                            p.strokeColor = Fade(new Color(baseColor.r, baseColor.g, baseColor.b, look == RoadLook.Active ? 0.55f : look == RoadLook.Blocked ? 0.18f : 0.35f));
                            p.lineWidth = look == RoadLook.Active ? 1.5f : 1.1f;
                        }
                        else if (look == RoadLook.Active)
                        {
                            var bright = Color.Lerp(freeColor, Color.white, 0.35f);
                            p.strokeColor = Fade(new Color(bright.r, bright.g, bright.b, 1f));
                            p.lineWidth = 2.2f;
                        }
                        else if (look == RoadLook.Blocked)
                        {
                            var muted = Color.Lerp(freeColor, new Color(0.5f, 0.5f, 0.52f), 0.6f);
                            p.strokeColor = Fade(new Color(muted.r, muted.g, muted.b, 0.35f));
                            p.lineWidth = 1.2f;
                        }
                        else
                        {
                            p.strokeColor = Fade(new Color(freeColor.r, freeColor.g, freeColor.b, 0.85f));
                            p.lineWidth = 1.6f;
                        }
                        SegmentPath(p, segment, 0f, Vector2.zero);
                        p.Stroke();
                    }
                }

                // Мост — перила со столбиками (у пунктирного, ещё не построенного моста перил нет).
                if (isBridge && !_use3D)
                {
                    float railOffset = _use3D ? half - 1.2f : half + 2.5f;
                    float railSide = half > 0.01f ? railOffset / half : 1f;
                    var rail = new Color(0.66f, 0.82f, 1f, isBuilt ? 0.95f : look == RoadLook.Blocked ? 0.3f : 0.6f);
                    p.strokeColor = Fade(rail);
                    p.lineWidth = 1.8f;
                    Line(p, SidePoint(segment, 0f, railSide), SidePoint(segment, 1f, railSide));
                    Line(p, SidePoint(segment, 0f, -railSide), SidePoint(segment, 1f, -railSide));
                    int posts = Mathf.Max(2, Mathf.RoundToInt(segment.Length / 6f));
                    for (int k = 0; k <= posts; k++)
                    {
                        float t = k / (float)posts;
                        p.fillColor = Fade(rail);
                        CirclePath(p, SidePoint(segment, t, railSide), 1.2f);
                        p.Fill();
                        CirclePath(p, SidePoint(segment, t, -railSide), 1.2f);
                        p.Fill();
                    }
                }

                // Магистраль: оранжевая рамка вокруг каждого участка.
                if (isBuilt && _game.IsRushHourRoad(road.Id))
                {
                    p.strokeColor = Fade(new Color(1f, 0.60f, 0.20f, 0.95f));
                    p.lineWidth = 2.2f;
                    SegmentPath(p, segment, wideBridge ? 5f : 3.5f, Vector2.zero);
                    p.Stroke();
                }

                // Подсказка наставника в обучении: голубая пульсирующая рамка.
                if (!owner.HasValue && road.Id == HintRoad)
                {
                    float pulse = 0.5f + 0.45f * Mathf.Sin(PulseTime * 4f);
                    p.strokeColor = Fade(new Color(0.35f, 0.85f, 1f, pulse));
                    p.lineWidth = 3.4f;
                    SegmentPath(p, segment, wideBridge ? 7f : 5.5f, Vector2.zero);
                    p.Stroke();
                }

                // Финальный заказ мэрии: свободная дорога в золотой пульсирующей рамке.
                if (!owner.HasValue && road.Id == _game.FinalOrderRoadId)
                {
                    float glow = 0.55f + 0.4f * Mathf.Sin(PulseTime * 2.4f);
                    p.strokeColor = Fade(new Color(1f, 0.80f, 0.25f, glow));
                    p.lineWidth = 3f;
                    SegmentPath(p, segment, wideBridge ? 6f : 4.5f, Vector2.zero);
                    p.Stroke();
                }

                // Пробка: пульсирующая красная рамка (поверх магистрали её не бывает).
                if (isBuilt && _game.IsJammed(road.Id))
                {
                    float jam = 0.55f + 0.35f * Mathf.Sin(PulseTime * 3f);
                    p.strokeColor = Fade(new Color(0.95f, 0.22f, 0.18f, jam));
                    p.lineWidth = 2.4f;
                    SegmentPath(p, segment, wideBridge ? 5f : 3.5f, Vector2.zero);
                    p.Stroke();
                }

                // Ремонт и парад: красно-белые полосы поперёк участка.
                if (!owner.HasValue && (_game.IsUnderRepair(road.Id) || _game.IsParadeClosed(road.Id))) DrawRepairStripes(p, segment);
                // Год ремонта: участки, которые закроют в следующем круге, — тонкая оранжевая рамка.
                else if (!owner.HasValue && _game.IsNextRepair(road.Id))
                {
                    p.strokeColor = Fade(new Color(1f, 0.55f, 0.15f, 0.75f));
                    p.lineWidth = 1.6f;
                    SegmentPath(p, segment, wideBridge ? 5f : 3.5f, Vector2.zero);
                    p.Stroke();
                }

                // Выбранная (наведённая или открытая для оплаты) дорога: чёткий белый контур
                // и тонкая золотая обводка снаружи — проявляются за доли секунды.
                if (selected)
                {
                    p.strokeColor = Fade(new Color(1f, 1f, 1f, 0.95f * _hoverFade));
                    p.lineWidth = 2.2f;
                    SegmentPath(p, segment, wideBridge ? 6f : 4f, Vector2.zero);
                    p.Stroke();
                    p.strokeColor = Fade(new Color(Glow.r, Glow.g, Glow.b, 0.6f * _hoverFade));
                    p.lineWidth = 1.4f;
                    SegmentPath(p, segment, wideBridge ? 8.5f : 6.5f, Vector2.zero);
                    p.Stroke();
                }
            }
            _fade = 1f;
        }

        /// <summary>Плоская заливка — для пути контракта поверх затемнения (текстуры остаются под ним).</summary>
        void DrawRoadFlat(Painter2D p, Road road)
        {
            s_roadWidthScale = RoadWidthScale(road);
            int? owner = _game.GetRoadOwner(road.Id);
            var segments = _layout.GetSegments(road);
            int built = BuiltSegments(road, segments.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                p.fillColor = i < built && owner.HasValue
                    ? Color.Lerp(NightRoadColor(road), Color.black, 0.35f)
                    : Color.Lerp(Land, NightRoadColor(road), 0.22f);
                SegmentPath(p, segments[i], 0f, Vector2.zero);
                p.Fill();
            }
        }

        // ---------------------------------------------------------------- Вода

        /// <summary>
        /// Река — текстурная лента вдоль сглаженной линии русла. Текстура «течёт»: границы плиток
        /// сдвигаются со временем, поэтому UV каждой плитки остаются в 0..1 (без повтора текстуры).
        /// </summary>
        void DrawWater(MeshGenerationContext context)
        {
            if (!CanDraw || _use3D) return;
            _quads.Clear();
            foreach (var river in _game.Map.Rivers)
                if (river.Count >= 2) AddWater(river);
            _quads.Flush(context);
        }

        /// <summary>Добавить в пачку квадов воду одной реки.</summary>
        void AddWater(IReadOnlyList<MapPoint> river)
        {
            var raw = river.Select(pt => _layout.ToScreen(pt.X, pt.Y)).ToList();
            var line = SmoothLine(raw, 6);
            var distances = new List<float> { 0f };
            for (int i = 1; i < line.Count; i++) distances.Add(distances[i - 1] + (line[i] - line[i - 1]).magnitude);
            float total = distances[distances.Count - 1];
            if (total < 1f) return;

            const float width = 24f;
            const float tile = width * 2f;           // текстура воды 2:1
            float offset = Mathf.Repeat(PulseTime * 14f, tile);

            // Станции: вершины линии и границы плиток.
            var stations = new List<float>(distances);
            for (float d = -offset; d < total; d += tile)
                if (d > 0f) stations.Add(d);
            stations.Sort();

            var texture = RoadTextures.Water;
            for (int i = 0; i < stations.Count - 1; i++)
            {
                float d0 = stations[i], d1 = stations[i + 1];
                if (d1 - d0 < 0.01f) continue;
                float tileStart = Mathf.Floor((d0 + offset) / tile) * tile - offset;
                float u0 = (d0 - tileStart) / tile, u1 = (d1 - tileStart) / tile;
                PointAt(line, distances, d0, out var p0, out var n0);
                PointAt(line, distances, d1, out var p1, out var n1);
                var h0 = n0 * width / 2f;
                var h1 = n1 * width / 2f;
                _quads.Add(texture, p0 - h0, p1 - h1, p1 + h1, p0 + h0,
                    new Vector2(u0, 1f), new Vector2(Mathf.Min(1f, u1), 1f), new Vector2(Mathf.Min(1f, u1), 0f), new Vector2(u0, 0f),
                    Color.white);
            }
        }

        /// <summary>Сглаживание ломаной сплайном Катмулла — Рома: steps точек на отрезок.</summary>
        static List<Vector2> SmoothLine(List<Vector2> points, int steps)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[Mathf.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    float t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        /// <summary>Точка и нормаль на ломаной на расстоянии distance от начала.</summary>
        static void PointAt(List<Vector2> line, List<float> distances, float distance, out Vector2 point, out Vector2 normal)
        {
            int i = 1;
            while (i < distances.Count - 1 && distances[i] < distance) i++;
            float span = Mathf.Max(0.0001f, distances[i] - distances[i - 1]);
            float t = Mathf.Clamp01((distance - distances[i - 1]) / span);
            point = Vector2.Lerp(line[i - 1], line[i], t);
            var dir = (line[i] - line[i - 1]).normalized;
            normal = new Vector2(-dir.y, dir.x);
        }

        static void DrawRepairStripes(Painter2D p, BoardLayout.Segment segment)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var normal = new Vector2(-dir.y, dir.x);
            float half = BoardLayout.RoadThickness * s_roadWidthScale / 2f;
            const int stripes = 4;
            var previousCap = p.lineCap;
            float step = segment.Length / stripes;
            for (int k = 0; k < stripes; k++)
            {
                float offset = -segment.Length / 2f + step * (k + 0.5f);
                var c = segment.Center + dir * offset;
                p.strokeColor = k % 2 == 0 ? new Color(0.92f, 0.20f, 0.20f) : Color.white;
                p.lineWidth = step * 0.9f;
                p.lineCap = LineCap.Butt;
                Line(p, c - normal * half, c + normal * half);
            }
            p.lineCap = previousCap;
        }

        /// <summary>Цвета дорог для ночной карты: светлее, чем на картах в руке, чтобы читались на тёмном.</summary>
        /// <summary>Цвет дороги «из любых материалов» (соревнование) — им же контур свободных дорог магната.</summary>
        static readonly Color AnyRoadColor = new Color(0.92f, 0.92f, 0.90f);

        static Color NightRoadColor(Road road)
        {
            if (!road.Resource.HasValue) return new Color(0.92f, 0.92f, 0.90f);
            switch (road.Resource.Value)
            {
                case Resource.Asphalt: return new Color(0.52f, 0.58f, 0.70f);
                case Resource.Concrete: return new Color(0.84f, 0.84f, 0.80f);
                case Resource.Steel: return new Color(0.42f, 0.68f, 1f);
                case Resource.Gravel: return new Color(0.90f, 0.64f, 0.34f);
                default: return Palette.Card(road.Resource.Value);
            }
        }

        /// <summary>
        /// Разметка магистрали: жёлтый двойной разделитель по оси и белые штрихи по полосам — штрихи
        /// бегут навстречу друг другу (движение в обе стороны), так магистраль видна издалека.
        /// </summary>
        void DrawHighwayMarks(Painter2D p, Vector2 a, Vector2 b, Vector2 dir, Vector2 normal, float half, int index)
        {
            p.strokeColor = Fade(new Color(1f, 0.78f, 0.22f, 0.95f));
            p.lineWidth = 1f;
            Line(p, a + normal * 0.9f, b + normal * 0.9f);
            Line(p, a - normal * 0.9f, b - normal * 0.9f);

            float length = (b - a).magnitude;
            const float dash = 3.2f, gap = 3.6f;
            float period = dash + gap;
            float shift = Mathf.Repeat(PulseTime * 9f + index * 1.7f, period);
            p.strokeColor = Fade(new Color(1f, 1f, 1f, 0.8f));
            p.lineWidth = 1.1f;
            for (int lane = -1; lane <= 1; lane += 2)
            {
                var side = normal * (half * 0.52f * lane);
                float offset = lane > 0 ? shift : period - shift;
                for (float t = offset - period; t < length; t += period)
                {
                    float t0 = Mathf.Max(0f, t), t1 = Mathf.Min(length, t + dash);
                    if (t1 - t0 < 0.4f) continue;
                    Line(p, a + dir * t0 + side, a + dir * t1 + side);
                }
            }
        }

        static void DrawLaneMark(Painter2D p, BoardLayout.Segment segment, Color color)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            p.strokeColor = color;
            p.lineWidth = 1.8f;
            float half = segment.Length * 0.22f;
            Line(p, segment.Center - dir * half, segment.Center + dir * half);
        }

        /// <summary>
        /// Место — фишка на поле. С наклоном у неё есть толщина (торец снизу) и тень от солнца;
        /// фишки района выбранного контракта плавно приподнимаются над полем.
        /// </summary>
        void DrawLocation(Painter2D p, Location location)
        {
            _fade = LocationFade(location);
            var ground = _layout.LocationPosition(location.Id);
            float r = BoardLayout.LocationRadius * LocationScale;
            int index = _districtIndex.TryGetValue(location.DistrictId, out var i) ? i : 0;
            float tilt = _layout.Tilt;

            float lit = DistrictLit(location.DistrictId);
            var litColor = DistrictLitColor(location.DistrictId);
            var shape = LocationShape(location);
            float ringRadius = r + 2.5f + lit;
            bool square = shape == Shape.Airport || shape == Shape.Station;

            // Высота фишки: 3 пикселя стоя, до 8 — приподнятой. Верх при покое — ровно в точке места.
            float rise = 5f * EaseOutBack(lit) * tilt;
            float thickness = 3f * tilt;
            var c = ground - new Vector2(0f, rise);
            var foot = ground + new Vector2(0f, thickness);

            if (tilt > 0.01f)
            {
                // Тень от солнца: чем выше фишка и ниже солнце, тем дальше.
                if (_daylight > 0.02f)
                {
                    var offset = _shadowDir * (thickness + rise + 2f);
                    p.fillColor = Fade(new Color(0f, 0f, 0f, 0.30f * _daylight * tilt));
                    EllipsePath(p, foot + offset, ringRadius + 1f, (ringRadius + 1f) * _layout.Squash);
                    p.Fill();
                }
                // Торец: от верха фишки до земли, темнее кольца.
                float halfWidth = square ? ringRadius * 0.92f : ringRadius;
                p.fillColor = Fade(new Color(0.30f, 0.32f, 0.36f));
                ShapePath(p, shape, foot, ringRadius);
                p.Fill();
                RectPath(p, new Rect(c.x - halfWidth, c.y, 2f * halfWidth, foot.y - c.y));
                p.Fill();
            }

            if (lit > 0.01f)
            {
                float pulse = 1f + 0.2f * Mathf.Sin(PulseTime * 5f);
                p.fillColor = Fade(new Color(litColor.r, litColor.g, litColor.b, 0.45f * lit));
                CirclePath(p, c, (r + 10f) * pulse * (0.6f + 0.4f * lit));
                p.Fill();
            }

            var district = Color.Lerp(Palette.DistrictColor(index), litColor, lit);
            // Ореол цвета района — место «горит» на ночной карте (ночью ярче).
            p.fillColor = Fade(new Color(district.r, district.g, district.b, 0.18f + 0.22f * _night));
            CirclePath(p, c, r + 5f + 3f * _night);
            p.Fill();

            var ring = Color.Lerp(new Color(0.92f, 0.94f, 0.98f), litColor, lit);
            ShapePath(p, shape, c, ringRadius);
            p.fillColor = Fade(ring);
            p.Fill();
            ShapePath(p, shape, c, r);
            p.fillColor = Fade(new Color(0.07f, 0.08f, 0.12f));
            p.Fill();

            // Значок внутри: самолёт у аэропортов, рельсы у вокзалов, звезда у Кремля, иначе точка района.
            p.fillColor = Fade(district);
            p.strokeColor = Fade(district);
            switch (shape)
            {
                case Shape.Airport:
                    p.lineWidth = 2f;
                    Line(p, c + new Vector2(0f, -r * 0.6f), c + new Vector2(0f, r * 0.6f));
                    Line(p, c + new Vector2(-r * 0.6f, 0f), c + new Vector2(r * 0.6f, 0f));
                    Line(p, c + new Vector2(-r * 0.3f, r * 0.55f), c + new Vector2(r * 0.3f, r * 0.55f));
                    break;
                case Shape.Station:
                    p.lineWidth = 1.6f;
                    Line(p, c + new Vector2(-r * 0.3f, -r * 0.6f), c + new Vector2(-r * 0.3f, r * 0.6f));
                    Line(p, c + new Vector2(r * 0.3f, -r * 0.6f), c + new Vector2(r * 0.3f, r * 0.6f));
                    for (int k = -1; k <= 1; k++)
                        Line(p, c + new Vector2(-r * 0.5f, k * r * 0.35f), c + new Vector2(r * 0.5f, k * r * 0.35f));
                    break;
                case Shape.Kremlin:
                    StarPath(p, c, r * 0.72f, r * 0.30f);
                    p.Fill();
                    break;
                case Shape.Port:
                    // Якорь: веретено, шток, дуга лап.
                    p.lineWidth = 1.6f;
                    Line(p, c + new Vector2(0f, -r * 0.55f), c + new Vector2(0f, r * 0.55f));
                    Line(p, c + new Vector2(-r * 0.32f, -r * 0.28f), c + new Vector2(r * 0.32f, -r * 0.28f));
                    p.BeginPath();
                    p.Arc(c + new Vector2(0f, r * 0.05f), r * 0.5f, Angle.Degrees(20f), Angle.Degrees(160f));
                    p.Stroke();
                    break;
                default:
                    CirclePath(p, c, r * (0.45f + 0.15f * lit));
                    p.Fill();
                    break;
            }
            _fade = 1f;
        }

        enum Shape { Circle, Airport, Station, Kremlin, Port }

        Shape LocationShape(Location location)
        {
            if (HubIcons && location.DistrictId == "port") return Shape.Port;
            if (location.DistrictId == "airports") return Shape.Airport;
            if (location.DistrictId == "vokzaly") return Shape.Station;
            if (location.Id == "kremlin" || location.Name == "Кремль") return Shape.Kremlin;
            return Shape.Circle;
        }

        /// <summary>Аэропорты и вокзалы — скруглённые квадраты, Кремль и прочие — круги.</summary>
        static void ShapePath(Painter2D p, Shape shape, Vector2 c, float r)
        {
            if (shape == Shape.Airport || shape == Shape.Station)
            {
                float k = r * 0.92f;
                RectPath(p, new Rect(c.x - k, c.y - k, 2f * k, 2f * k));
            }
            else CirclePath(p, c, r);
        }

        static void StarPath(Painter2D p, Vector2 c, float outer, float inner)
        {
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                var point = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
        }

        // ================================================================== Хеш

        /// <summary>Детерминированный «случайный» номер клетки — кварталы не прыгают между кадрами и партиями.</summary>
        static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Тёмные края — взгляд собирается к центру.</summary>
        static void DrawVignette(Painter2D p, Rect area)
        {
            const int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float inset = i * 14f;
                p.strokeColor = new Color(0f, 0f, 0f, 0.05f);
                p.lineWidth = 28f;
                RectPath(p, new Rect(area.x + inset, area.y + inset, area.width - 2f * inset, area.height - 2f * inset));
                p.Stroke();
            }
        }

        // ================================================================== Живая карта

        /// <summary>Машинки на построенных дорогах: пара фар едет туда и обратно вдоль дороги.</summary>
        void DrawCars(Painter2D p)
        {
            if (_use3D) return;   // в 3D машины едут в сцене (City3DScene.SyncTraffic)
            foreach (var road in _game.Map.Roads)
            {
                var owner = _game.GetRoadOwner(road.Id);
                if (!owner.HasValue || BuildProgress.ContainsKey(road.Id) || !RoadVisible(road)) continue;
                var segments = _layout.GetSegments(road);
                if (segments.Count == 0) continue;
                float rad = segments[0].Angle * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var start = segments[0].Center - dir * segments[0].Length / 2f;
                var end = segments[segments.Count - 1].Center + dir * segments[segments.Count - 1].Length / 2f;
                // Скорость — в пикселях ПОЛЯ (длина дороги по карте), а не экрана: иначе при зуме менялась бы
                // скорость, а фаза PulseTime × скорость прыгала бы вместе с ней — машинки «разгонялись».
                float length = MapLength(road);
                float scale = _use3D ? Mathf.Clamp(RoadWidthScale(road), 0.6f, 2.5f) : 1f;
                int cars = road.Length >= 4 ? 2 : 1;
                bool busy = _game.IsRushHourRoad(road.Id);
                bool jammed = _game.IsJammed(road.Id);
                if (busy) cars++;
                // Загрузка видна по машинкам: каждый маршрут соперника — ещё одна (не больше шести), в пробке они ползут.
                cars = Mathf.Min(6, cars + _game.TrafficOn(road.Id));
                int seed = road.Id.GetHashCode();
                for (int k = 0; k < cars; k++)
                {
                    float speed = (jammed ? 9f : busy ? 70f : 45f) / length;
                    float t = Mathf.PingPong(PulseTime * speed + Hash(seed, k, 13) * 2f, 1f);
                    bool forward = Mathf.Repeat(PulseTime * speed + Hash(seed, k, 13) * 2f, 2f) < 1f;
                    var pos = Vector2.Lerp(start, end, t);
                    var heading = forward ? dir : -dir;
                    var normal = new Vector2(-heading.y, heading.x);
                    // Правостороннее движение: машинка чуть правее середины.
                    pos += normal * (2.2f * scale);
                    // Фары: днём еле видны, ночью светят вперёд. В 3D размеры — по ширине дороги на экране.
                    p.fillColor = new Color(1f, 0.95f, 0.75f, 0.22f + 0.30f * _night);
                    CirclePath(p, pos + heading * ((4f + 2f * _night) * scale), (4.5f + 2f * _night) * scale);
                    p.Fill();
                    p.fillColor = new Color(1f, 0.97f, 0.85f, 0.95f);
                    CirclePath(p, pos + (heading * 1.5f + normal * 1.3f) * scale, 1.1f * scale);
                    p.Fill();
                    CirclePath(p, pos + (heading * 1.5f - normal * 1.3f) * scale, 1.1f * scale);
                    p.Fill();
                    p.fillColor = new Color(1f, 0.25f, 0.2f, 0.85f);
                    CirclePath(p, pos - heading * (2f * scale), 0.9f * scale);
                    p.Fill();
                }
            }
        }

        /// <summary>Длина дороги в пикселях поля 1400 × 780 — от зума и наклона не зависит.</summary>
        float MapLength(Road road)
        {
            var la = _game.Map.GetLocation(road.A);
            var lb = _game.Map.GetLocation(road.B);
            if (la == null || lb == null) return 100f;
            float dx = (lb.X - la.X) * CityPlanner.FieldW, dy = (lb.Y - la.Y) * CityPlanner.FieldH;
            return Mathf.Max(10f, Mathf.Sqrt(dx * dx + dy * dy) - 2f * (BoardLayout.LocationRadius + 3f));
        }

        struct Spark
        {
            public Vector2 Position, Velocity;
            public Color Color;
            public float Life, MaxLife;
        }

        readonly List<Spark> _sparks = new List<Spark>();
        readonly System.Random _sparkRandom = new System.Random(7);

        /// <summary>Салют в точке карты: выполненный контракт, взятая цель мэрии.</summary>
        public void Fireworks(Vector2 position, Color color, int count = 36)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (float)_sparkRandom.NextDouble() * Mathf.PI * 2f;
                float speed = 60f + (float)_sparkRandom.NextDouble() * 110f;
                float life = 0.8f + (float)_sparkRandom.NextDouble() * 0.6f;
                var c = Color.Lerp(color, Color.white, (float)_sparkRandom.NextDouble() * 0.4f);
                _sparks.Add(new Spark
                {
                    Position = position,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Color = c,
                    Life = life,
                    MaxLife = life
                });
            }
        }

        void DrawFireworks(Painter2D p, float dt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var spark = _sparks[i];
                spark.Life -= dt;
                if (spark.Life <= 0f)
                {
                    _sparks.RemoveAt(i);
                    continue;
                }
                spark.Velocity *= 1f - 1.6f * dt;
                spark.Velocity += new Vector2(0f, 60f) * dt;
                spark.Position += spark.Velocity * dt;
                _sparks[i] = spark;
                float k = spark.Life / spark.MaxLife;
                var glow = spark.Color;
                glow.a = 0.25f * k;
                p.fillColor = glow;
                CirclePath(p, spark.Position, 5f * k + 1f);
                p.Fill();
                var core = spark.Color;
                core.a = k;
                p.fillColor = core;
                CirclePath(p, spark.Position, 1.8f);
                p.Fill();
            }
        }

        Color _flashColor;
        float _flashStart = -10f;
        const float FlashDuration = 2.2f;

        /// <summary>
        /// Новое городское событие: мягкое свечение по краям карты цветом события — плавно появляется
        /// и гаснет. Без заливки всей карты и без расходящегося кольца, чтобы событие не «взрывалось».
        /// </summary>
        public void Flash(Color color)
        {
            _flashColor = color;
            _flashStart = PulseTime;
        }

        void DrawFlash(Painter2D p, Rect area)
        {
            float t = (PulseTime - _flashStart) / FlashDuration;
            if (t < 0f || t > 1f) return;
            float s = Mathf.Sin(t * Mathf.PI);
            float strength = s * s;                      // 0 → 1 → 0, без резкого начала
            // Внутреннее свечение рамки: несколько полос, чем глубже — тем прозрачнее.
            float[] widths = { 4f, 12f, 24f };
            float[] alphas = { 0.45f, 0.18f, 0.08f };
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i];
                p.strokeColor = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alphas[i] * strength);
                p.lineWidth = w;
                RectPath(p, new Rect(area.x + w / 2f, area.y + w / 2f, area.width - w, area.height - w));
                p.Stroke();
            }
        }

        /// <summary>Центр района на экране (для салюта над выполненным контрактом).</summary>
        public Vector2 DistrictScreenCenter(string districtId) => _game == null ? Vector2.zero : DistrictCenter(districtId);

        // ================================================================== Геометрия

        /// <summary>
        /// Точка участка: t — доля длины (0 — начало, 1 — конец), side — доля полуширины (+1 — край со стороны
        /// +нормали, −1 — со стороны −нормали). У участка на земле — по его трапеции.
        /// </summary>
        static Vector2 SidePoint(BoardLayout.Segment segment, float t, float side)
        {
            float k = (side + 1f) * 0.5f;
            if (segment.Quad)
                return Vector2.LerpUnclamped(Vector2.Lerp(segment.Q0, segment.Q1, t), Vector2.Lerp(segment.Q3, segment.Q2, t), k);
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var normal = new Vector2(-dir.y, dir.x);
            float half = BoardLayout.RoadThickness * s_roadWidthScale / 2f;
            return segment.Center + dir * segment.Length * (t - 0.5f) + normal * half * side;
        }

        /// <summary>Пунктир от a до b: штрихи по 7 px через 5 px, концы скруглены.</summary>
        static void DrawDashes(Painter2D p, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            var dir = d / length;
            const float dash = 7f, gap = 5f;
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            // Штрихи центрируются на участке, чтобы концы участка не обрезали их кое-как.
            int count = Mathf.Max(1, Mathf.FloorToInt((length + gap) / (dash + gap)));
            float used = count * dash + (count - 1) * gap;
            float start = (length - used) * 0.5f;
            if (used > length) { start = 0f; }
            for (int i = 0; i < count; i++)
            {
                float from = start + i * (dash + gap);
                float to = Mathf.Min(length, from + dash);
                p.MoveTo(a + dir * from);
                p.LineTo(a + dir * to);
            }
            p.Stroke();
        }

        static void SegmentPath(Painter2D p, BoardLayout.Segment segment, float inflate, Vector2 offset)
        {
            if (segment.Quad)
            {
                // Трапеция на земле, раздутая на inflate пикселей по длине и поперёк.
                var alongL = (segment.Q1 - segment.Q0).normalized;
                var alongR = (segment.Q2 - segment.Q3).normalized;
                var acrossS = (segment.Q0 - segment.Q3).normalized;
                var acrossE = (segment.Q1 - segment.Q2).normalized;
                p.BeginPath();
                p.MoveTo(segment.Q0 + offset + (-alongL + acrossS) * inflate);
                p.LineTo(segment.Q1 + offset + (alongL + acrossE) * inflate);
                p.LineTo(segment.Q2 + offset + (alongR - acrossE) * inflate);
                p.LineTo(segment.Q3 + offset + (-alongR - acrossS) * inflate);
                p.ClosePath();
                return;
            }
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var normal = new Vector2(-dir.y, dir.x);
            float halfLength = segment.Length / 2f + inflate;
            float halfThickness = BoardLayout.RoadThickness * s_roadWidthScale / 2f + inflate;
            var center = segment.Center + offset;

            p.BeginPath();
            p.MoveTo(center - dir * halfLength - normal * halfThickness);
            p.LineTo(center + dir * halfLength - normal * halfThickness);
            p.LineTo(center + dir * halfLength + normal * halfThickness);
            p.LineTo(center - dir * halfLength + normal * halfThickness);
            p.ClosePath();
        }

        static void SmoothPath(Painter2D p, List<Vector2> points)
        {
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Count - 1; i++)
                p.QuadraticCurveTo(points[i], (points[i] + points[i + 1]) / 2f);
            p.LineTo(points[points.Count - 1]);
        }

        static void RectPath(Painter2D p, Rect r)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
        }

        /// <summary>Эллипс из четырёх кривых Безье — круг, лежащий на наклонённой земле.</summary>
        static void EllipsePath(Painter2D p, Vector2 c, float rx, float ry)
        {
            const float k = 0.5523f;
            float ox = rx * k, oy = ry * k;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x + rx, c.y));
            p.BezierCurveTo(new Vector2(c.x + rx, c.y + oy), new Vector2(c.x + ox, c.y + ry), new Vector2(c.x, c.y + ry));
            p.BezierCurveTo(new Vector2(c.x - ox, c.y + ry), new Vector2(c.x - rx, c.y + oy), new Vector2(c.x - rx, c.y));
            p.BezierCurveTo(new Vector2(c.x - rx, c.y - oy), new Vector2(c.x - ox, c.y - ry), new Vector2(c.x, c.y - ry));
            p.BezierCurveTo(new Vector2(c.x + ox, c.y - ry), new Vector2(c.x + rx, c.y - oy), new Vector2(c.x + rx, c.y));
            p.ClosePath();
        }

        /// <summary>Круг на земле вокруг экранной точки с учётом наклона.</summary>
        void GroundEllipse(Painter2D p, Vector2 center, float radius)
        {
            float rx = radius * _layout.ScaleAt(center);
            EllipsePath(p, center, rx, rx * _layout.Squash);
        }

        static void CirclePath(Painter2D p, Vector2 center, float radius)
        {
            p.BeginPath();
            p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
        }

        static void PolyPath(Painter2D p, List<Vector2> points)
        {
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) p.LineTo(points[i]);
        }

        static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }
    }
}
