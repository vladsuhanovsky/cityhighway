using System;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Правила «Логистического магната» для страницы «Правила». Числа берутся из MagnateConfig (обычная сложность),
    /// описания грузов — из LogisticsGame.KindRule, как в самой партии. Термины классики здесь не подсвечиваются:
    /// у магната свои правила, и определения из глоссария соревнования сбивали бы с толку.
    /// </summary>
    sealed partial class RulesView
    {
        static readonly MagnateConfig Magnate = MagnateConfig.For(MagnateDifficulty.Normal);

        void AddMagnateSections()
        {
            AddGroup(LocalizationService.Get("rules.magnate.label.001"), MenuIcon.Glyph.Book);
            AddSection(LocalizationService.Get("rules.magnate.label.001"), LocalizationService.Get("rules.magnate.label.002"), MenuIcon.Glyph.Play, MagnateBasics);
            AddSection(LocalizationService.Get("rules.magnate.label.001"), LocalizationService.Get("rules.magnate.label.003"), MenuIcon.Glyph.Coins, MagnateCargo);
            AddSection(LocalizationService.Get("rules.magnate.label.001"), LocalizationService.Get("rules.magnate.label.004"), MenuIcon.Glyph.Road, MagnateRoads);
            AddGroup(LocalizationService.Get("rules.magnate.label.005"), MenuIcon.Glyph.Skyline);
            AddSection(LocalizationService.Get("rules.magnate.label.005"), LocalizationService.Get("rules.magnate.label.006"), MenuIcon.Glyph.Pin, MagnateHubs);
            AddSection(LocalizationService.Get("rules.magnate.label.005"), LocalizationService.Get("rules.magnate.label.007"), MenuIcon.Glyph.Star, MagnateContracts);
            AddSection(LocalizationService.Get("rules.magnate.label.005"), LocalizationService.Get("rules.magnate.label.008"), MenuIcon.Glyph.Bell, MagnateEvents);
            AddGroup(LocalizationService.Get("rules.magnate.label.009"), MenuIcon.Glyph.Gamepad);
            AddSection(LocalizationService.Get("rules.magnate.label.009"), LocalizationService.Get("rules.magnate.label.010"), MenuIcon.Glyph.Gear, MagnateDifficultySection);
            AddSection(LocalizationService.Get("rules.magnate.label.009"), LocalizationService.Get("rules.magnate.label.011"), MenuIcon.Glyph.Monitor, MagnateInterface);
        }

        void Para(VisualElement parent, string text)
        {
            var label = Ui.Text(text, 15f, Theme.Text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 10f;
            Ui.Radius(label, 6f);
            parent.Add(label);
        }

        static VisualElement PlainTile(string title, string text, Color color, VisualElement icon = null)
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

        /// <summary>Иконка события из листа Scenario (если есть), иначе запасной значок.</summary>
        static VisualElement EventIcon(LogisticsEventKind kind, VisualElement fallback) => HubEventIcons.Event(kind, 34f) ?? fallback;

        static Label KindChip(CargoKind kind, float size) => CargoIcons.Chip(kind, size, 0.55f, 6f);

        // ================================================================== Разделы

        VisualElement MagnateBasics()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.002"));
            Para(s, $"Вы — владелец транспортной компании. У вас {Magnate.TrucksPerPlayer} фуры: они забирают грузы на хабах — в аэропортах, на вокзалах и в портах — " +
                     $"и везут получателям. За каждую доставку дают очки. После {Magnate.Rounds} раундов перевозок побеждает компания с наибольшим счётом.");
            Para(s, $"Первый раунд — раунд стройки: каждая компания по очереди строит {(Magnate.OpeningRoads == 1 ? "одну дорогу" : Magnate.OpeningRoads + " дороги")} " +
                     "(магистрали и хабы — позже), фуры стоят. Грузы на хабах уже видны — по ним и выбирают, куда тянуть дорогу. " +
                     "Маршруты фур — со второго раунда, тогда же появляются заказы мэрии.");
            Para(s, LocalizationService.Get("rules.magnate.text.001") +
                     LocalizationService.Get("rules.magnate.text.002") +
                     LocalizationService.Get("rules.magnate.text.003") +
                     LocalizationService.Get("rules.magnate.text.004"));
            Para(s, LocalizationService.Get("rules.magnate.text.005") +
                     LocalizationService.Get("rules.magnate.text.006"));

            // Схема раунда: четыре шага.
            var flow = Ui.Row();
            flow.style.marginTop = 14f;
            var steps = new[]
            {
                (LocalizationService.Get("rules.magnate.label.012"), $"По очереди, первыми — отстающие. +{Magnate.BuildPointsPerRound} очков стройки (копятся до {Magnate.MaxBuildPoints}): купить дорогу, сделать свою магистралью, развить хаб.", Theme.Gold),
                (LocalizationService.Get("rules.magnate.label.013"), "По очереди и тайно: каждая фура берёт груз на своём хабе или едет к станции.", new Color(0.45f, 0.70f, 1f)),
                (LocalizationService.Get("rules.magnate.label.014"), $"Все фуры едут одновременно, до {Magnate.TruckSpeed} участков за раунд. Пробки, доставки, оплата проезда.", GameColors.Success),
                (LocalizationService.Get("rules.magnate.label.015"), $"Новые грузы на хабах, +{Magnate.InfluencePerRound} влияния, иногда — событие города.", new Color(0.75f, 0.60f, 0.98f)),
            };
            foreach (var (title, text, color) in steps)
            {
                var tile = PlainTile(title, text, color);
                tile.style.flexGrow = 1f;
                tile.style.flexBasis = 0f;
                tile.style.marginRight = 8f;
                tile.style.alignItems = Align.FlexStart;
                flow.Add(tile);
            }
            s.Add(flow);
            Para(s, $"Влияние: в начале {Magnate.StartInfluence}, каждый раунд +{Magnate.InfluencePerRound}. Оно нужно на взятку за опасный груз в Центре " +
                     $"({Magnate.HazardBribe}), охрану ценного груза ({Magnate.EscortCost}) и таможенную проверку ({Magnate.CustomsBribe}).");
            Para(s, $"За каждый доставленный опасный груз компания получает ещё одну фуру — она появляется там, где груз сдан. " +
                     $"Больше {Magnate.MaxTrucksPerPlayer} фур у компании не бывает.");
            Para(s, "Фура, у которой маршрут длиннее одного раунда, продолжит путь в следующем. Маршрут можно поменять в любом раунде, " +
                     "пока груз не доставлен.");
            return s;
        }


        static string MagnateCargoRule(CargoKind kind)
        {
            string key = "rules.magnate.cargo." + kind + ".rule";
            switch (kind)
            {
                case CargoKind.Perishable: return string.Format(LocalizationService.Get(key), Magnate.PerishableLatePenalty);
                case CargoKind.Hazard: return string.Format(LocalizationService.Get(key), Magnate.HazardBribe, Magnate.ConfiscationPenalty, Magnate.MaxTrucksPerPlayer);
                case CargoKind.Fragile: return string.Format(LocalizationService.Get(key), Magnate.FragileJamPenalty);
                case CargoKind.Valuable: return string.Format(LocalizationService.Get(key), Magnate.EscortCost);
                default: return LocalizationService.Get(key);
            }
        }

        VisualElement MagnateCargo()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.003"));
            Para(s, $"Грузы появляются на хабах. Над хабом на карте — квадратики цвета груза. Невывезенный груз исчезает через {Magnate.CargoLifetime} раундов. " +
                     "Цена груза зависит от расстояния и вида, а репутация компании умножает очки за доставку.");
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
                s.Add(PlainTile(LocalizationService.Get("rules.magnate.cargo." + kind + ".name"), MagnateCargoRule(kind), MagnateView.KindColor(kind), KindChip(kind, 30f)));
            Para(s, $"Скорость: обычная фура проезжает {Magnate.TruckSpeed} участков за раунд, со скоропортом — на 1 больше, с тяжеловесом и крупногабаритом — на 1 меньше.");
            return s;
        }

        VisualElement MagnateRoads()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.004"));
            Para(s, $"Купить свободную дорогу стоит столько очков стройки, какова её длина; мост — на {Magnate.BridgeExtraCost} дороже. Свою дорогу можно сделать " +
                     "магистралью — она пропускает больше фур.");
            Para(s, $"По своим дорогам ваши фуры ездят бесплатно, по чужим — платят владельцу {Magnate.TollPerRoad} очко за каждую дорогу маршрута. " +
                     "Серые городские дороги бесплатны для всех.");
            Para(s, $"Свои дороги ещё и быстрее: дорогу длиной от 2 участков ваша фура проходит на {Magnate.OwnRoadDiscount} участок быстрее. " +
                     "Сеть своих дорог — это и плата с соперников, и скорость.");
            var capacity = Ui.Row();
            capacity.style.marginTop = 6f;
            foreach (var (title, value, color) in new[]
            {
                (LocalizationService.Get("rules.magnate.label.016"), Magnate.CapacityStreet, new Color(0.80f, 0.82f, 0.86f)),
                (LocalizationService.Get("rules.magnate.label.017"), Magnate.CapacityWide, new Color(0.45f, 0.70f, 1f)),
                (LocalizationService.Get("rules.magnate.label.018"), Magnate.CapacityHighway, Theme.Gold),
            })
            {
                var tile = PlainTile(title, $"пропускает {value} фуры за раунд", color);
                tile.style.flexGrow = 1f;
                tile.style.flexBasis = 0f;
                tile.style.marginRight = 8f;
                capacity.Add(tile);
            }
            s.Add(capacity);
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.019"), $"Если за раунд на дорогу заехало больше фур, чем она пропускает, — все эти фуры стоят до следующего раунда. " +
                                       $"Каждая теряет {Magnate.JamPenalty} очко и немного репутации, хрупкий груз дешевеет на {Magnate.FragileJamPenalty}. " +
                                       "Над очередью на карте крутятся часы.", GameColors.Danger, new MenuIcon(MenuIcon.Glyph.Clock, 26f, GameColors.Danger)));
            Para(s, LocalizationService.Get("rules.magnate.text.007") +
                     LocalizationService.Get("rules.magnate.text.008"));
            Para(s, LocalizationService.Get("rules.magnate.text.009") +
                     LocalizationService.Get("rules.magnate.text.010"));
            return s;
        }

        VisualElement MagnateHubs()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.006"));
            Para(s, LocalizationService.Get("rules.magnate.text.011"));
            if (HubEventIcons.Hub(HubKind.Airport, 40f) != null)
            {
                var kinds = Ui.Row();
                kinds.style.marginTop = 8f;
                foreach (var (kind, name) in new[] { (HubKind.Airport, "Аэропорт: скоропорт, ценные, хрупкие"), (HubKind.Station, "Вокзал: обычные и тяжеловесы"), (HubKind.Port, "Порт: тяжеловесы, крупногабарит, опасные") })
                {
                    var item = Ui.Row();
                    item.style.alignItems = Align.Center;
                    item.style.marginRight = 18f;
                    var icon = HubEventIcons.Hub(kind, 40f);
                    icon.style.marginRight = 8f;
                    item.Add(icon);
                    var label = Ui.Text(name, 13f, Theme.Text);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.maxWidth = 170f;
                    item.Add(label);
                    kinds.Add(item);
                }
                s.Add(kinds);
            }
            var levels = Ui.Row();
            levels.style.marginTop = 10f;
            for (int level = 1; level <= 5; level++)
            {
                var tile = Ui.Column();
                tile.style.flexGrow = 1f;
                tile.style.flexBasis = 0f;
                tile.style.marginRight = 6f;
                tile.style.alignItems = Align.Center;
                tile.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(tile, 10f);
                Ui.Padding(tile, 10f, 8f);
                tile.Add(Ui.Text($"Уровень {level}", 14f, level == 1 ? Theme.Text : Theme.Gold, true));
                tile.Add(Ui.Text(new string('●', level), 12f, Theme.Gold));
                tile.Add(Ui.Text(level == 1 ? LocalizationService.Get("rules.magnate.label.036") : $"{Magnate.HubUpgradeCost[level]} очков", 12f, Theme.TextDim));
                string look = level == 1 ? LocalizationService.Get("rules.magnate.label.031") : level == 2 ? LocalizationService.Get("rules.magnate.label.032") : level == 3 ? LocalizationService.Get("rules.magnate.label.033") : level == 4 ? LocalizationService.Get("rules.magnate.label.034") : LocalizationService.Get("rules.magnate.label.035");
                tile.Add(Ui.Text(look, 11f, Theme.TextDim));
                levels.Add(tile);
            }
            s.Add(levels);
            Para(s, LocalizationService.Get("rules.magnate.text.012"));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.020"), $"Развивать хаб может любая компания. Владеет им та, что вложила больше очков стройки " +
                                              "(при равенстве хаб остаётся у прежнего владельца) — хаб можно перехватить. Фуры владельца грузятся первыми, " +
                                              $"а с каждой погрузки чужой фуры он получает {Magnate.HubFee} очко.", Theme.Gold,
                new MenuIcon(MenuIcon.Glyph.Pin, 26f, Theme.Gold)));
            Para(s, LocalizationService.Get("rules.magnate.text.013") +
                     LocalizationService.Get("rules.magnate.text.014"));
            return s;
        }

        VisualElement MagnateContracts()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.007"));
            Para(s, $"Одновременно действуют {Magnate.ActiveContracts} заказа мэрии, например «доставьте 3 скоропорта до раунда N». Прогресс всех компаний виден " +
                     "в разделе «Заказы мэрии» внизу экрана. Первая выполнившая компания получает очки, влияние и репутацию, вторая — половину очков и влияния; после второй заказ закрывается.");
            Para(s, LocalizationService.Get("rules.magnate.text.015") +
                     LocalizationService.Get("rules.magnate.text.016"));
            var ranks = new[] { ReputationLevel.Novice, ReputationLevel.Amateur, ReputationLevel.Professional, ReputationLevel.Master, ReputationLevel.Legendary };
            int[] thresholds = { 0, 25, 50, 75, 100 };
            var row = Ui.Row();
            row.style.marginTop = 8f;
            for (int i = 0; i < ranks.Length; i++)
            {
                var tile = Ui.Column();
                tile.style.flexGrow = 1f;
                tile.style.flexBasis = 0f;
                tile.style.marginRight = 6f;
                tile.style.alignItems = Align.Center;
                tile.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(tile, 10f);
                Ui.Padding(tile, 10f, 8f);
                tile.Add(Ui.Text(new string('★', i + 1), 14f, Theme.Gold));
                tile.Add(Ui.Text(LogisticsGame.LevelTitle(ranks[i]), 14f, Theme.Text, true));
                tile.Add(Ui.Text($"от {thresholds[i]}", 11f, Theme.TextDim));
                tile.Add(Ui.Text($"×{LogisticsGame.LevelMultiplier(ranks[i]):0.##}", 15f, Theme.Gold, true));
                row.Add(tile);
            }
            s.Add(row);
            Para(s, LocalizationService.Get("rules.magnate.text.017"));
            return s;
        }

        VisualElement MagnateEvents()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.008"));
            Para(s, $"В начале раунда с шансом {Mathf.RoundToInt((float)Magnate.EventChance * 100f)}% (на обычной сложности) происходит событие города. " +
                     "Оно видно баннером и плашкой в верхней строке. Фуры, чей маршрут шёл через закрытую дорогу, сами ищут объезд.");
            var red = GameColors.Danger;
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.021"), LocalizationService.Get("rules.magnate.text.018"), red,
                EventIcon(LogisticsEventKind.Accident, new MenuIcon(MenuIcon.Glyph.Flame, 24f, red))));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.022"), LocalizationService.Get("rules.magnate.text.019"), new Color(1f, 0.6f, 0.2f),
                EventIcon(LogisticsEventKind.RoadRepair, new MenuIcon(MenuIcon.Glyph.Road, 24f, new Color(1f, 0.6f, 0.2f)))));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.023"), LocalizationService.Get("rules.magnate.text.020"), new Color(0.95f, 0.75f, 0.3f),
                EventIcon(LogisticsEventKind.FuelShortage, new MenuIcon(MenuIcon.Glyph.Clock, 24f, new Color(0.95f, 0.75f, 0.3f)))));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.024"), $"Опасные и ценные грузы в пути платят {Magnate.CustomsBribe} влияния — иначе стоят раунд.", new Color(0.75f, 0.60f, 0.98f),
                EventIcon(LogisticsEventKind.CustomsCheck, new MenuIcon(MenuIcon.Glyph.Document, 24f, new Color(0.75f, 0.60f, 0.98f)))));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.025"), LocalizationService.Get("rules.magnate.text.021"), new Color(0.45f, 0.70f, 1f),
                EventIcon(LogisticsEventKind.Storm, new MenuIcon(MenuIcon.Glyph.Snow, 24f, new Color(0.45f, 0.70f, 1f)))));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.026"), LocalizationService.Get("rules.magnate.text.022"), GameColors.Success,
                EventIcon(LogisticsEventKind.Festival, new MenuIcon(MenuIcon.Glyph.Star, 24f, GameColors.Success))));
            return s;
        }

        VisualElement MagnateDifficultySection()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.010"));
            Para(s, LocalizationService.Get("rules.magnate.text.024"));
            foreach (MagnateDifficulty level in Enum.GetValues(typeof(MagnateDifficulty)))
            {
                var c = MagnateConfig.For(level);
                var color = level == MagnateDifficulty.Easy ? GameColors.Success : level == MagnateDifficulty.Normal ? Theme.Gold
                    : level == MagnateDifficulty.Hard ? new Color(1f, 0.6f, 0.2f) : GameColors.Danger;
                s.Add(PlainTile(MagnateConfig.Title(level),
                    $"грузы появляются с шансом {Mathf.RoundToInt((float)c.SpawnChance * 100f)}% · награды ×{c.RewardMultiplier:0.#} · " +
                    $"события {Mathf.RoundToInt((float)c.EventChance * 100f)}% · кража ценного груза {Mathf.RoundToInt((float)c.TheftChance * 100f)}% · " +
                    $"пробка −{c.JamPenalty} · стройка: {c.StartBuildPoints} на старте, +{c.BuildPointsPerRound} за раунд", color));
            }
            Para(s, LocalizationService.Get("rules.magnate.text.023"));
            return s;
        }

        VisualElement MagnateInterface()
        {
            var s = Section(LocalizationService.Get("rules.magnate.label.011"));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.027"), "Слева — что делать сейчас: стройка с кнопкой «Дальше», маршруты фур с кнопкой «Готово», ход соперника. " +
                                            "Дальше — «Грузы [N]» и «Заказы мэрии [N]»: клик по разделу открывает окно над панелью. «Правила» и «Журнал» — кнопки в верхней строке.",
                Theme.Gold, new MenuIcon(MenuIcon.Glyph.Monitor, 24f, Theme.Gold)));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.028"), "В окне «Грузы» кнопка «Выбрать» назначает груз ближайшей свободной фуре. Клик по строке груза — карточка: правило, путь на карте, " +
                                           "время, плата, сколько очков принесёт. Клик по фуре внизу — её карточка с грузами на её хабе.",
                new Color(0.45f, 0.70f, 1f), new MenuIcon(MenuIcon.Glyph.Road, 24f, new Color(0.45f, 0.70f, 1f))));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.029"), "Наведите на станцию — увидите хаб, грузы и (если выбрана фура) путь туда со временем и платой; клик — ехать. " +
                                    "Вдали от курсора карта тускнеет. «Слои карты» справа вверху прячут подписи, грузы, фуры и свободные дороги или включают загрузку дорог.",
                GameColors.Success, new MenuIcon(MenuIcon.Glyph.Pin, 24f, GameColors.Success)));
            s.Add(PlainTile(LocalizationService.Get("rules.magnate.label.030"), "В первой партии наставник ведёт по первому раунду. Открыть его снова — «Меню» → «? Обучение» в правом верхнем углу партии или раздел «Обучение» в главном меню.",
                new Color(0.75f, 0.60f, 0.98f), new MenuIcon(MenuIcon.Glyph.Cap, 24f, new Color(0.75f, 0.60f, 0.98f))));
            return s;
        }
    }
}
