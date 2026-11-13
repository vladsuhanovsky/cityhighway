using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Значок персонажа, нарисованный кодом (Painter2D): круг цвета игрока и эмблема.
    /// Везунчик — клевер, Прораб — каска, Страховщик — щит, Сборщик пошлин — шлагбаум,
    /// Чиновник — здание с колоннами, Мостовик — арочный мост.
    /// </summary>
    public sealed class CharacterIcon : VisualElement
    {
        CharacterKind _kind;
        Color _color;

        public CharacterIcon(float size, CharacterKind kind = CharacterKind.None, Color? color = null)
        {
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            _kind = kind;
            _color = color ?? Theme.Accent;
            generateVisualContent += Draw;
        }

        public void Set(CharacterKind kind, Color color)
        {
            _kind = kind;
            _color = color;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var rect = contentRect;
            float s = Mathf.Min(rect.width, rect.height);
            if (s <= 0f) return;
            var c = rect.center;
            float r = s / 2f;

            // Подложка: круг цвета игрока с тёмной серединой.
            p.fillColor = _color;
            Circle(p, c, r);
            p.Fill();
            p.fillColor = new Color(0.10f, 0.11f, 0.14f);
            Circle(p, c, r * 0.84f);
            p.Fill();

            var ink = _color;
            p.fillColor = ink;
            p.strokeColor = ink;
            p.lineWidth = Mathf.Max(1.2f, s * 0.07f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            float u = r * 0.84f; // радиус внутренней части — от него считаем эмблему

            switch (_kind)
            {
                case CharacterKind.Lucky:
                    // Клевер: четыре круга-листа и стебель.
                    foreach (var d in new[] { new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0) })
                    {
                        Circle(p, c + d * u * 0.30f + new Vector2(0, -u * 0.08f), u * 0.27f);
                        p.Fill();
                    }
                    Line(p, c + new Vector2(0, u * 0.25f), c + new Vector2(u * 0.22f, u * 0.65f));
                    break;

                case CharacterKind.Foreman:
                    // Каска: купол и поля.
                    p.BeginPath();
                    p.Arc(c + new Vector2(0, u * 0.18f), u * 0.52f, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Fill();
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-u * 0.72f, u * 0.22f));
                    p.LineTo(c + new Vector2(u * 0.72f, u * 0.22f));
                    p.LineTo(c + new Vector2(u * 0.72f, u * 0.36f));
                    p.LineTo(c + new Vector2(-u * 0.72f, u * 0.36f));
                    p.ClosePath();
                    p.Fill();
                    break;

                case CharacterKind.Insurer:
                    // Щит.
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(0, -u * 0.62f));
                    p.LineTo(c + new Vector2(u * 0.52f, -u * 0.40f));
                    p.LineTo(c + new Vector2(u * 0.44f, u * 0.18f));
                    p.LineTo(c + new Vector2(0, u * 0.66f));
                    p.LineTo(c + new Vector2(-u * 0.44f, u * 0.18f));
                    p.LineTo(c + new Vector2(-u * 0.52f, -u * 0.40f));
                    p.ClosePath();
                    p.Fill();
                    break;

                case CharacterKind.Tollman:
                {
                    // Шлагбаум: стойка и полосатая стрела.
                    Line(p, c + new Vector2(-u * 0.50f, u * 0.60f), c + new Vector2(-u * 0.50f, -u * 0.10f));
                    var a = c + new Vector2(-u * 0.50f, -u * 0.18f);
                    var b = c + new Vector2(u * 0.65f, -u * 0.18f);
                    float w = p.lineWidth;
                    p.lineWidth = u * 0.22f;
                    p.lineCap = LineCap.Butt;
                    for (int i = 0; i < 4; i++)
                    {
                        p.strokeColor = i % 2 == 0 ? ink : Color.white;
                        Line(p, Vector2.Lerp(a, b, i / 4f), Vector2.Lerp(a, b, (i + 1) / 4f));
                    }
                    p.lineWidth = w;
                    break;
                }

                case CharacterKind.Official:
                    // Здание мэрии: фронтон, три колонны, ступень.
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-u * 0.62f, -u * 0.22f));
                    p.LineTo(c + new Vector2(0, -u * 0.62f));
                    p.LineTo(c + new Vector2(u * 0.62f, -u * 0.22f));
                    p.ClosePath();
                    p.Fill();
                    for (int i = -1; i <= 1; i++)
                        Line(p, c + new Vector2(i * u * 0.38f, -u * 0.10f), c + new Vector2(i * u * 0.38f, u * 0.40f));
                    Line(p, c + new Vector2(-u * 0.62f, u * 0.56f), c + new Vector2(u * 0.62f, u * 0.56f));
                    break;

                case CharacterKind.Bridger:
                    // Арочный мост над водой.
                    Line(p, c + new Vector2(-u * 0.70f, -u * 0.12f), c + new Vector2(u * 0.70f, -u * 0.12f));
                    p.BeginPath();
                    p.Arc(c + new Vector2(0, u * 0.40f), u * 0.45f, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.Stroke();
                    Line(p, c + new Vector2(-u * 0.45f, -u * 0.12f), c + new Vector2(-u * 0.45f, u * 0.40f));
                    Line(p, c + new Vector2(u * 0.45f, -u * 0.12f), c + new Vector2(u * 0.45f, u * 0.40f));
                    p.strokeColor = new Color(0.45f, 0.70f, 1f);
                    Line(p, c + new Vector2(-u * 0.55f, u * 0.62f), c + new Vector2(u * 0.55f, u * 0.62f));
                    break;

                default:
                    // Без персонажа — просто точка.
                    Circle(p, c, u * 0.22f);
                    p.Fill();
                    break;
            }
        }

        static void Circle(Painter2D p, Vector2 center, float radius)
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
    }
}
