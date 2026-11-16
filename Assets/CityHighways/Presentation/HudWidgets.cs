using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Горизонтальная полоска-прогресс: заливка цветом на тёмной дорожке, ширина меняется плавно.</summary>
    public sealed class ProgressBar : VisualElement
    {
        readonly VisualElement _fill;

        public ProgressBar(float width, float height, Color color)
        {
            pickingMode = PickingMode.Ignore;
            style.width = width;
            style.height = height;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            Ui.Radius(this, height / 2f);
            style.overflow = Overflow.Hidden;
            _fill = Ui.Box();
            _fill.style.height = Length.Percent(100f);
            _fill.style.backgroundColor = color;
            Ui.Radius(_fill, height / 2f);
            Ui.Transitions(_fill, "width", "background-color");
            Add(_fill);
            Set(0f);
        }

        public void Set(float fraction, Color? color = null)
        {
            _fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            if (color.HasValue) _fill.style.backgroundColor = color.Value;
        }
    }

    /// <summary>
    /// Ресурс в верхней панели: маленькая карта-иллюстрация, полоска из делений (одно деление — одна карта)
    /// и число моноширинным шрифтом. Деления видны с одного взгляда, число — для точности.
    /// </summary>
    public sealed class ResourceChip : VisualElement
    {
        public const int Pips = 8;
        readonly VisualElement[] _pips = new VisualElement[Pips];
        readonly Label _count;
        readonly Color _color;

        public ResourceChip(Resource resource)
        {
            pickingMode = PickingMode.Position;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginRight = 10f;
            Ui.Padding(this, 3f, 6f);
            Ui.Radius(this, 8f);
            style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);
            _color = Palette.Card(resource);
            tooltip = ResourceInfo.RussianName(resource);

            var art = Ui.Box();
            art.style.width = 20f;
            art.style.height = 28f;
            art.style.marginRight = 6f;
            Ui.Radius(art, 3f);
            var texture = CardArt.Get(resource);
            if (texture != null) art.style.backgroundImage = new StyleBackground(texture);
            else art.style.backgroundColor = _color;
            Ui.Border(art, 1f, new Color(0f, 0f, 0f, 0.4f));
            Add(art);

            var column = Ui.Column();
            var pips = Ui.Row();
            for (int i = 0; i < Pips; i++)
            {
                var pip = Ui.Box();
                pip.style.width = 5f;
                pip.style.height = 12f;
                pip.style.marginRight = 1.5f;
                Ui.Radius(pip, 1.5f);
                Ui.Transitions(pip, "background-color");
                _pips[i] = pip;
                pips.Add(pip);
            }
            column.Add(pips);
            Add(column);

            _count = Ui.Number("0", 16f, Theme.Text, true);
            _count.style.marginLeft = 6f;
            _count.style.minWidth = 22f;
            Add(_count);
            Ui.Transitions(this, "scale");
        }

        /// <summary>Сколько карт; hidden — рука скрыта до передачи хода.</summary>
        public void Set(int count, bool hidden)
        {
            for (int i = 0; i < Pips; i++)
                _pips[i].style.backgroundColor = !hidden && i < count ? _color : new Color(1f, 1f, 1f, 0.10f);
            _count.text = hidden ? "?" : count > Pips ? $"{count}" : count.ToString();
            _count.style.color = hidden || count == 0 ? Theme.TextDim : Theme.Text;
            style.opacity = !hidden && count == 0 ? 0.65f : 1f;
        }

        /// <summary>Короткий «пульс», когда в руку прилетела карта.</summary>
        public void Pulse()
        {
            style.scale = new Scale(new Vector3(1.12f, 1.12f, 1f));
            schedule.Execute(() => style.scale = new Scale(Vector3.one)).ExecuteLater(160);
        }
    }

    /// <summary>Кнопка-значок, нарисованная кодом: «бургер» (три полоски) или шестерёнка, с подписью.</summary>
    public sealed class IconButton : Button
    {
        public new enum Glyph { Menu, Gear, Close, Plus, Minus, Fit, Left, Right }

        readonly Glyph _glyph;
        readonly VisualElement _icon;
        Color _ink = Theme.Text;

        public IconButton(Glyph glyph, string caption, Action onClick, float iconSize = 16f) : base(() =>
        {
            Ui.ButtonClicked?.Invoke();
            onClick?.Invoke();
        })
        {
            _glyph = glyph;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            style.minHeight = 34f;
            Ui.Radius(this, 8f);
            Ui.Border(this, 0f, Color.clear);
            Ui.Margin(this, 0f);
            Ui.Padding(this, 6f, string.IsNullOrEmpty(caption) ? 8f : 12f);
            text = "";

            _icon = Ui.Box();
            _icon.style.width = iconSize;
            _icon.style.height = iconSize;
            _icon.generateVisualContent += Draw;
            Add(_icon);
            if (!string.IsNullOrEmpty(caption))
            {
                var label = Ui.Text(caption, 14f, Theme.Text, true);
                label.style.marginLeft = 8f;
                Add(label);
            }

            Ui.Transitions(this, "background-color", "translate");
            RegisterCallback<PointerEnterEvent>(_ =>
            {
                style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
                style.translate = new Translate(new Length(0f), new Length(-1.5f));
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
                style.translate = new Translate(new Length(0f), new Length(0f));
            });
        }

        public void SetInk(Color ink)
        {
            _ink = ink;
            _icon.MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = _icon.contentRect;
            if (float.IsNaN(r.width) || r.width < 2f) return;
            var p = context.painter2D;
            p.strokeColor = _ink;
            p.fillColor = _ink;
            p.lineWidth = Mathf.Max(1.6f, r.width * 0.12f);
            p.lineCap = LineCap.Round;
            float w = r.width, h = r.height;
            switch (_glyph)
            {
                case Glyph.Menu:
                    for (int i = 0; i < 3; i++) Line(p, new Vector2(w * 0.12f, h * (0.22f + 0.28f * i)), new Vector2(w * 0.88f, h * (0.22f + 0.28f * i)));
                    break;
                case Glyph.Gear:
                {
                    var c = new Vector2(w / 2f, h / 2f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(p, c + d * w * 0.30f, c + d * w * 0.46f);
                    }
                    p.BeginPath();
                    p.Arc(c, w * 0.30f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Stroke();
                    p.BeginPath();
                    p.Arc(c, w * 0.11f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                    break;
                }
                case Glyph.Close:
                    Line(p, new Vector2(w * 0.2f, h * 0.2f), new Vector2(w * 0.8f, h * 0.8f));
                    Line(p, new Vector2(w * 0.8f, h * 0.2f), new Vector2(w * 0.2f, h * 0.8f));
                    break;
                case Glyph.Plus:
                    Line(p, new Vector2(w * 0.5f, h * 0.18f), new Vector2(w * 0.5f, h * 0.82f));
                    Line(p, new Vector2(w * 0.18f, h * 0.5f), new Vector2(w * 0.82f, h * 0.5f));
                    break;
                case Glyph.Minus:
                    Line(p, new Vector2(w * 0.18f, h * 0.5f), new Vector2(w * 0.82f, h * 0.5f));
                    break;
                case Glyph.Left:
                    Line(p, new Vector2(w * 0.65f, h * 0.15f), new Vector2(w * 0.3f, h * 0.5f));
                    Line(p, new Vector2(w * 0.3f, h * 0.5f), new Vector2(w * 0.65f, h * 0.85f));
                    break;
                case Glyph.Right:
                    Line(p, new Vector2(w * 0.35f, h * 0.15f), new Vector2(w * 0.7f, h * 0.5f));
                    Line(p, new Vector2(w * 0.7f, h * 0.5f), new Vector2(w * 0.35f, h * 0.85f));
                    break;
                default: // Fit — рамка «показать всё»
                    Line(p, new Vector2(w * 0.15f, h * 0.38f), new Vector2(w * 0.15f, h * 0.15f));
                    Line(p, new Vector2(w * 0.15f, h * 0.15f), new Vector2(w * 0.38f, h * 0.15f));
                    Line(p, new Vector2(w * 0.62f, h * 0.15f), new Vector2(w * 0.85f, h * 0.15f));
                    Line(p, new Vector2(w * 0.85f, h * 0.15f), new Vector2(w * 0.85f, h * 0.38f));
                    Line(p, new Vector2(w * 0.85f, h * 0.62f), new Vector2(w * 0.85f, h * 0.85f));
                    Line(p, new Vector2(w * 0.85f, h * 0.85f), new Vector2(w * 0.62f, h * 0.85f));
                    Line(p, new Vector2(w * 0.38f, h * 0.85f), new Vector2(w * 0.15f, h * 0.85f));
                    Line(p, new Vector2(w * 0.15f, h * 0.85f), new Vector2(w * 0.15f, h * 0.62f));
                    break;
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

    /// <summary>
    /// Карусель карточек со swipe-навигацией: тянуть мышью (или пальцем) влево-вправо, колесо, стрелки.
    /// Отпущенная лента доезжает до ближайшей карточки (с учётом скорости броска). Клик по карточке
    /// после перетаскивания не считается — см. WasDragged.
    /// </summary>
    public sealed class CardCarousel : VisualElement
    {
        readonly VisualElement _viewport;
        readonly VisualElement _strip;
        readonly IconButton _prev, _next;
        readonly VisualElement _dots;
        readonly float _step;      // ширина карточки с промежутком
        float _offset;             // текущий сдвиг ленты
        float _maxOffset;
        int _count;

        bool _tracking, _dragging;
        int _pointer = -1;
        float _downX, _downOffset, _lastX, _lastTime, _velocity;

        /// <summary>Последнее нажатие превратилось в перетаскивание — клик по карточке игнорировать.</summary>
        public bool WasDragged { get; private set; }
        /// <summary>Лента встала на новую карточку (для звука).</summary>
        public event Action Snapped;

        public CardCarousel(float cardWidth, float gap)
        {
            _step = cardWidth + gap;
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Ignore;

            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            Add(row);

            _prev = new IconButton(IconButton.Glyph.Left, "", () => Step(-1), 12f);
            _prev.style.marginRight = 8f;
            row.Add(_prev);

            _viewport = new VisualElement();
            _viewport.style.flexGrow = 1f;
            _viewport.style.flexShrink = 1f;
            _viewport.style.overflow = Overflow.Hidden;
            _viewport.pickingMode = PickingMode.Position;
            row.Add(_viewport);

            _strip = Ui.Row();
            _strip.style.alignItems = Align.Stretch;
            _strip.style.paddingTop = 6f;
            _strip.style.paddingBottom = 10f;
            _strip.style.paddingLeft = 4f;
            _viewport.Add(_strip);

            _next = new IconButton(IconButton.Glyph.Right, "", () => Step(1), 12f);
            _next.style.marginLeft = 8f;
            row.Add(_next);

            _dots = Ui.Row();
            _dots.style.justifyContent = Justify.Center;
            _dots.style.marginTop = 2f;
            Add(_dots);

            _viewport.RegisterCallback<GeometryChangedEvent>(_ => Recalculate());
            _strip.RegisterCallback<GeometryChangedEvent>(_ => Recalculate());
            // Нажатие ловим на «спуске» события, чтобы заметить перетаскивание раньше карточек.
            _viewport.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<WheelEvent>(e =>
            {
                float delta = Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y) ? e.delta.x : e.delta.y;
                if (Mathf.Abs(delta) < 0.01f) return;
                Step(delta > 0f ? 1 : -1);
                e.StopPropagation();
            });
        }

        /// <summary>Заменить карточки; положение ленты сохраняется (в пределах новой длины).</summary>
        public void SetCards(IEnumerable<VisualElement> cards)
        {
            _strip.Clear();
            _count = 0;
            foreach (var card in cards)
            {
                card.style.flexShrink = 0f;
                _strip.Add(card);
                _count++;
            }
            for (int i = 0; i < _strip.childCount; i++)
                _strip[i].style.marginRight = i < _strip.childCount - 1 ? Mathf.Max(0f, _step - CardWidthOf(_strip[i])) : 0f;
            Recalculate();
        }

        float CardWidthOf(VisualElement card)
        {
            var w = card.style.width.value.value;
            return w > 0f ? w : _step;
        }

        void Recalculate()
        {
            float viewport = _viewport.resolvedStyle.width;
            float content = _count * _step;
            _maxOffset = float.IsNaN(viewport) ? 0f : Mathf.Max(0f, content - viewport + 8f);
            Apply(Mathf.Clamp(_offset, 0f, _maxOffset), false);
            bool scrollable = _maxOffset > 1f;
            Ui.SetEnabled(_prev, scrollable && _offset > 1f);
            Ui.SetEnabled(_next, scrollable && _offset < _maxOffset - 1f);
            Ui.Show(_prev, scrollable);
            Ui.Show(_next, scrollable);
            UpdateDots();
        }

        int Positions => _maxOffset <= 1f ? 1 : Mathf.CeilToInt(_maxOffset / _step) + 1;
        int Current => Mathf.Clamp(Mathf.RoundToInt(_offset / _step), 0, Positions - 1);

        void UpdateDots()
        {
            int positions = Positions;
            if (_dots.childCount != positions)
            {
                _dots.Clear();
                for (int i = 0; i < positions; i++)
                {
                    var dot = Ui.Box();
                    dot.style.width = 6f;
                    dot.style.height = 6f;
                    dot.style.marginLeft = 3f;
                    dot.style.marginRight = 3f;
                    Ui.Radius(dot, 3f);
                    Ui.Transitions(dot, "background-color", "width");
                    _dots.Add(dot);
                }
            }
            Ui.Show(_dots, positions > 1);
            int current = Current;
            for (int i = 0; i < _dots.childCount; i++)
            {
                bool on = i == current;
                _dots[i].style.backgroundColor = on ? Theme.Accent : new Color(1f, 1f, 1f, 0.25f);
                _dots[i].style.width = on ? 16f : 6f;
            }
        }

        /// <summary>Сдвинуть ленту на n карточек.</summary>
        public void Step(int n) => SnapTo(Current + n);

        void SnapTo(int index)
        {
            int before = Current;
            float target = Mathf.Clamp(index * _step, 0f, _maxOffset);
            Apply(target, true);
            Recalculate();
            if (Current != before) Snapped?.Invoke();
        }

        void Apply(float offset, bool animate)
        {
            _offset = offset;
            Ui.Transitions(_strip, animate ? Theme.TransitionSeconds : 0f, "translate");
            _strip.style.translate = new Translate(new Length(-offset), new Length(0f));
        }

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            _tracking = true;
            _dragging = false;
            WasDragged = false;
            _pointer = e.pointerId;
            _downX = e.position.x;
            _downOffset = _offset;
            _lastX = e.position.x;
            _lastTime = Time.realtimeSinceStartup;
            _velocity = 0f;
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!_tracking || e.pointerId != _pointer) return;
            float dx = e.position.x - _downX;
            if (!_dragging && Mathf.Abs(dx) > 6f && _maxOffset > 1f)
            {
                _dragging = true;
                WasDragged = true;
                _viewport.CapturePointer(_pointer);
            }
            if (!_dragging) return;
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0.001f, now - _lastTime);
            _velocity = Mathf.Lerp(_velocity, (e.position.x - _lastX) / dt, 0.5f);
            _lastX = e.position.x;
            _lastTime = now;
            // За краями лента тянется с сопротивлением.
            float raw = _downOffset - dx;
            if (raw < 0f) raw *= 0.35f;
            else if (raw > _maxOffset) raw = _maxOffset + (raw - _maxOffset) * 0.35f;
            Apply(raw, false);
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (!_tracking || e.pointerId != _pointer) return;
            _tracking = false;
            if (!_dragging) return;
            _dragging = false;
            if (_viewport.HasPointerCapture(_pointer)) _viewport.ReleasePointer(_pointer);
            // Бросок: учитываем скорость — быстрый swipe перелистывает дальше.
            float projected = _offset - _velocity * 0.18f;
            SnapTo(Mathf.RoundToInt(projected / _step));
            e.StopPropagation();
        }
    }

    /// <summary>
    /// Мини-карта в углу поля: районы, река, МКАД, дороги цветами владельцев и рамка того, что видно на поле.
    /// Клик или перетаскивание — перейти к месту; кнопки — приблизить, отдалить, показать всё.
    /// </summary>
    public sealed class MiniMapView : VisualElement
    {
        readonly MapView _map;
        readonly VisualElement _canvas;
        readonly Vector2[] _corners = new Vector2[4];
        Game _game;
        bool _dragging;

        public MiniMapView(MapView map, float width, float height)
        {
            _map = map;
            style.width = width;
            style.flexDirection = FlexDirection.Column;
            style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(this, 10f);
            Ui.Border(this, 1f, Theme.Line);
            Ui.Padding(this, 6f, 6f);
            Ui.Shadow(this, 10f);

            _canvas = new VisualElement();
            _canvas.style.height = height;
            _canvas.style.backgroundColor = new Color(0.12f, 0.20f, 0.14f);
            Ui.Radius(_canvas, 6f);
            _canvas.generateVisualContent += Draw;
            Add(_canvas);

            var buttons = Ui.Row();
            buttons.style.marginTop = 6f;
            buttons.style.justifyContent = Justify.SpaceBetween;
            var caption = Ui.Text("Мини-карта", 12f, Theme.TextDim, true);
            caption.style.flexGrow = 1f;
            buttons.Add(caption);
            var zoomOut = new IconButton(IconButton.Glyph.Minus, "", () => _map.ZoomBy(1f / 1.4f), 12f);
            zoomOut.tooltip = "Отдалить";
            var zoomIn = new IconButton(IconButton.Glyph.Plus, "", () => _map.ZoomBy(1.4f), 12f);
            zoomIn.tooltip = "Приблизить";
            zoomIn.style.marginLeft = 4f;
            var fit = new IconButton(IconButton.Glyph.Fit, "", _map.ResetView, 12f);
            fit.tooltip = "Показать всю карту";
            fit.style.marginLeft = 4f;
            buttons.Add(zoomOut);
            buttons.Add(zoomIn);
            buttons.Add(fit);
            Add(buttons);

            _canvas.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _dragging = true;
                _canvas.CapturePointer(e.pointerId);
                Jump(e.localPosition);
            });
            _canvas.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_dragging) Jump(e.localPosition);
            });
            _canvas.RegisterCallback<PointerUpEvent>(e =>
            {
                _dragging = false;
                if (_canvas.HasPointerCapture(e.pointerId)) _canvas.ReleasePointer(e.pointerId);
            });
            map.ViewChanged += () => _canvas.MarkDirtyRepaint();
        }

        public void SetGame(Game game)
        {
            _game = game;
            Refresh();
        }

        public void Refresh() => _canvas.MarkDirtyRepaint();

        Rect Area
        {
            get
            {
                var r = _canvas.contentRect;
                const float pad = 6f;
                return new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad);
            }
        }

        Vector2 ToMini(float x, float y)
        {
            var a = Area;
            return new Vector2(a.x + x * a.width, a.y + y * a.height);
        }

        void Jump(Vector2 local)
        {
            var a = Area;
            if (a.width < 2f) return;
            _map.CenterOn(new Vector2((local.x - a.x) / a.width, (local.y - a.y) / a.height));
        }

        void Draw(MeshGenerationContext context)
        {
            if (_game == null || float.IsNaN(_canvas.contentRect.width) || _canvas.contentRect.width < 20f) return;
            var p = context.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            var map = _game.Map;

            // МКАД и река.
            p.strokeColor = new Color(1f, 1f, 1f, 0.18f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2f / 64f;
                var point = ToMini(0.5f + Mathf.Cos(a) * 0.40f, 0.5f + Mathf.Sin(a) * 0.49f);
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.Stroke();
            if (map.River.Count > 1)
            {
                p.strokeColor = new Color(0.30f, 0.55f, 0.85f, 0.9f);
                p.lineWidth = 3f;
                p.BeginPath();
                p.MoveTo(ToMini(map.River[0].X, map.River[0].Y));
                for (int i = 1; i < map.River.Count; i++) p.LineTo(ToMini(map.River[i].X, map.River[i].Y));
                p.Stroke();
            }

            // Дороги: свободные — тонкие серые, построенные — цветом компании.
            foreach (var road in map.Roads)
            {
                if (!_game.IsRoadOpen(road.Id)) continue;
                var a = map.GetLocation(road.A);
                var b = map.GetLocation(road.B);
                if (a == null || b == null) continue;
                var owner = _game.GetRoadOwner(road.Id);
                p.strokeColor = owner.HasValue ? Palette.PlayerColor(owner.Value) : new Color(1f, 1f, 1f, 0.22f);
                p.lineWidth = owner.HasValue ? 2.4f : 1f;
                p.BeginPath();
                p.MoveTo(ToMini(a.X, a.Y));
                p.LineTo(ToMini(b.X, b.Y));
                p.Stroke();
            }

            // Места — точки цвета района.
            var districts = new Dictionary<string, int>();
            for (int i = 0; i < map.Districts.Count; i++) districts[map.Districts[i].Id] = i;
            foreach (var location in map.Locations)
            {
                if (!_game.IsLocationOpen(location.Id)) continue;
                int index = districts.TryGetValue(location.DistrictId, out var d) ? d : 0;
                p.fillColor = Palette.DistrictColor(index);
                p.BeginPath();
                p.Arc(ToMini(location.X, location.Y), 2f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            // Рамка обзора: что сейчас видно на поле (с наклоном — трапеция).
            if (_map.Zoom > 1.01f)
            {
                _map.ViewportCorners(_corners);
                p.fillColor = new Color(1f, 1f, 1f, 0.08f);
                p.strokeColor = Theme.Accent;
                p.lineWidth = 1.6f;
                p.BeginPath();
                for (int i = 0; i < 4; i++)
                {
                    var c = new Vector2(Mathf.Clamp(_corners[i].x, -0.05f, 1.05f), Mathf.Clamp(_corners[i].y, -0.05f, 1.05f));
                    var point = ToMini(c.x, c.y);
                    if (i == 0) p.MoveTo(point);
                    else p.LineTo(point);
                }
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }
    }
}
