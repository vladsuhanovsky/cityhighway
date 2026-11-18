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
    /// «Правила» и «Журнал» — кнопки в верхней строке (слева от «Обучения»), их окна открываются под ней.
    /// Клик по разделу открывает окно над панелью (одно за раз): список грузов с кнопкой «Выбрать», карточку
    /// груза (правило, маршрут, время, плата, сколько очков принесёт, путь подсвечен на карте), заказы с
    /// прогрессом компаний, правила, журнал, карточку выбранной фуры.
    /// </summary>
    public sealed partial class MagnateView
    {
        const float BottomHeight = 132f, PopupWidth = 460f;
        /// <summary>«Раздел» окон правил и журнала: они открываются под верхней строкой, у её кнопок.</summary>
        const int TopSection = 9;

        VisualElement _bottom, _popup, _popupHead, _popupBody;
        Label _popupTitle;
        readonly List<VisualElement> _sections = new List<VisualElement>();
        /// <summary>Открытое окно: cargo, cargoDetail, contracts, rules, log, truck; null — закрыто.</summary>
        string _popupKind;
        string _popupCargo;
        int _popupSection;
        /// <summary>Путь груза из открытой карточки груза — подсвечен на карте золотом.</summary>
        List<string> _popupPreview;

        // ================================================================== Каркас

        void BuildBottomBar()
        {
            _bottom = Ui.Row();
            _bottom.style.position = Position.Absolute;
            _bottom.style.left = 0f;
            _bottom.style.right = 0f;
            _bottom.style.bottom = 0f;
            _bottom.style.height = BottomHeight;
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
            var close = Ui.Button("Закрыть ✕", ClosePopup, ButtonKind.Ghost, 12f);
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
            card.style.marginRight = index < 2 ? 10f : 0f;
            card.style.backgroundColor = Theme.PanelLight;
            card.style.justifyContent = Justify.SpaceBetween;
            Ui.Padding(card, 8f, 12f);
            Ui.Radius(card, 10f);
            bool open = _popupKind != null && _popupSection == index;
            Ui.Border(card, open ? 2f : 1f, open ? GameColors.Accent : Theme.Line);
            if (onClick != null)
            {
                card.pickingMode = PickingMode.Position;
                card.RegisterCallback<ClickEvent>(_ => onClick());
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
            _roundLabel.text = $"{_game.Map.Name} · раунд {_game.Round}/{_game.Config.Rounds} · {MagnateConfig.Title(_game.Config.Difficulty)}";
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
            _eventChip.text = events.Count == 1 ? $"Событие: {events[0].Title}" : $"События: {events.Count}";
            _eventChip.tooltip = string.Join("\n", events.Select(e => $"{e.Title}: {e.Description}" + (e.EndRound > _game.Round ? $" (до раунда {e.EndRound})" : "")));
        }

        void RefreshBottom()
        {
            _bottom.Clear();
            _sections.Clear();
            var player = _game.CurrentPlayer;

            // 1. Контекстный раздел.
            var context = Section(0, 2.3f, null);
            BuildContext(context, player);
            _sections.Add(context);
            _bottom.Add(context);

            // 2. Грузы.
            var waiting = _game.AllCargo.Where(c => c.Truck < 0 && !_hiddenKinds.Contains(c.Kind) && _game.HubAt(c.From) != null).ToList();
            var cargo = Section(1, 1f, () => TogglePopup("cargo", 1));
            cargo.Add(Header("Грузы", Theme.Text, waiting.Count.ToString()));
            var kinds = Ui.Row();
            kinds.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            kinds.style.marginTop = 4f;
            foreach (var group in waiting.GroupBy(c => c.Kind).OrderBy(g => g.Key))
            {
                var chip = Chip(group.Key, 20f);
                chip.tooltip = $"{LogisticsGame.KindTitle(group.Key)}: {group.Count()}";
                chip.style.marginRight = 2f;
                chip.style.marginBottom = 2f;
                kinds.Add(chip);
                // Сколько грузов этого вида — рядом с иконкой (на самой иконке число не читается).
                var count = Ui.Text(group.Count() > 1 ? $"×{group.Count()}" : "", 11f, Theme.TextDim, true);
                count.style.marginRight = 6f;
                count.style.alignSelf = Align.Center;
                kinds.Add(count);
            }
            cargo.Add(kinds);
            int urgent = waiting.Count(Urgent);
            cargo.Add(Ui.Text(urgent > 0 ? $"срочных: {urgent}" : waiting.Count == 0 ? "пока пусто" : "Открыть список →",
                12f, urgent > 0 ? new Color(1f, 0.45f, 0.4f) : Theme.TextDim, urgent > 0));
            _sections.Add(cargo);
            _bottom.Add(cargo);

            // 3. Заказы мэрии.
            var active = _game.Contracts.Where(c => c.Winner < 0).ToList();
            var orders = Section(2, 1f, () => TogglePopup("contracts", 2));
            orders.Add(Header("Заказы мэрии", Theme.Text, active.Count.ToString()));
            if (active.Count > 0)
            {
                var first = active.OrderBy(c => c.EndRound).First();
                var line = Ui.Text(first.Title, 12f, Theme.Text);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.marginTop = 4f;
                orders.Add(line);
                orders.Add(ProgressBar(first.Progress[player.Index], first.Needed, Palette.PlayerColor(player.Index)));
            }
            orders.Add(Ui.Text(active.Count == 0 ? "заказов нет" : "Открыть →", 12f, Theme.TextDim));
            _sections.Add(orders);
            _bottom.Add(orders);
        }

        /// <summary>Контекстный раздел: что делать прямо сейчас.</summary>
        void BuildContext(VisualElement card, MagnatePlayer player)
        {
            var color = Palette.PlayerColor(player.Index);
            if (_game.Phase == MagnatePhase.GameOver)
            {
                card.Add(Header("Партия окончена", Theme.Text));
                return;
            }
            if (_animating != null)
            {
                card.Add(Header($"Раунд {_animating.Round}: фуры в пути…", Theme.Text));
                card.Add(Hint("Все компании едут одновременно. Скоропорт быстрее, тяжёлые медленнее, у пробок — часы."));
                return;
            }
            if (player.IsBot)
            {
                card.Add(Header(player.Name, color));
                card.Add(Hint(_game.Phase == MagnatePhase.Build ? "Строит дороги и развивает хабы…" : "Планирует маршруты…"));
                return;
            }
            if (_awaitingHandoff)
            {
                card.Add(Header($"Ход: {player.Name}", color));
                card.Add(Hint("Нажмите «Начать ход»."));
                return;
            }

            if (_game.Phase == MagnatePhase.Build)
            {
                card.Add(Header($"{player.Name} · Стройка · {player.BuildPoints} очков стройки", color));
                card.Add(Hint($"Клик по пунктирной дороге — купить: цифра на ней — цена в очках стройки (длина, мост +{_game.Config.BridgeExtraCost}; серая — не хватает). По своей — магистраль, по хабу — развить. " +
                              $"Каждый раунд +{_game.Config.BuildPointsPerRound} очков."));
                var next = Ui.Button("Дальше: маршруты фур →", () => { _game.FinishBuild(); _app.Sound.Play(Sfx.Click, 0.6f); }, ButtonKind.Primary, 14f);
                next.style.alignSelf = Align.FlexStart;
                next.style.marginTop = 4f;
                if (_coachTarget == "next") Highlight(next);
                card.Add(next);
                return;
            }

            // Маршруты: фуры кнопками, клик — выбрать (откроется карточка фуры), «Готово».
            card.Add(Header($"{player.Name} · Маршруты фур", color));
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
            var done = Ui.Button(_game.IsLastTurnInRound ? "Готово — фуры поедут" : "Готово — передать ход", () =>
            {
                ClosePopup(false);
                _game.FinishPlanning();
                _app.Sound.Play(Sfx.Ticket, 0.6f);
                AfterTurn();
            }, ButtonKind.Primary, 14f);
            if (_coachTarget == "done") Highlight(done);
            bottom.Add(done);
            var tip = Hint("  Выберите фуру, затем груз в окне фуры или в «Грузах», или кликните станцию на карте.");
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
            title.Add(Ui.Text($"Фура {number}", 12f, Theme.Text, true));
            chip.Add(title);
            string status = truck.Cargo != null ? $"→ {_game.Map.LocationName(cargo?.To)}"
                : truck.PlannedCargo != null ? $"берёт → {_game.Map.LocationName(cargo?.To)}"
                : truck.Target != null ? $"пустая → {_game.Map.LocationName(truck.Target)}"
                : $"ждёт · {_game.Map.LocationName(truck.Location)}";
            var line = Ui.Text(status, 11f, truck.Route.Count > 0 || truck.Cargo != null ? Theme.TextDim : new Color(1f, 0.8f, 0.4f));
            line.style.overflow = Overflow.Hidden;
            line.style.textOverflow = TextOverflow.Ellipsis;
            line.style.whiteSpace = WhiteSpace.NoWrap;
            chip.Add(line);
            chip.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                if (selected)
                {
                    _selectedTruck = -1;
                    ClosePopup(false);
                }
                else
                {
                    _selectedTruck = truck.Id;
                    OpenPopup("truck", 0);
                }
                _preview = null;
                _app.Sound.Play(Sfx.Click, 0.4f);
                _dirty = true;
            });
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
            Ui.Show(_popup, false);
            PlaceCoach(false);
            if (sound) _app.Sound.Play(Sfx.Close, 0.35f);
            _dirty = true;
        }

        /// <summary>Подсказка обучения в правом нижнем углу поля; открыта панель грузов справа — подсказка левее неё.</summary>
        void PlaceCoach(bool besideCargo)
        {
            if (_coach != null) _coach.style.right = besideCargo ? PopupWidth + 28f : 14f;
        }

        void RefreshPopup()
        {
            _popupPreview = null;
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
                case "rules": FillRules(); break;
                case "log": FillLog(); break;
                case "truck": FillTruck(_game.GetTruck(_selectedTruck)); break;
            }
            float width = Root.layout.width;
            float height = float.IsNaN(Root.layout.height) ? 800f : Root.layout.height;
            if (_popupKind == "cargo" || _popupKind == "cargoDetail")
            {
                // Грузы — панелью справа на игровом поле, под кнопкой «Слои карты» (её список прячем, чтобы не наслаивался).
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
                // Правила и журнал — под верхней строкой, правым краем у кнопок «Правила»/«Журнал».
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
            _popupTitle.text = "Грузы на хабах";
            // Фильтры видов: на карте, мини-карте, в панели и здесь.
            var filters = Ui.Row();
            filters.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            filters.style.marginBottom = 6f;
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
            {
                bool hidden = _hiddenKinds.Contains(kind);
                var chip = Chip(kind, 22f);
                chip.style.opacity = hidden ? 0.25f : 1f;
                chip.style.marginBottom = 4f;
                chip.tooltip = LogisticsGame.KindTitle(kind) + (hidden ? " — скрыт, клик — показать" : " — клик, чтобы скрыть");
                chip.pickingMode = PickingMode.Position;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_hiddenKinds.Remove(kind)) _hiddenKinds.Add(kind);
                    _dirty = true;
                });
                filters.Add(chip);
            }
            _popupBody.Add(Pad(filters));
            bool any = false;
            foreach (var hub in _game.Hubs)
            {
                var waiting = _game.WaitingAt(hub.LocationId).Where(x => !_hiddenKinds.Contains(x.Kind)).ToList();
                if (waiting.Count == 0) continue;
                any = true;
                string kindName = hub.Kind == HubKind.Airport ? "аэропорт" : hub.Kind == HubKind.Port ? "порт" : "вокзал";
                var title = Ui.Row();
                title.style.alignItems = Align.Center;
                title.style.marginTop = 6f;
                var hubIcon = HubEventIcons.Hub(hub.Kind, 22f);
                if (hubIcon != null)
                {
                    hubIcon.style.marginRight = 6f;
                    title.Add(hubIcon);
                }
                title.Add(Ui.Text($"{_game.Map.LocationName(hub.LocationId)} · {kindName}, ур. {hub.Level}", 13f, Theme.TextDim, true));
                _popupBody.Add(Pad(title));
                foreach (var cargo in waiting) _popupBody.Add(Pad(CargoRow(cargo, true)));
            }
            if (!any) _popupBody.Add(Pad(Hint(_hiddenKinds.Count > 0 ? "Нет грузов выбранных видов." : "Пусто — новые грузы появятся в следующем раунде.")));
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
            row.tooltip = "Подробнее о грузе";
            row.Add(Chip(cargo.Kind, 20f));
            int roundsLeft = cargo.Kind == CargoKind.Perishable ? Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round) : -1;
            var text = Ui.Text($"{LogisticsGame.KindTitle(cargo.Kind)} → {_game.Map.LocationName(cargo.To)} · {cargo.Value} очк." +
                               (roundsLeft >= 0 ? $" · норма {roundsLeft} р." : "") + (ForContract(cargo) ? " · заказ мэрии" : ""), 12f, Theme.Text);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexShrink = 1f;
            text.style.flexGrow = 1f;
            row.Add(text);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                _popupKind = "cargoDetail";
                _popupCargo = cargo.Id;
                _dirty = true;
            });
            if (withPick)
            {
                var pick = Ui.Button("Выбрать", () => AssignCargo(cargo), ButtonKind.Secondary, 12f);
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
                Toast(_game.Phase == MagnatePhase.Build ? "Грузы назначаются в фазе маршрутов — сначала «Дальше: маршруты фур»." : "Сейчас не ваш ход.");
                _app.Sound.Play(Sfx.Error, 0.5f);
                return;
            }
            var truck = BestTruckFor(cargo, out var toHub);
            if (truck == null) { Toast("Нет свободной фуры, которая доедет до этого хаба."); _app.Sound.Play(Sfx.Error, 0.5f); return; }
            string error;
            if (truck.Location == cargo.From)
            {
                error = _game.PlanTruck(truck.Id, cargo.Id, null);
                if (error == null) Toast($"Фура {TruckNumber(truck)} берёт груз: {LogisticsGame.KindTitle(cargo.Kind).ToLowerInvariant()} → {_game.Map.LocationName(cargo.To)}.");
            }
            else
            {
                error = _game.PlanTruck(truck.Id, null, cargo.From);
                if (error == null) Toast($"Фура {TruckNumber(truck)} едет за грузом на {_game.Map.LocationName(cargo.From)} (~{_game.RouteRounds(toHub)} р.). Груз возьмёте, когда доедете, если его не заберут раньше.", 4f);
            }
            if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); return; }
            _app.Sound.Play(Sfx.Card, 0.6f);
            _selectedTruck = truck.Id;
            _popupKind = "truck";
            _popupSection = 0;
            _dirty = true;
        }

        /// <summary>Карточка груза: правило вида, откуда и куда, норма, путь на карте, время, плата, очки, кто повезёт.</summary>
        void FillCargoDetail(Cargo cargo)
        {
            if (cargo == null) { FillCargoList(); return; }
            _popupTitle.text = $"{LogisticsGame.KindTitle(cargo.Kind)} → {_game.Map.LocationName(cargo.To)}";
            var back = Ui.Button("← Все грузы", () => { _popupKind = "cargo"; _dirty = true; }, ButtonKind.Ghost, 12f);
            back.style.alignSelf = Align.FlexStart;
            _popupBody.Add(Pad(back));

            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            head.style.marginTop = 4f;
            head.Add(Chip(cargo.Kind, 26f));
            var rule = Ui.Text(LogisticsGame.KindRule(cargo.Kind, _game.Config), 12f, Theme.Text);
            rule.style.whiteSpace = WhiteSpace.Normal;
            rule.style.flexShrink = 1f;
            head.Add(rule);
            _popupBody.Add(Pad(head));

            var lines = new List<string>
            {
                $"Откуда: {_game.Map.LocationName(cargo.From)} · куда: {_game.Map.LocationName(cargo.To)}",
                $"Цена при доставке: {cargo.Value} очк. (до множителя репутации)"
            };
            if (cargo.Kind == CargoKind.Perishable)
                lines.Add($"Норма: ещё {Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round)} р., дальше −{_game.Config.PerishableLatePenalty} за каждый раунд опоздания");
            if (ForContract(cargo)) lines.Add("Засчитывается в заказ мэрии");
            var delivery = _game.FindRoute(_game.CurrentPlayerIndex, cargo.From, cargo.To, cargo.Kind);
            if (delivery != null)
            {
                _popupPreview = delivery;
                lines.Add($"Путь доставки (золотой на карте): {_game.RouteLength(delivery)} участков, ~{_game.RouteRounds(delivery, cargo.Kind)} р., " +
                          $"проезд −{_game.RouteTolls(_game.CurrentPlayerIndex, delivery)}, забитых дорог: {_game.RouteRisk(delivery)}");
                if (cargo.Kind == CargoKind.Hazard && _game.RouteEntersBribeZone(cargo.From, delivery))
                    lines.Add($"Путь идёт через Центр: взятка {_game.Config.HazardBribe} влияния, иначе конфискация");
            }
            else lines.Add(cargo.Kind == CargoKind.Heavy || cargo.Kind == CargoKind.Oversized
                ? "Сейчас не довезти: нужны широкие дороги — бетон, мосты, магистрали"
                : "Сейчас не довезти по открытым дорогам");
            foreach (var line in lines)
            {
                var label = Hint("• " + line);
                label.style.color = Theme.Text;
                label.style.marginTop = 4f;
                _popupBody.Add(Pad(label));
            }

            var truck = BestTruckFor(cargo, out var toHub);
            if (truck != null && delivery != null)
            {
                int rounds = _game.RouteRounds(toHub) + _game.RouteRounds(delivery, cargo.Kind);
                int value = _game.ValueIfDelivered(cargo, rounds, truck.Owner);
                string who = truck.Location == cargo.From
                    ? $"Фура {TruckNumber(truck)} уже на хабе: привезёт за ~{rounds} р. и получит ~{value} очк."
                    : $"Ближайшая свободная — фура {TruckNumber(truck)}: до хаба ~{_game.RouteRounds(toHub)} р., с доставкой ~{rounds} р., ~{value} очк.";
                var info = Hint(who);
                info.style.color = Theme.Gold;
                info.style.marginTop = 8f;
                _popupBody.Add(Pad(info));
                var take = Ui.Button(truck.Location == cargo.From ? $"Взять фурой {TruckNumber(truck)}" : $"Отправить фуру {TruckNumber(truck)} к хабу",
                    () => AssignCargo(cargo), ButtonKind.Primary, 13f);
                take.style.marginTop = 8f;
                take.style.alignSelf = Align.FlexStart;
                if (_coachTarget == "cargo") Highlight(take);
                _popupBody.Add(Pad(take));
            }
            else if (_game.Phase == MagnatePhase.Build && HumanTurn)
                _popupBody.Add(Pad(Hint("Фуры назначаются в фазе маршрутов.")));
        }

        // ---------------------------------------------------------------- Фура

        void FillTruck(Truck truck)
        {
            if (truck == null) { ClosePopup(false); return; }
            int number = TruckNumber(truck);
            var cargo = _game.GetCargo(truck.Cargo);
            var planned = _game.GetCargo(truck.PlannedCargo);
            var kind = cargo?.Kind ?? planned?.Kind;
            _popupTitle.text = $"Фура {number} · {_game.Map.LocationName(truck.Location)} · скорость {_game.SpeedOf(kind)}";
            string status = cargo != null
                ? $"Везёт: {LogisticsGame.KindTitle(cargo.Kind).ToLowerInvariant()} → {_game.Map.LocationName(cargo.To)} " +
                  $"(~{_game.ValueIfDelivered(cargo, _game.RouteRounds(truck.Route, cargo.Kind), truck.Owner)} очк.)" +
                  (cargo.Kind == CargoKind.Valuable ? (cargo.Escorted ? " · с охраной" : " · без охраны!") : "")
                : planned != null ? $"Берёт: {LogisticsGame.KindTitle(planned.Kind).ToLowerInvariant()} → {_game.Map.LocationName(planned.To)}"
                : truck.Target != null ? $"Пустая → {_game.Map.LocationName(truck.Target)}"
                : "Стоит, ждёт задания";
            var statusLabel = Ui.Text(status, 13f, Theme.Text, true);
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _popupBody.Add(Pad(statusLabel));
            if (truck.Route.Count > 0)
            {
                bool center = kind == CargoKind.Hazard && _game.RouteEntersBribeZone(truck.Location, truck.Route);
                _popupBody.Add(Pad(Hint($"Маршрут (белый на карте): {_game.RouteLength(truck.Route)} участков, ~{_game.RouteRounds(truck.Route, kind)} р. · " +
                                        $"проезд −{_game.RouteTolls(truck.Owner, truck.Route)} · забитых дорог: {_game.RouteRisk(truck.Route)}" +
                                        (center ? $" · через Центр: взятка {_game.Config.HazardBribe}" : ""))));
            }
            if (cargo == null)
            {
                var waiting = _game.WaitingAt(truck.Location).ToList();
                var caption = Ui.Text(waiting.Count > 0 ? "Грузы на этом хабе — клик по «Взять»:" : "Здесь грузов нет.", 13f, Theme.TextDim, true);
                caption.style.marginTop = 8f;
                _popupBody.Add(Pad(caption));
                foreach (var item in waiting)
                {
                    var row = CargoRow(item, false);
                    var path = _game.FindRoute(truck.Owner, truck.Location, item.To, item.Kind);
                    var takeButton = Ui.Button(path != null ? $"Взять · {_game.RouteRounds(path, item.Kind)} р." : "Не проехать", () =>
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
                _popupBody.Add(Pad(Hint("Или кликните станцию на карте — фура поедет туда (при наведении видны путь, время и плата). " +
                                        "Грузы на других хабах — в разделе «Грузы», кнопка «Выбрать».")));
            }
            if (truck.Route.Count > 0 || truck.PlannedCargo != null)
            {
                var clear = Ui.Button("Отменить маршрут", () => _game.ClearTruck(truck.Id), ButtonKind.Ghost, 12f);
                clear.style.marginTop = 8f;
                clear.style.alignSelf = Align.FlexStart;
                _popupBody.Add(Pad(clear));
            }
        }

        // ---------------------------------------------------------------- Заказы, правила, журнал

        void FillContracts()
        {
            _popupTitle.text = "Заказы мэрии";
            if (_game.Contracts.Count == 0) { _popupBody.Add(Pad(Hint("Заказов нет — новые появятся позже."))); return; }
            foreach (var contract in _game.Contracts)
            {
                var card = Ui.Card(10f);
                card.style.marginLeft = 14f;
                card.style.marginRight = 14f;
                card.style.marginBottom = 8f;
                card.style.backgroundColor = Theme.PanelLight;
                var titleRow = Ui.Row();
                titleRow.style.alignItems = Align.Center;
                if (contract.Kind.HasValue) titleRow.Add(Chip(contract.Kind.Value, 18f));
                var title = Ui.Text(contract.Title + (contract.Winner >= 0 ? $" — выполнил {_game.Players[contract.Winner].Name}" : ""), 14f, Theme.Text, true);
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.flexShrink = 1f;
                titleRow.Add(title);
                card.Add(titleRow);
                card.Add(Hint($"Первому: +{contract.RewardPoints} очков, +{contract.RewardInfluence} влияния и репутация. Срок — до конца раунда {contract.EndRound}."));
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

        void FillRules()
        {
            _popupTitle.text = "Правила коротко";
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
            {
                var row = Ui.Row();
                row.style.marginBottom = 4f;
                row.Add(Chip(kind, 18f));
                var text = Ui.Text($"{LogisticsGame.KindTitle(kind)}: {LogisticsGame.KindRule(kind, _game.Config)}", 12f, Theme.Text);
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.flexShrink = 1f;
                row.Add(text);
                _popupBody.Add(Pad(row));
            }
            var more = Hint($"Пропускная способность за раунд: улица {_game.Config.CapacityStreet}, широкая дорога и мост {_game.Config.CapacityWide}, " +
                            $"магистраль {_game.Config.CapacityHighway}. Больше фур — пробка: все они стоят, каждая −{_game.Config.JamPenalty}. " +
                            "Серые дороги — городские: общие и бесплатные. Хаб можно развить кликом в фазе стройки. Репутация растёт за доставки вовремя " +
                            "и заказы мэрии, падает за пробки и потери; с уровнем растут очки за доставку (до ×1,5).");
            more.style.marginTop = 8f;
            _popupBody.Add(Pad(more));
        }

        void FillLog()
        {
            _popupTitle.text = "Журнал";
            if (_log.Count == 0) _popupBody.Add(Pad(Hint("Пока пусто.")));
            foreach (var line in _log.Skip(Math.Max(0, _log.Count - 40)).Reverse())
            {
                var label = Hint(line);
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

        void RefreshPlayers()
        {
            _players.Clear();
            foreach (var p in _game.Players)
            {
                var chip = Ui.Row();
                chip.style.alignItems = Align.Center;
                chip.style.marginRight = 10f;
                Ui.Padding(chip, 4f, 8f);
                Ui.Radius(chip, 8f);
                bool current = p.Index == _game.CurrentPlayerIndex && _game.Phase != MagnatePhase.GameOver;
                chip.style.backgroundColor = current ? new Color(1f, 1f, 1f, 0.10f) : Color.clear;
                var dot = new VisualElement();
                dot.style.width = 12f;
                dot.style.height = 12f;
                dot.style.backgroundColor = Palette.PlayerColor(p.Index);
                Ui.Radius(dot, 3f);
                dot.style.marginRight = 6f;
                chip.Add(dot);
                chip.Add(Ui.Text($"{p.Name}  {p.Score}", 14f, Theme.Text, current));
                // Значок репутации: звёзды по уровню (Новичок — одна, Легенда — пять).
                var stars = Ui.Text(" " + new string('★', (int)p.Level + 1), 12f, Theme.Gold);
                stars.tooltip = $"Репутация {p.Reputation}: {LogisticsGame.LevelTitle(p.Level)} (очки за доставку ×{LogisticsGame.LevelMultiplier(p.Level):0.##})";
                stars.pickingMode = PickingMode.Position;
                chip.Add(stars);
                chip.Add(Ui.Text($"  влияние {p.Influence}" + (current ? $" · стройка {p.BuildPoints}" : ""), 12f, Theme.TextDim));
                _players.Add(chip);
            }
        }

        /// <summary>
        /// Подсветка: в стройку — доступные дороги, в план — маршруты своих фур (выбранная белым), голубой — путь
        /// при наведении на станцию, золотой — путь груза из открытой карточки груза.
        /// </summary>
        void RefreshHighlights()
        {
            _map.Affordable.Clear();
            _map.PathRoads.Clear();
            if (_popupPreview != null)
                foreach (var id in _popupPreview) _map.PathRoads[id] = new Color(1f, 0.82f, 0.3f, 0.9f);
            if (!HumanTurn) return;
            var player = _game.CurrentPlayer;
            if (_game.Phase == MagnatePhase.Build)
            {
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
