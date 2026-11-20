using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Логотип клуба строителей. Описывается строкой «форма.знак.цвет.ранг» (её выдаёт сервер):
    /// - форма — щит, круг, шестиугольник, ромб, плашка или восьмиугольник;
    /// - знак — значок из меню (дорога, город, кубок, звезда, флаг…);
    /// - цвет — из 12 насыщенных цветов;
    /// - ранг — 0 у стартовых логотипов, 1–3 у заработанных за цели недели: бронзовая, серебряная или золотая окантовка
    ///   и звёздочки сверху.
    /// Заработанный логотип собирается из id клуба и недели, поэтому у каждого клуба свои — уникальные.
    /// </summary>
    public sealed class ClubLogo : VisualElement
    {
        public const int Shapes = 6, Glyphs = 12, Colors = 12;

        static readonly MenuIcon.Glyph[] GlyphSet =
        {
            MenuIcon.Glyph.Road, MenuIcon.Glyph.Skyline, MenuIcon.Glyph.Trophy, MenuIcon.Glyph.Star, MenuIcon.Glyph.Flag, MenuIcon.Glyph.Flame,
            MenuIcon.Glyph.Snow, MenuIcon.Glyph.Globe, MenuIcon.Glyph.Gear, MenuIcon.Glyph.Coins, MenuIcon.Glyph.Box, MenuIcon.Glyph.Pin
        };

        static readonly Color[] Palette12 =
        {
            new Color(0.86f, 0.30f, 0.26f), new Color(0.93f, 0.55f, 0.18f), new Color(0.93f, 0.76f, 0.20f), new Color(0.50f, 0.74f, 0.25f),
            new Color(0.18f, 0.64f, 0.42f), new Color(0.15f, 0.62f, 0.66f), new Color(0.22f, 0.52f, 0.86f), new Color(0.32f, 0.34f, 0.80f),
            new Color(0.56f, 0.32f, 0.78f), new Color(0.80f, 0.30f, 0.62f), new Color(0.45f, 0.36f, 0.30f), new Color(0.36f, 0.42f, 0.48f)
        };

        static readonly Color[] TierColors =
        {
            new Color(1f, 1f, 1f, 0.25f), new Color(0.80f, 0.52f, 0.30f), new Color(0.82f, 0.85f, 0.90f), new Color(1f, 0.80f, 0.30f)
        };

        /// <summary>Стартовые логотипы — из них выбирают при создании клуба.</summary>
        public static readonly string[] Starters =
        {
            "0.0.6.0", "1.1.4.0", "2.4.0.0", "3.3.8.0", "4.9.2.0", "5.8.11.0", "0.11.5.0", "2.2.9.0"
        };

        int _shape, _color, _tier;
        readonly float _size;
        MenuIcon _icon;

        public string Logo { get; private set; } = "";

        public ClubLogo(string logo, float size = 40f)
        {
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            generateVisualContent += Draw;
            _size = size;
            Set(logo);
        }

        public static bool TryParse(string logo, out int shape, out int glyph, out int color, out int tier)
        {
            shape = glyph = color = tier = 0;
            if (string.IsNullOrEmpty(logo)) return false;
            var parts = logo.Split('.');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[0], out shape) || !int.TryParse(parts[1], out glyph) || !int.TryParse(parts[2], out color)
                || !int.TryParse(parts[3], out tier)) return false;
            shape = Mathf.Clamp(shape, 0, Shapes - 1);
            glyph = Mathf.Clamp(glyph, 0, Glyphs - 1);
            color = Mathf.Clamp(color, 0, Colors - 1);
            tier = Mathf.Clamp(tier, 0, 3);
            return true;
        }

        public void Set(string logo)
        {
            Logo = logo ?? "";
            TryParse(Logo, out _shape, out int glyph, out _color, out _tier);
            _icon?.RemoveFromHierarchy();
            _icon = new MenuIcon(GlyphSet[glyph], _size * 0.5f, Color.white);
            Add(_icon);
            MarkDirtyRepaint();
        }

        /// <summary>Название ранга для подписи.</summary>
        public static string TierTitle(string logo)
        {
            TryParse(logo, out _, out _, out _, out int tier);
            switch (tier)
            {
                case 1: return "бронзовый";
                case 2: return "серебряный";
                case 3: return "золотой";
                default: return "стартовый";
            }
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 2f) return;
            var p = context.painter2D;
            var c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.46f;

            p.fillColor = Palette12[_color];
            p.BeginPath();
            Outline(p, c, r);
            p.Fill();

            // Блик сверху — логотип выглядит «эмалевым».
            p.fillColor = new Color(1f, 1f, 1f, 0.12f);
            p.BeginPath();
            Outline(p, c + new Vector2(0f, -r * 0.08f), r * 0.82f);
            p.Fill();

            p.strokeColor = TierColors[_tier];
            p.lineWidth = _tier > 0 ? Mathf.Max(2f, r * 0.12f) : Mathf.Max(1f, r * 0.05f);
            p.BeginPath();
            Outline(p, c, r);
            p.Stroke();

            // Звёздочки ранга сверху.
            for (int i = 0; i < _tier; i++)
            {
                float x = c.x + (i - (_tier - 1) * 0.5f) * r * 0.36f;
                Star(p, new Vector2(x, c.y - r * 0.98f), r * 0.16f, TierColors[_tier]);
            }
        }

        void Outline(Painter2D p, Vector2 c, float r)
        {
            switch (_shape)
            {
                case 0: // щит
                    p.MoveTo(c + new Vector2(-r * 0.85f, -r * 0.8f));
                    p.LineTo(c + new Vector2(r * 0.85f, -r * 0.8f));
                    p.LineTo(c + new Vector2(r * 0.85f, r * 0.05f));
                    p.BezierCurveTo(c + new Vector2(r * 0.85f, r * 0.6f), c + new Vector2(r * 0.3f, r * 0.85f), c + new Vector2(0f, r));
                    p.BezierCurveTo(c + new Vector2(-r * 0.3f, r * 0.85f), c + new Vector2(-r * 0.85f, r * 0.6f), c + new Vector2(-r * 0.85f, r * 0.05f));
                    p.ClosePath();
                    break;
                case 1: // круг
                    p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    break;
                case 2:
                    Polygon(p, c, r, 6, 30f);
                    break;
                case 3:
                    Polygon(p, c, r, 4, 0f);
                    break;
                case 4: // плашка со скруглёнными углами
                {
                    float k = r * 0.82f, round = r * 0.28f;
                    p.MoveTo(c + new Vector2(-k + round, -k));
                    p.LineTo(c + new Vector2(k - round, -k));
                    p.ArcTo(c + new Vector2(k, -k), c + new Vector2(k, -k + round), round);
                    p.LineTo(c + new Vector2(k, k - round));
                    p.ArcTo(c + new Vector2(k, k), c + new Vector2(k - round, k), round);
                    p.LineTo(c + new Vector2(-k + round, k));
                    p.ArcTo(c + new Vector2(-k, k), c + new Vector2(-k, k - round), round);
                    p.LineTo(c + new Vector2(-k, -k + round));
                    p.ArcTo(c + new Vector2(-k, -k), c + new Vector2(-k + round, -k), round);
                    p.ClosePath();
                    break;
                }
                default:
                    Polygon(p, c, r, 8, 22.5f);
                    break;
            }
        }

        static void Polygon(Painter2D p, Vector2 c, float r, int sides, float startDegrees)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = (startDegrees + i * 360f / sides - 90f) * Mathf.Deg2Rad;
                var point = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
        }

        static void Star(Painter2D p, Vector2 c, float r, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float a = (i * 36f - 90f) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? r : r * 0.45f;
                var point = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
            p.Fill();
        }
    }
}
