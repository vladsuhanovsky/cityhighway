using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Цвета интерфейса (карта и карты ресурсов — в Palette).</summary>
    public static class Theme
    {
        public static readonly Color Background = new Color(0.10f, 0.11f, 0.14f);
        public static readonly Color Panel = new Color(0.15f, 0.16f, 0.20f);
        public static readonly Color PanelLight = new Color(0.20f, 0.22f, 0.27f);
        public static readonly Color PanelHover = new Color(0.26f, 0.28f, 0.34f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Text = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color TextDim = new Color(0.62f, 0.65f, 0.72f);
        public static readonly Color Accent = new Color(0.93f, 0.68f, 0.26f);      // латунь
        public static readonly Color AccentHover = new Color(1.00f, 0.77f, 0.36f);
        public static readonly Color AccentText = new Color(0.16f, 0.11f, 0.04f);
        public static readonly Color Danger = new Color(0.92f, 0.38f, 0.34f);
        public static readonly Color Success = new Color(0.42f, 0.82f, 0.47f);
        public static readonly Color Shade = new Color(0.03f, 0.04f, 0.06f, 0.72f);
        public static readonly Color ShadeOpaque = new Color(0.07f, 0.08f, 0.10f, 0.97f);
    }

    public enum ButtonKind
    {
        Primary,
        Secondary,
        Ghost
    }

    /// <summary>Короткие помощники для сборки интерфейса из кода (без UXML и USS).</summary>
    public static class Ui
    {
        /// <summary>Вызывается при нажатии любой кнопки — приложение проигрывает щелчок.</summary>
        public static Action ButtonClicked;

        public static VisualElement Box()
        {
            var e = new VisualElement();
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public static VisualElement Row()
        {
            var e = Box();
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            return e;
        }

        public static VisualElement Column()
        {
            var e = Box();
            e.style.flexDirection = FlexDirection.Column;
            return e;
        }

        public static Label Text(string text, float size = 15f, Color? color = null, bool bold = false)
        {
            var label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = size;
            label.style.color = color ?? Theme.Text;
            Fonts.Apply(label, Fonts.For(size, bold));
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            Margin(label, 0f);
            Padding(label, 0f, 0f);
            return label;
        }

        public static void Radius(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }

        public static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
        }

        public static void Padding(VisualElement e, float vertical, float horizontal)
        {
            e.style.paddingTop = vertical;
            e.style.paddingBottom = vertical;
            e.style.paddingLeft = horizontal;
            e.style.paddingRight = horizontal;
        }

        public static void Margin(VisualElement e, float all)
        {
            e.style.marginTop = all;
            e.style.marginBottom = all;
            e.style.marginLeft = all;
            e.style.marginRight = all;
        }

        /// <summary>Абсолютное позиционирование на всю площадь родителя.</summary>
        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.top = 0f;
            e.style.right = 0f;
            e.style.bottom = 0f;
        }

        public static void Show(VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>
        /// Тонкая вертикальная полоса прокрутки в цветах игры: без стрелок, со скруглённым ползунком,
        /// колесо мыши крутит быстрее стандартного. Стили заданы в коде, поэтому полоса выглядит
        /// одинаково и без темы в PanelSettings (без темы стандартная полоса рисуется криво).
        /// </summary>
        public static void StyleScroller(ScrollView scroll, float wheelStep = 48f)
        {
            scroll.mouseWheelScrollSize = wheelStep;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;

            // Раскладка самой прокрутки: содержимое слева, полоса справа, лишнее обрезается.
            var row = scroll.Q(className: "unity-scroll-view__content-and-vertical-scroll-container");
            if (row != null)
            {
                row.style.flexDirection = FlexDirection.Row;
                row.style.flexGrow = 1f;
                row.style.flexShrink = 1f;
                row.style.minHeight = 0f;
            }
            scroll.contentViewport.style.flexGrow = 1f;
            scroll.contentViewport.style.flexShrink = 1f;
            scroll.contentViewport.style.minWidth = 0f;
            scroll.contentViewport.style.overflow = Overflow.Hidden;
            scroll.contentContainer.style.flexShrink = 0f;

            var scroller = scroll.verticalScroller;
            scroller.style.flexShrink = 0f;
            scroller.style.width = 8f;
            scroller.style.minWidth = 8f;
            scroller.style.marginLeft = 4f;
            scroller.style.backgroundColor = Color.clear;
            NoBorder(scroller);
            Show(scroller.lowButton, false);
            Show(scroller.highButton, false);

            var slider = scroller.slider;
            slider.style.flexGrow = 1f;
            slider.style.marginLeft = 0f;
            slider.style.marginRight = 0f;
            slider.style.marginTop = 0f;
            slider.style.marginBottom = 0f;
            slider.style.minWidth = 0f;
            slider.style.minHeight = 0f;
            foreach (var name in new[] { "unity-base-slider__input", "unity-base-slider__drag-container" })
            {
                var part = slider.Q(className: name);
                if (part == null) continue;
                part.style.flexGrow = 1f;
                part.style.marginLeft = 0f;
                part.style.marginRight = 0f;
                part.style.minWidth = 0f;
                part.style.minHeight = 0f;
            }

            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.position = Position.Absolute;
                tracker.style.left = 0f;
                tracker.style.right = 0f;
                tracker.style.top = 0f;
                tracker.style.bottom = 0f;
                tracker.style.width = StyleKeyword.Auto;
                tracker.style.height = StyleKeyword.Auto;
                tracker.style.marginLeft = 0f;
                tracker.style.marginRight = 0f;
                tracker.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
                NoBorder(tracker);
                Radius(tracker, 4f);
            }

            var border = slider.Q(className: "unity-base-slider__dragger-border");
            if (border != null) Show(border, false);

            var dragger = slider.Q(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                var idle = new Color(Theme.TextDim.r, Theme.TextDim.g, Theme.TextDim.b, 0.35f);
                var hover = new Color(Theme.TextDim.r, Theme.TextDim.g, Theme.TextDim.b, 0.7f);
                dragger.style.position = Position.Absolute;
                dragger.style.left = 0f;
                dragger.style.width = 8f;
                dragger.style.minHeight = 28f;
                dragger.style.marginLeft = 0f;
                dragger.style.marginRight = 0f;
                dragger.style.backgroundColor = idle;
                NoBorder(dragger);
                Radius(dragger, 4f);
                dragger.RegisterCallback<PointerEnterEvent>(_ => dragger.style.backgroundColor = hover);
                dragger.RegisterCallback<PointerLeaveEvent>(_ => dragger.style.backgroundColor = idle);
            }
        }

        static void NoBorder(VisualElement e)
        {
            e.style.borderLeftWidth = 0f;
            e.style.borderRightWidth = 0f;
            e.style.borderTopWidth = 0f;
            e.style.borderBottomWidth = 0f;
        }

        /// <summary>Панель-карточка: тёмный фон, скругление, тонкая рамка.</summary>
        public static VisualElement Card(float padding = 14f)
        {
            var e = Column();
            e.style.backgroundColor = Theme.PanelLight;
            Radius(e, 10f);
            Border(e, 1f, Theme.Line);
            Padding(e, padding, padding);
            return e;
        }

        public static Label SectionTitle(string text)
        {
            var label = Text(text.ToUpperInvariant(), 12f, Theme.TextDim, true);
            label.style.letterSpacing = 1.5f;
            label.style.marginBottom = 8f;
            return label;
        }

        public static UnityEngine.UIElements.Button Button(string text, Action onClick, ButtonKind kind = ButtonKind.Primary, float fontSize = 16f)
        {
            Color normal, hover, textColor;
            switch (kind)
            {
                case ButtonKind.Primary:
                    normal = Theme.Accent;
                    hover = Theme.AccentHover;
                    textColor = Theme.AccentText;
                    break;
                case ButtonKind.Secondary:
                    normal = Theme.PanelHover;
                    hover = new Color(0.33f, 0.35f, 0.42f);
                    textColor = Theme.Text;
                    break;
                default:
                    normal = new Color(1f, 1f, 1f, 0f);
                    hover = new Color(1f, 1f, 1f, 0.08f);
                    textColor = Theme.TextDim;
                    break;
            }

            var button = new UnityEngine.UIElements.Button(() =>
            {
                ButtonClicked?.Invoke();
                onClick?.Invoke();
            });
            button.text = text;
            button.style.fontSize = fontSize;
            Fonts.Apply(button, fontSize >= Fonts.DisplayFrom ? Fonts.Weight.Bold : Fonts.Weight.SemiBold);
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.color = textColor;
            button.style.backgroundColor = normal;
            button.style.minHeight = 38f;
            Radius(button, 8f);
            Border(button, kind == ButtonKind.Ghost ? 1f : 0f, Theme.Line);
            Padding(button, 8f, 16f);
            Margin(button, 0f);

            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (button.enabledSelf) button.style.backgroundColor = hover;
            });
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = normal);
            return button;
        }

        public static void SetEnabled(VisualElement e, bool enabled)
        {
            e.SetEnabled(enabled);
            e.style.opacity = enabled ? 1f : 0.4f;
        }

        public static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        public static float EaseInOutCubic(float x) =>
            x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
