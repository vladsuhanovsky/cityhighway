using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Маленькая схема для страницы правил, нарисованная кодом: места-кружки, дороги-полосы, подписи.
    /// Координаты мест — доли от ширины и высоты схемы.
    /// </summary>
    public sealed class RuleDiagram : VisualElement
    {
        sealed class Spot
        {
            public Vector2 At;
            public Color Fill;
            public string Label;
        }

        sealed class Link
        {
            public string From, To;
            public Color Color;
            public bool Dashed;
            public bool Frame;
            public string Label;
        }

        readonly Dictionary<string, Spot> _spots = new Dictionary<string, Spot>();
        readonly List<Link> _links = new List<Link>();

        public RuleDiagram(float width, float height)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            Ui.Radius(this, 10f);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        public RuleDiagram Place(string id, float x, float y, Color fill, string label)
        {
            _spots[id] = new Spot { At = new Vector2(x, y), Fill = fill, Label = label };
            return this;
        }

        /// <summary>Дорога: dashed — ещё не построена; frame — магистраль (оранжевая рамка); label — подпись посередине.</summary>
        public RuleDiagram Road(string from, string to, Color color, string label = null, bool dashed = false, bool frame = false)
        {
            _links.Add(new Link { From = from, To = to, Color = color, Label = label, Dashed = dashed, Frame = frame });
            return this;
        }

        Vector2 P(Vector2 normalized) => new Vector2(normalized.x * contentRect.width, normalized.y * contentRect.height);

        void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var link in _links)
            {
                var a = P(_spots[link.From].At);
                var b = P(_spots[link.To].At);
                if (link.Frame)
                {
                    p.strokeColor = new Color(1f, 0.60f, 0.20f, 0.95f);
                    p.lineWidth = 16f;
                    Line(p, a, b);
                }
                p.strokeColor = link.Color;
                p.lineWidth = 10f;
                if (!link.Dashed) Line(p, a, b);
                else
                {
                    const int parts = 7;
                    for (int i = 0; i < parts; i += 2)
                        Line(p, Vector2.Lerp(a, b, i / (float)parts), Vector2.Lerp(a, b, (i + 1) / (float)parts));
                }
            }
            foreach (var spot in _spots.Values)
            {
                var c = P(spot.At);
                p.fillColor = new Color(0.08f, 0.09f, 0.11f);
                Circle(p, c, 11f);
                p.Fill();
                p.fillColor = spot.Fill;
                Circle(p, c, 8f);
                p.Fill();
            }
        }

        /// <summary>Подписи — обычные Label поверх схемы (Painter2D текст не рисует).</summary>
        void PlaceLabels()
        {
            foreach (var child in new List<VisualElement>(Children())) child.RemoveFromHierarchy();
            foreach (var spot in _spots.Values)
            {
                if (string.IsNullOrEmpty(spot.Label)) continue;
                var c = P(spot.At);
                var label = Ui.Text(spot.Label, 12f, Theme.Text, true);
                label.style.position = Position.Absolute;
                label.style.left = c.x - 60f;
                label.style.width = 120f;
                label.style.top = c.y + 12f;
                label.style.unityTextAlign = TextAnchor.UpperCenter;
                Add(label);
            }
            foreach (var link in _links)
            {
                if (string.IsNullOrEmpty(link.Label)) continue;
                var c = (P(_spots[link.From].At) + P(_spots[link.To].At)) / 2f;
                var label = Ui.Text(link.Label, 11f, Theme.Text, true);
                label.style.position = Position.Absolute;
                label.style.left = c.x - 50f;
                label.style.width = 100f;
                label.style.top = c.y - 24f;
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

        static void Circle(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
        }
    }
}
