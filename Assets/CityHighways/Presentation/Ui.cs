using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Цвета интерфейса (карта и карты ресурсов — в Palette). Основной — тёмно-синий #2C3E50,
    /// акцент для действий — красный #E74C3C, успех — зелёный #27AE60, затемнение под окнами — #00000080.
    /// Латунь (Gold) — для очков, наград и выделения, где красный читался бы как ошибка.
    /// </summary>
    public static class Theme
    {
        static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static readonly Color Background = Hex(0x1B2631);
        public static readonly Color Panel = Hex(0x2C3E50);
        public static readonly Color PanelLight = Hex(0x34495E);
        public static readonly Color PanelHover = Hex(0x3E5871);
        /// <summary>Плавающие панели поверх карты — тот же основной цвет, чуть прозрачнее.</summary>
        public static readonly Color PanelGlass = Hex(0x2C3E50, 0.94f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Text = Hex(0xECF0F1);
        public static readonly Color TextDim = Hex(0xAAB7B8);
        public static readonly Color Accent = Hex(0xE74C3C);
        public static readonly Color AccentHover = Hex(0xEC7063);
        public static readonly Color AccentText = Color.white;
        public static readonly Color Gold = new Color(0.95f, 0.69f, 0.26f);
        public static readonly Color GoldText = new Color(0.16f, 0.11f, 0.04f);
        public static readonly Color Danger = Hex(0xE74C3C);
        public static readonly Color Success = Hex(0x27AE60);
        public static readonly Color Shade = Hex(0x000000, 0.5f);
        public static readonly Color ShadeOpaque = new Color(0.07f, 0.09f, 0.12f, 0.97f);
        /// <summary>Длительность плавных переходов интерфейса.</summary>
        public const float TransitionSeconds = 0.3f;
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
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Action> BackActions =
            new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Action>();

        /// <summary>Recognize explicit back buttons in both shipped UI languages.</summary>
        public static bool IsBackLabel(string label) =>
            !string.IsNullOrEmpty(label) &&
            (label.StartsWith("← Назад", StringComparison.Ordinal) ||
             label.StartsWith("← Back", StringComparison.OrdinalIgnoreCase));

        /// <summary>Пометить элемент как «Назад»: по Esc вызывается его действие (если он виден).</summary>
        public static void MarkBack(VisualElement element, Action back)
        {
            BackActions.Remove(element);
            BackActions.Add(element, back);
        }

        /// <summary>
        /// Esc в меню: нажать самую вложенную видимую кнопку «Назад» внутри root (вложенная — ближе к открытому
        /// разделу: шаг брифинга, окно выбора персонажа). false — такой кнопки нет (главная страница).
        /// </summary>
        public static bool GoBack(VisualElement root)
        {
            Action best = null;
            int bestDepth = -1;
            void Walk(VisualElement e, int depth)
            {
                var style = e.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity < 0.05f) return;
                if (e is UnityEngine.UIElements.Button button && !button.enabledInHierarchy) return;
                if (BackActions.TryGetValue(e, out var action) && depth >= bestDepth)
                {
                    best = action;
                    bestDepth = depth;
                }
                foreach (var child in e.Children()) Walk(child, depth + 1);
            }
            if (root == null || root.panel == null) return false;
            Walk(root, 0);
            if (best == null) return false;
            best();
            return true;
        }

        /// <summary>Вызывается при нажатии любой кнопки — приложение проигрывает щелчок.</summary>
        public static Action ButtonClicked;
        /// <summary>Тихий звук наведения на пункт навигации (вкладки, разделы, строки истории); задаёт приложение.</summary>
        public static Action Hovered;
        /// <summary>Звук изменения настройки (переключатель, слайдер); задаёт приложение.</summary>
        public static Action Changed;
        /// <summary>«Меньше анимаций» (Доступность): переходы почти мгновенные, контент появляется без сдвига.</summary>
        public static bool ReducedMotion;

        /// <summary>
        /// Единое появление контента при смене вкладки или раздела: проявление и подъём на 14 px за 0,28 с.
        /// Вешайте на контейнер раздела, а не на интерактивные плитки (у тех свои переходы).
        /// </summary>
        /// <summary>
        /// «Выпрыгивание»: масштаб 0 → 1,1 → 1 за durationMs (из центра элемента), с задержкой delayMs.
        /// С «меньше анимаций» элемент просто появляется.
        /// </summary>
        public static void PopIn(VisualElement e, long delayMs = 0, float durationMs = 420f)
        {
            if (e == null) return;
            e.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f) });
            if (ReducedMotion)
            {
                e.style.scale = new Scale(Vector3.one);
                e.style.opacity = 1f;
                return;
            }
            e.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(60f), 0f);
            e.style.scale = new Scale(Vector3.zero);
            e.style.opacity = 0f;
            float start = -1f;
            IVisualElementScheduledItem item = null;
            item = e.schedule.Execute(() =>
            {
                float now = Time.realtimeSinceStartup;
                if (start < 0f) start = now;
                float t = Mathf.Clamp01((now - start) * 1000f / durationMs);
                // Две части: до 1,1 с замедлением (65 % времени), потом мягко назад к 1.
                float scale = t < 0.65f
                    ? 1.1f * (1f - Mathf.Pow(1f - t / 0.65f, 3f))
                    : Mathf.Lerp(1.1f, 1f, EaseOutCubic((t - 0.65f) / 0.35f));
                e.style.scale = new Scale(new Vector3(scale, scale, 1f));
                e.style.opacity = Mathf.Clamp01(t * 3f);
                if (t >= 1f) item?.Pause();
            }).Every(16).StartingIn(delayMs);
        }

        public static void Appear(VisualElement e, long delayMs = 0)
        {
            if (e == null) return;
            if (ReducedMotion)
            {
                e.style.opacity = 1f;
                e.style.translate = new Translate(new Length(0f), new Length(0f));
                return;
            }
            e.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f) });
            e.style.opacity = 0f;
            e.style.translate = new Translate(new Length(0f), new Length(14f));
            e.schedule.Execute(() =>
            {
                Transitions(e, 0.28f, "opacity", "translate");
                e.style.opacity = 1f;
                e.style.translate = new Translate(new Length(0f), new Length(0f));
            }).ExecuteLater(16 + delayMs);
        }

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
            var label = new Label(LocalizationService.TranslateLegacy(text));
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
            // Every styled ScrollView can reveal the keyboard/gamepad focused
            // control; callers don't need separate listeners per panel.
            GamepadScrollFocus.Register(scroll);
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
            var label = Text(LocalizationService.TranslateLegacy(text).ToUpperInvariant(), 12f, Theme.TextDim, true);
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
                    hover = new Color(0.30f, 0.42f, 0.53f);
                    textColor = Theme.Text;
                    break;
                default:
                    normal = new Color(1f, 1f, 1f, 0f);
                    hover = new Color(1f, 1f, 1f, 0.08f);
                    textColor = Theme.TextDim;
                    break;
            }
            return Button(text, onClick, normal, hover, textColor, fontSize, kind == ButtonKind.Ghost);
        }

        /// <summary>Кнопка своих цветов (фон, фон при наведении, текст) — для страниц со своей палитрой.</summary>
        public static UnityEngine.UIElements.Button Button(string text, Action onClick, Color normal, Color hover, Color textColor,
            float fontSize = 16f, bool outlined = false)
        {
            Action click = () =>
            {
                ButtonClicked?.Invoke();
                onClick?.Invoke();
            };
            var button = new UnityEngine.UIElements.Button(click);
            button.text = LocalizationService.TranslateLegacy(text);
            // Кнопки «← Назад» срабатывают и по Esc (см. GoBack).
            if (IsBackLabel(text) || IsBackLabel(button.text)) MarkBack(button, click);
            button.style.fontSize = fontSize;
            Fonts.Apply(button, fontSize >= Fonts.DisplayFrom ? Fonts.Weight.Bold : Fonts.Weight.SemiBold);
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.color = textColor;
            button.style.backgroundColor = normal;
            button.style.minHeight = 38f;
            Radius(button, 8f);
            Border(button, outlined ? 1f : 0f, Theme.Line);
            Padding(button, 8f, 16f);
            Margin(button, 0f);

            // Наведение: цвет светлеет, кнопка чуть приподнимается; нажатие — слегка «вдавливается».
            Transitions(button, "background-color", "translate", "scale");
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!button.enabledSelf) return;
                button.style.backgroundColor = hover;
                button.style.translate = new Translate(new Length(0f),
                    new Length(GameFxTuning.ButtonHoverLift(ReducedMotion)));
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.style.backgroundColor = normal;
                button.style.translate = new Translate(new Length(0f), new Length(0f));
                button.style.scale = new Scale(Vector3.one);
            });
            button.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (button.enabledSelf)
                {
                    float amount = GameFxTuning.ButtonPressScale(ReducedMotion);
                    button.style.scale = new Scale(new Vector3(amount, amount, 1f));
                }
            }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ => button.style.scale = new Scale(Vector3.one), TrickleDown.TrickleDown);
            // Native UI Toolkit Buttons handle NavigationSubmitEvent. Make their
            // keyboard/gamepad focus visible, even without hover from a mouse.
            button.RegisterCallback<FocusInEvent>(_ =>
            {
                Border(button, 2f, Theme.Accent);
                button.style.backgroundColor = hover;
            });
            button.RegisterCallback<FocusOutEvent>(_ =>
            {
                Border(button, outlined ? 1f : 0f, Theme.Line);
                button.style.backgroundColor = normal;
                button.style.scale = new Scale(Vector3.one);
            });
            return button;
        }

        /// <summary>Плавные переходы перечисленных свойств (0.3 с, ease) — без USS, прямо в коде.</summary>
        public static void Transitions(VisualElement e, params string[] properties) =>
            Transitions(e, Theme.TransitionSeconds, properties);

        public static void Transitions(VisualElement e, float seconds, params string[] properties)
        {
            if (ReducedMotion) seconds = Mathf.Min(seconds, 0.04f);
            var names = new List<StylePropertyName>();
            var durations = new List<TimeValue>();
            var easings = new List<EasingFunction>();
            foreach (var property in properties)
            {
                names.Add(new StylePropertyName(property));
                durations.Add(new TimeValue(seconds, TimeUnit.Second));
                easings.Add(new EasingFunction(EasingMode.Ease));
            }
            e.style.transitionProperty = new StyleList<StylePropertyName>(names);
            e.style.transitionDuration = new StyleList<TimeValue>(durations);
            e.style.transitionTimingFunction = new StyleList<EasingFunction>(easings);
        }

        /// <summary>Число моноширинным шрифтом — цифры не «пляшут», когда меняются.</summary>
        public static Label Number(string text, float size = 15f, Color? color = null, bool bold = false)
        {
            var label = Text(text, size, color, false);
            Fonts.Apply(label, bold ? Fonts.Weight.MonoBold : Fonts.Weight.Mono);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        /// <summary>
        /// Мягкая тень вокруг панели: несколько полупрозрачных скруглённых обводок снаружи, гуще книзу.
        /// UI Toolkit не умеет box-shadow, поэтому тень рисуется кодом (Painter2D) за краем элемента.
        /// </summary>
        public static void Shadow(VisualElement e, float radius, float spread = 14f, float strength = 0.45f)
        {
            e.generateVisualContent += context =>
            {
                var size = e.layout.size;
                if (float.IsNaN(size.x) || size.x < 2f || size.y < 2f) return;
                var p = context.painter2D;
                const int steps = 7;
                float width = spread / steps;
                for (int i = 0; i < steps; i++)
                {
                    float t = (i + 0.5f) / steps;
                    float grow = width * (i + 0.5f);
                    float fade = (1f - t) * (1f - t);
                    p.strokeColor = new Color(0f, 0f, 0f, strength * fade / steps * 2.2f);
                    p.lineWidth = width + 0.5f;
                    // Снизу тень длиннее: смещаем внешние кольца вниз, но не залезаем внутрь панели.
                    float drop = Mathf.Min(grow * 0.5f, 4f);
                    RoundRectPath(p, new Rect(-grow, -grow + drop, size.x + 2f * grow, size.y + 2f * grow), radius + grow);
                    p.Stroke();
                }
            };
        }

        static void RoundRectPath(Painter2D p, Rect r, float radius)
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

        /// <summary>
        /// Плавное появление и исчезновение панели: прозрачность и сдвиг за 0.3 с. Скрытая панель
        /// после анимации убирается из раскладки (display: none), чтобы не перехватывать клики.
        /// hiddenX / hiddenY — сдвиг скрытой панели в процентах её размера (например, 0 и 100 — уехала вниз).
        /// </summary>
        public static void Slide(VisualElement e, bool show, float hiddenX, float hiddenY)
        {
            int version = (e.userData is int v ? v : 0) + 1;
            e.userData = version;
            Transitions(e, "opacity", "translate");
            var hidden = new Translate(new Length(hiddenX, LengthUnit.Percent), new Length(hiddenY, LengthUnit.Percent));
            var shown = new Translate(new Length(0f), new Length(0f));
            if (show)
            {
                bool wasHidden = e.style.display.value == DisplayStyle.None || e.resolvedStyle.opacity < 0.01f;
                if (wasHidden)
                {
                    e.style.opacity = 0f;
                    e.style.translate = hidden;
                }
                e.style.display = DisplayStyle.Flex;
                // Переход срабатывает, только если стартовое состояние успело примениться, — ждём кадр.
                e.schedule.Execute(() =>
                {
                    if (!(e.userData is int now) || now != version) return;
                    e.style.opacity = 1f;
                    e.style.translate = shown;
                }).ExecuteLater(wasHidden ? 16 : 0);
            }
            else
            {
                e.style.opacity = 0f;
                e.style.translate = hidden;
                e.schedule.Execute(() =>
                {
                    if (e.userData is int now && now == version) e.style.display = DisplayStyle.None;
                }).ExecuteLater((long)(Theme.TransitionSeconds * 1000f) + 30);
            }
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
