using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Карточка с двумя сторонами: клик переворачивает её (сжатие по ширине → смена стороны → раскрытие).
    /// Лицо — иконка и название, оборот — числа и применение.
    /// </summary>
    public sealed class FlipCard : VisualElement
    {
        readonly VisualElement _front;
        readonly VisualElement _back;
        bool _flipped, _busy;

        public FlipCard(float width, float height, VisualElement front, VisualElement back)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            pickingMode = PickingMode.Position;
            _front = front;
            _back = back;
            foreach (var side in new[] { front, back })
            {
                side.style.position = Position.Absolute;
                side.style.left = 0f;
                side.style.top = 0f;
                side.style.right = 0f;
                side.style.bottom = 0f;
                side.pickingMode = PickingMode.Ignore;
                Add(side);
            }
            Ui.Show(back, false);
            Ui.Transitions(this, 0.16f, "scale", "translate");
            RegisterCallback<PointerEnterEvent>(_ =>
            {
                style.translate = new Translate(new Length(0f), new Length(-4f));
                Ui.Hovered?.Invoke();
            });
            RegisterCallback<PointerLeaveEvent>(_ => style.translate = new Translate(new Length(0f), new Length(0f)));
            RegisterCallback<ClickEvent>(_ => Flip());
        }

        public void Flip()
        {
            if (_busy) return;
            _busy = true;
            Ui.ButtonClicked?.Invoke();
            style.scale = new Scale(new Vector3(0.02f, 1f, 1f));
            schedule.Execute(() =>
            {
                _flipped = !_flipped;
                Ui.Show(_front, !_flipped);
                Ui.Show(_back, _flipped);
                style.scale = new Scale(Vector3.one);
                _busy = false;
            }).ExecuteLater(Ui.ReducedMotion ? 10 : 170);
        }
    }

    /// <summary>Стрелка между шагами схемы: линия с наконечником (в шрифте нет надёжных стрелок).</summary>
    public sealed class FlowArrow : VisualElement
    {
        readonly Color _color;
        readonly bool _down;

        public FlowArrow(float width, float height, Color color, bool down = false)
        {
            _color = color;
            _down = down;
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 4f || r.height < 4f) return;
            var p = context.painter2D;
            p.strokeColor = _color;
            p.lineWidth = 3f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            Vector2 a, b, side;
            if (_down)
            {
                a = new Vector2(r.center.x, r.yMin + 4f);
                b = new Vector2(r.center.x, r.yMax - 4f);
                side = new Vector2(7f, -9f);
            }
            else
            {
                a = new Vector2(r.xMin + 4f, r.center.y);
                b = new Vector2(r.xMax - 4f, r.center.y);
                side = new Vector2(-9f, 7f);
            }
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
            p.BeginPath();
            if (_down)
            {
                p.MoveTo(b + new Vector2(-side.x, side.y));
                p.LineTo(b);
                p.LineTo(b + side);
            }
            else
            {
                p.MoveTo(b + new Vector2(side.x, -side.y));
                p.LineTo(b);
                p.LineTo(b + side);
            }
            p.Stroke();
        }
    }

    /// <summary>
    /// Короткая зацикленная анимация вместо видео: дорога строится участок за участком, затем её улучшают
    /// до магистрали (вспыхивает оранжевая рамка), и всё начинается снова. Живёт, пока видна на экране.
    /// </summary>
    public sealed class RoadBuildAnimation : VisualElement
    {
        readonly int _pieces;
        readonly Color _color;
        readonly Label _caption;
        float _time;
        const float PieceTime = 0.45f, Hold = 1.2f, Upgrade = 1.8f, Rest = 0.8f;

        public RoadBuildAnimation(float width, float height, int pieces, Color color)
        {
            _pieces = Mathf.Max(1, pieces);
            _color = color;
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            Ui.Radius(this, 10f);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            _caption = Ui.Text("", 13f, Theme.Text, true);
            _caption.style.position = Position.Absolute;
            _caption.style.left = 0f;
            _caption.style.right = 0f;
            _caption.style.bottom = 10f;
            _caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(_caption);
            schedule.Execute(() =>
            {
                _time += 0.033f;
                MarkDirtyRepaint();
            }).Every(33);
        }

        float Cycle => _pieces * PieceTime + Hold + Upgrade + Rest;

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 20f) return;
            float t = _time % Cycle;
            float buildEnd = _pieces * PieceTime;
            int built = Mathf.Min(_pieces, Mathf.FloorToInt(t / PieceTime));
            float partial = t < buildEnd ? (t % PieceTime) / PieceTime : 1f;
            bool upgrading = t >= buildEnd + Hold && t < buildEnd + Hold + Upgrade;
            bool upgraded = t >= buildEnd + Hold;

            _caption.text = LocalizationService.Language == "en"
                ? (t < buildEnd ? $"Construction: segment {Mathf.Min(_pieces, built + 1)} of {_pieces}"
                    : !upgraded ? "Road built — score points for its length"
                    : "Highway upgrade: orange outline and higher tolls")
                : (t < buildEnd ? $"Стройка: участок {Mathf.Min(_pieces, built + 1)} из {_pieces}"
                    : !upgraded ? "Дорога построена — очки за длину"
                    : "Улучшение до магистрали: оранжевая рамка, проезд дороже");

            var p = context.painter2D;
            float y = r.height * 0.45f;
            var a = new Vector2(r.xMin + 50f, y);
            var b = new Vector2(r.xMax - 50f, y);
            float segment = (b.x - a.x) / _pieces;
            p.lineCap = LineCap.Round;

            if (upgraded)
            {
                float glow = upgrading ? 0.5f + 0.5f * Mathf.Sin((t - buildEnd - Hold) * 9f) : 1f;
                p.strokeColor = new Color(1f, 0.60f, 0.20f, 0.35f + 0.6f * glow);
                p.lineWidth = 20f + (upgrading ? 6f * glow : 0f);
                Line(p, a, b);
            }
            // Пустые места под участки — пунктиром.
            for (int i = 0; i < _pieces; i++)
            {
                var from = new Vector2(a.x + segment * i + 4f, y);
                var to = new Vector2(a.x + segment * (i + 1) - 4f, y);
                bool done = i < built || t >= buildEnd;
                bool current = i == built && t < buildEnd;
                p.strokeColor = done ? _color : new Color(1f, 1f, 1f, 0.15f);
                p.lineWidth = 12f;
                if (done) Line(p, from, to);
                else if (current) Line(p, from, Vector2.Lerp(from, to, Ui.EaseOutCubic(partial)));
                else
                {
                    p.lineWidth = 3f;
                    Line(p, from, to);
                }
            }
            foreach (var spot in new[] { a, b })
            {
                p.fillColor = new Color(0.08f, 0.09f, 0.11f);
                Circle(p, spot, 13f);
                p.fillColor = new Color(0.85f, 0.86f, 0.90f);
                Circle(p, spot, 9f);
            }
        }

        static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        static void Circle(Painter2D p, Vector2 c, float radius)
        {
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
        }
    }

    /// <summary>
    /// Схема для песочницы: места и дороги, по дороге можно кликнуть. Состояние дороги задаёт песочница
    /// (цвет, пунктир, рамка магистрали, подпись), а схема сообщает, по какой дороге кликнули.
    /// </summary>
    public sealed class SandboxBoard : VisualElement
    {
        public sealed class Spot
        {
            public string Id, Label;
            public Vector2 At;
            public Color Fill;
        }

        public sealed class Link
        {
            public string Id, From, To, Label;
            public Color Color;
            public bool Dashed, Frame, Highlight;
        }

        readonly Dictionary<string, Spot> _spots = new Dictionary<string, Spot>();
        readonly List<Link> _links = new List<Link>();
        public event Action<Link> Clicked;
        Link _hover;

        public IReadOnlyList<Link> Links => _links;

        public SandboxBoard(float width, float height)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            Ui.Radius(this, 10f);
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
            RegisterCallback<PointerMoveEvent>(e =>
            {
                var link = Hit(e.localPosition);
                if (link == _hover) return;
                _hover = link;
                MarkDirtyRepaint();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hover = null;
                MarkDirtyRepaint();
            });
            RegisterCallback<PointerDownEvent>(e =>
            {
                var link = Hit(e.localPosition);
                if (link == null) return;
                Ui.ButtonClicked?.Invoke();
                Clicked?.Invoke(link);
            });
        }

        public SandboxBoard Place(string id, float x, float y, Color fill, string label)
        {
            _spots[id] = new Spot { Id = id, At = new Vector2(x, y), Fill = fill, Label = label };
            return this;
        }

        public Link Road(string id, string from, string to)
        {
            var link = new Link { Id = id, From = from, To = to, Color = Color.gray, Dashed = true };
            _links.Add(link);
            return link;
        }

        /// <summary>Перерисовать после смены состояний дорог.</summary>
        public void Refresh()
        {
            MarkDirtyRepaint();
            PlaceLabels();
        }

        Vector2 P(Vector2 normalized) => new Vector2(normalized.x * contentRect.width, normalized.y * contentRect.height);

        Link Hit(Vector2 local)
        {
            Link best = null;
            float bestDistance = 16f;
            foreach (var link in _links)
            {
                var a = P(_spots[link.From].At);
                var b = P(_spots[link.To].At);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(local - a, ab) / Mathf.Max(1f, ab.sqrMagnitude));
                float distance = Vector2.Distance(local, a + ab * t);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = link;
                }
            }
            return best;
        }

        void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var link in _links)
            {
                var a = P(_spots[link.From].At);
                var b = P(_spots[link.To].At);
                if (link.Highlight)
                {
                    p.strokeColor = new Color(0.40f, 0.85f, 0.55f, 0.55f);
                    p.lineWidth = 24f;
                    Line(p, a, b);
                }
                if (link == _hover)
                {
                    p.strokeColor = new Color(1f, 1f, 1f, 0.18f);
                    p.lineWidth = 22f;
                    Line(p, a, b);
                }
                if (link.Frame)
                {
                    p.strokeColor = new Color(1f, 0.60f, 0.20f, 0.95f);
                    p.lineWidth = 16f;
                    Line(p, a, b);
                }
                p.strokeColor = link.Color;
                p.lineWidth = link.Dashed ? 5f : 10f;
                if (!link.Dashed) Line(p, a, b);
                else
                {
                    const int parts = 9;
                    for (int i = 0; i < parts; i += 2)
                        Line(p, Vector2.Lerp(a, b, i / (float)parts), Vector2.Lerp(a, b, (i + 1) / (float)parts));
                }
            }
            foreach (var spot in _spots.Values)
            {
                var c = P(spot.At);
                p.fillColor = new Color(0.08f, 0.09f, 0.11f);
                p.BeginPath();
                p.Arc(c, 12f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                p.fillColor = spot.Fill;
                p.BeginPath();
                p.Arc(c, 9f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }

        void PlaceLabels()
        {
            foreach (var child in new List<VisualElement>(Children())) child.RemoveFromHierarchy();
            foreach (var spot in _spots.Values)
            {
                if (string.IsNullOrEmpty(spot.Label)) continue;
                var c = P(spot.At);
                var label = Ui.Text(spot.Label, 12f, Theme.Text, true);
                label.pickingMode = PickingMode.Ignore;
                label.style.position = Position.Absolute;
                label.style.left = c.x - 60f;
                label.style.width = 120f;
                label.style.top = c.y + 13f;
                label.style.unityTextAlign = TextAnchor.UpperCenter;
                Add(label);
            }
            foreach (var link in _links)
            {
                if (string.IsNullOrEmpty(link.Label)) continue;
                var c = (P(_spots[link.From].At) + P(_spots[link.To].At)) / 2f;
                var label = Ui.Text(link.Label, 11f, Theme.Text, true);
                label.pickingMode = PickingMode.Ignore;
                label.style.position = Position.Absolute;
                label.style.left = c.x - 60f;
                label.style.width = 120f;
                label.style.top = c.y - 26f;
                label.style.unityTextAlign = TextAnchor.LowerCenter;
                Add(label);
            }
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
