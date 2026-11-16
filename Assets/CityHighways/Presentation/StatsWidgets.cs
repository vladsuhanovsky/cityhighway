using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Линейный график очков по партиям: точки — победа (зелёная) или поражение (красная), пунктир — средний счёт.
    /// Подписи осей — обычные Label поверх рисунка.
    /// </summary>
    public sealed class ScoreLineChart : VisualElement
    {
        readonly List<(string label, int value, bool win)> _points = new List<(string, int, bool)>();
        readonly VisualElement _labels;
        int _min, _max;

        public ScoreLineChart(float height)
        {
            style.height = height;
            style.flexGrow = 1f;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            _labels = Ui.Box();
            _labels.pickingMode = PickingMode.Ignore;
            _labels.style.position = Position.Absolute;
            _labels.style.left = 0f;
            _labels.style.top = 0f;
            _labels.style.right = 0f;
            _labels.style.bottom = 0f;
            Add(_labels);
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        public void SetPoints(IEnumerable<(string label, int value, bool win)> points)
        {
            _points.Clear();
            _points.AddRange(points);
            if (_points.Count > 0)
            {
                _min = Mathf.Min(0, _points.Min(p => p.value));
                _max = Mathf.Max(10, _points.Max(p => p.value));
                int pad = Mathf.Max(5, (_max - _min) / 8);
                _max += pad;
            }
            MarkDirtyRepaint();
            PlaceLabels();
        }

        const float Left = 34f, Bottom = 20f, Top = 8f, Right = 10f;

        Vector2 Point(int index, int value)
        {
            var r = contentRect;
            float w = r.width - Left - Right, h = r.height - Top - Bottom;
            float x = Left + (_points.Count <= 1 ? w / 2f : w * index / (_points.Count - 1));
            float y = Top + h * (1f - (value - _min) / (float)Mathf.Max(1, _max - _min));
            return new Vector2(x, y);
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 40f || _points.Count == 0) return;
            var p = context.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            // Сетка: 4 горизонтали.
            p.strokeColor = Theme.Line;
            p.lineWidth = 1f;
            for (int i = 0; i <= 4; i++)
            {
                float y = Top + (r.height - Top - Bottom) * i / 4f;
                p.BeginPath();
                p.MoveTo(new Vector2(Left, y));
                p.LineTo(new Vector2(r.width - Right, y));
                p.Stroke();
            }
            // Средний счёт — пунктир.
            float average = (float)_points.Average(x => x.value);
            var a = Point(0, Mathf.RoundToInt(average));
            p.strokeColor = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.6f);
            p.lineWidth = 1.5f;
            for (float x = Left; x < r.width - Right; x += 10f)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x, a.y));
                p.LineTo(new Vector2(Mathf.Min(x + 5f, r.width - Right), a.y));
                p.Stroke();
            }
            // Заливка под линией и сама линия.
            if (_points.Count > 1)
            {
                p.fillColor = new Color(0.36f, 0.62f, 0.95f, 0.15f);
                p.BeginPath();
                p.MoveTo(new Vector2(Point(0, _points[0].value).x, r.height - Bottom));
                for (int i = 0; i < _points.Count; i++) p.LineTo(Point(i, _points[i].value));
                p.LineTo(new Vector2(Point(_points.Count - 1, 0).x, r.height - Bottom));
                p.ClosePath();
                p.Fill();
                p.strokeColor = new Color(0.45f, 0.70f, 1f);
                p.lineWidth = 3f;
                p.BeginPath();
                for (int i = 0; i < _points.Count; i++)
                {
                    var point = Point(i, _points[i].value);
                    if (i == 0) p.MoveTo(point);
                    else p.LineTo(point);
                }
                p.Stroke();
            }
            for (int i = 0; i < _points.Count; i++)
            {
                var point = Point(i, _points[i].value);
                p.fillColor = new Color(0.08f, 0.09f, 0.11f);
                p.BeginPath();
                p.Arc(point, 7f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                p.fillColor = _points[i].win ? GameColors.Success : GameColors.Danger;
                p.BeginPath();
                p.Arc(point, 5f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }

        void PlaceLabels()
        {
            _labels.Clear();
            var r = contentRect;
            if (r.width < 40f || _points.Count == 0) return;
            for (int i = 0; i <= 4; i++)
            {
                int value = Mathf.RoundToInt(Mathf.Lerp(_max, _min, i / 4f));
                var label = Ui.Number(value.ToString(), 10f, Theme.TextDim);
                label.style.position = Position.Absolute;
                label.style.left = 0f;
                label.style.width = Left - 6f;
                label.style.unityTextAlign = TextAnchor.MiddleRight;
                label.style.top = Top + (r.height - Top - Bottom) * i / 4f - 7f;
                _labels.Add(label);
            }
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points.Count > 6 && i % 2 == 1 && i != _points.Count - 1) continue;
                var point = Point(i, _points[i].value);
                var label = Ui.Text(_points[i].label, 10f, Theme.TextDim);
                label.style.position = Position.Absolute;
                label.style.left = point.x - 24f;
                label.style.width = 48f;
                label.style.unityTextAlign = TextAnchor.UpperCenter;
                label.style.top = r.height - Bottom + 3f;
                _labels.Add(label);
            }
        }
    }

    /// <summary>Кольцевая диаграмма: доли по цветам; в центре — подпись (например, процент побед).</summary>
    public sealed class DonutChart : VisualElement
    {
        readonly List<(float value, Color color)> _slices = new List<(float, Color)>();
        readonly Label _center;
        readonly Label _caption;

        public DonutChart(float size)
        {
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            _center = Ui.Number("", size * 0.2f, Theme.Text, true);
            _caption = Ui.Text("", 11f, Theme.TextDim);
            Add(_center);
            Add(_caption);
        }

        public void Set(IEnumerable<(float value, Color color)> slices, string center, string caption)
        {
            _slices.Clear();
            _slices.AddRange(slices.Where(s => s.value > 0f));
            _center.text = center;
            _caption.text = caption;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size < 10f) return;
            var p = context.painter2D;
            var c = r.center;
            float radius = size / 2f - 8f;
            p.lineWidth = size * 0.14f;
            p.lineCap = LineCap.Butt;
            float total = _slices.Sum(s => s.value);
            if (total <= 0f)
            {
                p.strokeColor = Theme.Line;
                p.BeginPath();
                p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
                return;
            }
            float angle = -90f;
            foreach (var (value, color) in _slices)
            {
                float sweep = 360f * value / total;
                p.strokeColor = color;
                p.BeginPath();
                p.Arc(c, radius, Angle.Degrees(angle + (_slices.Count > 1 ? 1f : 0f)), Angle.Degrees(angle + sweep - (_slices.Count > 1 ? 1f : 0f)));
                p.Stroke();
                angle += sweep;
            }
        }
    }

    /// <summary>
    /// Тепловая карта: все дороги карты тонкими линиями, дороги игрока — толще и «горячее» (синий → оранжевый → красный)
    /// в зависимости от того, в скольких партиях он их строил.
    /// </summary>
    public sealed class RoadHeatMap : VisualElement
    {
        CityMap _map;
        Dictionary<string, int> _counts = new Dictionary<string, int>();
        int _maxCount = 1;
        Rect _bounds;

        public RoadHeatMap(float width, float height)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.backgroundColor = new Color(0.08f, 0.10f, 0.13f);
            Ui.Radius(this, 10f);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(CityMap map, Dictionary<string, int> counts)
        {
            _map = map;
            _counts = counts ?? new Dictionary<string, int>();
            _maxCount = Mathf.Max(1, _counts.Count > 0 ? _counts.Values.Max() : 1);
            if (map != null && map.Locations.Count > 0)
            {
                float minX = map.Locations.Min(l => l.X), maxX = map.Locations.Max(l => l.X);
                float minY = map.Locations.Min(l => l.Y), maxY = map.Locations.Max(l => l.Y);
                _bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
            MarkDirtyRepaint();
        }

        public static Color Heat(float t)
        {
            var cold = new Color(0.25f, 0.55f, 0.95f);
            var warm = new Color(1f, 0.65f, 0.20f);
            var hot = new Color(0.95f, 0.25f, 0.25f);
            return t < 0.5f ? Color.Lerp(cold, warm, t * 2f) : Color.Lerp(warm, hot, (t - 0.5f) * 2f);
        }

        Vector2 Project(Location location)
        {
            var r = contentRect;
            const float pad = 14f;
            float w = r.width - 2f * pad, h = r.height - 2f * pad;
            // Координаты карты нормированы, а поле в игре растягивается по прямоугольнику — делаем так же.
            return new Vector2(pad + (location.X - _bounds.xMin) / Mathf.Max(0.001f, _bounds.width) * w,
                               pad + (location.Y - _bounds.yMin) / Mathf.Max(0.001f, _bounds.height) * h);
        }

        void Draw(MeshGenerationContext context)
        {
            if (_map == null || contentRect.width < 20f) return;
            var p = context.painter2D;
            p.lineCap = LineCap.Round;
            var where = _map.Locations.ToDictionary(l => l.Id, Project);
            foreach (var road in _map.Roads)
            {
                if (!where.TryGetValue(road.A, out var a) || !where.TryGetValue(road.B, out var b)) continue;
                p.strokeColor = new Color(1f, 1f, 1f, 0.10f);
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
            foreach (var road in _map.Roads.OrderBy(r => _counts.TryGetValue(r.Id, out var n) ? n : 0))
            {
                if (!_counts.TryGetValue(road.Id, out var count) || count <= 0) continue;
                if (!where.TryGetValue(road.A, out var a) || !where.TryGetValue(road.B, out var b)) continue;
                float t = count / (float)_maxCount;
                var color = Heat(t);
                p.strokeColor = new Color(color.r, color.g, color.b, 0.25f);
                p.lineWidth = 9f + 6f * t;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
                p.strokeColor = color;
                p.lineWidth = 3f + 2f * t;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
            p.fillColor = new Color(1f, 1f, 1f, 0.35f);
            foreach (var point in where.Values)
            {
                p.BeginPath();
                p.Arc(point, 1.8f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }
    }
}
