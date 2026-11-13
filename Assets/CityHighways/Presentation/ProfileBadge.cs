using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Эмблема профиля у имени игрока, нарисованная кодом: звезда, ромб, шестерня, флажок, молния, корона.</summary>
    public sealed class ProfileBadge : VisualElement
    {
        string _emblem;
        Color _color;

        public ProfileBadge(float size, string emblem = "", Color? color = null)
        {
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            _emblem = emblem ?? "";
            _color = color ?? new Color(0.98f, 0.78f, 0.25f);
            generateVisualContent += Draw;
        }

        public void Set(string emblem, Color color)
        {
            _emblem = emblem ?? "";
            _color = color;
            style.display = string.IsNullOrEmpty(_emblem) ? DisplayStyle.None : DisplayStyle.Flex;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (string.IsNullOrEmpty(_emblem)) return;
            var p = context.painter2D;
            var rect = contentRect;
            float s = Mathf.Min(rect.width, rect.height);
            if (s <= 0f) return;
            var c = rect.center;
            float r = s * 0.46f;
            p.fillColor = _color;
            p.strokeColor = _color;
            p.lineWidth = Mathf.Max(1.2f, s * 0.12f);
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            switch (_emblem)
            {
                case "emblem_star":
                    Polygon(p, c, r, r * 0.45f, 5, -90f);
                    p.Fill();
                    break;
                case "emblem_diamond":
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(0f, -r));
                    p.LineTo(c + new Vector2(r * 0.7f, 0f));
                    p.LineTo(c + new Vector2(0f, r));
                    p.LineTo(c + new Vector2(-r * 0.7f, 0f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case "emblem_gear":
                    Polygon(p, c, r, r * 0.72f, 8, 0f);
                    p.Fill();
                    p.fillColor = new Color(0.10f, 0.11f, 0.14f);
                    p.BeginPath();
                    p.Arc(c, r * 0.32f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                    break;
                case "emblem_flag":
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-r * 0.6f, r));
                    p.LineTo(c + new Vector2(-r * 0.6f, -r));
                    p.Stroke();
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-r * 0.5f, -r));
                    p.LineTo(c + new Vector2(r * 0.9f, -r * 0.55f));
                    p.LineTo(c + new Vector2(-r * 0.5f, -r * 0.1f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case "emblem_bolt":
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(r * 0.25f, -r));
                    p.LineTo(c + new Vector2(-r * 0.55f, r * 0.15f));
                    p.LineTo(c + new Vector2(-r * 0.05f, r * 0.15f));
                    p.LineTo(c + new Vector2(-r * 0.25f, r));
                    p.LineTo(c + new Vector2(r * 0.55f, -r * 0.2f));
                    p.LineTo(c + new Vector2(r * 0.05f, -r * 0.2f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case "emblem_crown":
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-r, r * 0.6f));
                    p.LineTo(c + new Vector2(-r, -r * 0.5f));
                    p.LineTo(c + new Vector2(-r * 0.45f, 0f));
                    p.LineTo(c + new Vector2(0f, -r * 0.8f));
                    p.LineTo(c + new Vector2(r * 0.45f, 0f));
                    p.LineTo(c + new Vector2(r, -r * 0.5f));
                    p.LineTo(c + new Vector2(r, r * 0.6f));
                    p.ClosePath();
                    p.Fill();
                    break;
            }
        }

        /// <summary>Звезда/зубчатое колесо: points вершин, чередуются внешний и внутренний радиусы.</summary>
        static void Polygon(Painter2D p, Vector2 c, float outer, float inner, int points, float startDegrees)
        {
            p.BeginPath();
            for (int i = 0; i < points * 2; i++)
            {
                float angle = (startDegrees + i * 180f / points) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? outer : inner;
                var point = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
        }
    }
}
