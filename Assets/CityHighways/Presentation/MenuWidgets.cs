using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Значки меню, нарисованные кодом (в шрифтах игры нет эмодзи): геймпад, шапочка выпускника, глобус,
    /// открытая книга, диаграмма, шестерёнка, луна, солнце, палитра, колокольчик, крестик, силуэт города,
    /// газета, «плей» и звезда.
    /// </summary>
    public sealed class MenuIcon : VisualElement
    {
        public enum Glyph { Gamepad, Cap, Globe, Book, Chart, Gear, Moon, Sun, Palette, Bell, Close, Skyline, News, Play, Star }

        Glyph _glyph;
        Color _color;

        public MenuIcon(Glyph glyph, float size, Color color)
        {
            _glyph = glyph;
            _color = color;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            generateVisualContent += Draw;
        }

        public void SetColor(Color color)
        {
            _color = color;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (float.IsNaN(s) || s < 4f) return;
            var p = context.painter2D;
            var c = r.center;
            float u = s / 24f; // значки размечены на сетке 24×24
            Vector2 P(float x, float y) => new Vector2(c.x + (x - 12f) * u, c.y + (y - 12f) * u);
            p.strokeColor = _color;
            p.fillColor = _color;
            p.lineWidth = 2f * u;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            void Line(float x0, float y0, float x1, float y1)
            {
                p.BeginPath();
                p.MoveTo(P(x0, y0));
                p.LineTo(P(x1, y1));
                p.Stroke();
            }
            void Circle(float x, float y, float radius, bool fill)
            {
                p.BeginPath();
                p.Arc(P(x, y), radius * u, Angle.Degrees(0f), Angle.Degrees(360f));
                if (fill) p.Fill();
                else p.Stroke();
            }
            void Poly(bool fill, params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                p.ClosePath();
                if (fill) p.Fill();
                else p.Stroke();
            }

            switch (_glyph)
            {
                case Glyph.Gamepad:
                    Poly(false, 5, 8, 19, 8, 22, 17, 18, 18, 15, 14, 9, 14, 6, 18, 2, 17);
                    Line(6.5f, 11f, 9.5f, 11f);
                    Line(8f, 9.5f, 8f, 12.5f);
                    Circle(15.5f, 10.5f, 1.1f, true);
                    Circle(17.8f, 12.5f, 1.1f, true);
                    break;
                case Glyph.Cap:
                    Poly(false, 12, 5, 23, 10, 12, 15, 1, 10);
                    Poly(false, 6, 12.5f, 6, 17, 12, 19.5f, 18, 17, 18, 12.5f);
                    Line(21f, 11f, 21f, 17f);
                    break;
                case Glyph.Globe:
                    Circle(12f, 12f, 9f, false);
                    Line(3f, 12f, 21f, 12f);
                    p.BeginPath();
                    for (int i = 0; i <= 16; i++)
                    {
                        float a = -Mathf.PI / 2f + i * Mathf.PI / 16f;
                        var point = P(12f + Mathf.Cos(a) * 4f, 12f + Mathf.Sin(a) * 9f);
                        if (i == 0) p.MoveTo(point);
                        else p.LineTo(point);
                    }
                    for (int i = 0; i <= 16; i++)
                    {
                        float a = Mathf.PI / 2f + i * Mathf.PI / 16f;
                        p.LineTo(P(12f + Mathf.Cos(a) * 4f, 12f + Mathf.Sin(a) * 9f));
                    }
                    p.Stroke();
                    Line(5f, 7.5f, 19f, 7.5f);
                    Line(5f, 16.5f, 19f, 16.5f);
                    break;
                case Glyph.Book:
                    Poly(false, 12, 6, 3, 4, 3, 18, 12, 20);
                    Poly(false, 12, 6, 21, 4, 21, 18, 12, 20);
                    Line(6f, 8f, 9.5f, 8.8f);
                    Line(6f, 11.5f, 9.5f, 12.3f);
                    Line(14.5f, 8.8f, 18f, 8f);
                    break;
                case Glyph.Chart:
                    Line(3f, 21f, 21f, 21f);
                    Poly(true, 5, 13, 8, 13, 8, 20, 5, 20);
                    Poly(true, 10.5f, 8, 13.5f, 8, 13.5f, 20, 10.5f, 20);
                    Poly(true, 16, 4, 19, 4, 19, 20, 16, 20);
                    break;
                case Glyph.Gear:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Line(12f + Mathf.Cos(a) * 6.5f, 12f + Mathf.Sin(a) * 6.5f, 12f + Mathf.Cos(a) * 10f, 12f + Mathf.Sin(a) * 10f);
                    }
                    Circle(12f, 12f, 6.5f, false);
                    Circle(12f, 12f, 2.4f, true);
                    break;
                case Glyph.Moon:
                {
                    // Полумесяц: внешняя дуга слева и внутренняя справа.
                    p.BeginPath();
                    for (int i = 0; i <= 20; i++)
                    {
                        float a = Mathf.Deg2Rad * Mathf.Lerp(55f, 305f, i / 20f);
                        var point = P(12f + Mathf.Cos(a) * 9f, 12f + Mathf.Sin(a) * 9f);
                        if (i == 0) p.MoveTo(point);
                        else p.LineTo(point);
                    }
                    for (int i = 0; i <= 20; i++)
                    {
                        float a = Mathf.Deg2Rad * Mathf.Lerp(270f, 90f, i / 20f);
                        p.LineTo(P(16.5f + Mathf.Cos(a) * 7.4f, 12f + Mathf.Sin(a) * 7.4f));
                    }
                    p.ClosePath();
                    p.Fill();
                    break;
                }
                case Glyph.Sun:
                    Circle(12f, 12f, 4.5f, true);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Line(12f + Mathf.Cos(a) * 7.5f, 12f + Mathf.Sin(a) * 7.5f, 12f + Mathf.Cos(a) * 10.5f, 12f + Mathf.Sin(a) * 10.5f);
                    }
                    break;
                case Glyph.Palette:
                    p.BeginPath();
                    for (int i = 0; i <= 28; i++)
                    {
                        float a = Mathf.Deg2Rad * (40f + i * 290f / 28f);
                        var point = P(12f + Mathf.Cos(a) * 9.5f, 12f + Mathf.Sin(a) * 8.5f);
                        if (i == 0) p.MoveTo(point);
                        else p.LineTo(point);
                    }
                    p.LineTo(P(14f, 13f));
                    p.ClosePath();
                    p.Stroke();
                    p.fillColor = new Color(0.91f, 0.30f, 0.24f);
                    Circle(8f, 9f, 1.6f, true);
                    p.fillColor = new Color(0.95f, 0.77f, 0.06f);
                    Circle(12.5f, 6.5f, 1.6f, true);
                    p.fillColor = new Color(0.15f, 0.68f, 0.38f);
                    Circle(7f, 14f, 1.6f, true);
                    p.fillColor = new Color(0.20f, 0.60f, 0.86f);
                    Circle(10.5f, 18f, 1.6f, true);
                    break;
                case Glyph.Bell:
                    p.BeginPath();
                    p.MoveTo(P(5f, 17f));
                    p.BezierCurveTo(P(7f, 15f), P(6f, 6f), P(12f, 5f));
                    p.BezierCurveTo(P(18f, 6f), P(17f, 15f), P(19f, 17f));
                    p.ClosePath();
                    p.Stroke();
                    Line(12f, 3f, 12f, 5f);
                    Circle(12f, 19.5f, 1.8f, true);
                    break;
                case Glyph.Close:
                    Line(6f, 6f, 18f, 18f);
                    Line(18f, 6f, 6f, 18f);
                    break;
                case Glyph.Skyline:
                    Poly(true, 2, 21, 2, 12, 6, 12, 6, 21);
                    Poly(true, 7, 21, 7, 6, 10, 4, 10, 21);
                    Poly(true, 11, 21, 11, 10, 13, 10, 13, 7, 14, 3, 15, 7, 15, 10, 17, 10, 17, 21);
                    Poly(true, 18, 21, 18, 14, 22, 14, 22, 21);
                    break;
                case Glyph.News:
                    Poly(false, 4, 5, 20, 5, 20, 19, 4, 19);
                    Poly(true, 6.5f, 7.5f, 11.5f, 7.5f, 11.5f, 11.5f, 6.5f, 11.5f);
                    Line(13.5f, 8f, 17.5f, 8f);
                    Line(13.5f, 11f, 17.5f, 11f);
                    Line(6.5f, 14f, 17.5f, 14f);
                    Line(6.5f, 16.5f, 17.5f, 16.5f);
                    break;
                case Glyph.Play:
                    Poly(true, 8, 5, 19, 12, 8, 19);
                    break;
                default: // Star
                {
                    p.BeginPath();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                        float radius = i % 2 == 0 ? 10f : 4.2f;
                        var point = P(12f + Mathf.Cos(a) * radius, 12.5f + Mathf.Sin(a) * radius);
                        if (i == 0) p.MoveTo(point);
                        else p.LineTo(point);
                    }
                    p.ClosePath();
                    p.Fill();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Кнопка главного меню: градиентный фон с бегущим бликом, значок слева, подпись, место под значок справа
    /// (например, «Быстрый старт» или «3 открытых игры»). Наведение — подсветка, рамка-свечение и сдвиг вверх.
    /// </summary>
    public sealed class MenuButton : VisualElement
    {
        /// <summary>Курсор наведён на любую кнопку меню (приложение играет тихий щелчок).</summary>
        public static Action Hovered;

        readonly bool _primary;
        readonly ColorMesh _mesh = new ColorMesh();
        readonly MenuIcon _icon;
        readonly Label _title;
        float _phase, _hover, _hoverTarget;

        /// <summary>Место у правого края: туда кладут маленькую кнопку или значок-счётчик.</summary>
        public VisualElement Badge { get; }

        public MenuButton(MenuIcon.Glyph glyph, string title, Action onClick, bool primary)
        {
            _primary = primary;
            _phase = UnityEngine.Random.value * 10f;
            pickingMode = PickingMode.Position;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = 58f;
            style.marginBottom = 10f;
            Ui.Padding(this, 0f, 18f);
            Ui.Transitions(this, "translate");
            generateVisualContent += Draw;

            _icon = new MenuIcon(glyph, 26f, Color.white);
            _icon.style.marginRight = 14f;
            Add(_icon);
            _title = Ui.Text(title, 20f, Color.white, true);
            _title.style.flexGrow = 1f;
            _title.style.flexShrink = 1f;
            Add(_title);
            Badge = Ui.Row();
            Badge.pickingMode = PickingMode.Ignore;
            Add(Badge);

            RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hoverTarget = 1f;
                style.translate = new Translate(new Length(0f), new Length(-2f));
                Hovered?.Invoke();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hoverTarget = 0f;
                style.translate = new Translate(new Length(0f), new Length(0f));
            });
            RegisterCallback<ClickEvent>(e =>
            {
                // Клик по маленькой кнопке справа — её дело, а не всей строки.
                if (e.target is VisualElement target && target != this && Badge.Contains(target)) return;
                Ui.ButtonClicked?.Invoke();
                onClick?.Invoke();
            });
        }

        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        public void Tick(float dt)
        {
            _phase += dt * (0.25f + 0.55f * _hover);
            _hover = Mathf.MoveTowards(_hover, _hoverTarget, dt / 0.3f);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var size = layout.size;
            if (float.IsNaN(size.x) || size.x < 10f) return;
            var rect = new Rect(0f, 0f, size.x, size.y);
            // Основная — красный градиент действия, остальные — тёмно-синий.
            Color a = _primary ? new Color(0.75f, 0.22f, 0.17f) : new Color(0.17f, 0.24f, 0.31f);
            Color b = _primary ? new Color(0.91f, 0.30f, 0.24f) : new Color(0.24f, 0.35f, 0.44f);
            float lift = 0.10f * _hover;
            a = new Color(a.r + lift, a.g + lift, a.b + lift, 0.94f);
            b = new Color(b.r + lift, b.g + lift, b.b + lift, 0.94f);
            float band = Mathf.Repeat(_phase, 1.6f) - 0.3f; // блик бежит слева направо
            float shine = 0.06f + 0.10f * _hover;
            _mesh.Clear();
            _mesh.RoundRectGradient(rect, 12f, t =>
            {
                var c = Color.Lerp(a, b, t);
                float glow = Mathf.Exp(-(t - band) * (t - band) / 0.012f) * shine;
                return new Color(Mathf.Min(1f, c.r + glow), Mathf.Min(1f, c.g + glow), Mathf.Min(1f, c.b + glow), c.a);
            });
            _mesh.Flush(context);

            // Подсветка при наведении: свечение за краем кнопки.
            if (_hover > 0.01f)
            {
                var p = context.painter2D;
                var glowColor = _primary ? new Color(1f, 0.45f, 0.35f) : new Color(0.45f, 0.70f, 0.95f);
                for (int i = 0; i < 4; i++)
                {
                    float grow = 1.5f + i * 2.5f;
                    p.strokeColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.22f * _hover * (1f - i / 4f));
                    p.lineWidth = 2.5f;
                    RoundRect(p, new Rect(-grow, -grow, size.x + 2f * grow, size.y + 2f * grow), 12f + grow);
                    p.Stroke();
                }
            }
        }

        internal static void RoundRect(Painter2D p, Rect r, float radius)
        {
            radius = Mathf.Min(radius, r.width / 2f, r.height / 2f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            p.LineTo(new Vector2(r.xMax - radius, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + radius), radius);
            p.LineTo(new Vector2(r.xMax, r.yMax - radius));
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - radius, r.yMax), radius);
            p.LineTo(new Vector2(r.xMin + radius, r.yMax));
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMax - radius), radius);
            p.LineTo(new Vector2(r.xMin, r.yMin + radius));
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + radius, r.yMin), radius);
            p.ClosePath();
        }
    }

    /// <summary>
    /// Уровень профиля из того, что уже копится: партии, победы, достижения, задания дня.
    /// Опыт: 20 за партию, +30 за победу, +25 за достижение, +15 за задание дня.
    /// До второго уровня — 100 опыта, каждый следующий на 50 дороже.
    /// </summary>
    public static class ProfileLevel
    {
        public const int XpPerGame = 20, XpPerWin = 30, XpPerAchievement = 25, XpPerDaily = 15;

        public static int Xp(PlayerProfile profile) => profile == null ? 0 :
            profile.games * XpPerGame + profile.wins * XpPerWin
            + (profile.achievements?.Count ?? 0) * XpPerAchievement + profile.dailyTotal * XpPerDaily;

        /// <summary>Уровень по опыту; into — сколько набрано на текущем уровне, need — сколько нужно до следующего.</summary>
        public static int Of(int xp, out int into, out int need)
        {
            int level = 1;
            need = 100;
            while (xp >= need)
            {
                xp -= need;
                level++;
                need = 100 + 50 * (level - 1);
            }
            into = xp;
            return level;
        }

        /// <summary>Звание по уровню — показывается, если титул в профиле не выбран.</summary>
        public static string Rank(int level) =>
            level < 3 ? "Стажёр" : level < 5 ? "Прораб" : level < 8 ? "Инженер" : level < 12 ? "Главный инженер" : "Градостроитель";

        /// <summary>Цвет звёзд: каждые 5 уровней — следующая ступень (бронза, серебро, золото, платина).</summary>
        public static Color TierColor(int level)
        {
            switch (Mathf.Clamp((level - 1) / 5, 0, 3))
            {
                case 0: return new Color(0.80f, 0.52f, 0.30f);
                case 1: return new Color(0.78f, 0.82f, 0.88f);
                case 2: return new Color(0.98f, 0.78f, 0.25f);
                default: return new Color(0.55f, 0.92f, 0.95f);
            }
        }

        /// <summary>Сколько звёзд из пяти горит: 1–5 внутри ступени.</summary>
        public static int Stars(int level) => (level - 1) % 5 + 1;
    }

    /// <summary>
    /// Карточка профиля 150×200 в главном меню: персонаж или аватар в анимированной рамке, имя, титул,
    /// пять звёзд с уровнем и полоска опыта. Клик открывает полный профиль.
    /// </summary>
    public sealed class ProfileCard : VisualElement
    {
        readonly VisualElement _ring;
        readonly CharacterView _figure;
        readonly AvatarView _avatar;
        readonly Label _name, _title, _level, _xp;
        readonly MenuIcon[] _stars = new MenuIcon[5];
        readonly ProgressBar _bar;
        Color _ringColor = Theme.Accent;
        float _time, _hover, _hoverTarget;

        public ProfileCard(Action onClick)
        {
            pickingMode = PickingMode.Position;
            style.width = 150f;
            style.height = 200f;
            style.alignItems = Align.Center;
            style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(this, 16f);
            Ui.Border(this, 1f, Theme.Line);
            Ui.Padding(this, 10f, 8f);
            Ui.Shadow(this, 16f);
            Ui.Transitions(this, "translate", "background-color");

            // Кольцо вокруг портрета рисуется кодом и вращается.
            _ring = new VisualElement();
            _ring.pickingMode = PickingMode.Ignore;
            _ring.style.width = 84f;
            _ring.style.height = 84f;
            _ring.style.alignItems = Align.Center;
            _ring.style.justifyContent = Justify.Center;
            _ring.generateVisualContent += DrawRing;
            Add(_ring);

            var portrait = new VisualElement();
            portrait.pickingMode = PickingMode.Ignore;
            portrait.style.width = 70f;
            portrait.style.height = 70f;
            portrait.style.overflow = Overflow.Hidden;
            portrait.style.backgroundColor = Theme.Panel;
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;
            Ui.Radius(portrait, 35f);
            _ring.Add(portrait);
            _figure = new CharacterView(70f, true);
            _figure.pickingMode = PickingMode.Ignore;
            portrait.Add(_figure);
            _avatar = new AvatarView(70f);
            Ui.Border(_avatar, 0f, Color.clear);
            Ui.Radius(_avatar, 35f);
            portrait.Add(_avatar);

            _name = Ui.Text("", 15f, Theme.Text, true);
            _name.style.marginTop = 6f;
            _name.style.unityTextAlign = TextAnchor.MiddleCenter;
            _name.style.whiteSpace = WhiteSpace.NoWrap;
            _name.style.overflow = Overflow.Hidden;
            _name.style.textOverflow = TextOverflow.Ellipsis;
            _name.style.maxWidth = 134f;
            Add(_name);
            _title = Ui.Text("", 11f, Theme.Gold, true);
            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.whiteSpace = WhiteSpace.NoWrap;
            Add(_title);

            var stars = Ui.Row();
            stars.style.marginTop = 6f;
            for (int i = 0; i < 5; i++)
            {
                _stars[i] = new MenuIcon(MenuIcon.Glyph.Star, 13f, Theme.Gold);
                _stars[i].style.marginRight = 1f;
                stars.Add(_stars[i]);
            }
            _level = Ui.Number("", 12f, Theme.Text, true);
            _level.style.marginLeft = 5f;
            stars.Add(_level);
            Add(stars);

            _bar = new ProgressBar(118f, 5f, Theme.Success);
            _bar.style.marginTop = 6f;
            Add(_bar);
            _xp = Ui.Number("", 10f, Theme.TextDim);
            _xp.style.marginTop = 2f;
            Add(_xp);

            RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hoverTarget = 1f;
                style.translate = new Translate(new Length(0f), new Length(-3f));
                style.backgroundColor = Theme.PanelHover;
                MenuButton.Hovered?.Invoke();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hoverTarget = 0f;
                style.translate = new Translate(new Length(0f), new Length(0f));
                style.backgroundColor = Theme.PanelGlass;
            });
            RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                onClick?.Invoke();
            });
        }

        public void Set(PlayerProfile profile)
        {
            if (profile == null) return;
            bool picture = Profiles.AvatarOf(profile) != null;
            Ui.Show(_avatar, picture);
            Ui.Show(_figure, !picture);
            if (picture) _avatar.Set(profile);
            else _figure.Set(profile.Outfit);

            int xp = ProfileLevel.Xp(profile);
            int level = ProfileLevel.Of(xp, out int into, out int need);
            _name.text = profile.name;
            string title = Profiles.TitleText(profile.title);
            _title.text = string.IsNullOrEmpty(title) ? ProfileLevel.Rank(level) : title;
            var tier = ProfileLevel.TierColor(level);
            int lit = ProfileLevel.Stars(level);
            for (int i = 0; i < 5; i++) _stars[i].SetColor(i < lit ? tier : new Color(1f, 1f, 1f, 0.15f));
            _level.text = $"Lvl {level}";
            _bar.Set(into / (float)Mathf.Max(1, need));
            _xp.text = $"{into}/{need} опыта";
            var frame = Profiles.FrameColor(profile.frame);
            _ringColor = frame.a > 0f ? frame : tier;
        }

        public void Tick(float dt)
        {
            _time += dt * (1f + 1.5f * _hover);
            _hover = Mathf.MoveTowards(_hover, _hoverTarget, dt / 0.3f);
            _ring.MarkDirtyRepaint();
        }

        void DrawRing(MeshGenerationContext context)
        {
            var r = _ring.contentRect;
            if (float.IsNaN(r.width) || r.width < 10f) return;
            var p = context.painter2D;
            var c = r.center;
            float radius = r.width / 2f - 3f;
            // Тусклое кольцо и две бегущие по нему яркие дуги-«кометы».
            p.lineWidth = 3f;
            p.lineCap = LineCap.Round;
            p.strokeColor = new Color(_ringColor.r, _ringColor.g, _ringColor.b, 0.25f);
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();
            for (int comet = 0; comet < 2; comet++)
            {
                float start = _time * 90f + comet * 180f;
                const int pieces = 10;
                for (int i = 0; i < pieces; i++)
                {
                    float a0 = start + i * 9f;
                    p.strokeColor = new Color(_ringColor.r, _ringColor.g, _ringColor.b, (i + 1f) / pieces * (0.7f + 0.3f * _hover));
                    p.BeginPath();
                    p.Arc(c, radius, Angle.Degrees(a0), Angle.Degrees(a0 + 10f));
                    p.Stroke();
                }
            }
        }
    }
}
