using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Экран загрузки «Логистического магната»: пока генерируется город и строится 3D, видно, что происходит.
    /// Шаги выполняются по одному за кадр (между ними интерфейс успевает перерисоваться): полоса растёт,
    /// подпись меняется, по полосе едет фура, внизу — советы по правилам (сменяются каждые 5 с). Фон — картина из
    /// Scenario с медленным приближением, после первого шага под заголовком — название города.
    /// Ошибка на любом шаге — сообщение и кнопка «В меню».
    /// </summary>
    public sealed class MagnateLoading
    {
        public VisualElement Root { get; }
        readonly Label _title, _subtitle, _stage, _tip;
        readonly VisualElement _bar, _truck, _art;
        int _tipIndex;
        float _time;

        static readonly string[] Tips =
        {
            "Скоропорт приносит много очков, но за каждый лишний раунд в пути теряет по 2.",
            "Тяжеловес едет только по широким дорогам: бетон, мосты и магистрали.",
            "Опасный груз в Центре требует взятки влиянием — иначе его конфискуют.",
            "Если на дорогу за раунд заехало больше фур, чем она пропускает, — пробка: все стоят и теряют очки.",
            "Своя дорога — бесплатный проезд для ваших фур и плата с чужих.",
            "Своя дорога, кликнутая ещё раз, становится магистралью: больше фур и тяжеловесы.",
            "Маршруты планируются тайно, а фуры едут разом — угадайте, где соберётся пробка.",
            "Первый раунд — раунд стройки: посмотрите, где лежат грузы, и проложите дорогу к ним.",
            "Хабом владеет тот, кто вложил в него больше очков стройки, — хаб можно перехватить.",
            "Отстающие ходят и грузятся первыми, а компания с наименьшим счётом получает +1 очко стройки.",
            "По своей дороге длиной от 2 участков фура едет на участок быстрее.",
            "Если груз увёл соперник, фура сама возьмёт другой с того же хаба.",
            "Второй, кто выполнит заказ мэрии, получает половину награды.",
            "Пустая фура ездит по любым дорогам — её можно заранее отправить к тяжеловесу.",
            "Тупики и единственные переправы строят только игроки — это ценные участки.",
        };

        public MagnateLoading(string title)
        {
            Root = new VisualElement();
            Ui.Fill(Root);
            Root.style.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.pickingMode = PickingMode.Position;
            // Картина из Scenario (логистический город на закате, если нет — ключевой арт меню): фоном, медленно
            // приближается (эффект Кена Бёрнса). Сверху — лёгкое затемнение и тёмная плашка под текстом.
            var art = GeneratedArt.Load(GeneratedArt.MagnateLoading) ?? GeneratedArt.Load(GeneratedArt.KeyArt);
            if (art != null)
            {
                _art = new VisualElement();
                Ui.Fill(_art);
                _art.style.backgroundImage = art;
                _art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _art.pickingMode = PickingMode.Ignore;
                Root.Add(_art);
                var shade = new VisualElement();
                Ui.Fill(shade);
                shade.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.38f);
                shade.pickingMode = PickingMode.Ignore;
                Root.Add(shade);
            }

            var box = Ui.Column();
            box.style.alignItems = Align.Center;
            box.style.width = 640f;
            box.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, art != null ? 0.72f : 0f);
            Ui.Radius(box, 18f);
            box.style.paddingTop = 28f;
            box.style.paddingBottom = 26f;
            box.style.paddingLeft = 32f;
            box.style.paddingRight = 32f;
            Root.Add(box);

            _title = Ui.Text(title, 34f, Theme.Text, true);
            _title.style.marginBottom = 6f;
            box.Add(_title);
            _subtitle = Ui.Text("Генерируем новый город", 16f, Theme.TextDim);
            _subtitle.style.marginBottom = 34f;
            box.Add(_subtitle);

            // Фура едет по полосе загрузки: стоит на её конце и движется вместе с ним.
            var lane = new VisualElement();
            lane.style.width = 520f;
            lane.style.height = 22f;
            box.Add(lane);
            _truck = new TruckIcon();
            _truck.style.position = Position.Absolute;
            _truck.style.bottom = 0f;
            _truck.style.left = Length.Percent(2f);
            _truck.style.translate = new Translate(Length.Percent(-100f), new Length(0f));
            Ui.Transitions(_truck, 0.25f, "left");
            lane.Add(_truck);

            var track = new VisualElement();
            track.style.width = 520f;
            track.style.height = 10f;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            Ui.Radius(track, 5f);
            track.style.overflow = Overflow.Hidden;
            box.Add(track);
            _bar = new VisualElement();
            _bar.style.height = Length.Percent(100f);
            _bar.style.width = Length.Percent(2f);
            _bar.style.backgroundColor = GameColors.Accent;
            Ui.Radius(_bar, 5f);
            Ui.Transitions(_bar, 0.25f, "width");
            track.Add(_bar);

            _stage = Ui.Text("", 15f, Theme.Text);
            _stage.style.marginTop = 14f;
            box.Add(_stage);

            _tipIndex = UnityEngine.Random.Range(0, Tips.Length);
            _tip = Ui.Text("Совет: " + Tips[_tipIndex], 14f, Theme.TextDim);
            _tip.style.marginTop = 34f;
            _tip.style.whiteSpace = WhiteSpace.Normal;
            _tip.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tip.style.maxWidth = 560f;
            _tip.style.minHeight = 38f;
            Ui.Transitions(_tip, 0.35f, "opacity");
            box.Add(_tip);

            // Анимация: фон медленно приближается, советы сменяются каждые 5 с (с затуханием), фура покачивается.
            Root.schedule.Execute(Animate).Every(33);
        }

        void Animate(TimerState timer)
        {
            float dt = Mathf.Min(0.1f, timer.deltaTime / 1000f);
            _time += dt;
            if (_art != null && !Ui.ReducedMotion)
            {
                float zoom = 1f + 0.08f * Mathf.Clamp01(_time / 25f);
                _art.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            }
            if (_truck != null && !Ui.ReducedMotion)
                _truck.style.translate = new Translate(Length.Percent(-100f), new Length(Mathf.Sin(_time * 18f) * 0.6f));
            int slot = (int)(_time / 5f);
            if (slot != _shownSlot)
            {
                _shownSlot = slot;
                if (slot == 0) return;
                _tip.style.opacity = 0f;
                _tip.schedule.Execute(() =>
                {
                    _tipIndex = (_tipIndex + 1) % Tips.Length;
                    _tip.text = "Совет: " + Tips[_tipIndex];
                    _tip.style.opacity = 1f;
                }).ExecuteLater(350);
            }
        }

        int _shownSlot;

        /// <summary>Город сгенерирован — показать его название под заголовком.</summary>
        public void SetCity(string name)
        {
            if (!string.IsNullOrEmpty(name)) _subtitle.text = $"Город «{name}»";
        }

        /// <summary>Маленькая фура цвета акцента: прицеп, кабина с окном, колёса.</summary>
        sealed class TruckIcon : VisualElement
        {
            public TruckIcon()
            {
                style.width = 40f;
                style.height = 20f;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            static void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                void Fill(float x, float y, float w, float h, Color color)
                {
                    p.fillColor = color;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, y));
                    p.LineTo(new Vector2(x + w, y));
                    p.LineTo(new Vector2(x + w, y + h));
                    p.LineTo(new Vector2(x, y + h));
                    p.ClosePath();
                    p.Fill();
                }
                Fill(1f, 2f, 25f, 12f, GameColors.Accent);                       // прицеп
                Fill(27f, 6f, 11f, 8f, GameColors.Accent * 0.85f);              // кабина
                Fill(31f, 7.5f, 5f, 3.5f, new Color(0.75f, 0.88f, 1f, 1f));     // окно
                p.fillColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                foreach (float x in new[] { 7f, 19f, 33f })
                {
                    p.BeginPath();
                    p.Arc(new Vector2(x, 16f), 3.2f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                }
            }
        }

        /// <summary>Выполнить шаги по одному за кадр; в конце — done. onError — сообщение об ошибке.</summary>
        public void Run(CityHighwaysApp app, IList<(string label, Action action)> steps, Action done, Action onError)
        {
            int index = 0;
            void Next()
            {
                if (index >= steps.Count)
                {
                    _bar.style.width = Length.Percent(100f);
                    _truck.style.left = Length.Percent(100f);
                    _stage.text = "Готово!";
                    app.Tweener.After(0.25f, done);
                    return;
                }
                var step = steps[index];
                _stage.text = step.label;
                float percent = Mathf.Max(2f, 100f * index / steps.Count);
                _bar.style.width = Length.Percent(percent);
                _truck.style.left = Length.Percent(percent);
                // Даём кадру отрисовать подпись, потом выполняем тяжёлый шаг.
                app.Tweener.After(0.06f, () =>
                {
                    try
                    {
                        step.action();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Fail(app, "Не удалось построить город: " + e.Message, onError);
                        return;
                    }
                    index++;
                    Next();
                });
            }
            Next();
        }

        void Fail(CityHighwaysApp app, string message, Action onError)
        {
            _stage.text = message;
            _stage.style.color = Theme.Danger;
            var back = Ui.Button("В меню", () => onError?.Invoke(), ButtonKind.Secondary, 15f);
            back.style.marginTop = 18f;
            _stage.parent.Add(back);
        }
    }
}
