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

        public event Action<Road, Vector2> Hovered;
        public event Action<Road, Vector2> RoadClicked;

        public Road HoverRoad { get; private set; }
        /// <summary>Дороги, которые текущий игрок может построить прямо сейчас (мягко пульсируют).</summary>
        public readonly HashSet<string> Affordable = new HashSet<string>();
        /// <summary>Подсвеченный контракт: первый район (жёлтый) и второй (красный); null — подсветки нет.</summary>
        public string HighlightFrom { get; private set; }
        public string HighlightTo { get; private set; }
        public string HighlightThird { get; private set; }
        public bool HasHighlight => HighlightFrom != null;
        /// <summary>Дороги пути контракта и цвет обводки (свои — зелёным, чужие — оранжевым).</summary>
        public readonly Dictionary<string, Color> PathRoads = new Dictionary<string, Color>();
        /// <summary>Прогресс анимации строительства 0..1 для только что построенных дорог.</summary>
        public readonly Dictionary<string, float> BuildProgress = new Dictionary<string, float>();
        public float PulseTime;
        public bool Interactive = true;

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
                SetHover(Interactive && _game != null ? OpenRoadAt(position) : null, position);
            });
            RegisterCallback<PointerLeaveEvent>(_ => SetHover(null, Vector2.zero));
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || !Interactive || _game == null) return;
                Vector2 position = e.localPosition;
                var road = OpenRoadAt(position);
                if (road != null) RoadClicked?.Invoke(road, position);
            });
        }

        public void SetGame(Game game)
        {
            _game = game;
            _layout = new BoardLayout(game.Map);
            Affordable.Clear();
            HighlightFrom = null;
            HighlightTo = null;
            HighlightThird = null;
            PathRoads.Clear();
            BuildProgress.Clear();
            HoverRoad = null;

            _districtIndex.Clear();
            for (int i = 0; i < game.Map.Districts.Count; i++) _districtIndex[game.Map.Districts[i].Id] = i;

            _labels.Clear();
            _locationLabels.Clear();
            _districtLabels.Clear();

            foreach (var district in game.Map.Districts)
            {
                var label = Ui.Text(district.Name.ToUpperInvariant(), 14f, DistrictLabelColor, true);
                label.style.letterSpacing = 3f;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                var holder = Holder(240f);
                holder.Add(label);
                _labels.Add(holder);
                _districtLabels[district.Id] = holder;
            }

            foreach (var location in game.Map.Locations)
            {
                var label = Ui.Text(location.Name, 12f, Ink, true);
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.backgroundColor = LabelBack;
                Ui.Radius(label, 4f);
                Ui.Padding(label, 1f, 5f);
                var holder = Holder(200f);
                holder.Add(label);
                _labels.Add(holder);
                _locationLabels[location.Id] = holder;
            }

            var ring = Ui.Text("МКАД", 12f, new Color(1f, 0.72f, 0.35f, 0.85f), true);
            ring.style.backgroundColor = LabelBack;
            Ui.Padding(ring, 0f, 4f);
            _ringLabel = Holder(80f);
            _ringLabel.Add(ring);
            _labels.Add(_ringLabel);

            BuildTitle(game.Map.Name);
            Relayout();
        }

        public Vector2 LocationPosition(string locationId) => _layout.LocationPosition(locationId);

        /// <summary>Дорога под курсором, если её район уже открыт.</summary>
        Road OpenRoadAt(Vector2 position)
        {
            var road = _layout.RoadAt(position);
            return road != null && _game.IsRoadOpen(road.Id) ? road : null;
        }

        /// <summary>Спрятать подписи ещё не открытых районов (вызывается при каждом обновлении экрана).</summary>
        public void RefreshVisibility()
        {
            if (_game == null) return;
            foreach (var pair in _locationLabels) Ui.Show(pair.Value, _game.IsLocationOpen(pair.Key));
            foreach (var pair in _districtLabels) Ui.Show(pair.Value, _game.IsDistrictOpen(pair.Key));
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
            return holder;
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
            var title = Ui.Text("City Highways", 20f, Color.white, true);
            title.style.whiteSpace = WhiteSpace.NoWrap;
            sign.Add(title);
            sign.Add(Ui.Text(city, 13f, new Color(1f, 1f, 1f, 0.85f)));
            _labels.Add(sign);
        }

        void Relayout()
        {
            if (_game == null) return;
            var rect = contentRect;
            if (float.IsNaN(rect.width) || rect.width < 50f || rect.height < 50f) return;
            _layout.Fit(new Rect(0f, 0f, rect.width, rect.height));

            foreach (var pair in _locationLabels)
            {
                var p = _layout.LocationPosition(pair.Key);
                pair.Value.style.left = p.x - 100f;
                pair.Value.style.top = p.y + BoardLayout.LocationRadius + 4f;
            }

            foreach (var pair in _districtLabels)
            {
                var c = DistrictCenter(pair.Key);
                pair.Value.style.left = c.x - 120f;
                pair.Value.style.top = c.y - 48f;
            }

            if (_ringLabel != null)
            {
                var top = RingPoint(-Mathf.PI / 2f);
                _ringLabel.style.left = top.x - 40f;
                _ringLabel.style.top = top.y - 9f;
            }
            _terrainLayer.MarkDirtyRepaint();
            _staticLayer.MarkDirtyRepaint();
            Repaint();
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
        /// Точка на МКАД: эллипс вокруг (0.5, 0.5) с полуосями 0.40 × 0.49 поля. Размеры подобраны скриптом карты так,
        /// чтобы внутри был весь город, а снаружи — только аэропорты и Новая Москва.
        /// </summary>
        Vector2 RingPoint(float angle)
        {
            var center = _layout.ToScreen(0.5f, 0.5f);
            var edge = _layout.ToScreen(0.90f, 0.99f);
            float rx = edge.x - center.x;
            float ry = edge.y - center.y;
            return center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        }

        void SetHover(Road road, Vector2 position)
        {
            if (road != HoverRoad)
            {
                HoverRoad = road;
                Repaint();
            }
            Hovered?.Invoke(road, position);
        }

        // ================================================================== Рисование

        /// <summary>Местность — одна запечённая текстура на всё поле с запасом под отступы.</summary>
        void DrawTerrain(MeshGenerationContext context)
        {
            if (!CanDraw) return;
            const float m = TerrainTexture.Margin;
            var origin = _layout.ToScreen(0f, 0f);
            var unit = _layout.ToScreen(1f, 1f);
            var texture = TerrainTexture.For(_game.Map, unit.x - origin.x, unit.y - origin.y, _layout.Area.height);
            var tl = _layout.ToScreen(-m, -m);
            var br = _layout.ToScreen(1f + m, 1f + m);
            _terrainQuads.Clear();
            _terrainQuads.Add(texture, tl, new Vector2(br.x, tl.y), br, new Vector2(tl.x, br.y),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), Color.white);
            _terrainQuads.Flush(context);
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

            foreach (var road in _game.Map.Roads)
                if (_game.IsRoadOpen(road.Id)) DrawRoadOver(p, road);
            DrawCars(p);
            foreach (var location in _game.Map.Locations)
                if (_game.IsLocationOpen(location.Id)) DrawLocation(p, location);

            if (HasHighlight) DrawContractHighlight(p, area);
            DrawFireworks(p, dt);
            DrawFlash(p, area);
        }

        float _lastDrawTime;

        /// <summary>Затемнение всей карты и поверх него — два района контракта и путь между ними.</summary>
        void DrawContractHighlight(Painter2D p, Rect area)
        {
            p.fillColor = Dimmer;
            RectPath(p, area);
            p.Fill();

            float pulse = 0.5f + 0.5f * Mathf.Sin(PulseTime * 4f);
            float radius = Mathf.Max(32f, _layout.Area.height * 0.045f);
            foreach (var location in _game.Map.Locations)
            {
                if (!IsHighlighted(location.DistrictId)) continue;
                var color = HighlightColor(location.DistrictId);
                color.a = 0.30f + 0.12f * pulse;
                p.fillColor = color;
                CirclePath(p, _layout.LocationPosition(location.Id), radius);
                p.Fill();
            }

            foreach (var road in _game.Map.Roads)
                if (PathRoads.ContainsKey(road.Id))
                {
                    DrawRoadFlat(p, road);
                    DrawRoadOver(p, road);
                }

            foreach (var location in _game.Map.Locations)
                if (IsHighlighted(location.DistrictId)) DrawLocation(p, location);
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
                p.fillColor = new Color(color.r, color.g, color.b, 0.16f);
                CirclePath(p, center, radius);
                p.Fill();
                p.strokeColor = new Color(color.r, color.g, color.b, 0.70f);
                p.lineWidth = 2f;
                CirclePath(p, center, radius);
                p.Stroke();
            }
        }

        void DrawRiver(Painter2D p)
        {
            var points = _game.Map.River;
            if (points.Count < 2) return;
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

        void DrawRing(Painter2D p)
        {
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

        // ---------------------------------------------------------------- Проход 1: свечение под дорогой

        void DrawRoadsUnder(MeshGenerationContext context)
        {
            if (!CanDraw) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            foreach (var road in _game.Map.Roads)
                if (_game.IsRoadOpen(road.Id)) DrawRoadUnder(p, road);
        }

        void DrawRoadUnder(Painter2D p, Road road)
        {
            int? owner = _game.GetRoadOwner(road.Id);
            var segments = _layout.GetSegments(road);
            int built = BuiltSegments(road, segments.Count);

            // Обводка пути контракта, наведение, доступные дороги.
            Color? glow = null;
            float glowWidth = 0f;
            if (PathRoads.TryGetValue(road.Id, out var pathColor))
            {
                glow = pathColor;
                glowWidth = 9f;
            }
            else if (road == HoverRoad)
            {
                glow = new Color(Glow.r, Glow.g, Glow.b, 0.95f);
                glowWidth = 8f;
            }
            else if (Affordable.Contains(road.Id))
            {
                glow = new Color(Glow.r, Glow.g, Glow.b, 0.30f + 0.22f * Mathf.Sin(PulseTime * 4f));
                glowWidth = 6f;
            }
            // Субсидия мэрии: свободные мосты подсвечены голубым.
            else if (!owner.HasValue && road.IsBridge && _game.CurrentEvent != null && _game.CurrentEvent.Kind == CityEventKind.Subsidy)
            {
                glow = new Color(0.45f, 0.78f, 1f, 0.45f + 0.25f * Mathf.Sin(PulseTime * 4f));
                glowWidth = 7f;
            }
            if (glow.HasValue)
            {
                p.strokeColor = glow.Value;
                p.lineWidth = glowWidth;
                foreach (var segment in segments)
                {
                    SegmentPath(p, segment, 1.5f, Vector2.zero);
                    p.Stroke();
                }
            }

            // Построенные участки слегка светятся цветом компании.
            if (owner.HasValue)
            {
                var c = Palette.PlayerColor(owner.Value);
                p.strokeColor = new Color(c.r, c.g, c.b, 0.30f);
                p.lineWidth = 7f;
                for (int i = 0; i < built; i++)
                {
                    SegmentPath(p, segments[i], 2.5f, Vector2.zero);
                    p.Stroke();
                }
            }
        }

        // ---------------------------------------------------------------- Проход 2: текстуры покрытий

        void DrawRoadTextures(MeshGenerationContext context)
        {
            if (!CanDraw) return;
            _quads.Clear();
            foreach (var road in _game.Map.Roads)
            {
                if (!_game.IsRoadOpen(road.Id)) continue;
                var texture = RoadTextures.For(road);
                var segments = _layout.GetSegments(road);
                int built = BuiltSegments(road, segments.Count);
                for (int i = 0; i < segments.Count; i++)
                {
                    // Построенный участок — покрытие целиком; свободный — едва проступающее «будущее» покрытие.
                    var tint = i < built ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                    AddSegmentTexture(texture, segments[i], tint);
                }
            }
            _quads.Flush(context);
        }

        /// <summary>Участок — несколько квадратных «плиток» текстуры по длине (без растяжения).</summary>
        void AddSegmentTexture(Texture2D texture, BoardLayout.Segment segment, Color tint)
        {
            var dir = Dir(segment);
            float thickness = BoardLayout.RoadThickness;
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
            int? owner = _game.GetRoadOwner(road.Id);
            var segments = _layout.GetSegments(road);
            int built = BuiltSegments(road, segments.Count);
            var baseColor = NightRoadColor(road);
            var ownerColor = owner.HasValue ? Palette.PlayerColor(owner.Value) : baseColor;
            bool isBridge = road.IsBridge;
            float half = BoardLayout.RoadThickness / 2f;

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var dir = Dir(segment);
                var normal = new Vector2(-dir.y, dir.x);
                var a = segment.Center - dir * segment.Length / 2f;
                var b = segment.Center + dir * segment.Length / 2f;
                bool isBuilt = i < built;

                if (isBuilt)
                {
                    // Бордюры цвета компании по обеим сторонам — так видно, чья дорога.
                    p.strokeColor = ownerColor;
                    p.lineWidth = 2.4f;
                    Line(p, a + normal * half, b + normal * half);
                    Line(p, a - normal * half, b - normal * half);
                    // Разметка: асфальт — белый пунктир, бетон — жёлтая осевая.
                    if (road.Resource == Resource.Asphalt) DrawLaneMark(p, segment, new Color(1f, 1f, 1f, 0.85f));
                    else if (road.Resource == Resource.Concrete) DrawLaneMark(p, segment, new Color(1f, 0.82f, 0.25f, 0.85f));
                }
                else
                {
                    // Свободный участок — неоновая обводка цвета ресурса.
                    p.strokeColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f);
                    p.lineWidth = 1.6f;
                    SegmentPath(p, segment, 0f, Vector2.zero);
                    p.Stroke();
                }

                // Мост — перила со столбиками.
                if (isBridge)
                {
                    var rail = new Color(0.66f, 0.82f, 1f, isBuilt ? 0.95f : 0.6f);
                    p.strokeColor = rail;
                    p.lineWidth = 1.8f;
                    Line(p, a + normal * (half + 2.5f), b + normal * (half + 2.5f));
                    Line(p, a - normal * (half + 2.5f), b - normal * (half + 2.5f));
                    int posts = Mathf.Max(2, Mathf.RoundToInt(segment.Length / 6f));
                    for (int k = 0; k <= posts; k++)
                    {
                        var point = Vector2.Lerp(a, b, k / (float)posts);
                        p.fillColor = rail;
                        CirclePath(p, point + normal * (half + 2.5f), 1.2f);
                        p.Fill();
                        CirclePath(p, point - normal * (half + 2.5f), 1.2f);
                        p.Fill();
                    }
                }

                // Магистраль: оранжевая рамка вокруг каждого участка.
                if (isBuilt && _game.IsRushHourRoad(road.Id))
                {
                    p.strokeColor = new Color(1f, 0.60f, 0.20f, 0.95f);
                    p.lineWidth = 2.2f;
                    SegmentPath(p, segment, isBridge ? 5f : 3.5f, Vector2.zero);
                    p.Stroke();
                }

                // Ремонт и парад: красно-белые полосы поперёк участка.
                if (!owner.HasValue && (_game.IsUnderRepair(road.Id) || _game.IsParadeClosed(road.Id))) DrawRepairStripes(p, segment);
            }
        }

        /// <summary>Плоская заливка — для пути контракта поверх затемнения (текстуры остаются под ним).</summary>
        void DrawRoadFlat(Painter2D p, Road road)
        {
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
            if (!CanDraw || _game.Map.River.Count < 2) return;
            var raw = _game.Map.River.Select(pt => _layout.ToScreen(pt.X, pt.Y)).ToList();
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

            _quads.Clear();
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
            _quads.Flush(context);
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
            float half = BoardLayout.RoadThickness / 2f;
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

        static void DrawLaneMark(Painter2D p, BoardLayout.Segment segment, Color color)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            p.strokeColor = color;
            p.lineWidth = 1.8f;
            float half = segment.Length * 0.22f;
            Line(p, segment.Center - dir * half, segment.Center + dir * half);
        }

        void DrawLocation(Painter2D p, Location location)
        {
            var c = _layout.LocationPosition(location.Id);
            float r = BoardLayout.LocationRadius;
            int index = _districtIndex.TryGetValue(location.DistrictId, out var i) ? i : 0;

            bool lit = IsHighlighted(location.DistrictId);
            if (lit)
            {
                var halo = HighlightColor(location.DistrictId);
                float pulse = 1f + 0.2f * Mathf.Sin(PulseTime * 5f);
                p.fillColor = new Color(halo.r, halo.g, halo.b, 0.45f);
                CirclePath(p, c, (r + 10f) * pulse);
                p.Fill();
            }

            var district = lit ? HighlightColor(location.DistrictId) : Palette.DistrictColor(index);
            // Ореол цвета района — место «горит» на ночной карте.
            p.fillColor = new Color(district.r, district.g, district.b, 0.18f);
            CirclePath(p, c, r + 5f);
            p.Fill();

            var shape = LocationShape(location);
            var ring = lit ? HighlightColor(location.DistrictId) : new Color(0.92f, 0.94f, 0.98f);
            ShapePath(p, shape, c, r + (lit ? 3.5f : 2.5f));
            p.fillColor = ring;
            p.Fill();
            ShapePath(p, shape, c, r);
            p.fillColor = new Color(0.07f, 0.08f, 0.12f);
            p.Fill();

            // Значок внутри: самолёт у аэропортов, рельсы у вокзалов, звезда у Кремля, иначе точка района.
            p.fillColor = district;
            p.strokeColor = district;
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
                default:
                    CirclePath(p, c, r * (lit ? 0.6f : 0.45f));
                    p.Fill();
                    break;
            }
        }

        enum Shape { Circle, Airport, Station, Kremlin }

        static Shape LocationShape(Location location)
        {
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
            foreach (var road in _game.Map.Roads)
            {
                var owner = _game.GetRoadOwner(road.Id);
                if (!owner.HasValue || BuildProgress.ContainsKey(road.Id)) continue;
                var segments = _layout.GetSegments(road);
                if (segments.Count == 0) continue;
                float rad = segments[0].Angle * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var start = segments[0].Center - dir * segments[0].Length / 2f;
                var end = segments[segments.Count - 1].Center + dir * segments[segments.Count - 1].Length / 2f;
                float length = Mathf.Max(1f, (end - start).magnitude);
                int cars = road.Length >= 4 ? 2 : 1;
                bool busy = _game.IsRushHourRoad(road.Id);
                if (busy) cars++;
                int seed = road.Id.GetHashCode();
                for (int k = 0; k < cars; k++)
                {
                    float speed = (busy ? 70f : 45f) / length;
                    float t = Mathf.PingPong(PulseTime * speed + Hash(seed, k, 13) * 2f, 1f);
                    bool forward = Mathf.Repeat(PulseTime * speed + Hash(seed, k, 13) * 2f, 2f) < 1f;
                    var pos = Vector2.Lerp(start, end, t);
                    var heading = forward ? dir : -dir;
                    var normal = new Vector2(-heading.y, heading.x);
                    // Правостороннее движение: машинка чуть правее середины.
                    pos += normal * 2.2f;
                    p.fillColor = new Color(1f, 0.95f, 0.75f, 0.22f);
                    CirclePath(p, pos + heading * 4f, 4.5f);
                    p.Fill();
                    p.fillColor = new Color(1f, 0.97f, 0.85f, 0.95f);
                    CirclePath(p, pos + heading * 1.5f + normal * 1.3f, 1.1f);
                    p.Fill();
                    CirclePath(p, pos + heading * 1.5f - normal * 1.3f, 1.1f);
                    p.Fill();
                    p.fillColor = new Color(1f, 0.25f, 0.2f, 0.85f);
                    CirclePath(p, pos - heading * 2f, 0.9f);
                    p.Fill();
                }
            }
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

        static void SegmentPath(Painter2D p, BoardLayout.Segment segment, float inflate, Vector2 offset)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var normal = new Vector2(-dir.y, dir.x);
            float halfLength = segment.Length / 2f + inflate;
            float halfThickness = BoardLayout.RoadThickness / 2f + inflate;
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
