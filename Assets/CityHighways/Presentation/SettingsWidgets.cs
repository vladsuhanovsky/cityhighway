using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Переключатель-«тумблер»: дорожка и бегунок, который плавно едет; вкл — зелёный.</summary>
    public sealed class SwitchToggle : VisualElement
    {
        readonly VisualElement _knob;
        readonly Action<bool> _changed;
        bool _value;

        public SwitchToggle(bool value, Action<bool> changed)
        {
            _changed = changed;
            pickingMode = PickingMode.Position;
            focusable = true;
            style.width = 52f;
            style.height = 28f;
            style.flexShrink = 0f;
            Ui.Radius(this, 14f);
            Ui.Transitions(this, 0.2f, "background-color");
            _knob = Ui.Box();
            _knob.pickingMode = PickingMode.Ignore;
            _knob.style.position = Position.Absolute;
            _knob.style.top = 3f;
            _knob.style.width = 22f;
            _knob.style.height = 22f;
            _knob.style.backgroundColor = Color.white;
            Ui.Radius(_knob, 11f);
            Ui.Transitions(_knob, 0.2f, "left");
            Add(_knob);
            Set(value, false);
            RegisterCallback<ClickEvent>(_ => Toggle());
            RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                Toggle();
                evt.StopPropagation();
            });
            RegisterCallback<FocusInEvent>(_ => Ui.Border(this, 2f, Theme.Accent));
            RegisterCallback<FocusOutEvent>(_ => Ui.Border(this, 0f, Color.clear));
            RegisterCallback<PointerEnterEvent>(_ => style.opacity = 0.9f);
            RegisterCallback<PointerLeaveEvent>(_ => style.opacity = 1f);
        }

        void Toggle()
        {
            Set(!_value, true);
            Ui.Changed?.Invoke();
        }

        public bool Value => _value;

        public void Set(bool value, bool notify)
        {
            _value = value;
            style.backgroundColor = value ? GameColors.Success : new Color(1f, 1f, 1f, 0.18f);
            _knob.style.left = value ? 27f : 3f;
            if (notify) _changed?.Invoke(value);
        }
    }

    /// <summary>
    /// Слайдер, нарисованный элементами (стандартный Slider без темы не виден): дорожка, заливка, бегунок.
    /// Тяните или кликайте; changed — на каждом шаге, released — когда отпустили.
    /// </summary>
    public sealed class ValueSlider : VisualElement
    {
        readonly VisualElement _fill;
        readonly VisualElement _knob;
        readonly float _step;
        readonly Action<float> _changed;
        readonly Action<float> _released;
        float _value;
        bool _dragging;

        public ValueSlider(float width, float value, float step, Action<float> changed, Action<float> released = null)
        {
            _step = step;
            _changed = changed;
            _released = released;
            pickingMode = PickingMode.Position;
            style.width = width;
            style.height = 28f;
            style.flexShrink = 0f;
            style.justifyContent = Justify.Center;
            var track = Ui.Box();
            track.pickingMode = PickingMode.Ignore;
            track.style.height = 8f;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            Ui.Radius(track, 4f);
            _fill = Ui.Box();
            _fill.pickingMode = PickingMode.Ignore;
            _fill.style.height = Length.Percent(100f);
            _fill.style.backgroundColor = GameColors.Accent;
            Ui.Radius(_fill, 4f);
            track.Add(_fill);
            Add(track);
            _knob = Ui.Box();
            _knob.pickingMode = PickingMode.Ignore;
            _knob.style.position = Position.Absolute;
            _knob.style.top = 3f;
            _knob.style.width = 22f;
            _knob.style.height = 22f;
            _knob.style.backgroundColor = Color.white;
            Ui.Radius(_knob, 11f);
            Ui.Border(_knob, 3f, GameColors.Accent);
            Add(_knob);
            SetValue(value, false);

            RegisterCallback<PointerDownEvent>(e =>
            {
                _dragging = true;
                this.CapturePointer(e.pointerId);
                FromPointer(e.localPosition.x);
                _knob.style.scale = new Scale(new Vector3(1.15f, 1.15f, 1f));
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_dragging) FromPointer(e.localPosition.x);
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (!_dragging) return;
                _dragging = false;
                if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
                _knob.style.scale = new Scale(Vector3.one);
                _released?.Invoke(_value);
            });
            RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public float Value => _value;

        void FromPointer(float x)
        {
            float width = resolvedStyle.width;
            if (float.IsNaN(width) || width <= 22f) return;
            float t = Mathf.Clamp01((x - 11f) / (width - 22f));
            SetValue(t, true);
        }

        public void SetValue(float value, bool notify)
        {
            float stepped = _step > 0f ? Mathf.Round(value / _step) * _step : value;
            stepped = Mathf.Clamp01(stepped);
            bool changed = !Mathf.Approximately(stepped, _value);
            _value = stepped;
            Place();
            if (notify && changed)
            {
                _changed?.Invoke(_value);
                Ui.Changed?.Invoke();
            }
        }

        void Place()
        {
            _fill.style.width = Length.Percent(_value * 100f);
            float width = resolvedStyle.width;
            if (float.IsNaN(width)) width = style.width.value.value;
            _knob.style.left = (width - 22f) * _value;
        }
    }

    /// <summary>Сегментный выбор: ряд кнопок, активная — оранжевая.</summary>
    public sealed class Segmented : VisualElement
    {
        readonly List<UnityEngine.UIElements.Button> _buttons = new List<UnityEngine.UIElements.Button>();
        readonly Action<int> _changed;
        int _index;

        public Segmented(IList<string> options, int index, Action<int> changed, float fontSize = 13f)
        {
            _changed = changed;
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            Ui.Radius(this, 10f);
            Ui.Padding(this, 3f, 3f);
            for (int i = 0; i < options.Count; i++)
            {
                int value = i;
                var button = Ui.Button(options[i], () => Select(value, true), ButtonKind.Ghost, fontSize);
                Ui.Border(button, 0f, Color.clear);
                button.style.minHeight = 30f;
                Ui.Padding(button, 4f, 12f);
                _buttons.Add(button);
                Add(button);
            }
            Select(index, false);
        }

        public void Select(int index, bool notify)
        {
            _index = Mathf.Clamp(index, 0, _buttons.Count - 1);
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool active = i == _index;
                _buttons[i].style.backgroundColor = active ? GameColors.Accent : Color.clear;
                _buttons[i].style.color = active ? GameColors.AccentText : Theme.Text;
            }
            if (notify)
            {
                Ui.Changed?.Invoke();
                _changed?.Invoke(_index);
            }
        }
    }

    /// <summary>
    /// Живой предпросмотр настроек: небо и город в палитре меню (день или ночь), под ним — поле партии
    /// (наклон 2.5D или плоское), машинка на дороге (замирает при «меньше анимаций»), мини-карта в углу,
    /// плашка интерфейса в выбранном масштабе.
    /// </summary>
    public sealed class SettingsPreview : VisualElement
    {
        public bool Tilted = true;
        public bool Minimap = true;
        public bool ReducedMotion;
        public int Palette;
        public int Light;
        public float UiScale = 1f;
        public int Quality = -1;
        float _time;

        public SettingsPreview(float width, float height)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.overflow = Overflow.Hidden;
            Ui.Radius(this, 12f);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(() =>
            {
                if (!ReducedMotion) _time += 0.033f;
                MarkDirtyRepaint();
            }).Every(33);
        }

        public void Refresh() => MarkDirtyRepaint();

        bool Night => Light == 2 || (Light == 0 && MenuBackground.IsNightNow());

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 40f) return;
            var p = context.painter2D;
            var (top, bottom, window) = MenuBackground.PreviewColors(Palette, Night);

            // Небо полосами (градиент) — верхние 40%.
            float skyHeight = r.height * 0.40f;
            const int bands = 10;
            for (int i = 0; i < bands; i++)
            {
                p.fillColor = Color.Lerp(top, bottom, i / (float)(bands - 1));
                Rect(p, 0f, skyHeight * i / bands, r.width, skyHeight / bands + 1f);
            }
            // Солнце или луна.
            p.fillColor = Night ? new Color(0.95f, 0.95f, 0.85f) : new Color(1f, 0.92f, 0.60f);
            p.BeginPath();
            p.Arc(new Vector2(r.width * 0.78f, skyHeight * 0.35f), 12f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
            // Силуэт города с окнами.
            var silhouette = Night ? new Color(0.05f, 0.06f, 0.10f) : new Color(0.20f, 0.26f, 0.36f);
            float x = 0f;
            int seed = 3;
            while (x < r.width)
            {
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                float w = 18f + seed % 22;
                float h = skyHeight * (0.30f + (seed % 50) / 100f);
                p.fillColor = silhouette;
                Rect(p, x, skyHeight - h, w - 2f, h);
                p.fillColor = new Color(window.r, window.g, window.b, Night ? 0.85f : 0.35f);
                for (float wy = skyHeight - h + 5f; wy < skyHeight - 4f; wy += 7f)
                    for (float wx = x + 3f; wx < x + w - 6f; wx += 6f)
                        if (((int)(wx * 7 + wy * 3) + seed) % 3 != 0) Rect(p, wx, wy, 2.5f, 3f);
                x += w;
            }

            // Поле партии: плоское или с наклоном (трапеция).
            float boardTop = skyHeight + 8f, boardBottom = r.height - 8f;
            float inset = Tilted ? r.width * 0.16f : 10f;
            Vector2 tl = new Vector2(inset, boardTop), tr = new Vector2(r.width - inset, boardTop);
            Vector2 bl = new Vector2(10f, boardBottom), br = new Vector2(r.width - 10f, boardBottom);
            p.fillColor = new Color(0.16f, 0.22f, 0.20f);
            p.BeginPath();
            p.MoveTo(tl);
            p.LineTo(tr);
            p.LineTo(br);
            p.LineTo(bl);
            p.ClosePath();
            p.Fill();
            Vector2 Board(float u, float v) => Vector2.Lerp(Vector2.Lerp(tl, tr, u), Vector2.Lerp(bl, br, u), v);
            // Сетка — чтобы наклон был заметен.
            p.strokeColor = new Color(1f, 1f, 1f, 0.07f);
            p.lineWidth = 1f;
            for (int i = 1; i < 6; i++)
            {
                Line(p, Board(i / 6f, 0f), Board(i / 6f, 1f));
                Line(p, Board(0f, i / 6f), Board(1f, i / 6f));
            }
            // Дороги и места.
            var spots = new[] { (0.12f, 0.75f), (0.35f, 0.35f), (0.62f, 0.62f), (0.88f, 0.28f), (0.55f, 0.15f) };
            var roadColors = new[] { new Color(0.85f, 0.35f, 0.30f), new Color(0.30f, 0.60f, 0.95f), new Color(0.95f, 0.75f, 0.25f), new Color(0.45f, 0.80f, 0.45f) };
            p.lineCap = LineCap.Round;
            for (int i = 0; i < 4; i++)
            {
                p.strokeColor = roadColors[i];
                p.lineWidth = Tilted ? Mathf.Lerp(4f, 7f, (spots[i].Item2 + spots[i + 1].Item2) / 2f) : 6f;
                Line(p, Board(spots[i].Item1, spots[i].Item2), Board(spots[i + 1].Item1, spots[i + 1].Item2));
            }
            foreach (var (u, v) in spots)
            {
                p.fillColor = new Color(0.92f, 0.93f, 0.95f);
                p.BeginPath();
                p.Arc(Board(u, v), Tilted ? Mathf.Lerp(3.5f, 6f, v) : 5f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
            // Машинка едет по второй дороге.
            float t = ReducedMotion ? 0.5f : Mathf.PingPong(_time * 0.4f, 1f);
            var car = Vector2.Lerp(Board(spots[1].Item1, spots[1].Item2), Board(spots[2].Item1, spots[2].Item2), t);
            p.fillColor = Color.white;
            p.BeginPath();
            p.Arc(car, 4f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
            p.fillColor = new Color(1f, 0.85f, 0.4f, 0.5f);
            p.BeginPath();
            p.Arc(car, 8f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();

            // Мини-карта в углу.
            if (Minimap)
            {
                float mw = 54f, mh = 36f;
                var corner = new Vector2(r.width - mw - 12f, r.height - mh - 12f);
                p.fillColor = new Color(0.05f, 0.07f, 0.10f, 0.85f);
                Rect(p, corner.x, corner.y, mw, mh);
                p.strokeColor = new Color(1f, 1f, 1f, 0.5f);
                p.lineWidth = 1f;
                p.BeginPath();
                p.MoveTo(corner);
                p.LineTo(corner + new Vector2(mw, 0f));
                p.LineTo(corner + new Vector2(mw, mh));
                p.LineTo(corner + new Vector2(0f, mh));
                p.ClosePath();
                p.Stroke();
                p.strokeColor = new Color(0.95f, 0.75f, 0.25f);
                p.lineWidth = 1.5f;
                Line(p, corner + new Vector2(8f, 26f), corner + new Vector2(22f, 12f));
                Line(p, corner + new Vector2(22f, 12f), corner + new Vector2(36f, 22f));
                Line(p, corner + new Vector2(36f, 22f), corner + new Vector2(48f, 8f));
            }

            // Плашка интерфейса в выбранном масштабе.
            float s = Mathf.Clamp(UiScale, 0.8f, 1.4f);
            p.fillColor = new Color(0.17f, 0.24f, 0.31f, 0.92f);
            Rect(p, 10f, boardTop + 6f, 92f * s, 18f * s);
            p.fillColor = GameColors.Accent;
            Rect(p, 14f * s, boardTop + 10f * s, 10f * s, 10f * s);
            p.fillColor = new Color(1f, 1f, 1f, 0.75f);
            Rect(p, 30f * s, boardTop + 12f * s, 56f * s, 3f * s);
            Rect(p, 30f * s, boardTop + 17f * s, 38f * s, 3f * s);
        }

        static void Rect(Painter2D p, float x, float y, float w, float h)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
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
