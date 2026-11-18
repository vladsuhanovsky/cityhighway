using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Экран загрузки «Логистического магната»: пока генерируется город и строится 3D, видно, что происходит.
    /// Шаги выполняются по одному за кадр (между ними интерфейс успевает перерисоваться): полоса растёт,
    /// подпись меняется, внизу — совет по правилам. Ошибка на любом шаге — сообщение и кнопка «В меню».
    /// </summary>
    public sealed class MagnateLoading
    {
        public VisualElement Root { get; }
        readonly Label _title, _stage, _tip;
        readonly VisualElement _bar;

        static readonly string[] Tips =
        {
            "Скоропорт приносит много очков, но за каждый лишний раунд в пути теряет по 2.",
            "Тяжеловес едет только по широким дорогам: бетон, мосты и магистрали.",
            "Опасный груз в Центре требует взятки влиянием — иначе его конфискуют.",
            "Если на дорогу за раунд заехало больше фур, чем она пропускает, — пробка: все стоят и теряют очки.",
            "Своя дорога — бесплатный проезд для ваших фур и плата с чужих.",
            "Своя дорога, кликнутая ещё раз, становится магистралью: больше фур и тяжеловесы.",
            "Маршруты планируются тайно, а фуры едут разом — угадайте, где соберётся пробка.",
        };

        public MagnateLoading(string title)
        {
            Root = new VisualElement();
            Ui.Fill(Root);
            Root.style.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.pickingMode = PickingMode.Position;
            // Картина города из Scenario (если есть) — фоном, под затемнением.
            var art = GeneratedArt.Load(GeneratedArt.KeyArt);
            if (art != null)
            {
                var image = new VisualElement();
                Ui.Fill(image);
                image.style.backgroundImage = art;
                image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                image.pickingMode = PickingMode.Ignore;
                Root.Add(image);
                var shade = new VisualElement();
                Ui.Fill(shade);
                shade.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.62f);
                shade.pickingMode = PickingMode.Ignore;
                Root.Add(shade);
            }

            var box = Ui.Column();
            box.style.alignItems = Align.Center;
            box.style.width = 640f;
            Root.Add(box);

            _title = Ui.Text(title, 34f, Theme.Text, true);
            _title.style.marginBottom = 6f;
            box.Add(_title);
            var subtitle = Ui.Text("Генерируем новый город", 16f, Theme.TextDim);
            subtitle.style.marginBottom = 28f;
            box.Add(subtitle);

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

            _tip = Ui.Text("Совет: " + Tips[UnityEngine.Random.Range(0, Tips.Length)], 14f, Theme.TextDim);
            _tip.style.marginTop = 46f;
            _tip.style.whiteSpace = WhiteSpace.Normal;
            _tip.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tip.style.maxWidth = 560f;
            box.Add(_tip);
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
                    _stage.text = "Готово!";
                    app.Tweener.After(0.25f, done);
                    return;
                }
                var step = steps[index];
                _stage.text = step.label;
                _bar.style.width = Length.Percent(Mathf.Max(2f, 100f * index / steps.Count));
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
