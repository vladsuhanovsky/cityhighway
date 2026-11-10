using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RailGame.Presentation.App
{
    /// <summary>Цвета интерфейса (карта и карты вагонов — в Ui.Palette).</summary>
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
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
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
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
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
