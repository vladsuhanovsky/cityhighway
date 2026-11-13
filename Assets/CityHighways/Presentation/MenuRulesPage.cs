using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Страница «Правила»: вкладки слева, справа — текст с картинками, нарисованными кодом.</summary>
    public static class MenuRulesPage
    {
        public static VisualElement Build(GameConfig config, Action back)
        {
            var page = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 18f;
            var backButton = Ui.Button("← Назад", back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            header.Add(Ui.Text("Правила", 34f, Theme.Accent, true));
            page.Add(header);

            var body = Ui.Row();
            body.style.alignItems = Align.FlexStart;
            page.Add(body);

            var tabs = Ui.Column();
            tabs.style.width = 230f;
            tabs.style.marginRight = 16f;
            body.Add(tabs);

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.style.flexGrow = 1f;
            content.style.height = 640f;
            content.style.backgroundColor = Theme.Panel;
            Ui.Radius(content, 12f);
            Ui.Padding(content.contentContainer, 22f, 26f);
            body.Add(content);

            var sections = new List<(string title, Func<VisualElement> build)>
            {
                ("Цель и ход", () => Basics(config)),
                ("Ресурсы", () => ResourcesSection(config)),
                ("Контракты и проезд", () => Contracts(config)),
                ("События", () => Events(config)),
                ("Цели мэрии и удача", () => GoalsAndLuck(config)),
                ("Сделки и магистрали", () => Deals(config)),
                ("Влияние", () => InfluenceSection(config)),
                ("Персонажи", () => CharactersSection(config)),
                ("Конец партии и очки", () => Ending(config))
            };

            var buttons = new List<UnityEngine.UIElements.Button>();
            void Select(int index)
            {
                content.Clear();
                content.Add(sections[index].build());
                content.scrollOffset = Vector2.zero;
                for (int i = 0; i < buttons.Count; i++)
                {
                    bool active = i == index;
                    buttons[i].style.backgroundColor = active ? Theme.Accent : Theme.PanelLight;
                    buttons[i].style.color = active ? Theme.AccentText : Theme.Text;
                }
            }
            for (int i = 0; i < sections.Count; i++)
            {
                int index = i;
                var button = Ui.Button(sections[i].title, () => Select(index), ButtonKind.Secondary, 15f);
                button.style.marginBottom = 8f;
                button.style.height = 44f;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                buttons.Add(button);
                tabs.Add(button);
            }
            Select(0);
            return page;
        }

        // ================================================================== Строительные блоки

        static VisualElement Section(string title)
        {
            var column = Ui.Column();
            column.Add(Ui.Text(title, 24f, Theme.Accent, true));
            return column;
        }

        static void Paragraph(VisualElement parent, string text)
        {
            var label = Ui.Text(text, 15f, Theme.Text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 10f;
            parent.Add(label);
        }

        static void Picture(VisualElement parent, VisualElement picture, string caption = null)
        {
            var box = Ui.Column();
            box.style.alignItems = Align.Center;
            box.style.marginTop = 14f;
            box.style.marginBottom = 4f;
            box.Add(picture);
            if (caption != null)
            {
                var label = Ui.Text(caption, 12f, Theme.TextDim);
                label.style.marginTop = 6f;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.maxWidth = 640f;
                box.Add(label);
            }
            parent.Add(box);
        }

        /// <summary>Плитка «заголовок + текст» цветной полосой слева — для событий, карт удачи и т. п.</summary>
        static VisualElement Tile(string title, string text, Color color, VisualElement icon = null)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8f;
            row.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(row, 8f);
            Ui.Padding(row, 10f, 12f);
            row.style.borderLeftWidth = 5f;
            row.style.borderLeftColor = color;
            if (icon != null)
            {
                icon.style.marginRight = 12f;
                row.Add(icon);
            }
            var texts = Ui.Column();
            texts.style.flexShrink = 1f;
            texts.Add(Ui.Text(title, 15f, color, true));
            var body = Ui.Text(text, 13f, Theme.Text);
            body.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(body);
            row.Add(texts);
            return row;
        }

        static readonly Color Own = Palette.PlayerColor(0);
        static readonly Color Rival = Palette.PlayerColor(1);
        static readonly Color Spot = new Color(0.85f, 0.86f, 0.90f);

        // ================================================================== Разделы

        static VisualElement Basics(GameConfig config)
        {
            var s = Section("Цель и ход");
            Paragraph(s, "Вы — дорожная компания в Москве. Стройте дороги между местами города, выполняйте контракты мэрии на связь районов и берите плату, когда соперники ездят по вашим дорогам. Побеждает тот, у кого больше очков в конце партии.");
            Paragraph(s, "В свой ход выберите одно действие:");

            var row = Ui.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 10f;
            row.Add(Option("1. Взять карты", "2 карты ресурсов из открытого ряда или колоды. Одну можно заменить картой удачи."));
            row.Add(Option("2. Построить", "Сдать карты нужного ресурса по длине дороги и поставить на неё свои участки."));
            row.Add(Option("3. Контракты", $"Взять {config.ContractsDrawn} контракта и оставить хотя бы один."));
            s.Add(row);

            Paragraph(s, "До основного действия можно предложить сопернику сделку, а вместо стройки — улучшить свою дорогу до магистрали. В начале круга может открыться городское событие.");

            var points = Ui.Row();
            points.style.marginTop = 14f;
            points.style.justifyContent = Justify.Center;
            for (int length = 1; length <= config.MaxRoadLength; length++)
            {
                var cell = Ui.Column();
                cell.style.alignItems = Align.Center;
                cell.style.marginRight = 10f;
                var bar = Ui.Box();
                bar.style.width = 14f * length + 6f;
                bar.style.height = 12f;
                bar.style.backgroundColor = Own;
                Ui.Radius(bar, 3f);
                cell.Add(bar);
                cell.Add(Ui.Text($"длина {length}", 11f, Theme.TextDim));
                cell.Add(Ui.Text($"+{config.PointsForLength(length)}", 16f, Theme.Accent, true));
                points.Add(cell);
            }
            Picture(s, points, "Очки за построенную дорогу: длинные дороги выгоднее.");
            return s;
        }

        static VisualElement Option(string title, string text)
        {
            var card = Ui.Card(12f);
            card.style.width = 220f;
            card.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(card, 10f);
            card.Add(Ui.Text(title, 15f, Theme.Accent, true));
            var body = Ui.Text(text, 13f, Theme.Text);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginTop = 4f;
            card.Add(body);
            return card;
        }

        static VisualElement ResourcesSection(GameConfig config)
        {
            var s = Section("Ресурсы");
            Paragraph(s, "Цвет дороги на карте — ресурс, которым её строят. Дорога длины 3 стоит 3 карты своего ресурса; белые дороги — любым одним ресурсом.");

            var row = Ui.Row();
            row.style.justifyContent = Justify.Center;
            var roles = new[]
            {
                (Resource.Asphalt, "улицы"), (Resource.Concrete, "проспекты"), (Resource.Steel, "мосты"),
                (Resource.Gravel, "загородные"), (Resource.Machinery, "джокер")
            };
            foreach (var (resource, role) in roles)
            {
                var cell = Ui.Column();
                cell.style.alignItems = Align.Center;
                cell.style.marginRight = 14f;
                var card = new CardView(78f, 108f, false);
                card.Set(resource);
                cell.Add(card);
                cell.Add(Ui.Text(Palette.CardTitle(resource), 13f, Theme.Text, true));
                cell.Add(Ui.Text(role, 12f, Theme.TextDim));
                row.Add(cell);
            }
            Picture(s, row);
            Paragraph(s, "Техника заменяет любую карту при стройке. Но если взять открытую технику первой картой, это займёт весь ход, а второй картой брать её нельзя.");
            Paragraph(s, $"В открытом ряду {config.FaceUpSlots} карт. Если в нём больше {config.MaxFaceUpMachinery} техники, ряд обновляется.");
            return s;
        }

        static VisualElement Contracts(GameConfig config)
        {
            var s = Section("Контракты и проезд");
            Paragraph(s, "Контракт — соединить два района, подойдёт любое их место. Выполненный приносит очки, невыполненный в конце партии их отнимает.");
            Paragraph(s, $"Чужая дорога — не тупик. Контракт засчитается, если на пути есть хотя бы одна ваша дорога и не больше {config.MaxForeignRoadsPerContract} чужих. За каждую чужую дорогу вы платите её владельцу {config.TollPerRoad} очка.");

            var diagram = new RuleDiagram(640f, 170f)
                .Place("a", 0.08f, 0.55f, MapView.FromColor, "Район А")
                .Place("x", 0.36f, 0.30f, Spot, null)
                .Place("y", 0.64f, 0.30f, Spot, null)
                .Place("b", 0.92f, 0.55f, MapView.ToColor, "Район Б")
                .Road("a", "x", Own, "ваша")
                .Road("x", "y", Rival, $"чужая −{config.TollPerRoad}")
                .Road("y", "b", Rival, $"чужая −{config.TollPerRoad}");
            Picture(s, diagram, $"Контракт А — Б выполнен: одна своя дорога и две чужие. Владелец чужих получит {2 * config.TollPerRoad} очка.");

            Paragraph(s, "Сложные контракты приносят больше очков, но и провал стоит дороже:");
            var transit = new RuleDiagram(310f, 150f)
                .Place("a", 0.10f, 0.50f, MapView.FromColor, "А")
                .Place("c", 0.50f, 0.25f, MapView.ThirdColor, "через В")
                .Place("b", 0.90f, 0.50f, MapView.ToColor, "Б")
                .Road("a", "c", Own)
                .Road("c", "b", Own);
            var hub = new RuleDiagram(310f, 150f)
                .Place("a", 0.50f, 0.30f, MapView.FromColor, "А")
                .Place("b", 0.12f, 0.70f, MapView.ToColor, "Б")
                .Place("c", 0.88f, 0.70f, MapView.ThirdColor, "В")
                .Road("a", "b", Own)
                .Road("a", "c", Own);
            var pair = Ui.Row();
            pair.style.justifyContent = Justify.SpaceBetween;
            var left = Ui.Column();
            Picture(left, transit, "Транзит: один путь из А в Б через район В.");
            var right = Ui.Column();
            Picture(right, hub, "Узел: район А связан и с Б, и с В.");
            pair.Add(left);
            pair.Add(right);
            s.Add(pair);
            return s;
        }

        static VisualElement Events(GameConfig config)
        {
            var s = Section("Городские события");
            Paragraph(s, $"В начале каждого круга с шансом {Mathf.RoundToInt(config.EventChance * 100)}% открывается событие и действует до конца круга; иначе круг спокойный. Карточка события видна справа во время игры.");
            void Add(CityEventKind kind, string title, string text) => s.Add(Tile(title, text, GameView.EventColor(kind)));
            Add(CityEventKind.RushHour, "Час пик", $"Дороги, построенные в этот круг, становятся загруженными магистралями: проезд по ним — {config.TollPerRoad * config.RushHourTollMultiplier} очка.");
            Add(CityEventKind.Repair, "Ремонт", $"{config.RepairRoads} случайных свободных участка закрыты для стройки.");
            Add(CityEventKind.Subsidy, "Субсидия мэрии", $"Мосты дешевле на {config.BridgeSubsidy} карту стали.");
            Add(CityEventKind.NewDistrict, "Новый район", "На окраине открываются новые места, дороги и контракты.");
            Add(CityEventKind.Parade, "Парад", "Дороги, касающиеся Центра, закрыты для стройки.");
            Add(CityEventKind.Snow, "Снегопад", $"Щебёночные дороги на {config.SnowExtraCost} карту дороже.");
            Add(CityEventKind.Investor, "Инвестор", $"Каждая дорога, построенная в этот круг, приносит ещё +{config.InvestorBonus} очка.");
            return s;
        }

        static VisualElement GoalsAndLuck(GameConfig config)
        {
            var s = Section("Цели мэрии и удача");
            Paragraph(s, $"В начале партии открываются {config.MayorGoalsDealt} общие цели мэрии — например, «своя дорога в 2 аэропортах» или «3 моста». Награду получает тот, кто выполнит цель первым; остальным она уже не достанется.");
            Paragraph(s, "Когда берёте карты, одну из двух можно заменить картой удачи — не больше одной за ход. Под кнопкой «Удача» видно, сколько бафов и дебафов осталось в колоде.");

            var cards = Ui.Row();
            cards.style.flexWrap = Wrap.Wrap;
            cards.style.justifyContent = Justify.SpaceBetween;
            for (int k = 0; k < LuckInfo.KindCount; k++)
            {
                var kind = (LuckCardKind)k;
                bool buff = LuckInfo.IsBuff(kind);
                var card = Ui.Card(12f);
                card.style.width = 205f;
                card.style.minHeight = 120f;
                card.style.marginTop = 10f;
                card.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(card, 10f);
                Ui.Border(card, 2f, GameView.LuckColor(buff));
                card.Add(Ui.Text((buff ? "БАФ" : "ДЕБАФ") + $" · {LuckInfo.CountIn(config, kind)} шт.", 11f, GameView.LuckColor(buff), true));
                card.Add(Ui.Text(LuckInfo.Title(kind), 15f, Theme.Text, true));
                var text = Ui.Text(LuckInfo.Describe(kind, config), 12f, Theme.TextDim);
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.marginTop = 4f;
                card.Add(text);
                cards.Add(card);
            }
            s.Add(cards);
            return s;
        }

        static VisualElement Deals(GameConfig config)
        {
            var s = Section("Сделки и магистрали");
            Paragraph(s, $"Сделка: раз в ход, до основного действия, предложите одному сопернику обмен — от 1 до {config.TradeMaxCards} карт одного ресурса на 1–{config.TradeMaxCards} карты другого. Он соглашается или отказывается. Ход сделка не тратит.");
            Paragraph(s, $"Магистраль: кликните по своей дороге — её можно улучшить за ход и половину её длины в картах её ресурса. Она приносит +{config.UpgradePoints} очка, а проезд по ней для соперников становится в {config.RushHourTollMultiplier} раза дороже.");
            var diagram = new RuleDiagram(640f, 150f)
                .Place("a", 0.10f, 0.55f, Spot, null)
                .Place("b", 0.50f, 0.55f, Spot, null)
                .Place("c", 0.90f, 0.55f, Spot, null)
                .Road("a", "b", Own, "обычная: проезд " + config.TollPerRoad)
                .Road("b", "c", Own, "магистраль: проезд " + config.TollPerRoad * config.RushHourTollMultiplier, frame: true);
            Picture(s, diagram, "Магистраль на карте — с оранжевой рамкой. Так же выглядят дороги, построенные в час пик.");
            return s;
        }

        static VisualElement InfluenceSection(GameConfig config)
        {
            var s = Section("Влияние");
            Paragraph(s, "Влияние — особая валюта мэрии. В очки оно не превращается: его тратят во время партии, а остаток пригодится в профиле.");
            Paragraph(s, $"Откуда берётся: выполненный контракт +{config.InfluencePerContract} (сложный +{config.InfluencePerComplexContract}); дорога в новый для вашей сети район +{config.InfluencePerNewDistrict} (кроме самой первой дороги); цель мэрии +{config.InfluencePerGoal}; «Грант мэрии» из колоды удачи +{config.LuckGrantInfluence}; в начале круга, если вы отстаёте от лидера на {config.InfluenceCatchUpGap} очков и больше, +{config.InfluenceCatchUp}.");
            Paragraph(s, "Тратить можно раз в ход, до основного действия; ход это не занимает. Кнопка «Влияние» под открытыми картами:");
            foreach (var action in InfluenceInfo.All)
                Paragraph(s, $"• {InfluenceInfo.Title(action)} — {InfluenceInfo.Cost(action, config)} вл. {InfluenceInfo.Describe(action, config)}");
            Paragraph(s, $"В сделках влиянием можно доплатить или купить карты: до {config.TradeMaxInfluence} влияния с каждой стороны.");
            return s;
        }

        static VisualElement CharactersSection(GameConfig config)
        {
            var s = Section("Персонажи");
            Paragraph(s, "У каждой компании свой персонаж со способностью. Его выбирают при создании партии или раздают случайно. Способность написана под именем игрока.");
            int i = 0;
            foreach (var kind in Characters.All)
            {
                s.Add(Tile(Characters.Title(kind), Characters.Ability(kind, config) + " " + Characters.Hint(kind),
                    Palette.PlayerColor(i), new CharacterIcon(44f, kind, Palette.PlayerColor(i))));
                i++;
            }
            return s;
        }

        static VisualElement Ending(GameConfig config)
        {
            var s = Section("Конец партии и очки");
            Paragraph(s, $"Когда у кого-то остаётся {config.FinalRoundPieces} участка дороги или меньше, каждый делает ещё по одному ходу.");
            Paragraph(s, "Итог складывается из:");
            var lines = new[]
            {
                ("Дороги", "очки за каждую построенную дорогу и магистрали"),
                ("Контракты", "выполненные — плюсом, невыполненные — минусом"),
                ("Проезд", "плата владельцам чужих дорог и плата вам за ваши"),
                ("Крупнейшая сеть", $"+{config.NetworkBonus} за связную сеть на больше всего районов"),
                ("Цели мэрии", "награды за цели, выполненные первым"),
                ("Удача", "гранты, премии и штрафы карт удачи"),
                ("Персонаж", "Чиновник — за контракты, Мостовик — за мосты")
            };
            foreach (var (title, text) in lines) s.Add(Tile(title, text, Theme.Accent));
            return s;
        }
    }
}
