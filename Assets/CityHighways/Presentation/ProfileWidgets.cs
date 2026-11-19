using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Редкость предмета по цене: обычный, редкий, эпический, легендарный — с цветом.</summary>
    public static class Rarity
    {
        public enum Level { Common, Rare, Epic, Legendary }

        public static Level Of(int price) =>
            price <= 10 ? Level.Common : price <= 20 ? Level.Rare : price <= 30 ? Level.Epic : Level.Legendary;

        public static string Title(Level level)
        {
            switch (level)
            {
                case Level.Rare: return "Редкий";
                case Level.Epic: return "Эпический";
                case Level.Legendary: return "Легендарный";
                default: return "Обычный";
            }
        }

        public static Color ColorOf(Level level)
        {
            switch (level)
            {
                case Level.Rare: return GameColors.Rare;
                case Level.Epic: return GameColors.Epic;
                case Level.Legendary: return GameColors.Legendary;
                default: return GameColors.Common;
            }
        }

        /// <summary>Плашка «Редкий» цветом редкости.</summary>
        public static Label Tag(int price)
        {
            var level = Of(price);
            var color = ColorOf(level);
            var tag = Ui.Text(Title(level).ToUpperInvariant(), 10f, color, true);
            tag.style.letterSpacing = 1f;
            tag.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            Ui.Radius(tag, 6f);
            Ui.Padding(tag, 2f, 6f);
            return tag;
        }
    }

    /// <summary>
    /// Персонаж на поворотной площадке. Настоящей 3D-модели у персонажа нет (слои — плоские PNG спереди),
    /// поэтому поворот сделан как 2.5D: фигура сужается по ширине и чуть смещается, меняется освещение и тень на
    /// полу. Все слои двигаются вместе: раньше одежда, причёска и шляпа сдвигались с разной «глубиной» и при
    /// повороте съезжали с фигуры. Тяните мышью влево-вправо — фигура поворачивается до ±40°, отпустите —
    /// плавно покачивается. Надетая вещь «проявляется» анимацией.
    /// </summary>
    public sealed class TurntableCharacter : VisualElement
    {
        readonly float _width;
        readonly VisualElement _stage;
        readonly VisualElement _shadow;
        readonly Dictionary<string, VisualElement> _layers = new Dictionary<string, VisualElement>();
        Outfit _outfit;
        float _angle, _velocity, _time;
        bool _dragging;
        float _lastX;
        int _pointer = -1;

        public TurntableCharacter(float width)
        {
            _width = width;
            pickingMode = PickingMode.Position;
            style.width = width;
            style.height = width * 1.5f + 18f;
            style.alignItems = Align.Center;

            _shadow = Ui.Box();
            _shadow.style.position = Position.Absolute;
            _shadow.style.bottom = 4f;
            _shadow.style.width = width * 0.6f;
            _shadow.style.height = 16f;
            _shadow.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            Ui.Radius(_shadow, 8f);
            Add(_shadow);

            _stage = new VisualElement { pickingMode = PickingMode.Ignore };
            _stage.style.width = width;
            _stage.style.height = width * 1.5f;
            Add(_stage);
            foreach (var slot in CharacterCatalog.DrawOrder)
            {
                var layer = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Fill(layer);
                layer.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                Ui.Transitions(layer, 0.35f, "scale", "opacity");
                _stage.Add(layer);
                _layers[slot] = layer;
            }

            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _dragging = true;
                _pointer = e.pointerId;
                _lastX = e.position.x;
                this.CapturePointer(e.pointerId);
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging || e.pointerId != _pointer) return;
                float dx = e.position.x - _lastX;
                _lastX = e.position.x;
                _angle = Mathf.Clamp(_angle + dx * 0.5f, -40f, 40f);
                _velocity = dx * 30f;
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != _pointer) return;
                _dragging = false;
                if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            });
        }

        /// <summary>Показать наряд. animateSlot — слот, вещь в котором только что надели или примерили (проявится).</summary>
        public void Set(Outfit outfit, string animateSlot = null)
        {
            _outfit = outfit ?? Outfit.Default;
            foreach (var pair in _layers)
            {
                var texture = _outfit.TextureFor(pair.Key);
                pair.Value.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            }
            if (animateSlot != null && _layers.TryGetValue(animateSlot, out var changed))
            {
                // Анимация надевания: вещь появляется чуть крупнее и прозрачной и «садится» на место.
                Ui.Transitions(changed, 0f, "scale", "opacity");
                changed.style.scale = new Scale(new Vector3(1.18f, 1.18f, 1f));
                changed.style.opacity = 0f;
                changed.schedule.Execute(() =>
                {
                    Ui.Transitions(changed, 0.35f, "scale", "opacity");
                    changed.style.scale = new Scale(Vector3.one);
                    changed.style.opacity = 1f;
                }).ExecuteLater(30);
            }
        }

        public void Tick(float dt)
        {
            _time += dt;
            if (!_dragging)
            {
                // Отпустили — по инерции, потом плавное покачивание вокруг небольшого угла.
                _angle = Mathf.Clamp(_angle + _velocity * dt, -40f, 40f);
                _velocity *= Mathf.Exp(-dt * 4f);
                float rest = Mathf.Sin(_time * 0.8f) * 6f;
                _angle = Mathf.Lerp(_angle, rest, 1f - Mathf.Exp(-dt * 0.8f));
            }
            float rad = _angle * Mathf.Deg2Rad;
            float squeeze = Mathf.Lerp(1f, Mathf.Cos(rad), 0.8f);
            _stage.style.scale = new Scale(new Vector3(squeeze, 1f, 1f));
            // Сдвиг — целиком и с округлением до пикселя: при дробном сдвиге каждый кадр уменьшенная текстура
            // заново пересчитывается и края слоёв мерцают.
            _stage.style.translate = new Translate(new Length(Mathf.Round(Mathf.Sin(rad) * _width * 0.03f)), new Length(0f));
            // Свет падает спереди-слева: при повороте фигура чуть темнеет.
            float light = 1f - 0.22f * Mathf.Abs(Mathf.Sin(rad));
            var tint = new Color(light, light, light, 1f);
            foreach (var pair in _layers) pair.Value.style.unityBackgroundImageTintColor = tint;
            _shadow.style.translate = new Translate(new Length(-Mathf.Sin(rad) * _width * 0.08f), new Length(0f));
            _shadow.style.scale = new Scale(new Vector3(0.8f + 0.2f * squeeze, 1f, 1f));
        }
    }

    /// <summary>
    /// Анимированная рамка вокруг элемента (превью игрока): мягкое свечение пульсирует, по контуру бегут
    /// яркие «кометы». Цвет — рамки из профиля; без рамки — едва заметный контур.
    /// </summary>
    public sealed class AnimatedFrame : VisualElement
    {
        Color _color = Color.clear;
        float _time;
        readonly float _radius;

        public AnimatedFrame(float radius)
        {
            _radius = radius;
            pickingMode = PickingMode.Ignore;
            Ui.Fill(this);
            generateVisualContent += Draw;
        }

        public void SetColor(Color color)
        {
            _color = color;
            MarkDirtyRepaint();
        }

        public void Tick(float dt)
        {
            _time += dt;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var size = layout.size;
            if (float.IsNaN(size.x) || size.x < 10f) return;
            var p = context.painter2D;
            var rect = new Rect(1f, 1f, size.x - 2f, size.y - 2f);
            if (_color.a <= 0f)
            {
                p.strokeColor = Theme.Line;
                p.lineWidth = 1f;
                MenuButton.RoundRect(p, rect, _radius);
                p.Stroke();
                return;
            }
            // Свечение: несколько колец снаружи, яркость пульсирует.
            float pulse = 0.6f + 0.4f * Mathf.Sin(_time * 2.4f);
            for (int i = 1; i <= 4; i++)
            {
                p.strokeColor = new Color(_color.r, _color.g, _color.b, 0.10f * pulse * (5 - i) / 4f);
                p.lineWidth = 3f;
                MenuButton.RoundRect(p, new Rect(rect.x - i * 2f, rect.y - i * 2f, rect.width + i * 4f, rect.height + i * 4f), _radius + i * 2f);
                p.Stroke();
            }
            p.strokeColor = new Color(_color.r, _color.g, _color.b, 0.75f);
            p.lineWidth = 2f;
            MenuButton.RoundRect(p, rect, _radius);
            p.Stroke();
            // Кометы: короткие яркие отрезки, бегущие по периметру.
            float perimeter = 2f * (rect.width + rect.height);
            for (int comet = 0; comet < 2; comet++)
            {
                float head = Mathf.Repeat(_time * 160f + comet * perimeter / 2f, perimeter);
                for (int k = 0; k < 8; k++)
                {
                    float d = head - k * 5f;
                    var a = PointOn(rect, Mathf.Repeat(d, perimeter));
                    var b = PointOn(rect, Mathf.Repeat(d + 5f, perimeter));
                    p.strokeColor = new Color(Mathf.Min(1f, _color.r + 0.3f), Mathf.Min(1f, _color.g + 0.3f), Mathf.Min(1f, _color.b + 0.3f), 1f - k / 8f);
                    p.lineWidth = 3f;
                    p.BeginPath();
                    p.MoveTo(a);
                    p.LineTo(b);
                    p.Stroke();
                }
            }
        }

        /// <summary>Точка на периметре прямоугольника (по часовой от левого верхнего угла).</summary>
        static Vector2 PointOn(Rect r, float d)
        {
            if (d < r.width) return new Vector2(r.xMin + d, r.yMin);
            d -= r.width;
            if (d < r.height) return new Vector2(r.xMax, r.yMin + d);
            d -= r.height;
            if (d < r.width) return new Vector2(r.xMax - d, r.yMax);
            d -= r.width;
            return new Vector2(r.xMin, r.yMax - Mathf.Min(d, r.height));
        }
    }

    /// <summary>Столбики по дням: доход вверх (зелёным), траты вниз (красным); подписи дат под осью.</summary>
    public sealed class EarningsChart : VisualElement
    {
        readonly List<(string label, int income, int spent)> _days = new List<(string, int, int)>();
        readonly VisualElement _labels;

        public EarningsChart(float height)
        {
            pickingMode = PickingMode.Ignore;
            style.height = height;
            style.flexGrow = 1f;
            generateVisualContent += Draw;
            _labels = Ui.Row();
            _labels.style.position = Position.Absolute;
            _labels.style.left = 0f;
            _labels.style.right = 0f;
            _labels.style.bottom = 0f;
            Add(_labels);
        }

        public void SetDays(IEnumerable<(string label, int income, int spent)> days)
        {
            _days.Clear();
            _days.AddRange(days);
            _labels.Clear();
            for (int i = 0; i < _days.Count; i++)
            {
                var label = Ui.Text(i % 2 == 0 || _days.Count <= 8 ? _days[i].label : "", 10f, Theme.TextDim);
                label.style.flexGrow = 1f;
                label.style.flexBasis = 0f;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                _labels.Add(label);
            }
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var size = layout.size;
            if (_days.Count == 0 || float.IsNaN(size.x) || size.x < 20f) return;
            var p = context.painter2D;
            float bottomPad = 16f;
            float top = 6f;
            float max = 1f;
            foreach (var day in _days) max = Mathf.Max(max, Mathf.Max(day.income, day.spent));
            float plotHeight = size.y - bottomPad - top;
            float axis = top + plotHeight * 0.72f;    // доходы — выше оси, траты — ниже
            float upScale = plotHeight * 0.70f / max;
            float downScale = plotHeight * 0.26f / max;
            float slot = size.x / _days.Count;
            float bar = Mathf.Min(22f, slot * 0.6f);

            p.strokeColor = Theme.Line;
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, axis));
            p.LineTo(new Vector2(size.x, axis));
            p.Stroke();

            for (int i = 0; i < _days.Count; i++)
            {
                float cx = slot * (i + 0.5f);
                var day = _days[i];
                if (day.income > 0)
                {
                    p.fillColor = GameColors.Success;
                    MenuButton.RoundRect(p, new Rect(cx - bar / 2f, axis - day.income * upScale, bar, day.income * upScale), 3f);
                    p.Fill();
                }
                if (day.spent > 0)
                {
                    p.fillColor = new Color(GameColors.Danger.r, GameColors.Danger.g, GameColors.Danger.b, 0.8f);
                    MenuButton.RoundRect(p, new Rect(cx - bar / 2f, axis, bar, day.spent * downScale), 3f);
                    p.Fill();
                }
            }
        }
    }
}
