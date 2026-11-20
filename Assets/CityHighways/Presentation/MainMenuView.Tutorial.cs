using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Обучение»: три режима — три карточки.
    /// - Логистический магнат: учебная карта (маленький город), вы и бот на лёгкой сложности, наставник ведёт по первому раунду.
    /// - Соревнование: прежнее обучение с наставником на учебной карте (TutorialCoach).
    /// - Кооператив: знакомство из пяти шагов (окно поверх меню), затем тренировочная партия с двумя ботами-союзниками.
    /// У каждой карточки — отметка «пройдено» и кнопка «Правила режима», открывающая правила на нужной вкладке.
    /// </summary>
    public sealed partial class MainMenuView
    {
        const string CoopBriefingKey = "CityHighways.CoopBriefing.v1";

        VisualElement _tutorialCards;

        static bool CoopBriefingDone => PlayerPrefs.GetInt(CoopBriefingKey, 0) == 1;
        bool MagnateTutorialDone => GameSetup.Load().magnateTutorialDone;
        bool AnyTutorialDone => TutorialCoach.IsDone || MagnateTutorialDone || CoopBriefingDone;

        VisualElement BuildTutorial()
        {
            var page = Ui.Column();
            page.Add(PageTitle("Обучение", () => ShowPage(Page.Home)));
            var intro = Ui.Text("Выберите режим. В каждом — своё обучение: наставник проведёт по первым ходам, а правила режима всегда под рукой.", 15f, Theme.TextDim);
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.marginBottom = 16f;
            page.Add(intro);
            _tutorialCards = Ui.Row();
            _tutorialCards.style.alignItems = Align.Stretch;
            page.Add(_tutorialCards);
            RefreshTutorial();
            return page;
        }

        void RefreshTutorial()
        {
            if (_tutorialCards == null) return;
            _tutorialCards.Clear();
            var profileName = Profiles.Active?.name;

            _tutorialCards.Add(TutorialCard(GameMode.Magnate, MagnateTutorialDone,
                $"Учебная карта: небольшой город с аэропортом, вокзалом и портом. Вы и один бот, лёгкая сложность, {GameSetup.TutorialMagnateRounds} раундов. Наставник проведёт по первому раунду.",
                new[] { "хабы и грузы семи видов", "покупка дорог (пунктир) и развитие хабов", "маршруты фур, пробки и доставка", "нижняя панель и окна" },
                "Начать с наставником", () =>
                {
                    var setup = GameSetup.CreateMagnatePractice(GameSetup.Load(), profileName);
                    _app.StartGame(setup);
                }, null, null));

            _tutorialCards.Add(TutorialCard(GameMode.Classic, TutorialCoach.IsDone,
                "Учебная партия с наставником на небольшой карте — около 10 минут.",
                new[] { "карты ресурсов и стройка дорог", "контракты мэрии", "плата за проезд и магистрали", "конец партии и подсчёт очков" },
                "Начать с наставником", _app.StartTutorial, null, null));

            _tutorialCards.Add(TutorialCard(GameMode.Coop, CoopBriefingDone,
                "Знакомство из пяти шагов, затем тренировочная партия: вы и два бота-союзника против города.",
                new[] { "общая сеть и общий счёт", "заказы города", "шкала кризиса", "как победить и как проиграть" },
                "Знакомство: 5 шагов", () => ShowCoopBriefing(0),
                "Тренировочная партия", StartCoopPractice));
        }

        VisualElement TutorialCard(GameMode mode, bool done, string text, string[] learn, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            var card = Ui.Card(18f);
            card.style.flexGrow = 1f;
            card.style.flexBasis = 0f;
            card.style.marginRight = mode == GameMode.Coop ? 0f : 14f;
            card.style.backgroundColor = Theme.Panel;
            Ui.Radius(card, 14f);
            Ui.Border(card, 1f, done ? new Color(GameColors.Success.r, GameColors.Success.g, GameColors.Success.b, 0.6f) : Theme.Line);
            Ui.Shadow(card, 10f);

            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            var icon = new MenuIcon(ModeTabs.Glyph(mode), 34f, Theme.Gold);
            icon.style.marginRight = 12f;
            head.Add(icon);
            var titles = Ui.Column();
            titles.style.flexShrink = 1f;
            titles.Add(Ui.Text(ModeTabs.Title(mode), 20f, Theme.Gold, true));
            titles.Add(Ui.Text(done ? "✓ Пройдено" : "Ещё не пройдено", 12f, done ? GameColors.Success : Theme.TextDim, true));
            head.Add(titles);
            card.Add(head);

            var tagline = Ui.Text(ModeTabs.Tagline(mode), 13f, Theme.TextDim);
            tagline.style.whiteSpace = WhiteSpace.Normal;
            tagline.style.marginTop = 10f;
            card.Add(tagline);
            var about = Ui.Text(text, 14f, Theme.Text);
            about.style.whiteSpace = WhiteSpace.Normal;
            about.style.marginTop = 8f;
            card.Add(about);

            var caption = Ui.Text("Чему научитесь", 12f, Theme.TextDim, true);
            caption.style.marginTop = 12f;
            card.Add(caption);
            foreach (var item in learn)
            {
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4f;
                var star = new MenuIcon(MenuIcon.Glyph.Star, 12f, Theme.Gold);
                star.style.marginRight = 8f;
                row.Add(star);
                var label = Ui.Text(item, 13f, Theme.Text);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.flexShrink = 1f;
                row.Add(label);
                card.Add(row);
            }

            var spacer = Ui.Box();
            spacer.style.flexGrow = 1f;
            spacer.style.minHeight = 14f;
            card.Add(spacer);
            var start = Ui.Button(done ? primary.Replace("Начать", "Пройти снова") : primary, onPrimary, ButtonKind.Primary, 15f);
            start.style.marginTop = 6f;
            card.Add(start);
            if (secondary != null)
            {
                var more = Ui.Button(secondary, onSecondary, ButtonKind.Secondary, 14f);
                more.style.marginTop = 6f;
                card.Add(more);
            }
            var rules = Ui.Button("Правила режима", () => OpenRules(mode, Page.Tutorial), ButtonKind.Ghost, 13f);
            rules.style.marginTop = 6f;
            card.Add(rules);
            return card;
        }

        /// <summary>Открыть правила (на вкладке режима, если он задан); «Назад» вернёт на страницу from.</summary>
        void OpenRules(GameMode? mode, Page from)
        {
            _rulesReturn = from;
            if (mode.HasValue) _rules.SetMode(mode.Value);
            ShowPage(Page.Rules);
        }

        void StartCoopPractice()
        {
            Ui.Show(_overlay, false);
            _app.StartGame(GameSetup.CreateCoopPractice(GameSetup.Load(), Profiles.Active?.name));
        }

        // ================================================================== Знакомство с кооперативом

        static List<(MenuIcon.Glyph glyph, string title, string text)> CoopSteps()
        {
            var c = new GameConfig().MakeCooperative();
            return new List<(MenuIcon.Glyph, string, string)>
            {
                (MenuIcon.Glyph.Skyline, "Одна команда против города",
                    "Все компании за столом — одна команда, противник — сам город. Побеждают или проигрывают все вместе, счёт тоже общий."),
                (MenuIcon.Glyph.Road, "Общая сеть",
                    "Дорога любого союзника засчитывается в ваши контракты, и проезд по ней бесплатный. Взяток, аукциона и пропусков нет — " +
                    "делиться картами ресурсов помогают сделки."),
                (MenuIcon.Glyph.Document, "Заказы города",
                    $"На столе — {c.CoopOpenOrdersFor(2)}–{c.CoopOpenOrdersFor(4)} заказа города (зависит от числа игроков) — общие контракты команды. " +
                    "Заказ выполнен, когда общая сеть связала его районы. Срок зависит от длины заказа: вдвоём на заказ в 10 очков — " +
                    $"{c.CoopOrderRoundsFor(10, 2)} кругов; просроченный поднимает кризис на {c.CoopCrisisPerFailedOrder}. Панель заказов — в правом верхнем углу поля."),
                (MenuIcon.Glyph.Flame, "Шкала кризиса",
                    $"Кризис начинается с {c.CoopCrisisStart} и растёт сам: +1 каждые {c.CoopCrisisEveryRounds} круга, а ремонт, снегопад и парад добавляют ещё " +
                    $"+{c.CoopCrisisPerBadEvent}. Каждый выполненный заказ снижает кризис на {c.CoopCrisisReliefPerOrder}."),
                (MenuIcon.Glyph.Trophy, "Победа и поражение",
                    $"Команда побеждает, выполнив заказы города: на двоих — {c.CoopOrdersToWin(2)}, на троих — {c.CoopOrdersToWin(3)}, на четверых — {c.CoopOrdersToWin(4)}. " +
                    $"Город побеждает, если кризис дошёл до {c.CoopCrisisMax} или участки кончились раньше, чем набралось заказов."),
            };
        }

        void ShowCoopBriefing(int index)
        {
            var steps = CoopSteps();
            index = Mathf.Clamp(index, 0, steps.Count - 1);
            var (glyph, title, text) = steps[index];
            _overlay.Clear();
            var box = Ui.Card(24f);
            box.pickingMode = PickingMode.Position;
            box.style.width = 620f;
            box.style.backgroundColor = Theme.Panel;
            Ui.Shadow(box, 10f, 20f, 0.6f);

            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            head.style.justifyContent = Justify.SpaceBetween;
            head.Add(Ui.Text("Кооператив · знакомство", 13f, Theme.TextDim, true));
            head.Add(Ui.Text($"{index + 1} / {steps.Count}", 13f, Theme.TextDim, true));
            box.Add(head);

            var body = Ui.Row();
            body.style.alignItems = Align.Center;
            body.style.marginTop = 14f;
            var icon = new MenuIcon(glyph, 56f, Theme.Gold);
            icon.style.marginRight = 18f;
            icon.style.flexShrink = 0f;
            body.Add(icon);
            var texts = Ui.Column();
            texts.style.flexShrink = 1f;
            texts.Add(Ui.Text(title, 24f, Theme.Gold, true));
            var label = Ui.Text(text, 15f, Theme.Text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 6f;
            texts.Add(label);
            body.Add(texts);
            box.Add(body);

            // Точки шагов.
            var dots = Ui.Row();
            dots.style.justifyContent = Justify.Center;
            dots.style.marginTop = 18f;
            for (int i = 0; i < steps.Count; i++)
            {
                var dot = Ui.Box();
                dot.style.width = i == index ? 22f : 8f;
                dot.style.height = 8f;
                dot.style.marginLeft = 3f;
                dot.style.marginRight = 3f;
                dot.style.backgroundColor = i == index ? Theme.Gold : new Color(1f, 1f, 1f, 0.2f);
                Ui.Radius(dot, 4f);
                dots.Add(dot);
            }
            box.Add(dots);

            var buttons = Ui.Row();
            buttons.style.marginTop = 18f;
            buttons.style.justifyContent = Justify.SpaceBetween;
            var left = Ui.Row();
            if (index > 0) left.Add(Ui.Button("← Назад", () => ShowCoopBriefing(index - 1), ButtonKind.Ghost, 14f));
            left.Add(Ui.Button("Закрыть", () => { Ui.Show(_overlay, false); RefreshTutorial(); }, ButtonKind.Ghost, 14f));
            buttons.Add(left);
            var right = Ui.Row();
            if (index < steps.Count - 1)
                right.Add(Ui.Button("Дальше →", () => ShowCoopBriefing(index + 1), ButtonKind.Primary, 15f));
            else
            {
                PlayerPrefs.SetInt(CoopBriefingKey, 1);
                PlayerPrefs.Save();
                var rules = Ui.Button("Правила кооператива", () => { Ui.Show(_overlay, false); OpenRules(GameMode.Coop, Page.Tutorial); }, ButtonKind.Secondary, 14f);
                rules.style.marginRight = 8f;
                right.Add(rules);
                right.Add(Ui.Button("Тренировочная партия", StartCoopPractice, ButtonKind.Primary, 15f));
            }
            buttons.Add(right);
            box.Add(buttons);
            _overlay.Add(box);
            Ui.Show(_overlay, true);
            Ui.Appear(box);
        }
    }
}
