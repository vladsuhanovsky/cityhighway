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
            AddGroup("Основы", MenuIcon.Glyph.Book);
            AddSection("Основы", "Цель и раунд", MenuIcon.Glyph.Play, MagnateBasics);
            AddSection("Основы", "Грузы", MenuIcon.Glyph.Coins, MagnateCargo);
            AddSection("Основы", "Дороги и пробки", MenuIcon.Glyph.Road, MagnateRoads);
            AddGroup("Город", MenuIcon.Glyph.Skyline);
            AddSection("Город", "Хабы", MenuIcon.Glyph.Pin, MagnateHubs);
            AddSection("Город", "Заказы мэрии и репутация", MenuIcon.Glyph.Star, MagnateContracts);
            AddSection("Город", "События", MenuIcon.Glyph.Bell, MagnateEvents);
            AddGroup("Партия", MenuIcon.Glyph.Gamepad);
            AddSection("Партия", "Сложность", MenuIcon.Glyph.Gear, MagnateDifficultySection);
            AddSection("Партия", "Панель и карта", MenuIcon.Glyph.Monitor, MagnateInterface);
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
            var s = Section("Цель и раунд");
            Para(s, $"Вы — владелец транспортной компании. У вас {Magnate.TrucksPerPlayer} фуры: они забирают грузы на хабах — в аэропортах, на вокзалах и в портах — " +
                     $"и везут получателям. За каждую доставку дают очки. После {Magnate.Rounds} раундов побеждает компания с наибольшим счётом.");
            Para(s, "Город каждый раз новый: реки, мосты, районы и хабы создаются при запуске партии. Серые городские дороги есть с начала — " +
                     "они общие и бесплатные для всех. Пунктиром на карте показаны дороги, которые ещё можно построить.");

            // Схема раунда: четыре шага.
            var flow = Ui.Row();
            flow.style.marginTop = 14f;
            var steps = new[]
            {
                ("1. Стройка", $"По очереди. +{Magnate.BuildPointsPerRound} очков стройки (копятся до {Magnate.MaxBuildPoints}): купить дорогу, сделать свою магистралью, развить хаб.", Theme.Gold),
                ("2. Маршруты", "По очереди и тайно: каждая фура берёт груз на своём хабе или едет к станции.", new Color(0.45f, 0.70f, 1f)),
                ("3. Движение", $"Все фуры едут одновременно, до {Magnate.TruckSpeed} участков за раунд. Пробки, доставки, оплата проезда.", GameColors.Success),
                ("4. Новый раунд", $"Новые грузы на хабах, +{Magnate.InfluencePerRound} влияния, иногда — событие города.", new Color(0.75f, 0.60f, 0.98f)),
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
            Para(s, "Фура, у которой маршрут длиннее одного раунда, продолжит путь в следующем. Маршрут можно поменять в любом раунде, " +
                     "пока груз не доставлен.");
            return s;
        }

        VisualElement MagnateCargo()
        {
            var s = Section("Грузы");
            Para(s, $"Грузы появляются на хабах. Над хабом на карте — квадратики цвета груза. Невывезенный груз исчезает через {Magnate.CargoLifetime} раундов. " +
                     "Цена груза зависит от расстояния и вида, а репутация компании умножает очки за доставку.");
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
                s.Add(PlainTile(LogisticsGame.KindTitle(kind), LogisticsGame.KindRule(kind, Magnate), MagnateView.KindColor(kind), KindChip(kind, 30f)));
            Para(s, $"Скорость: обычная фура проезжает {Magnate.TruckSpeed} участков за раунд, со скоропортом — на 1 больше, с тяжеловесом и негабаритом — на 1 меньше.");
            return s;
        }

        VisualElement MagnateRoads()
        {
            var s = Section("Дороги и пробки");
            Para(s, $"Купить свободную дорогу стоит столько очков стройки, какова её длина; мост — на {Magnate.BridgeExtraCost} дороже. Свою дорогу можно сделать " +
                     "магистралью — она пропускает больше фур.");
            Para(s, $"По своим дорогам ваши фуры ездят бесплатно, по чужим — платят владельцу {Magnate.TollPerRoad} очко за каждую дорогу маршрута. " +
                     "Серые городские дороги бесплатны для всех.");
            var capacity = Ui.Row();
            capacity.style.marginTop = 6f;
            foreach (var (title, value, color) in new[]
            {
                ("Улица", Magnate.CapacityStreet, new Color(0.80f, 0.82f, 0.86f)),
                ("Широкая дорога и мост", Magnate.CapacityWide, new Color(0.45f, 0.70f, 1f)),
                ("Магистраль", Magnate.CapacityHighway, Theme.Gold),
            })
            {
                var tile = PlainTile(title, $"пропускает {value} фуры за раунд", color);
                tile.style.flexGrow = 1f;
                tile.style.flexBasis = 0f;
                tile.style.marginRight = 8f;
                capacity.Add(tile);
            }
            s.Add(capacity);
            s.Add(PlainTile("Пробка", $"Если за раунд на дорогу заехало больше фур, чем она пропускает, — все эти фуры стоят до следующего раунда. " +
                                       $"Каждая теряет {Magnate.JamPenalty} очко и немного репутации, хрупкий груз дешевеет на {Magnate.FragileJamPenalty}. " +
                                       "Над очередью на карте крутятся часы.", GameColors.Danger, new MenuIcon(MenuIcon.Glyph.Clock, 26f, GameColors.Danger)));
            Para(s, "Широкие дороги — бетонные, мосты и магистрали. Тяжеловес и негабарит едут только по ним. Негабарит к тому же перекрывает участок " +
                     "для всех, кто въезжает туда следом в том же раунде.");
            Para(s, "Совет: наведите курсор на дорогу — подсказка покажет владельца, пропускную способность и сколько фур проехало по ней в прошлом раунде. " +
                     "Слой «Загрузка дорог» раскрашивает дороги по этой загрузке.");
            return s;
        }

        VisualElement MagnateHubs()
        {
            var s = Section("Хабы");
            Para(s, "Хабы — аэропорты, вокзалы и порты. Только на них появляются грузы. В фазе стройки хаб можно развить: кликните по нему на карте.");
            if (HubEventIcons.Hub(HubKind.Airport, 40f) != null)
            {
                var kinds = Ui.Row();
                kinds.style.marginTop = 8f;
                foreach (var (kind, name) in new[] { (HubKind.Airport, "Аэропорт: скоропорт, ценные, хрупкие"), (HubKind.Station, "Вокзал: обычные и тяжеловесы"), (HubKind.Port, "Порт: тяжеловесы, негабарит, опасные") })
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
                tile.Add(Ui.Text(level == 1 ? "с начала" : $"{Magnate.HubUpgradeCost[level]} очков", 12f, Theme.TextDim));
                string look = level == 1 ? "терминал" : level == 2 ? "склады" : level == 3 ? "кран" : level == 4 ? "ещё склады" : "комплекс";
                tile.Add(Ui.Text(look, 11f, Theme.TextDim));
                levels.Add(tile);
            }
            s.Add(levels);
            Para(s, "Чем выше уровень, тем больше грузов может ждать на хабе (уровень + 1), тем чаще они появляются и тем дороже стоят.");
            s.Add(PlainTile("Владелец хаба", $"Кто первым развил хаб, тот им и владеет: дальше развивать его может только он. Его фуры грузятся первыми, " +
                                              $"а с каждой погрузки чужой фуры он получает {Magnate.HubFee} очко.", Theme.Gold,
                new MenuIcon(MenuIcon.Glyph.Pin, 26f, Theme.Gold)));
            Para(s, "В 3D-городе рядом со станцией растёт комплекс: склады, кран, у аэропорта на пятом уровне — вертолётная площадка. " +
                     "На карте видно кольцо цвета владельца и точки уровня.");
            return s;
        }

        VisualElement MagnateContracts()
        {
            var s = Section("Заказы мэрии и репутация");
            Para(s, $"Одновременно действуют {Magnate.ActiveContracts} заказа мэрии, например «доставьте 3 скоропорта до раунда N». Прогресс всех компаний виден " +
                     "в разделе «Заказы мэрии» внизу экрана. Первая выполнившая компания получает очки, влияние и репутацию; остальные — ничего.");
            Para(s, "Репутация растёт за доставки вовремя (+3) и заказы мэрии (+5), падает за пробки (−1) и потерянные грузы (−4). " +
                     "Уровень репутации умножает очки за каждую доставку:");
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
            Para(s, "Звёзды рядом с именем компании в верхней строке — её уровень репутации.");
            return s;
        }

        VisualElement MagnateEvents()
        {
            var s = Section("События");
            Para(s, $"В начале раунда с шансом {Mathf.RoundToInt((float)Magnate.EventChance * 100f)}% (на обычной сложности) происходит событие города. " +
                     "Оно видно баннером и плашкой в верхней строке. Фуры, чей маршрут шёл через закрытую дорогу, сами ищут объезд.");
            var red = GameColors.Danger;
            s.Add(PlainTile("ДТП", "Фура сломалась на одной из самых загруженных дорог: дорога закрыта на раунд. На карте — шлагбаум и гаечный ключ.", red,
                EventIcon(LogisticsEventKind.Accident, new MenuIcon(MenuIcon.Glyph.Flame, 24f, red))));
            s.Add(PlainTile("Ремонт дороги", "Одна из построенных дорог закрыта на 2 раунда.", new Color(1f, 0.6f, 0.2f),
                EventIcon(LogisticsEventKind.RoadRepair, new MenuIcon(MenuIcon.Glyph.Road, 24f, new Color(1f, 0.6f, 0.2f)))));
            s.Add(PlainTile("Нехватка топлива", "Все фуры проезжают на 2 участка меньше.", new Color(0.95f, 0.75f, 0.3f),
                EventIcon(LogisticsEventKind.FuelShortage, new MenuIcon(MenuIcon.Glyph.Clock, 24f, new Color(0.95f, 0.75f, 0.3f)))));
            s.Add(PlainTile("Таможенная проверка", $"Опасные и ценные грузы в пути платят {Magnate.CustomsBribe} влияния — иначе стоят раунд.", new Color(0.75f, 0.60f, 0.98f),
                EventIcon(LogisticsEventKind.CustomsCheck, new MenuIcon(MenuIcon.Glyph.Document, 24f, new Color(0.75f, 0.60f, 0.98f)))));
            s.Add(PlainTile("Штормовое предупреждение", "Мосты пропускают на одну фуру меньше, тяжеловесы и негабарит по мостам не едут.", new Color(0.45f, 0.70f, 1f),
                EventIcon(LogisticsEventKind.Storm, new MenuIcon(MenuIcon.Glyph.Snow, 24f, new Color(0.45f, 0.70f, 1f)))));
            s.Add(PlainTile("Фестиваль", "Улицы Центра перекрыты на раунд.", GameColors.Success,
                EventIcon(LogisticsEventKind.Festival, new MenuIcon(MenuIcon.Glyph.Star, 24f, GameColors.Success))));
            return s;
        }

        VisualElement MagnateDifficultySection()
        {
            var s = Section("Сложность");
            Para(s, "Сложность выбирается в «Новой игре» (строка «Сложность» видна только у магната). Она меняет частоту грузов, награды, события и штрафы:");
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
            Para(s, "За победу на сложности «Эксперт» — отдельное достижение.");
            return s;
        }

        VisualElement MagnateInterface()
        {
            var s = Section("Панель и карта");
            s.Add(PlainTile("Нижняя панель", "Слева — что делать сейчас: стройка с кнопкой «Дальше», маршруты фур с кнопкой «Готово», ход соперника. " +
                                            "Дальше — «Грузы [N]» и «Заказы мэрии [N]»: клик по разделу открывает окно над панелью. «Правила» и «Журнал» — кнопки в верхней строке.",
                Theme.Gold, new MenuIcon(MenuIcon.Glyph.Monitor, 24f, Theme.Gold)));
            s.Add(PlainTile("Грузы и фуры", "В окне «Грузы» кнопка «Выбрать» назначает груз ближайшей свободной фуре. Клик по строке груза — карточка: правило, путь на карте, " +
                                           "время, плата, сколько очков принесёт. Клик по фуре внизу — её карточка с грузами на её хабе.",
                new Color(0.45f, 0.70f, 1f), new MenuIcon(MenuIcon.Glyph.Road, 24f, new Color(0.45f, 0.70f, 1f))));
            s.Add(PlainTile("Карта", "Наведите на станцию — увидите хаб, грузы и (если выбрана фура) путь туда со временем и платой; клик — ехать. " +
                                    "Вдали от курсора карта тускнеет. «Слои карты» справа вверху прячут подписи, грузы, фуры и свободные дороги или включают загрузку дорог.",
                GameColors.Success, new MenuIcon(MenuIcon.Glyph.Pin, 24f, GameColors.Success)));
            s.Add(PlainTile("Обучение", "В первой партии наставник ведёт по первому раунду. Открыть его снова — кнопка «? Обучение» вверху или раздел «Обучение» в меню.",
                new Color(0.75f, 0.60f, 0.98f), new MenuIcon(MenuIcon.Glyph.Cap, 24f, new Color(0.75f, 0.60f, 0.98f))));
            return s;
        }
    }
}
