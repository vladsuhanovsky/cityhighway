using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Нижняя панель магната и всплывающие окна. Карта занимает весь экран между верхней строкой и панелью.
    /// В панели четыре раздела:
    /// - контекстный — меняется по ходу: «Стройка» (очки, «Дальше»), «Маршруты» (фуры и «Готово»), ход бота,
    ///   фуры в пути, итог;
    /// - «Грузы [N]» — сколько грузов ждёт на хабах, срочные, по видам;
    /// - «Заказы [N]» — действующие заказы мэрии и ваш прогресс.
    /// Справа в верхней строке — «Журнал», «Меню» (обучение, правила, выход) и «Настройки» (MagnateView.Menu.cs);
    /// окна журнала и меню открываются под ней.
    /// Клик по разделу открывает окно над панелью (одно за раз): список грузов с кнопкой «Выбрать», карточку
    /// груза (правило, маршрут, время, плата, сколько очков принесёт, путь подсвечен на карте), заказы с
    /// прогрессом компаний, правила, журнал, карточку выбранной фуры.
    /// </summary>
    public sealed partial class MagnateView
    {
        const float BottomHeight = 132f, PopupWidth = 460f;
        /// <summary>Иконки грузов: на нижней панели и в панелях «Грузы» и «Заказы мэрии» (там — одного размера).</summary>
        const float CargoIconBottom = 32f, CargoIconPanel = 28f;
        /// <summary>«Раздел» окон правил и журнала: они открываются под верхней строкой, у её кнопок.</summary>
        const int TopSection = 9;
        /// <summary>Разделы нижней панели слева направо.</summary>
        const int PlayersSection = 0, ContextSection = 1, CargoSection = 2, OrdersSection = 3, LastSection = OrdersSection;

        VisualElement _bottom, _popup, _popupHead, _popupBody;
        Label _popupTitle;
        readonly List<VisualElement> _sections = new List<VisualElement>();
        /// <summary>Открытое окно: cargo, cargoDetail, contracts, rules, log, truck; null — закрыто.</summary>
        string _popupKind;
        string _popupCargo;
        int _popupSection;
        /// <summary>Путь груза из открытой карточки груза — подсвечен на карте золотом.</summary>
        List<string> _popupPreview;
        /// <summary>Путь груза, который сейчас не проехать (красным): дороги, которые мешают, — ярче.</summary>
        List<string> _popupBlocked;
        CargoKind? _popupBlockedKind;

        // ================================================================== Каркас

        void BuildBottomBar()
        {
            _bottom = Ui.Row();
            _bottom.style.position = Position.Absolute;
            _bottom.style.left = 0f;
            _bottom.style.right = 0f;
            _bottom.style.bottom = 0f;
            _bottom.style.height = BottomHeight;
            // Все разделы одной высоты — по высоте панели, а не по содержимому.
            _bottom.style.alignItems = Align.Stretch;
            _bottom.style.backgroundColor = Theme.Panel;
            Ui.Padding(_bottom, 10f, 12f);
            Root.Add(_bottom);

            _popup = new VisualElement();
            _popup.style.position = Position.Absolute;
            _popup.style.bottom = BottomHeight + 8f;
            _popup.style.width = PopupWidth;
            _popup.style.maxHeight = 560f;
            _popup.style.backgroundColor = new Color(0.11f, 0.13f, 0.17f, 0.98f);
            Ui.Radius(_popup, 12f);
            Ui.Border(_popup, 1f, Theme.Line);
            Ui.Shadow(_popup, 14f);
            _popupHead = Ui.Row();
            _popupHead.style.alignItems = Align.Center;
            _popupHead.style.justifyContent = Justify.SpaceBetween;
            Ui.Padding(_popupHead, 10f, 14f);
            _popupHead.style.borderBottomWidth = 1f;
            _popupHead.style.borderBottomColor = Theme.Line;
            _popupTitle = Ui.Text("", 15f, Theme.Text, true);
            _popupTitle.style.flexShrink = 1f;
            _popupHead.Add(_popupTitle);
            var close = Ui.Button(LocalizationService.Get("magnate.panel.001"), ClosePopup, ButtonKind.Ghost, 12f);
            _popupHead.Add(close);
            _popup.Add(_popupHead);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1f;
            Ui.StyleScroller(scroll);
            _popupBody = scroll.contentContainer;
            _popupBody.style.paddingTop = 8f;
            _popupBody.style.paddingBottom = 12f;
            _popup.Add(scroll);
            Ui.Show(_popup, false);
            Root.Add(_popup);
        }

        VisualElement Section(int index, float grow, Action onClick)
        {
            var card = new VisualElement();
            card.style.flexGrow = grow;
            card.style.flexBasis = 0f;
            card.style.marginRight = index < LastSection ? 10f : 0f;
            card.style.backgroundColor = Theme.PanelLight;
            card.style.justifyContent = Justify.SpaceBetween;
            Ui.Padding(card, 8f, 12f);
            Ui.Radius(card, 10f);
            bool open = _popupKind != null && _popupSection == index;
            Ui.Border(card, open ? 2f : 1f, open ? GameColors.Accent : Theme.Line);
            if (onClick != null)
            {
                card.pickingMode = PickingMode.Position;
                card.name = "gamepad-magnate-section-" + index;
                card.RegisterCallback<ClickEvent>(_ => onClick());
                GamepadSelectable.Attach(card, onClick);
                card.RegisterCallback<PointerEnterEvent>(_ => card.style.backgroundColor = Theme.PanelHover);
                card.RegisterCallback<PointerLeaveEvent>(_ => card.style.backgroundColor = Theme.PanelLight);
            }
            return card;
        }

        static VisualElement Header(string title, Color color, string counter = null)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            var label = Ui.Text(title, 14f, color, true);
            label.style.flexShrink = 1f;
            row.Add(label);
            if (counter != null)
            {
                var badge = Ui.Text(counter, 12f, new Color(0.08f, 0.08f, 0.1f), true);
                badge.style.backgroundColor = Theme.Gold;
                Ui.Padding(badge, 1f, 7f);
                Ui.Radius(badge, 8f);
                badge.style.marginLeft = 8f;
                row.Add(badge);
            }
            return row;
        }

        // ================================================================== Обновление

        void Refresh()
        {
            _roundLabel.text = En ? $"{Place(_game.Map.Name)} · round {_game.Round}/{_game.TotalRounds}{(_game.IsOpeningRound ? " · construction" : "")} · {DifficultyName(_game.Config.Difficulty)}" : $"{_game.Map.Name} · раунд {_game.Round}/{_game.TotalRounds}{(_game.IsOpeningRound ? " · стройка" : "")} · {MagnateConfig.Title(_game.Config.Difficulty)}";
            RefreshEventChip();
            RefreshPlayers();
            if (_popupKind == "truck" && (!HumanTurn || _game.Phase != MagnatePhase.Plan || _selectedTruck < 0)) ClosePopup(false);
            RefreshPopup();
            RefreshHighlights();
            SyncHubs();
            RefreshBottom();
        }

        void RefreshEventChip()
        {
            var events = _game.Events;
            Ui.Show(_eventChip, events.Count > 0);
            _eventIcon.Clear();
            if (events.Count == 0) return;
            var icon = HubEventIcons.Event(events[0].Kind, 26f);
            if (icon != null) _eventIcon.Add(icon);
            _eventChip.text = events.Count == 1 ? (En ? $"Event: {EventTitle(events[0])}" : $"Событие: {events[0].Title}") : (En ? $"Events: {events.Count}" : $"События: {events.Count}");
            _eventChip.tooltip = string.Join("\n", events.Select(e => En ? $"{EventTitle(e)}: {EventDescription(e)}" + (e.EndRound > _game.Round ? $" (until round {e.EndRound})" : "") : $"{e.Title}: {e.Description}" + (e.EndRound > _game.Round ? $" (до раунда {e.EndRound})" : "")));
        }

        void RefreshBottom()
        {
            _bottom.Clear();
            _sections.Clear();
            var player = _game.CurrentPlayer;

            // 0. Игроки: все компании — цвет, имя и очки; клик — панель «Игроки» справа на поле.
            var players = Section(PlayersSection, 1f, () => TogglePopup("players", PlayersSection));
            players.Add(Header(LocalizationService.Get("magnate.panel.002"), Theme.Text, _game.Players.Count.ToString()));
            var list = Ui.Row();
            list.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            list.style.marginTop = 4f;
            // В порядке хода раунда: первыми ходят отстающие — номер перед именем.
            int position = 0;
            foreach (var p in _game.TurnOrder.Select(i => _game.Players[i]))
            {
                position++;
                bool current = p.Index == _game.CurrentPlayerIndex && _game.Phase != MagnatePhase.GameOver;
                var chip = Ui.Row();
                chip.style.alignItems = Align.Center;
                chip.style.marginRight = 8f;
                chip.style.marginBottom = 3f;
                var dot = new VisualElement();
                dot.style.width = 10f;
                dot.style.height = 10f;
                dot.style.backgroundColor = Palette.PlayerColor(p.Index);
                Ui.Radius(dot, 3f);
                if (current) Ui.Border(dot, 1.5f, Color.white);
                dot.style.marginRight = 4f;
                chip.Add(dot);
                chip.Add(Ui.Text($"{position}. {p.Name} {p.Score}", 12f, current ? Theme.Text : Theme.TextDim, current));
                list.Add(chip);
            }
            players.Add(list);
            players.Add(Ui.Text(LocalizationService.Get("magnate.panel.003"), 12f, Theme.TextDim));
            _sections.Add(players);
            _bottom.Add(players);

            // 1. Контекстный раздел.
            var context = Section(ContextSection, 2.3f, null);
            BuildContext(context, player);
            _sections.Add(context);
            _bottom.Add(context);

            // 2. Грузы.
            var waiting = _game.AllCargo.Where(c => c.Truck < 0 && !_hiddenKinds.Contains(c.Kind) && _game.HubAt(c.From) != null).ToList();
            var cargo = Section(CargoSection, 1f, () => TogglePopup("cargo", CargoSection));
            cargo.Add(Header(LocalizationService.Get("magnate.panel.004"), Theme.Text, waiting.Count.ToString()));
            var kinds = Ui.Row();
            kinds.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            kinds.style.marginTop = 4f;
            foreach (var group in waiting.GroupBy(c => c.Kind).OrderBy(g => g.Key))
            {
                var chip = Chip(group.Key, CargoIconBottom);
                chip.tooltip = $"{QuickCargoTitle(group.Key)}: {group.Count()}";
                chip.style.marginRight = 3f;
                chip.style.marginBottom = 2f;
                kinds.Add(chip);
                // Сколько грузов этого вида — рядом с иконкой (на самой иконке число не читается).
                var count = Ui.Text(group.Count() > 1 ? $"×{group.Count()}" : "", 13f, Theme.TextDim, true);
                count.style.marginRight = 6f;
                count.style.alignSelf = Align.Center;
                kinds.Add(count);
            }
            cargo.Add(kinds);
            int urgent = waiting.Count(Urgent);
            cargo.Add(Ui.Text(urgent > 0 ? (En ? $"urgent: {urgent}" : $"срочных: {urgent}") : waiting.Count == 0 ? LocalizationService.Get("magnate.panel.005") : LocalizationService.Get("magnate.panel.006"),
                12f, urgent > 0 ? new Color(1f, 0.45f, 0.4f) : Theme.TextDim, urgent > 0));
            _sections.Add(cargo);
            _bottom.Add(cargo);

            // 3. Заказы мэрии.
            var active = _game.Contracts.Where(c => c.OpenFor(player.Index)).ToList();
            var orders = Section(OrdersSection, 1f, () => TogglePopup("contracts", OrdersSection));
            orders.Add(Header(LocalizationService.Get("magnate.panel.007"), Theme.Text, active.Count.ToString()));
            if (active.Count > 0)
            {
                var first = active.OrderBy(c => c.EndRound).First();
                var line = Ui.Text(first.Title, 12f, Theme.Text);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.marginTop = 4f;
                orders.Add(line);
                orders.Add(ProgressBar(first.Progress[player.Index], first.Needed, Palette.PlayerColor(player.Index)));
            }
            orders.Add(Ui.Text(active.Count == 0 ? LocalizationService.Get("magnate.panel.008") : LocalizationService.Get("magnate.panel.003"), 12f, Theme.TextDim));
            _sections.Add(orders);
            _bottom.Add(orders);
        }

        /// <summary>Контекстный раздел: что делать прямо сейчас.</summary>
        void BuildContext(VisualElement card, MagnatePlayer player)
        {
            var color = Palette.PlayerColor(player.Index);
            if (_game.Phase == MagnatePhase.GameOver)
            {
                card.Add(Header(LocalizationService.Get("magnate.panel.009"), Theme.Text));
                return;
            }
            if (_animating != null)
            {
                card.Add(Header(En ? $"Round {_animating.Round}: trucks en route…" : $"Раунд {_animating.Round}: фуры в пути…", Theme.Text));
                card.Add(Hint(LocalizationService.Get("magnate.panel.010")));
                return;
            }
            if (player.IsBot)
            {
                card.Add(Header(player.Name, color));
                card.Add(Hint(_game.IsOpeningRound ? LocalizationService.Get("magnate.panel.011")
                    : _game.Phase == MagnatePhase.Build ? LocalizationService.Get("magnate.panel.012") : LocalizationService.Get("magnate.panel.013")));
                return;
            }
            if (_awaitingHandoff)
            {
                card.Add(Header(En ? $"Turn: {player.Name}" : $"Ход: {player.Name}", color));
                card.Add(Hint(LocalizationService.Get("magnate.panel.014")));
                return;
            }

            if (_game.IsOpeningRound)
            {
                // Раунд стройки: по одной дороге на компанию, фуры стоят; маршруты — со второго раунда.
                bool built = _game.OpeningRoadsLeft <= 0;
                card.Add(Header(En ? $"{player.Name} · Opening construction · {player.BuildPoints} build points" : $"{player.Name} · Раунд стройки · {player.BuildPoints} очков стройки", color));
                card.Add(Hint(built
                    ? LocalizationService.Get("magnate.panel.015")
                    : En ? $"Opening round: each company builds {(_game.Config.OpeningRoads == 1 ? "one road" : _game.Config.OpeningRoads + " roads")} (no highways or hubs). " + LocalizationService.Get("magnate.panel.016")
                    : $"Первый раунд — только стройка: каждая компания строит {(_game.Config.OpeningRoads == 1 ? "одну дорогу" : _game.Config.OpeningRoads + " дороги")} (без магистралей и хабов). " + LocalizationService.Get("magnate.panel.016")));
                var pass = Ui.Button(_game.IsLastTurnInRound ? LocalizationService.Get("magnate.panel.017") : LocalizationService.Get("magnate.panel.018"), () =>
                {
                    _game.FinishBuild();
                    _app.Sound.Play(Sfx.Ticket, 0.6f);
                    AfterTurn();
                }, built ? ButtonKind.Primary : ButtonKind.Secondary, 14f);
                pass.style.alignSelf = Align.FlexStart;
                pass.style.marginTop = 4f;
                card.Add(pass);
                return;
            }
            if (_game.Phase == MagnatePhase.Build)
            {
                card.Add(Header(En ? $"{player.Name} · Construction · {player.BuildPoints} build points" : $"{player.Name} · Стройка · {player.BuildPoints} очков стройки", color));
                card.Add(Hint(En ? $"Click a dashed road to buy it: its number is the construction cost (length, bridge +{_game.Config.BridgeExtraCost}; grey means insufficient points). Click your road to upgrade it or a hub to develop it. Gain +{_game.Config.BuildPointsPerRound} construction points each round." : $"Клик по пунктирной дороге — купить: цифра на ней — цена в очках стройки (длина, мост +{_game.Config.BridgeExtraCost}; серая — не хватает). По своей — магистраль, по хабу — развить. " + $"Каждый раунд +{_game.Config.BuildPointsPerRound} очков."));
                var next = Ui.Button(LocalizationService.Get("magnate.panel.019"), () => { _game.FinishBuild(); _app.Sound.Play(Sfx.Click, 0.6f); }, ButtonKind.Primary, 14f);
                next.style.alignSelf = Align.FlexStart;
                next.style.marginTop = 4f;
                if (_coachTarget == "next") Highlight(next);
                card.Add(next);
                return;
            }

            // Маршруты: фуры кнопками, клик — выбрать (откроется карточка фуры), «Готово».
            card.Add(Header(En ? $"{player.Name} · Truck Routes" : $"{player.Name} · Маршруты фур", color));
            var trucks = Ui.Row();
            trucks.style.marginTop = 4f;
            int number = 0;
            foreach (var truck in _game.Trucks.Where(t => t.Owner == player.Index))
            {
                number++;
                var chip = TruckChip(truck, number);
                trucks.Add(chip);
            }
            card.Add(trucks);
            var bottom = Ui.Row();
            bottom.style.alignItems = Align.Center;
            bottom.style.marginTop = 4f;
            var done = Ui.Button(_game.IsLastTurnInRound ? LocalizationService.Get("magnate.panel.020") : LocalizationService.Get("magnate.panel.018"), () =>
            {
                ClosePopup(false);
                _game.FinishPlanning();
                _app.Sound.Play(Sfx.Ticket, 0.6f);
                AfterTurn();
            }, ButtonKind.Primary, 14f);
            if (_coachTarget == "done") Highlight(done);
            bottom.Add(done);
            var tip = Hint(LocalizationService.Get("magnate.panel.021"));
            tip.style.flexShrink = 1f;
            bottom.Add(tip);
            card.Add(bottom);
        }

        VisualElement TruckChip(Truck truck, int number)
        {
            bool selected = truck.Id == _selectedTruck;
            var chip = new VisualElement();
            chip.style.flexGrow = 1f;
            chip.style.flexBasis = 0f;
            chip.style.marginRight = 6f;
            Ui.Padding(chip, 4f, 8f);
            Ui.Radius(chip, 8f);
            chip.style.backgroundColor = selected ? Theme.PanelHover : new Color(1f, 1f, 1f, 0.05f);
            Ui.Border(chip, selected ? 2f : 1f, selected ? GameColors.Accent : Theme.Line);
            if (!selected && _coachTarget == "truck") Highlight(chip);
            chip.pickingMode = PickingMode.Position;
            var cargo = _game.GetCargo(truck.Cargo) ?? _game.GetCargo(truck.PlannedCargo);
            var title = Ui.Row();
            title.style.alignItems = Align.Center;
            if (cargo != null)
            {
                var kind = Chip(cargo.Kind, 14f);
                kind.style.marginRight = 5f;
                title.Add(kind);
            }
            title.Add(Ui.Text(En ? $"Truck {number}" : $"Фура {number}", 12f, Theme.Text, true));
            chip.Add(title);
            string status = truck.Cargo != null ? $"→ {Place(_game.Map.LocationName(cargo?.To))}"
                 : truck.PlannedCargo != null ? (En ? $"picking up → {Place(_game.Map.LocationName(cargo?.To))}" : $"берёт → {_game.Map.LocationName(cargo?.To)}")
                 : truck.Target != null ? (En ? $"empty → {Place(_game.Map.LocationName(truck.Target))}" : $"пустая → {_game.Map.LocationName(truck.Target)}")
                 : (En ? $"waiting · {Place(_game.Map.LocationName(truck.Location))}" : $"ждёт · {_game.Map.LocationName(truck.Location)}");
            var line = Ui.Text(status, 11f, truck.Route.Count > 0 || truck.Cargo != null ? Theme.TextDim : new Color(1f, 0.8f, 0.4f));
            line.style.overflow = Overflow.Hidden;
            line.style.textOverflow = TextOverflow.Ellipsis;
            line.style.whiteSpace = WhiteSpace.NoWrap;
            chip.Add(line);
            chip.name = "gamepad-magnate-truck-" + truck.Id;
            void ActivateTruck()
            {
                if (selected)
                {
                    _selectedTruck = -1;
                    ClosePopup(false);
                }
                else
                {
                    _selectedTruck = truck.Id;
                    OpenPopup("truck", ContextSection);
                }
                _preview = null;
                _app.Sound.Play(Sfx.Click, 0.4f);
                _dirty = true;
            }
            chip.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                ActivateTruck();
            });
            GamepadSelectable.Attach(chip, ActivateTruck);
            return chip;
        }

        static Label Hint(string text)
        {
            var label = Ui.Text(text, 12f, Theme.TextDim);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 2f;
            return label;
        }

        static VisualElement ProgressBar(int value, int needed, Color color)
        {
            var track = new VisualElement();
            track.style.height = 6f;
            track.style.marginTop = 4f;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            Ui.Radius(track, 3f);
            var fill = new VisualElement();
            fill.style.height = Length.Percent(100f);
            fill.style.width = Length.Percent(100f * Mathf.Clamp01(value / (float)Math.Max(1, needed)));
            fill.style.backgroundColor = color;
            Ui.Radius(fill, 3f);
            track.Add(fill);
            return track;
        }

        // ================================================================== Всплывающие окна

        void TogglePopup(string kind, int section)
        {
            if (_popupKind == kind || (kind == "cargo" && _popupKind == "cargoDetail")) ClosePopup();
            else OpenPopup(kind, section);
        }

        void OpenPopup(string kind, int section)
        {
            _popupKind = kind;
            _popupSection = section;
            _app.Sound.Play(Sfx.Open, 0.35f);
            _dirty = true;
        }

        void ClosePopup() => ClosePopup(true);

        void ClosePopup(bool sound)
        {
            if (_popupKind == null) return;
            if (_popupKind == "truck") _selectedTruck = -1;
            _popupKind = null;
            _popupCargo = null;
            _popupPreview = null;
            _popupBlocked = null;
            Ui.Show(_popup, false);
            PlaceCoach(false);
            if (sound) _app.Sound.Play(Sfx.Close, 0.35f);
            _dirty = true;
        }

        /// <summary>Окна, которые открываются панелью справа на игровом поле, а не над своим разделом.</summary>
        static bool RightPanel(string kind) => kind == "cargo" || kind == "cargoDetail" || kind == "contracts" || kind == "players";

        /// <summary>Подсказка обучения в правом нижнем углу поля; открыта панель грузов справа — подсказка левее неё.</summary>
        void PlaceCoach(bool besideCargo)
        {
            if (_coach != null) _coach.style.right = besideCargo ? PopupWidth + 28f : 14f;
        }

        void RefreshPopup()
        {
            _popupPreview = null;
            _popupBlocked = null;
            RefreshJournalButton();
            if (_popupKind == null) { Ui.Show(_popup, false); PlaceCoach(false); return; }
            if (_popupKind == "cargoDetail")
            {
                var shown = _game.GetCargo(_popupCargo);
                if (shown == null || shown.Truck >= 0) _popupKind = "cargo";   // груз забрали или он истёк
            }
            _popupBody.Clear();
            switch (_popupKind)
            {
                case "cargo": FillCargoList(); break;
                case "cargoDetail": FillCargoDetail(_game.GetCargo(_popupCargo)); break;
                case "contracts": FillContracts(); break;
                case "players": FillPlayers(); break;
                case "rules": FillRules(); break;
                case "menu": FillMenu(); break;
                case "log": FillLog(); break;
                case "truck": FillTruck(_game.GetTruck(_selectedTruck)); break;
            }
            float width = Root.layout.width;
            float height = float.IsNaN(Root.layout.height) ? 800f : Root.layout.height;
            if (RightPanel(_popupKind))
            {
                // Грузы, заказы мэрии и игроки — панелью справа на игровом поле, под кнопкой «Слои карты»
                // (её список прячем, чтобы не наслаивался).
                Ui.Show(_layersList, false);
                PlaceCoach(true);
                const float underLayers = 54f;
                _popup.style.top = TopBarHeight + underLayers;
                _popup.style.bottom = StyleKeyword.Auto;
                _popup.style.maxHeight = new Length(Mathf.Max(240f, height - TopBarHeight - underLayers - BottomHeight - 14f));
                _popup.style.left = float.IsNaN(width) ? 12f : Mathf.Max(12f, width - PopupWidth - 14f);
                Ui.Show(_popup, true);
                return;
            }
            PlaceCoach(false);
            if (_popupSection == TopSection)
            {
                // Журнал и меню партии — под верхней строкой, правым краем у кнопок справа.
                _popup.style.top = TopBarHeight + 8f;
                _popup.style.bottom = StyleKeyword.Auto;
                _popup.style.maxHeight = new Length(Mathf.Max(240f, height - TopBarHeight - BottomHeight - 24f));
                float right = 12f;
                if (_topActions != null && !float.IsNaN(_topActions.worldBound.xMax) && !float.IsNaN(width))
                    right = Mathf.Max(12f, Root.worldBound.xMax - _topActions.worldBound.xMax);
                float left = float.IsNaN(width) ? 12f : Mathf.Max(12f, width - right - PopupWidth);
                _popup.style.left = left;
                Ui.Show(_popup, true);
                return;
            }
            // Окно — над своим разделом, не вылезая за край экрана.
            _popup.style.top = StyleKeyword.Auto;
            _popup.style.bottom = BottomHeight + 8f;
            _popup.style.maxHeight = 560f;
            float x = 12f;
            if (_popupSection < _sections.Count && !float.IsNaN(_sections[_popupSection].layout.x))
                x = _sections[_popupSection].layout.x + 12f;
            if (!float.IsNaN(width) && width > PopupWidth + 24f) x = Mathf.Min(x, width - PopupWidth - 12f);
            _popup.style.left = x;
            Ui.Show(_popup, true);
        }

        VisualElement Pad(VisualElement e)
        {
            e.style.marginLeft = 14f;
            e.style.marginRight = 14f;
            return e;
        }

        // ---------------------------------------------------------------- Грузы

        void FillCargoList()
        {
            _popupTitle.text = LocalizationService.Get("magnate.panel.022");
            // Фильтры видов: на карте, мини-карте, в панели и здесь.
            var filters = Ui.Row();
            filters.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            filters.style.marginBottom = 6f;
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
            {
                bool hidden = _hiddenKinds.Contains(kind);
                var chip = Chip(kind, CargoIconPanel);
                chip.style.opacity = hidden ? 0.25f : 1f;
                chip.style.marginBottom = 4f;
                chip.tooltip = QuickCargoTitle(kind) + (hidden ? LocalizationService.Get("magnate.panel.023") : LocalizationService.Get("magnate.panel.024"));
                chip.pickingMode = PickingMode.Position;
                chip.name = "gamepad-magnate-cargo-filter-" + kind;
                void ToggleCargoFilter()
                {
                    if (!_hiddenKinds.Remove(kind)) _hiddenKinds.Add(kind);
                    _dirty = true;
                }
                chip.RegisterCallback<ClickEvent>(_ => ToggleCargoFilter());
                GamepadSelectable.Attach(chip, ToggleCargoFilter);
                filters.Add(chip);
            }
            _popupBody.Add(Pad(filters));
            bool any = false;
            foreach (var hub in _game.Hubs)
            {
                var waiting = _game.WaitingAt(hub.LocationId).Where(x => !_hiddenKinds.Contains(x.Kind)).ToList();
                if (waiting.Count == 0) continue;
                any = true;
                string kindName = hub.Kind == HubKind.Airport ? LocalizationService.Get("magnate.panel.025") : hub.Kind == HubKind.Port ? LocalizationService.Get("magnate.panel.026") : LocalizationService.Get("magnate.panel.027");
                var title = Ui.Row();
                title.style.alignItems = Align.Center;
                title.style.marginTop = 6f;
                var hubIcon = HubEventIcons.Hub(hub.Kind, 22f);
                if (hubIcon != null)
                {
                    hubIcon.style.marginRight = 6f;
                    title.Add(hubIcon);
                }
                title.Add(Ui.Text(En ? $"{Place(_game.Map.LocationName(hub.LocationId))} · {kindName}, level {hub.Level}" : $"{_game.Map.LocationName(hub.LocationId)} · {kindName}, ур. {hub.Level}", 13f, Theme.TextDim, true));
                _popupBody.Add(Pad(title));
                foreach (var cargo in waiting) _popupBody.Add(Pad(CargoRow(cargo, true)));
            }
            if (!any) _popupBody.Add(Pad(Hint(_hiddenKinds.Count > 0 ? LocalizationService.Get("magnate.panel.028") : LocalizationService.Get("magnate.panel.029"))));
        }

        /// <summary>Строка груза: клик — карточка груза; «Выбрать» — назначить ближайшую свободную фуру.</summary>
        VisualElement CargoRow(Cargo cargo, bool withPick)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            Ui.Padding(row, 5f, 6f);
            Ui.Radius(row, 6f);
            row.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            if (Urgent(cargo)) Ui.Border(row, 1.5f, new Color(0.95f, 0.25f, 0.2f));
            row.pickingMode = PickingMode.Position;
            row.tooltip = En ? "Click for cargo details" : $"{LogisticsGame.KindTitle(cargo.Kind)} — подробнее о грузе";
            row.Add(Chip(cargo.Kind, CargoIconPanel));
            int roundsLeft = cargo.Kind == CargoKind.Perishable ? Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round) : -1;
            // Маршрут груза — откуда (станция, где он ждёт) и куда; вид груза показывает значок слева.
            var text = Ui.Text(En
                ? $"{Place(_game.Map.LocationName(cargo.From))} → {Place(_game.Map.LocationName(cargo.To))} · {cargo.Value} pts" + (roundsLeft >= 0 ? $" · due in {roundsLeft} rounds" : "") + (ForContract(cargo) ? " · city order" : "")
                : $"{_game.Map.LocationName(cargo.From)} → {_game.Map.LocationName(cargo.To)} · {cargo.Value} очк." + (roundsLeft >= 0 ? $" · норма {roundsLeft} р." : "") + (ForContract(cargo) ? " · заказ мэрии" : ""), 12f, Theme.Text);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexShrink = 1f;
            text.style.flexGrow = 1f;
            row.Add(text);
            row.name = "gamepad-magnate-cargo-" + cargo.Id;
            void OpenDetails()
            {
                _popupKind = "cargoDetail";
                _popupCargo = cargo.Id;
                _dirty = true;
            }
            row.RegisterCallback<ClickEvent>(_ => OpenDetails());
            GamepadSelectable.Attach(row, OpenDetails);
            if (withPick)
            {
                var pick = Ui.Button(LocalizationService.Get("magnate.panel.030"), () => AssignCargo(cargo), ButtonKind.Secondary, 12f);
                pick.style.marginLeft = 6f;
                pick.style.flexShrink = 0f;
                pick.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                if (_coachTarget == "cargo") Highlight(pick);
                row.Add(pick);
            }
            return row;
        }

        /// <summary>Фура для груза: свободная фура на его хабе, иначе выбранная, иначе ближайшая свободная.</summary>
        Truck BestTruckFor(Cargo cargo, out List<string> toHub)
        {
            toHub = null;
            if (_game.Phase != MagnatePhase.Plan || _game.CurrentPlayer.IsBot) return null;
            var free = _game.Trucks.Where(t => t.Owner == _game.CurrentPlayerIndex && t.Cargo == null).ToList();
            var here = free.FirstOrDefault(t => t.Location == cargo.From && (t.PlannedCargo == null || t.PlannedCargo == cargo.Id));
            if (here != null) { toHub = new List<string>(); return here; }
            Truck best = null;
            int bestLength = int.MaxValue;
            foreach (var truck in free.OrderByDescending(t => t.Id == _selectedTruck))
            {
                var route = _game.FindRoute(truck.Owner, truck.Location, cargo.From, null);
                if (route == null) continue;
                int length = _game.RouteLength(route) - (truck.Id == _selectedTruck ? 3 : 0); // выбранной — фора
                if (length < bestLength) { bestLength = length; best = truck; toHub = route; }
            }
            return best;
        }

        int TruckNumber(Truck truck) => _game.Trucks.Where(t => t.Owner == truck.Owner).ToList().IndexOf(truck) + 1;

        void AssignCargo(Cargo cargo)
        {
            if (!HumanTurn || _game.Phase != MagnatePhase.Plan)
            {
                Toast(_game.IsOpeningRound ? LocalizationService.Get("magnate.panel.031")
                    : _game.Phase == MagnatePhase.Build ? LocalizationService.Get("magnate.panel.032") : LocalizationService.Get("magnate.panel.033"));
                _app.Sound.Play(Sfx.Error, 0.5f);
                return;
            }
            var truck = BestTruckFor(cargo, out var toHub);
            if (truck == null) { Toast(LocalizationService.Get("magnate.panel.034")); _app.Sound.Play(Sfx.Error, 0.5f); return; }
            string error;
            if (truck.Location == cargo.From && _game.FindRoute(truck.Owner, cargo.From, cargo.To, cargo.Kind) == null)
            {
                // Фура уже на хабе, а довезти нельзя — ехать никуда не нужно, нужен широкий путь.
                Toast(En ? $"Truck {TruckNumber(truck)} is already at the hub, but this cargo needs wide roads (concrete, bridges or highways) to {Place(_game.Map.LocationName(cargo.To))}." : $"Фура {TruckNumber(truck)} уже на хабе, но довезти пока нельзя: этому грузу нужны широкие дороги (бетон, мост или магистраль) до «{_game.Map.LocationName(cargo.To)}».", 4f);
                _app.Sound.Play(Sfx.Error, 0.5f);
                return;
            }
            if (truck.Location == cargo.From)
            {
                error = _game.PlanTruck(truck.Id, cargo.Id, null);
                if (error == null) Toast(En ? $"Truck {TruckNumber(truck)} picks up {QuickCargoTitle(cargo.Kind).ToLowerInvariant()}: {Place(_game.Map.LocationName(cargo.From))} → {Place(_game.Map.LocationName(cargo.To))}." : $"Фура {TruckNumber(truck)} берёт груз ({LogisticsGame.KindTitle(cargo.Kind).ToLowerInvariant()}): {_game.Map.LocationName(cargo.From)} → {_game.Map.LocationName(cargo.To)}.");
            }
            else
            {
                error = _game.PlanTruck(truck.Id, null, cargo.From);
                if (error == null) Toast(En ? $"Truck {TruckNumber(truck)} heads to {Place(_game.Map.LocationName(cargo.From))} (~{_game.RouteRounds(toHub)} rounds). It will collect cargo on arrival if still available." : $"Фура {TruckNumber(truck)} едет за грузом на {_game.Map.LocationName(cargo.From)} (~{_game.RouteRounds(toHub)} р.). Груз возьмёте, когда доедете, если его не заберут раньше.", 4f);
            }
            if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); return; }
            _app.Sound.Play(Sfx.Card, 0.6f);
            _selectedTruck = truck.Id;
            _popupKind = "truck";
            _popupSection = ContextSection;
            _dirty = true;
        }

        /// <summary>Карточка груза: правило вида, откуда и куда, норма, путь на карте, время, плата, очки, кто повезёт.</summary>
        void FillCargoDetail(Cargo cargo)
        {
            if (cargo == null) { FillCargoList(); return; }
            _popupTitle.text = $"{Place(_game.Map.LocationName(cargo.From))} → {Place(_game.Map.LocationName(cargo.To))}";
            var back = Ui.Button(LocalizationService.Get("magnate.panel.035"), () => { _popupKind = "cargo"; _dirty = true; }, ButtonKind.Ghost, 12f);
            back.style.alignSelf = Align.FlexStart;
            _popupBody.Add(Pad(back));

            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            head.style.marginTop = 4f;
            head.Add(Chip(cargo.Kind, 26f));
            var rule = Ui.Text($"{QuickCargoTitle(cargo.Kind)}. {QuickCargoRule(cargo.Kind)}", 12f, Theme.Text);
            rule.style.whiteSpace = WhiteSpace.Normal;
            rule.style.flexShrink = 1f;
            head.Add(rule);
            _popupBody.Add(Pad(head));

            var lines = new List<string>
            {
                En ? $"From: {Place(_game.Map.LocationName(cargo.From))} · to: {Place(_game.Map.LocationName(cargo.To))}" : $"Откуда: {_game.Map.LocationName(cargo.From)} · куда: {_game.Map.LocationName(cargo.To)}",
                En ? $"Delivery value: {cargo.Value} pts (before reputation multiplier)" : $"Цена при доставке: {cargo.Value} очк. (до множителя репутации)"
            };
            if (cargo.Kind == CargoKind.Perishable)
                lines.Add(En ? $"Deadline: {Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round)} rounds left, then −{_game.Config.PerishableLatePenalty} per late round" : $"Норма: ещё {Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round)} р., дальше −{_game.Config.PerishableLatePenalty} за каждый раунд опоздания");
            if (ForContract(cargo)) lines.Add(LocalizationService.Get("magnate.panel.036"));
            var delivery = _game.FindRoute(_game.CurrentPlayerIndex, cargo.From, cargo.To, cargo.Kind);
            if (delivery != null)
            {
                _popupPreview = delivery;
                lines.Add(En ? $"Delivery route (gold on map): {_game.RouteLength(delivery)} segments, ~{_game.RouteRounds(delivery, cargo.Kind)} rounds, toll −{_game.RouteTolls(_game.CurrentPlayerIndex, delivery)}, congested roads: {_game.RouteRisk(delivery)}" : $"Путь доставки (золотой на карте): {_game.RouteLength(delivery)} участков, ~{_game.RouteRounds(delivery, cargo.Kind)} р., " +
                          $"проезд −{_game.RouteTolls(_game.CurrentPlayerIndex, delivery)}, забитых дорог: {_game.RouteRisk(delivery)}");
                if (cargo.Kind == CargoKind.Hazard && _game.RouteEntersBribeZone(cargo.From, delivery))
                    lines.Add(En ? $"Route crosses the Centre: pay {_game.Config.HazardBribe} influence or cargo will be confiscated" : $"Путь идёт через Центр: взятка {_game.Config.HazardBribe} влияния, иначе конфискация");
            }
            else
            {
                lines.Add(cargo.Kind == CargoKind.Heavy || cargo.Kind == CargoKind.Oversized
                    ? LocalizationService.Get("magnate.panel.037")
                    : LocalizationService.Get("magnate.panel.038"));
                // Путь всё равно показываем — красным: где он пройдёт и какие дороги мешают (ярче).
                List<string> ideal = ShortestRouteAnyRoads(cargo.From, cargo.To);
                if (ideal != null && ideal.Count > 0)
                {
                    _popupBlocked = ideal;
                    _popupBlockedKind = cargo.Kind;
                    var roads = ideal.Select(id => _game.Map.GetRoad(id)).ToList();
                    int unbuilt = roads.Count(r => !_game.IsBuilt(r.Id));
                    int narrow = roads.Count(r => _game.IsBuilt(r.Id) && !_game.Allowed(r, cargo.Kind));
                    var why = new List<string>();
                    if (unbuilt > 0) why.Add(En ? $"unbuilt roads: {unbuilt}" : $"не построено дорог: {unbuilt}");
                    if (narrow > 0) why.Add(cargo.Kind == CargoKind.Heavy || cargo.Kind == CargoKind.Oversized
                        ? (En ? $"narrow or closed: {narrow}" : $"узких или закрытых: {narrow}") : (En ? $"closed: {narrow}" : $"закрытых: {narrow}"));
                    lines.Add((En ? $"The shortest route is red on the map ({_game.RouteLength(ideal)} segments); bright red roads block it" : $"Красным на карте — кратчайший путь ({_game.RouteLength(ideal)} участков); ярко-красные дороги мешают") +
                              (why.Count > 0 ? " — " + string.Join(", ", why) : "") + ".");
                }
            }
            foreach (var line in lines)
            {
                var label = Hint("• " + line);
                label.style.color = Theme.Text;
                label.style.marginTop = 4f;
                _popupBody.Add(Pad(label));
            }

            var truck = BestTruckFor(cargo, out var toHub);
            if (truck != null && delivery == null)
            {
                // Довезти пока нельзя (крупногабарит и тяжеловес — только по широким дорогам), но доехать до хаба
                // пустой фурой можно: пока она едет, достройте широкий путь.
                bool here = truck.Location == cargo.From;
                var info = Hint(here
                    ? (En ? $"Truck {TruckNumber(truck)} is at the hub. Build wide roads (concrete, bridge or highway) to {Place(_game.Map.LocationName(cargo.To))} before it can deliver." : $"Фура {TruckNumber(truck)} уже на хабе: возьмёт груз, как только появится путь — постройте широкие дороги (бетон, мост или магистраль) до «{_game.Map.LocationName(cargo.To)}».")
                    : (En ? $"Closest available truck: {TruckNumber(truck)}, ~{_game.RouteRounds(toHub)} rounds to the hub. Empty trucks can use any roads; meanwhile build a wide route to {Place(_game.Map.LocationName(cargo.To))}." : $"Ближайшая свободная — фура {TruckNumber(truck)}: до хаба ~{_game.RouteRounds(toHub)} р. Пустая фура доедет по любым дорогам, " +
                      $"а пока она в пути — постройте широкий путь до «{_game.Map.LocationName(cargo.To)}».") );
                info.style.color = Theme.Gold;
                info.style.marginTop = 8f;
                _popupBody.Add(Pad(info));
                if (!here)
                {
                    var go = Ui.Button(En ? $"Send Truck {TruckNumber(truck)} to Hub" : $"Отправить фуру {TruckNumber(truck)} к хабу", () => AssignCargo(cargo), ButtonKind.Primary, 13f);
                    go.style.marginTop = 8f;
                    go.style.alignSelf = Align.FlexStart;
                    _popupBody.Add(Pad(go));
                }
            }
            else if (truck != null && delivery != null)
            {
                int rounds = _game.RouteRounds(toHub) + _game.RouteRounds(delivery, cargo.Kind);
                int value = _game.ValueIfDelivered(cargo, rounds, truck.Owner);
                string who = truck.Location == cargo.From
                    ? (En ? $"Truck {TruckNumber(truck)} is at the hub: ~{rounds} rounds to deliver for ~{value} pts." : $"Фура {TruckNumber(truck)} уже на хабе: привезёт за ~{rounds} р. и получит ~{value} очк.")
                    : (En ? $"Closest available truck: {TruckNumber(truck)}, ~{_game.RouteRounds(toHub)} rounds to the hub, ~{rounds} rounds including delivery, ~{value} pts." : $"Ближайшая свободная — фура {TruckNumber(truck)}: до хаба ~{_game.RouteRounds(toHub)} р., с доставкой ~{rounds} р., ~{value} очк.");
                var info = Hint(who);
                info.style.color = Theme.Gold;
                info.style.marginTop = 8f;
                _popupBody.Add(Pad(info));
                var take = Ui.Button(truck.Location == cargo.From ? (En ? $"Pick Up with Truck {TruckNumber(truck)}" : $"Взять фурой {TruckNumber(truck)}") : (En ? $"Send Truck {TruckNumber(truck)} to Hub" : $"Отправить фуру {TruckNumber(truck)} к хабу"),
                    () => AssignCargo(cargo), ButtonKind.Primary, 13f);
                take.style.marginTop = 8f;
                take.style.alignSelf = Align.FlexStart;
                if (_coachTarget == "cargo") Highlight(take);
                _popupBody.Add(Pad(take));
            }
            else if (_game.Phase == MagnatePhase.Build && HumanTurn)
                _popupBody.Add(Pad(Hint(LocalizationService.Get("magnate.panel.039"))));
        }

        // ---------------------------------------------------------------- Фура

        void FillTruck(Truck truck)
        {
            if (truck == null) { ClosePopup(false); return; }
            int number = TruckNumber(truck);
            var cargo = _game.GetCargo(truck.Cargo);
            var planned = _game.GetCargo(truck.PlannedCargo);
            var kind = cargo?.Kind ?? planned?.Kind;
            _popupTitle.text = En ? $"Truck {number} · {Place(_game.Map.LocationName(truck.Location))} · speed {_game.SpeedOf(kind)}" : $"Фура {number} · {_game.Map.LocationName(truck.Location)} · скорость {_game.SpeedOf(kind)}";
            string status = cargo != null
                ? (En ? $"Delivering {QuickCargoTitle(cargo.Kind).ToLowerInvariant()}: {Place(_game.Map.LocationName(cargo.From))} → {Place(_game.Map.LocationName(cargo.To))} " : $"Везёт ({LogisticsGame.KindTitle(cargo.Kind).ToLowerInvariant()}): {_game.Map.LocationName(cargo.From)} → {_game.Map.LocationName(cargo.To)} ") +
                  (En ? $"(~{_game.ValueIfDelivered(cargo, _game.RouteRounds(truck.Route, cargo.Kind), truck.Owner)} pts)" : $"(~{_game.ValueIfDelivered(cargo, _game.RouteRounds(truck.Route, cargo.Kind), truck.Owner)} очк.)") +
                  (cargo.Kind == CargoKind.Valuable ? (cargo.Escorted ? (En ? " · escorted" : " · с охраной") : (En ? " · unescorted!" : " · без охраны!")) : "")
                : planned != null ? (En ? $"Picking up {QuickCargoTitle(planned.Kind).ToLowerInvariant()}: {Place(_game.Map.LocationName(planned.From))} → {Place(_game.Map.LocationName(planned.To))}" : $"Берёт ({LogisticsGame.KindTitle(planned.Kind).ToLowerInvariant()}): {_game.Map.LocationName(planned.From)} → {_game.Map.LocationName(planned.To)}")
                : truck.Target != null ? (En ? $"Empty → {Place(_game.Map.LocationName(truck.Target))}" : $"Пустая → {_game.Map.LocationName(truck.Target)}")
                : LocalizationService.Get("magnate.panel.040");
            var statusLabel = Ui.Text(status, 13f, Theme.Text, true);
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _popupBody.Add(Pad(statusLabel));
            if (truck.Route.Count > 0)
            {
                bool center = kind == CargoKind.Hazard && _game.RouteEntersBribeZone(truck.Location, truck.Route);
                _popupBody.Add(Pad(Hint(En ? $"Route (white on map): {_game.RouteLength(truck.Route)} segments, ~{_game.RouteRounds(truck.Route, kind)} rounds · tolls −{_game.RouteTolls(truck.Owner, truck.Route)} · congested roads: {_game.RouteRisk(truck.Route)}" + (center ? $" · Centre bribe: {_game.Config.HazardBribe}" : "") : $"Маршрут (белый на карте): {_game.RouteLength(truck.Route)} участков, ~{_game.RouteRounds(truck.Route, kind)} р. · " +
                                        $"проезд −{_game.RouteTolls(truck.Owner, truck.Route)} · забитых дорог: {_game.RouteRisk(truck.Route)}" +
                                        (center ? $" · через Центр: взятка {_game.Config.HazardBribe}" : ""))));
            }
            if (cargo == null)
            {
                var waiting = _game.WaitingAt(truck.Location).ToList();
                var caption = Ui.Text(waiting.Count > 0 ? LocalizationService.Get("magnate.panel.041") : LocalizationService.Get("magnate.panel.042"), 13f, Theme.TextDim, true);
                caption.style.marginTop = 8f;
                _popupBody.Add(Pad(caption));
                foreach (var item in waiting)
                {
                    var row = CargoRow(item, false);
                    var path = _game.FindRoute(truck.Owner, truck.Location, item.To, item.Kind);
                    bool wide = item.Kind == CargoKind.Heavy || item.Kind == CargoKind.Oversized;
                    var takeButton = Ui.Button(path != null ? (En ? $"Pick Up · {_game.RouteRounds(path, item.Kind)} rounds" : $"Взять · {_game.RouteRounds(path, item.Kind)} р.") : wide ? LocalizationService.Get("magnate.panel.043") : LocalizationService.Get("magnate.panel.044"), () =>
                    {
                        var error = _game.PlanTruck(truck.Id, item.Id, null);
                        if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); }
                        else _app.Sound.Play(Sfx.Card, 0.6f);
                    }, ButtonKind.Secondary, 12f);
                    takeButton.SetEnabled(path != null);
                    takeButton.style.marginLeft = 6f;
                    takeButton.style.flexShrink = 0f;
                    takeButton.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    if (_coachTarget == "cargo") Highlight(takeButton);
                    row.Add(takeButton);
                    _popupBody.Add(Pad(row));
                }
                _popupBody.Add(Pad(Hint(LocalizationService.Get("magnate.panel.045") +
                                        LocalizationService.Get("magnate.panel.046"))));
            }
            if (truck.Route.Count > 0 || truck.PlannedCargo != null)
            {
                var clear = Ui.Button(LocalizationService.Get("magnate.panel.047"), () => _game.ClearTruck(truck.Id), ButtonKind.Ghost, 12f);
                clear.style.marginTop = 8f;
                clear.style.alignSelf = Align.FlexStart;
                _popupBody.Add(Pad(clear));
            }
        }

        // ---------------------------------------------------------------- Заказы, правила, журнал

        /// <summary>Панель «Игроки»: все компании — очки, репутация, влияние, стройка, фуры, дороги и хабы; чей ход — в рамке.</summary>
        static string ReputationName(ReputationLevel rank)
        {
            if (!En) return LogisticsGame.LevelTitle(rank);
            switch (rank)
            {
                case ReputationLevel.Novice: return "Novice";
                case ReputationLevel.Amateur: return "Amateur";
                case ReputationLevel.Professional: return "Professional";
                case ReputationLevel.Master: return "Master";
                default: return "Legendary";
            }
        }

        void FillPlayers()
        {
            _popupTitle.text = LocalizationService.Get("magnate.panel.002");
            foreach (var p in _game.Players.OrderByDescending(x => x.Score).ThenBy(x => x.Index))
            {
                var color = Palette.PlayerColor(p.Index);
                bool current = p.Index == _game.CurrentPlayerIndex && _game.Phase != MagnatePhase.GameOver;
                var card = Ui.Card(10f);
                card.style.marginLeft = 14f;
                card.style.marginRight = 14f;
                card.style.marginBottom = 8f;
                card.style.backgroundColor = current ? Theme.PanelHover : Theme.PanelLight;
                Ui.Border(card, current ? 2f : 1f, current ? color : Theme.Line);
                card.style.borderLeftWidth = 5f;
                card.style.borderLeftColor = color;

                var head = Ui.Row();
                head.style.alignItems = Align.Center;
                int turn = _game.TurnOrder.ToList().IndexOf(p.Index) + 1;
                var name = Ui.Text(p.Name + (p.IsBot ? LocalizationService.Get("magnate.panel.048") : "") + (current ? LocalizationService.Get("magnate.panel.049") : En ? $" · turn {turn}" : $" · ход {turn}-й"), 15f, color, true);
                name.style.flexGrow = 1f;
                name.style.flexShrink = 1f;
                head.Add(name);
                head.Add(Ui.Number(p.Score.ToString(), 22f, Theme.Text, true));
                card.Add(head);

                var stars = Ui.Text(En ? $"{new string('★', (int)p.Level + 1)} {ReputationName(p.Level)} · reputation {p.Reputation} · delivery score ×{LogisticsGame.LevelMultiplier(p.Level):0.##}" : $"{new string('★', (int)p.Level + 1)} {LogisticsGame.LevelTitle(p.Level)} · репутация {p.Reputation} · очки за доставку ×{LogisticsGame.LevelMultiplier(p.Level):0.##}",
                    12f, Theme.Gold);
                stars.style.whiteSpace = WhiteSpace.Normal;
                stars.style.marginTop = 2f;
                card.Add(stars);

                int trucks = _game.Trucks.Count(t => t.Owner == p.Index);
                int roads = _game.Map.Roads.Count(r => _game.Board.GetRoadOwner(r.Id) == p.Index);
                int hubs = _game.Hubs.Count(h => h.Owner == p.Index);
                var stats = Ui.Text(En ? $"Influence {p.Influence} · construction {p.BuildPoints} · trucks {trucks} · roads {roads} · hubs {hubs}" : $"Влияние {p.Influence} · стройка {p.BuildPoints} · фур {trucks} · дорог {roads} · хабов {hubs}", 12f, Theme.TextDim);
                stats.style.whiteSpace = WhiteSpace.Normal;
                stats.style.marginTop = 4f;
                card.Add(stats);
                _popupBody.Add(card);
            }
        }

        void FillContracts()
        {
            _popupTitle.text = LocalizationService.Get("magnate.panel.007");
            if (_game.Contracts.Count == 0) { _popupBody.Add(Pad(Hint(LocalizationService.Get("magnate.panel.050")))); return; }
            foreach (var contract in _game.Contracts)
            {
                var card = Ui.Card(10f);
                card.style.marginLeft = 14f;
                card.style.marginRight = 14f;
                card.style.marginBottom = 8f;
                card.style.backgroundColor = Theme.PanelLight;
                var titleRow = Ui.Row();
                titleRow.style.alignItems = Align.Center;
                if (contract.Kind.HasValue) titleRow.Add(Chip(contract.Kind.Value, CargoIconPanel));
                string status = contract.Second >= 0 ? (En ? $" — completed by {_game.Players[contract.Winner].Name} and {_game.Players[contract.Second].Name}" : $" — выполнили {_game.Players[contract.Winner].Name} и {_game.Players[contract.Second].Name}")
                    : contract.Winner >= 0 ? (En ? $" — completed by {_game.Players[contract.Winner].Name}, second place available" : $" — выполнил {_game.Players[contract.Winner].Name}, второе место открыто") : "";
                var title = Ui.Text(ContractName(contract.Title) + status, 14f, Theme.Text, true);
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.flexShrink = 1f;
                titleRow.Add(title);
                card.Add(titleRow);
                card.Add(Hint(En ? $"First place: +{contract.RewardPoints} pts, +{contract.RewardInfluence} influence and reputation. Second place: +{contract.SecondPoints} pts" + (contract.SecondInfluence > 0 ? $", +{contract.SecondInfluence} influence" : "") + $". Deadline: end of round {contract.EndRound}." : $"Первому: +{contract.RewardPoints} очков, +{contract.RewardInfluence} влияния и репутация. Второму — половина: +{contract.SecondPoints} очков" +
                              (contract.SecondInfluence > 0 ? $", +{contract.SecondInfluence} влияния" : "") + $". Срок — до конца раунда {contract.EndRound}."));
                foreach (var company in _game.Players)
                {
                    var row = Ui.Row();
                    row.style.alignItems = Align.Center;
                    row.style.marginTop = 3f;
                    var name = Ui.Text(company.Name, 11f, Theme.TextDim);
                    name.style.width = 90f;
                    row.Add(name);
                    var bar = ProgressBar(contract.Progress[company.Index], contract.Needed, Palette.PlayerColor(company.Index));
                    bar.style.flexGrow = 1f;
                    bar.style.marginTop = 0f;
                    row.Add(bar);
                    var count = Ui.Text($" {contract.Progress[company.Index]}/{contract.Needed}", 11f, Theme.TextDim);
                    count.style.width = 34f;
                    row.Add(count);
                    card.Add(row);
                }
                _popupBody.Add(card);
            }
        }

        static string QuickCargoTitle(CargoKind kind) =>
            LocalizationService.Language == "en"
                ? LocalizationService.Get("rules.magnate.cargo." + kind + ".name")
                : LogisticsGame.KindTitle(kind);

        string QuickCargoRule(CargoKind kind)
        {
            if (!En) return LogisticsGame.KindRule(kind, _game.Config);
            string rule = LocalizationService.Get("rules.magnate.cargo." + kind + ".rule");
            switch (kind)
            {
                case CargoKind.Perishable: return string.Format(rule, _game.Config.PerishableLatePenalty);
                case CargoKind.Hazard: return string.Format(rule, _game.Config.HazardBribe, _game.Config.ConfiscationPenalty, _game.Config.MaxTrucksPerPlayer);
                case CargoKind.Fragile: return string.Format(rule, _game.Config.FragileJamPenalty);
                case CargoKind.Valuable: return string.Format(rule, _game.Config.EscortCost);
                default: return rule;
            }
        }

        void FillRules()
        {
            _popupTitle.text = LocalizationService.Get("magnate.panel.051");
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
            {
                var row = Ui.Row();
                row.style.marginBottom = 4f;
                row.Add(Chip(kind, 18f));
                var text = Ui.Text($"{QuickCargoTitle(kind)}: {QuickCargoRule(kind)}", 12f, Theme.Text);
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.flexShrink = 1f;
                row.Add(text);
                _popupBody.Add(Pad(row));
            }
            var more = Hint(En
                ? $"Road capacity per round: street {_game.Config.CapacityStreet}, wide road or bridge {_game.Config.CapacityWide}, highway {_game.Config.CapacityHighway}. Excess trucks cause a traffic jam: all must wait and each loses {_game.Config.JamPenalty} points. " +
                  "Grey roads belong to the city and are free for everyone. Trucks travel faster on their own roads of length two or more. " +
                  "Upgrade a hub during construction; whoever invests the most construction points owns it. " +
                  "Trailing companies move and load first, with a construction bonus for the company in last place. If a rival takes a shipment, your truck tries another at the same hub. " +
                  "Reputation increases for timely deliveries and city orders, and decreases due to traffic jams and lost cargo; higher reputation increases delivery scores (up to ×1.5)."
                : $"Пропускная способность за раунд: улица {_game.Config.CapacityStreet}, широкая дорога и мост {_game.Config.CapacityWide}, " +
                  $"магистраль {_game.Config.CapacityHighway}. Больше фур — пробка: все они стоят, каждая −{_game.Config.JamPenalty}. " +
                  "Серые дороги — городские: общие и бесплатные. Свои дороги от 2 участков фура проходит на участок быстрее. " +
                  "Хаб можно развить кликом в фазе стройки; владелец — кто вложил в него больше очков. " +
                  "Первыми ходят и грузятся отстающие, компании с наименьшим счётом — +1 очко стройки. Если груз забрал соперник, фура возьмёт другой с того же хаба. " +
                  "Репутация растёт за доставки вовремя и заказы мэрии, падает за пробки и потери; с уровнем растут очки за доставку (до ×1,5).");
            more.style.marginTop = 8f;
            _popupBody.Add(Pad(more));
        }

        void FillLog()
        {
            _popupTitle.text = LocalizationService.Get("magnate.panel.052");
            if (_log.Count == 0) _popupBody.Add(Pad(Hint(LocalizationService.Get("magnate.panel.053"))));
            foreach (var line in _log.Skip(Math.Max(0, _log.Count - 40)).Reverse())
            {
                var label = Hint(JournalText(line));
                label.style.marginTop = 4f;
                _popupBody.Add(Pad(label));
            }
        }

        // ================================================================== Верхняя строка, подсветка, 3D

        static Label Chip(CargoKind kind, float size)
        {
            var chip = CargoIcons.Chip(kind, size);
            chip.style.marginRight = 8f;
            return chip;
        }

        static void Highlight(VisualElement e) => Ui.Border(e, 2f, Theme.Gold);

        /// <summary>Игроки теперь в нижней панели (раздел «Игроки») и в панели справа; верхняя строка — только пустое место.</summary>
        void RefreshPlayers()
        {
            _players.Clear();
        }

        /// <summary>
        /// Кратчайший путь по всем открытым дорогам карты, построенным и нет (по длине): где пройдёт груз, когда
        /// всё нужное достроят. null — точки не связаны дорогами. Считается здесь, в интерфейсе, по открытым данным карты.
        /// </summary>
        List<string> ShortestRouteAnyRoads(string from, string to)
        {
            if (from == to) return new List<string>();
            var distance = new Dictionary<string, float> { [from] = 0f };
            var via = new Dictionary<string, Road>();
            var open = new SortedSet<(float, string)> { (0f, from) };
            while (open.Count > 0)
            {
                var (du, u) = open.Min;
                open.Remove(open.Min);
                if (u == to) break;
                foreach (var road in _game.Map.RoadsAt(u))
                {
                    if (!_game.Board.IsRoadOpen(road.Id)) continue;
                    string v = road.A == u ? road.B : road.A;
                    float nd = du + road.Length;
                    if (distance.TryGetValue(v, out var old) && nd >= old) continue;
                    if (distance.ContainsKey(v)) open.Remove((old, v));
                    distance[v] = nd;
                    via[v] = road;
                    open.Add((nd, v));
                }
            }
            if (!via.ContainsKey(to)) return null;
            var route = new List<string>();
            for (string at = to; at != from;)
            {
                var road = via[at];
                route.Add(road.Id);
                at = road.A == at ? road.B : road.A;
            }
            route.Reverse();
            return route;
        }

        /// <summary>
        /// Подсветка: в стройку — доступные дороги, в план — маршруты своих фур (выбранная белым), голубой — путь
        /// при наведении на станцию, золотой — путь груза из открытой карточки груза, красный — путь груза, который
        /// пока не проехать (ярко-красные — мешающие дороги: не построены, узкие, закрыты).
        /// </summary>
        void RefreshHighlights()
        {
            _map.Affordable.Clear();
            _map.PathRoads.Clear();
            if (_popupPreview != null)
                foreach (var id in _popupPreview) _map.PathRoads[id] = new Color(1f, 0.82f, 0.3f, 0.9f);
            if (_popupBlocked != null)
                foreach (var id in _popupBlocked)
                {
                    var road = _game.Map.GetRoad(id);
                    bool blocks = road != null && !_game.Allowed(road, _popupBlockedKind);
                    _map.PathRoads[id] = blocks ? new Color(1f, 0.18f, 0.16f, 0.95f) : new Color(0.95f, 0.42f, 0.38f, 0.7f);
                }
            if (!HumanTurn) return;
            var player = _game.CurrentPlayer;
            if (_game.Phase == MagnatePhase.Build)
            {
                if (_game.OpeningRoadsLeft <= 0) return; // в раунде стройки дорога уже куплена
                foreach (var road in _game.Map.Roads)
                    if (!_game.IsBuilt(road.Id) && _game.Board.IsRoadOpen(road.Id) && _game.BuildCost(road) <= player.BuildPoints)
                        _map.Affordable.Add(road.Id);
                return;
            }
            var color = Palette.PlayerColor(player.Index);
            foreach (var truck in _game.Trucks.Where(t => t.Owner == player.Index))
            {
                var c = truck.Id == _selectedTruck ? Color.white : new Color(color.r, color.g, color.b, 0.75f);
                foreach (var id in truck.Route) _map.PathRoads[id] = c;
            }
            if (_preview != null)
                foreach (var id in _preview) _map.PathRoads[id] = new Color(0.45f, 0.9f, 1f, 0.9f);
        }

        void SyncHubs()
        {
            if (_city3D == null) return;
            foreach (var hub in _game.Hubs)
                _city3D.SetHub(hub.LocationId, hub.Kind == HubKind.Airport ? 0 : hub.Kind == HubKind.Station ? 1 : 2, hub.Level);
        }
    }
}
