using System;
using System.Collections.Generic;
using RailGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RailGame.Presentation.App
{
    /// <summary>
    /// Карта, нарисованная кодом (Painter2D): пергамент, сетка, реки, рамка, роза ветров,
    /// маршруты из вагонов с тенями, станции. Названия городов — обычные Label поверх.
    /// </summary>
    public sealed class MapView : VisualElement
    {
        static readonly Color Parchment = new Color(0.92f, 0.87f, 0.74f);
        static readonly Color Ink = new Color(0.30f, 0.22f, 0.13f);
        static readonly Color River = new Color(0.38f, 0.55f, 0.70f);

        /// <summary>Наведение: маршрут под курсором (или null) и позиция курсора в координатах карты.</summary>
        public event Action<Route, Vector2> Hovered;
        public event Action<Route, Vector2> RouteClicked;

        public Route HoverRoute { get; private set; }
        /// <summary>Маршруты, которые текущий игрок может занять прямо сейчас (мягко пульсируют).</summary>
        public readonly HashSet<string> Affordable = new HashSet<string>();
        /// <summary>Города, которые нужно подсветить (наведение на задание).</summary>
        public readonly HashSet<string> HighlightCities = new HashSet<string>();
        /// <summary>Прогресс анимации прокладки 0..1 для маршрутов, которые только что заняли.</summary>
        public readonly Dictionary<string, float> ClaimProgress = new Dictionary<string, float>();
        public float PulseTime;
        public bool Interactive = true;

        Game _game;
        BoardLayout _layout;
        readonly VisualElement _labels;
        readonly Dictionary<string, VisualElement> _cityLabels = new Dictionary<string, VisualElement>();
        readonly List<Vector2[]> _rivers = new List<Vector2[]>();
        VisualElement _compassLabel;

        public MapView()
        {
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;

            _labels = Ui.Box();
            Ui.Fill(_labels);
            Add(_labels);

            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(_ => SetHover(null, Vector2.zero));
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        public void SetGame(Game game)
        {
            _game = game;
            _layout = new BoardLayout(game.Map);
            Affordable.Clear();
            HighlightCities.Clear();
            ClaimProgress.Clear();
            HoverRoute = null;

            _labels.Clear();
            _cityLabels.Clear();
            BuildCartouche(game.Map.Name);
            foreach (var city in game.Map.Cities) BuildCityLabel(city);

            _compassLabel = Ui.Text("С", 16f, Ink, true);
            _compassLabel.style.position = Position.Absolute;
            _compassLabel.style.width = 20f;
            _compassLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _labels.Add(_compassLabel);

            BuildRivers(game.Map.Name);
            Relayout();
        }

        /// <summary>Центр города в координатах карты — для анимаций и всплывающих окон.</summary>
        public Vector2 CityPosition(string cityId) => _layout.CityPosition(cityId);

        // ================================================================== Раскладка и ввод

        void Relayout()
        {
            if (_game == null) return;
            var rect = contentRect;
            if (float.IsNaN(rect.width) || rect.width < 50f || rect.height < 50f) return;

            _layout.Fit(new Rect(0f, 0f, rect.width, rect.height));
            foreach (var pair in _cityLabels)
            {
                var position = _layout.CityPosition(pair.Key);
                pair.Value.style.left = position.x - 100f;
                pair.Value.style.top = position.y + BoardLayout.CityRadius + 5f;
            }

            if (_compassLabel != null)
            {
                _compassLabel.style.left = 82f - 10f;
                _compassLabel.style.top = rect.height - 82f - 64f;
            }
            MarkDirtyRepaint();
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            Vector2 position = e.localPosition;
            SetHover(Interactive && _game != null ? _layout.RouteAt(position) : null, position);
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0 || !Interactive || _game == null) return;
            Vector2 position = e.localPosition;
            var route = _layout.RouteAt(position);
            if (route != null) RouteClicked?.Invoke(route, position);
        }

        void SetHover(Route route, Vector2 position)
        {
            if (route != HoverRoute)
            {
                HoverRoute = route;
                MarkDirtyRepaint();
            }
            Hovered?.Invoke(route, position);
        }

        // ================================================================== Подписи

        void BuildCartouche(string title)
        {
            var box = Ui.Column();
            box.style.position = Position.Absolute;
            box.style.left = 28f;
            box.style.top = 26f;
            box.style.backgroundColor = new Color(0.95f, 0.91f, 0.80f, 0.92f);
            Ui.Border(box, 2f, Ink);
            Ui.Radius(box, 4f);
            Ui.Padding(box, 8f, 16f);

            var name = Ui.Text(title, 22f, Ink, true);
            name.style.whiteSpace = WhiteSpace.NoWrap;
            box.Add(name);
            var subtitle = Ui.Text("Железнодорожная карта", 12f, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            subtitle.style.unityFontStyleAndWeight = FontStyle.Italic;
            box.Add(subtitle);
            _labels.Add(box);
        }

        void BuildCityLabel(City city)
        {
            // Внешний контейнер фиксированной ширины центрирует подпись под кружком.
            var holder = Ui.Column();
            holder.style.position = Position.Absolute;
            holder.style.width = 200f;
            holder.style.alignItems = Align.Center;

            var label = Ui.Text(city.Name, 13f, Ink, true);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.backgroundColor = new Color(0.95f, 0.91f, 0.80f, 0.85f);
            Ui.Radius(label, 4f);
            Ui.Padding(label, 1f, 6f);
            holder.Add(label);

            _labels.Add(holder);
            _cityLabels[city.Id] = holder;
        }

        void BuildRivers(string seedText)
        {
            _rivers.Clear();
            int seed = 17;
            foreach (char c in seedText ?? "") seed = seed * 31 + c;
            var rng = new System.Random(seed);

            // Несколько рек, текущих примерно с севера на юг или с запада на восток, с плавными изгибами.
            for (int r = 0; r < 3; r++)
            {
                bool vertical = r != 1;
                var start = vertical ? new Vector2(0.2f + 0.6f * (float)rng.NextDouble(), -0.05f)
                                     : new Vector2(-0.05f, 0.3f + 0.4f * (float)rng.NextDouble());
                var end = vertical ? new Vector2(Mathf.Clamp01(start.x + 0.3f * ((float)rng.NextDouble() - 0.5f)), 1.05f)
                                   : new Vector2(1.05f, Mathf.Clamp01(start.y + 0.3f * ((float)rng.NextDouble() - 0.5f)));
                var points = new Vector2[8];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / (float)(points.Length - 1);
                    var p = Vector2.Lerp(start, end, t);
                    float wobble = (i == 0 || i == points.Length - 1) ? 0f : 0.07f * ((float)rng.NextDouble() - 0.5f) * 2f;
                    if (vertical) p.x += wobble;
                    else p.y += wobble;
                    points[i] = p;
                }
                _rivers.Add(points);
            }
        }

        // ================================================================== Рисование

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (_game == null || float.IsNaN(rect.width) || rect.width < 50f) return;
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            DrawBackground(painter, new Rect(0f, 0f, rect.width, rect.height));
            foreach (var route in _game.Map.Routes) DrawRoute(painter, route);
            foreach (var city in _game.Map.Cities) DrawCity(painter, city);
        }

        void DrawBackground(Painter2D p, Rect r)
        {
            p.fillColor = Parchment;
            RectPath(p, r);
            p.Fill();

            // Потемнение к краям — «старая бумага».
            p.lineWidth = 16f;
            for (int i = 0; i < 9; i++)
            {
                p.strokeColor = new Color(0.45f, 0.31f, 0.14f, 0.05f);
                RectPath(p, Inset(r, 8f + i * 8f));
                p.Stroke();
            }

            // Сетка параллелей и меридианов.
            p.lineWidth = 1f;
            p.strokeColor = new Color(Ink.r, Ink.g, Ink.b, 0.10f);
            for (float x = r.x + 90f; x < r.xMax - 20f; x += 120f) Line(p, new Vector2(x, r.y + 20f), new Vector2(x, r.yMax - 20f));
            for (float y = r.y + 80f; y < r.yMax - 20f; y += 120f) Line(p, new Vector2(r.x + 20f, y), new Vector2(r.xMax - 20f, y));

            // Реки: широкая бледная полоса и тонкая линия поверх.
            foreach (var river in _rivers)
            {
                p.strokeColor = new Color(River.r, River.g, River.b, 0.16f);
                p.lineWidth = 9f;
                RiverPath(p, river, r);
                p.Stroke();
                p.strokeColor = new Color(River.r, River.g, River.b, 0.55f);
                p.lineWidth = 2.5f;
                RiverPath(p, river, r);
                p.Stroke();
            }

            // Двойная рамка.
            p.strokeColor = Ink;
            p.lineWidth = 3f;
            RectPath(p, Inset(r, 10f));
            p.Stroke();
            p.lineWidth = 1f;
            RectPath(p, Inset(r, 17f));
            p.Stroke();

            DrawCompass(p, new Vector2(r.x + 82f, r.yMax - 82f), 40f);
        }

        void DrawCompass(Painter2D p, Vector2 c, float size)
        {
            p.strokeColor = new Color(Ink.r, Ink.g, Ink.b, 0.6f);
            p.lineWidth = 1.5f;
            CirclePath(p, c, size * 0.75f);
            p.Stroke();
            CirclePath(p, c, size * 0.68f);
            p.Stroke();

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                var side = new Vector2(-dir.y, dir.x);
                float length = i == 0 ? size : size * 0.8f;

                p.fillColor = i == 0 ? new Color(0.62f, 0.18f, 0.12f) : Ink;
                p.BeginPath();
                p.MoveTo(c + dir * length);
                p.LineTo(c + side * size * 0.16f);
                p.LineTo(c - side * size * 0.16f);
                p.ClosePath();
                p.Fill();
            }

            p.fillColor = Parchment;
            CirclePath(p, c, size * 0.1f);
            p.Fill();
        }

        void DrawRoute(Painter2D p, Route route)
        {
            int? owner = _game.GetRouteOwner(route.Id);
            bool closed = !owner.HasValue && IsClosedParallel(route);
            bool hovered = route == HoverRoute;
            bool affordable = Affordable.Contains(route.Id);
            var segments = _layout.GetSegments(route);

            int claimed = 0;
            if (owner.HasValue)
            {
                claimed = segments.Count;
                if (ClaimProgress.TryGetValue(route.Id, out var progress))
                    claimed = Mathf.Clamp(Mathf.FloorToInt(progress * segments.Count + 0.001f), 0, segments.Count);
            }

            // Свечение: наведение — ярко, доступный маршрут — мягкая пульсация.
            if (hovered || affordable)
            {
                float alpha = hovered ? 0.9f : 0.30f + 0.22f * Mathf.Sin(PulseTime * 4f);
                p.strokeColor = new Color(1f, 0.85f, 0.2f, alpha);
                p.lineWidth = hovered ? 8f : 6f;
                foreach (var segment in segments)
                {
                    SegmentPath(p, segment, 1f, Vector2.zero);
                    p.Stroke();
                }
            }

            var baseColor = Palette.RouteColor(route);
            var ownerColor = owner.HasValue ? Palette.PlayerColor(owner.Value) : baseColor;
            float alphaScale = closed ? 0.28f : 1f;

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                bool isClaimed = i < claimed;
                var fill = isClaimed ? ownerColor : baseColor;
                fill.a *= alphaScale;

                p.fillColor = new Color(0f, 0f, 0f, 0.20f * alphaScale);
                SegmentPath(p, segment, 0f, new Vector2(1.5f, 2.5f));
                p.Fill();

                p.fillColor = fill;
                SegmentPath(p, segment, 0f, Vector2.zero);
                p.Fill();

                p.strokeColor = new Color(0.18f, 0.13f, 0.08f, 0.85f * alphaScale);
                p.lineWidth = 1.6f;
                SegmentPath(p, segment, 0f, Vector2.zero);
                p.Stroke();

                if (isClaimed) DrawWagonDetails(p, segment, fill);
            }
        }

        /// <summary>На занятом пути каждый прямоугольник превращается в вагончик с окнами.</summary>
        static void DrawWagonDetails(Painter2D p, BoardLayout.Segment segment, Color fill)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var window = Color.Lerp(fill, Color.white, 0.6f);
            float windowLength = segment.Length * 0.2f;
            float windowHeight = BoardLayout.TrackThickness * 0.38f;

            p.fillColor = window;
            foreach (float offset in new[] { -0.22f, 0.22f })
            {
                var center = segment.Center + dir * (segment.Length * offset) - new Vector2(-dir.y, dir.x) * 1f;
                RotatedRectPath(p, center, dir, windowLength / 2f, windowHeight / 2f);
                p.Fill();
            }
        }

        void DrawCity(Painter2D p, City city)
        {
            var c = _layout.CityPosition(city.Id);
            float r = BoardLayout.CityRadius;

            if (HighlightCities.Contains(city.Id))
            {
                float pulse = 1f + 0.18f * Mathf.Sin(PulseTime * 5f);
                p.fillColor = new Color(1f, 0.8f, 0.15f, 0.45f);
                CirclePath(p, c, (r + 11f) * pulse);
                p.Fill();
            }

            p.fillColor = new Color(0f, 0f, 0f, 0.25f);
            CirclePath(p, c + new Vector2(1.5f, 2.5f), r + 3f);
            p.Fill();
            p.fillColor = Ink;
            CirclePath(p, c, r + 3f);
            p.Fill();
            p.fillColor = new Color(0.98f, 0.95f, 0.86f);
            CirclePath(p, c, r);
            p.Fill();
            p.fillColor = new Color(0.62f, 0.18f, 0.12f);
            CirclePath(p, c, r * 0.38f);
            p.Fill();
        }

        bool IsClosedParallel(Route route) =>
            route.ParallelRouteId != null
            && _game.GetRouteOwner(route.ParallelRouteId).HasValue
            && _game.Players.Count < _game.Config.DoubleRoutesMinPlayers;

        // ================================================================== Геометрия путей

        static void SegmentPath(Painter2D p, BoardLayout.Segment segment, float inflate, Vector2 offset)
        {
            float rad = segment.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            RotatedRectPath(p, segment.Center + offset, dir,
                segment.Length / 2f + inflate, BoardLayout.TrackThickness / 2f + inflate);
        }

        static void RotatedRectPath(Painter2D p, Vector2 center, Vector2 dir, float halfLength, float halfThickness)
        {
            var normal = new Vector2(-dir.y, dir.x);
            p.BeginPath();
            p.MoveTo(center - dir * halfLength - normal * halfThickness);
            p.LineTo(center + dir * halfLength - normal * halfThickness);
            p.LineTo(center + dir * halfLength + normal * halfThickness);
            p.LineTo(center - dir * halfLength + normal * halfThickness);
            p.ClosePath();
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

        static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        static void RiverPath(Painter2D p, Vector2[] points, Rect r)
        {
            Vector2 ToMap(Vector2 v) => new Vector2(r.x + v.x * r.width, r.y + v.y * r.height);

            p.BeginPath();
            p.MoveTo(ToMap(points[0]));
            for (int i = 1; i < points.Length - 1; i++)
            {
                var control = ToMap(points[i]);
                var middle = (control + ToMap(points[i + 1])) / 2f;
                p.QuadraticCurveTo(control, middle);
            }
            p.LineTo(ToMap(points[points.Length - 1]));
        }

        static Rect Inset(Rect r, float amount) =>
            new Rect(r.x + amount, r.y + amount, r.width - 2f * amount, r.height - 2f * amount);
    }
}
