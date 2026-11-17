using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Экран «Логистического магната»: карта (MapView по доске LogisticsGame.Board, с 3D-городом), сверху —
    /// раунд и компании, справа — панель хода: стройка (клик по дороге — купить, по своей — магистраль),
    /// затем фуры (выбрать фуру → взять груз на её хабе или кликнуть станцию на карте). Фуры и грузы рисуются
    /// поверх карты (MapView.Overlay). Когда все спланировали — фуры едут разом (анимация), пробки краснеют.
    /// Несколько людей за одним экраном — между ходами заставка «передайте ход», чужие планы не видны.
    /// </summary>
    public sealed class MagnateView
    {
        const float TopBarHeight = 56f, PanelWidth = 390f, AnimationSeconds = 1.9f;

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly LogisticsGame _game;
        readonly System.Random _botRandom = new System.Random(17);

        public VisualElement Root { get; }
        readonly VisualElement _mapArea, _players, _panel, _handoff, _results;
        readonly ScrollView _panelScroll;
        readonly Label _roundLabel, _tooltip, _banner;
        readonly MapView _map;
        readonly List<string> _log = new List<string>();

        City3DScene _city3D;
        bool _dirty = true;
        int _selectedTruck = -1;
        float _botTimer = -1f;
        bool _awaitingHandoff;
        RoundReport _animating;
        float _animationTime;
        RoundReport _shownReport;

        public MagnateView(CityHighwaysApp app, GameSetup setup, LogisticsGame game)
        {
            _app = app;
            _setup = setup;
            _game = game;
            _game.StateChanged += () => _dirty = true;
            _game.Log += message =>
            {
                _log.Add(message);
                if (_log.Count > 40) _log.RemoveAt(0);
                _dirty = true;
            };

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.backgroundColor = Theme.Background;

            _mapArea = Ui.Box();
            _mapArea.style.position = Position.Absolute;
            _mapArea.style.left = 0f;
            _mapArea.style.right = PanelWidth;
            _mapArea.style.top = TopBarHeight;
            _mapArea.style.bottom = 0f;
            Root.Add(_mapArea);

            _map = new MapView();
            Ui.Fill(_map);
            _map.Hovered += OnRoadHover;
            _map.LocationHovered += OnLocationHover;
            _map.RoadClicked += OnRoadClicked;
            _map.LocationClicked += OnLocationClicked;
            _map.Overlay = DrawOverlay;
            _mapArea.Add(_map);

            _banner = Ui.Text("", 16f, Theme.Text, true);
            _banner.style.position = Position.Absolute;
            _banner.style.top = 12f;
            _banner.style.left = Length.Percent(50f);
            _banner.style.translate = new Translate(Length.Percent(-50f), new Length(0f));
            _banner.style.backgroundColor = Theme.PanelGlass;
            Ui.Padding(_banner, 8f, 16f);
            Ui.Radius(_banner, 10f);
            _banner.pickingMode = PickingMode.Ignore;
            _mapArea.Add(_banner);

            _tooltip = Ui.Text("", 13f, Theme.Text);
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.backgroundColor = new Color(0.08f, 0.10f, 0.13f, 0.95f);
            _tooltip.style.whiteSpace = WhiteSpace.Normal;
            _tooltip.style.maxWidth = 300f;
            Ui.Padding(_tooltip, 6f, 10f);
            Ui.Radius(_tooltip, 6f);
            _tooltip.pickingMode = PickingMode.Ignore;
            Ui.Show(_tooltip, false);
            _mapArea.Add(_tooltip);

            // Верхняя панель: город и раунд, компании, меню.
            var top = Ui.Row();
            top.style.position = Position.Absolute;
            top.style.left = 0f;
            top.style.right = 0f;
            top.style.top = 0f;
            top.style.height = TopBarHeight;
            top.style.backgroundColor = Theme.Panel;
            top.style.alignItems = Align.Center;
            Ui.Padding(top, 0f, 16f);
            Root.Add(top);
            var title = Ui.Text("Логистический магнат", 17f, Theme.Text, true);
            title.style.marginRight = 14f;
            top.Add(title);
            _roundLabel = Ui.Text("", 14f, Theme.TextDim);
            _roundLabel.style.marginRight = 18f;
            top.Add(_roundLabel);
            _players = Ui.Row();
            _players.style.flexGrow = 1f;
            top.Add(_players);
            var menu = Ui.Button("Меню", () => _app.ShowMenu(), ButtonKind.Secondary, 14f);
            menu.style.width = 96f;
            top.Add(menu);

            // Правая панель хода.
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.right = 0f;
            _panel.style.top = TopBarHeight;
            _panel.style.bottom = 0f;
            _panel.style.width = PanelWidth;
            _panel.style.backgroundColor = Theme.Panel;
            Root.Add(_panel);
            _panelScroll = new ScrollView(ScrollViewMode.Vertical);
            _panelScroll.style.flexGrow = 1f;
            Ui.StyleScroller(_panelScroll);
            _panel.Add(_panelScroll);

            _handoff = Overlay();
            _results = Overlay();
            Root.Add(_handoff);
            Root.Add(_results);
        }

        static VisualElement Overlay()
        {
            var overlay = new VisualElement();
            Ui.Fill(overlay);
            overlay.style.backgroundColor = Theme.ShadeOpaque;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.pickingMode = PickingMode.Position;
            Ui.Show(overlay, false);
            return overlay;
        }

        // ================================================================== Запуск и кадр

        public void Begin()
        {
            _map.TiltEnabled = _app.Display == null || _app.Display.tiltedBoard;
            _map.SetGame(_game.Board);
            _log.Add($"Город {_game.Map.Name}. Хабы: {string.Join(", ", _game.Hubs.Select(h => _game.Map.LocationName(h.LocationId)))}.");
            StartTurn();
            _dirty = true;
        }

        public void Tick(float deltaTime)
        {
            _map.Tick(deltaTime);
            Sync3D(deltaTime);

            if (_animating != null)
            {
                _animationTime += deltaTime;
                if (_animationTime >= AnimationSeconds) FinishAnimation();
            }
            else if (_game.LastReport != null && _game.LastReport != _shownReport)
                StartAnimation(_game.LastReport);

            if (_botTimer >= 0f && _animating == null)
            {
                _botTimer -= deltaTime;
                if (_botTimer < 0f) BotStep();
            }

            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
        }

        void StartTurn()
        {
            if (_game.Phase == MagnatePhase.GameOver)
            {
                ShowResults();
                return;
            }
            _selectedTruck = -1;
            var player = _game.CurrentPlayer;
            if (player.IsBot)
            {
                _botTimer = 0.6f;
                return;
            }
            // Несколько людей за одним экраном — заставка, чтобы не подсмотреть чужой план.
            if (_game.Players.Count(p => !p.IsBot) > 1)
            {
                _awaitingHandoff = true;
                _handoff.Clear();
                var card = Ui.Card(26f);
                card.style.backgroundColor = Theme.Panel;
                card.style.alignItems = Align.Center;
                card.Add(Ui.Text($"Ход: {player.Name}", 26f, Palette.PlayerColor(player.Index), true));
                var hint = Ui.Text("Соперники, отвернитесь: сейчас будут планироваться маршруты.", 15f, Theme.TextDim);
                hint.style.marginTop = 10f;
                hint.style.marginBottom = 18f;
                card.Add(hint);
                card.Add(Ui.Button("Начать ход", () =>
                {
                    _awaitingHandoff = false;
                    Ui.Show(_handoff, false);
                    _dirty = true;
                }, ButtonKind.Primary, 16f));
                _handoff.Add(card);
                Ui.Show(_handoff, true);
            }
            _dirty = true;
        }

        void BotStep()
        {
            if (_game.Phase == MagnatePhase.GameOver || !_game.CurrentPlayer.IsBot) return;
            if (_game.Phase == MagnatePhase.Build)
            {
                LogisticsBot.Build(_game, _botRandom);
                _botTimer = 0.35f;
                return;
            }
            int round = _game.Round;
            LogisticsBot.Plan(_game, _botRandom);
            AfterTurn(round);
        }

        /// <summary>После «Готово»: следующий игрок или (если раунд закрылся) анимация движения.</summary>
        void AfterTurn(int roundBefore)
        {
            _botTimer = -1f;
            _selectedTruck = -1;
            if (_game.LastReport != null && _game.LastReport != _shownReport) return; // запустит Tick: StartAnimation
            StartTurn();
        }

        // ================================================================== Анимация раунда

        void StartAnimation(RoundReport report)
        {
            _animating = report;
            _shownReport = report;
            _animationTime = 0f;
            _banner.text = $"Раунд {report.Round}: фуры в пути…";
            Ui.Show(_banner, true);
            if (report.Jams.Count > 0) _app.Sound.Play(Sfx.Horn, 0.6f);
            _dirty = true;
        }

        void FinishAnimation()
        {
            var report = _animating;
            _animating = null;
            int delivered = report.Moves.Count(m => m.Delivered != null);
            _banner.text = delivered > 0 || report.Jams.Count > 0
                ? $"Доставлено: {delivered}" + (report.Jams.Count > 0 ? $" · пробок: {report.Jams.Count}" : "")
                : "Раунд прошёл спокойно";
            if (delivered > 0) _app.Sound.Play(Sfx.Coin, 0.7f);
            _app.Tweener.After(2.2f, () => { if (_animating == null) Ui.Show(_banner, false); });
            StartTurn();
        }

        /// <summary>Где фура на экране: во время анимации — на пути по пройденным местам, иначе — на своём месте.</summary>
        Vector2 TruckPosition(Truck truck)
        {
            if (_animating != null)
            {
                var move = _animating.Moves.FirstOrDefault(m => m.Truck == truck.Id);
                if (move != null && move.Stops.Count > 1)
                {
                    float k = Ui.EaseInOutCubic(Mathf.Clamp01(_animationTime / (AnimationSeconds * 0.85f)));
                    float u = k * (move.Stops.Count - 1);
                    int i = Mathf.Min(move.Stops.Count - 2, Mathf.FloorToInt(u));
                    return Vector2.Lerp(_map.ScreenPositionOf(move.Stops[i]), _map.ScreenPositionOf(move.Stops[i + 1]), u - i);
                }
                if (move != null && move.JammedOn != null)
                {
                    // Застрявшая фура подрагивает у въезда на дорогу в пробке.
                    var road = _game.Map.GetRoad(move.JammedOn);
                    string other = road.A == move.From ? road.B : road.A;
                    var a = _map.ScreenPositionOf(move.From);
                    var b = _map.ScreenPositionOf(other);
                    return Vector2.Lerp(a, b, 0.18f + 0.04f * Mathf.Sin(_animationTime * 30f));
                }
                if (move != null) return _map.ScreenPositionOf(move.From);
            }
            return _map.ScreenPositionOf(truck.Location);
        }

        // ================================================================== Рисунок поверх карты

        static readonly Color RegularColor = new Color(0.72f, 0.52f, 0.32f);
        static readonly Color PerishableColor = new Color(0.36f, 0.80f, 0.36f);
        static readonly Color HeavyColor = new Color(1.00f, 0.60f, 0.18f);
        static readonly Color HazardColor = new Color(1.00f, 0.86f, 0.10f);

        public static Color KindColor(CargoKind kind) =>
            kind == CargoKind.Perishable ? PerishableColor : kind == CargoKind.Heavy ? HeavyColor : kind == CargoKind.Hazard ? HazardColor : RegularColor;

        public static string KindLetter(CargoKind kind) =>
            kind == CargoKind.Perishable ? "С" : kind == CargoKind.Heavy ? "Т" : kind == CargoKind.Hazard ? "!" : "О";

        void DrawOverlay(Painter2D p)
        {
            // Грузы на хабах — квадратики цвета груза над станцией.
            foreach (var hub in _game.Hubs)
            {
                var at = _map.ScreenPositionOf(hub.LocationId);
                int k = 0;
                foreach (var cargo in _game.WaitingAt(hub.LocationId))
                {
                    var c = at + new Vector2(13f + k * 11f, -15f);
                    Square(p, c, 4.6f, new Color(0f, 0f, 0f, 0.6f));
                    Square(p, c, 3.8f, KindColor(cargo.Kind));
                    k++;
                }
            }

            // Фуры: квадрат цвета компании, внутри — цвет груза. Несколько на одной станции — веером.
            var groups = new Dictionary<string, int>();
            foreach (var truck in _game.Trucks)
            {
                var position = TruckPosition(truck);
                string key = _animating == null ? truck.Location : null;
                int index = 0;
                if (key != null)
                {
                    groups.TryGetValue(key, out index);
                    groups[key] = index + 1;
                }
                float angle = index * 1.9f + 0.6f;
                var c = position + (key != null ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (index == 0 ? 0f : 9f) + new Vector2(-12f, -12f) : Vector2.zero);
                var color = Palette.PlayerColor(truck.Owner);
                bool selected = truck.Id == _selectedTruck;
                if (selected) Square(p, c, 9f, Color.white);
                Square(p, c, 7f, new Color(0.05f, 0.06f, 0.08f, 0.95f));
                Square(p, c, 5.8f, color);
                var cargo = _game.GetCargo(truck.Cargo);
                if (cargo != null)
                {
                    Square(p, c, 3f, new Color(0f, 0f, 0f, 0.7f));
                    Square(p, c, 2.4f, KindColor(cargo.Kind));
                }
            }
        }

        static void Square(Painter2D p, Vector2 c, float half, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-half, -half));
            p.LineTo(c + new Vector2(half, -half));
            p.LineTo(c + new Vector2(half, half));
            p.LineTo(c + new Vector2(-half, half));
            p.ClosePath();
            p.Fill();
        }

        // ================================================================== Карта: наведение и клики

        bool HumanTurn => _animating == null && !_awaitingHandoff && _game.Phase != MagnatePhase.GameOver && !_game.CurrentPlayer.IsBot;

        void OnRoadHover(Road road, Vector2 position)
        {
            if (road == null) { Ui.Show(_tooltip, false); return; }
            var owner = _game.Board.GetRoadOwner(road.Id);
            string who = !owner.HasValue ? "свободна — можно купить" : owner == _game.PublicOwner ? "городская (общая, бесплатно)"
                : $"компания {_game.Players[owner.Value].Name}" + (owner == _game.CurrentPlayerIndex ? " (ваша)" : $" · проезд −{_game.Config.TollPerRoad}");
            string kind = _game.IsHighway(road.Id) ? "магистраль" : road.IsBridge ? "мост" : _game.IsWide(road) ? "широкая (бетон)" : "улица";
            string text = $"{_game.Map.LocationName(road.A)} — {_game.Map.LocationName(road.B)}\n{kind}, длина {road.Length} · {who}\n" +
                          $"Пропускает {_game.Capacity(road)} фур за раунд · в прошлом раунде: {_game.LastTraffic(road.Id)}" +
                          (_game.IsWide(road) ? "\nТяжеловесы — можно" : "\nТяжеловесы — нельзя");
            if (HumanTurn && _game.Phase == MagnatePhase.Build)
            {
                if (!owner.HasValue) text += $"\nКупить: {_game.BuildCost(road)} очков стройки";
                else if (owner == _game.CurrentPlayerIndex && !_game.IsHighway(road.Id)) text += $"\nУлучшить до магистрали: {_game.UpgradeCost(road)}";
            }
            ShowTooltip(text, position);
        }

        void OnLocationHover(Location location, Vector2 position)
        {
            if (location == null) { Ui.Show(_tooltip, false); return; }
            var hub = _game.Hubs.FirstOrDefault(h => h.LocationId == location.Id);
            string text = location.Name + " · " + _game.Map.DistrictName(location.DistrictId);
            if (hub != null)
            {
                text += "\n" + (hub.Kind == HubKind.Airport ? "Аэропорт" : hub.Kind == HubKind.Port ? "Порт" : "Вокзал") + " — здесь появляются грузы";
                foreach (var cargo in _game.WaitingAt(location.Id))
                    text += $"\n• {LogisticsGame.KindTitle(cargo.Kind)} → {_game.Map.LocationName(cargo.To)}: {cargo.Value} очк.";
            }
            if (location.DistrictId == _game.Config.BribeDistrict) text += $"\nОпасный груз здесь — взятка {_game.Config.HazardBribe} влияния";
            if (HumanTurn && _game.Phase == MagnatePhase.Plan && _selectedTruck >= 0) text += "\nКлик — фура поедет сюда";
            ShowTooltip(text, position);
        }

        void ShowTooltip(string text, Vector2 position)
        {
            _tooltip.text = text;
            _tooltip.style.left = Mathf.Min(position.x + 16f, _mapArea.layout.width - 310f);
            _tooltip.style.top = Mathf.Min(position.y + 16f, _mapArea.layout.height - 120f);
            Ui.Show(_tooltip, true);
        }

        void OnRoadClicked(Road road, Vector2 position)
        {
            if (!HumanTurn || _game.Phase != MagnatePhase.Build) return;
            var error = _game.BuildOrUpgrade(road.Id);
            if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); }
            else _app.Sound.Play(Sfx.Clack, 0.8f);
            OnRoadHover(road, position);
        }

        void OnLocationClicked(Location location, Vector2 position)
        {
            if (!HumanTurn || _game.Phase != MagnatePhase.Plan || _selectedTruck < 0) return;
            var truck = _game.GetTruck(_selectedTruck);
            var error = _game.PlanTruck(truck.Id, null, location.Id);
            if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); }
            else _app.Sound.Play(Sfx.Click, 0.6f);
        }

        void Toast(string text)
        {
            _banner.text = text;
            Ui.Show(_banner, true);
            _app.Tweener.After(2.4f, () => { if (_animating == null && _banner.text == text) Ui.Show(_banner, false); });
        }

        // ================================================================== Панель

        void Refresh()
        {
            _roundLabel.text = $"{_game.Map.Name} · раунд {_game.Round}/{_game.Config.Rounds}";
            RefreshPlayers();
            RefreshHighlights();
            _panelScroll.Clear();
            var c = _panelScroll.contentContainer;
            var player = _game.CurrentPlayer;

            if (_game.Phase == MagnatePhase.GameOver) { c.Add(Section("Партия окончена")); return; }
            if (_animating != null) { c.Add(Section("Фуры в пути…")); AddLog(c); return; }

            var head = Ui.Text(player.Name, 22f, Palette.PlayerColor(player.Index), true);
            c.Add(Padded(head));
            if (player.IsBot)
            {
                c.Add(Padded(Ui.Text(_game.Phase == MagnatePhase.Build ? "Строит дороги…" : "Планирует маршруты…", 15f, Theme.TextDim)));
                AddCargoBoard(c);
                AddLog(c);
                return;
            }
            if (_awaitingHandoff) return;

            if (_game.Phase == MagnatePhase.Build)
            {
                c.Add(Section($"Стройка · очков: {player.BuildPoints}"));
                c.Add(Padded(Wrap($"Кликните свободную дорогу на карте — купить (цена = длина, мост +{_game.Config.BridgeExtraCost}). " +
                                  "Клик по своей дороге — магистраль: пропускает больше фур и тяжеловесов. " +
                                  $"Свои дороги бесплатны для ваших фур, с чужих фур — {_game.Config.TollPerRoad} очко за проезд. " +
                                  $"Каждый раунд +{_game.Config.BuildPointsPerRound} очков стройки (до {_game.Config.MaxBuildPoints}).")));
                var next = Ui.Button("Дальше: маршруты фур →", () => { _game.FinishBuild(); _app.Sound.Play(Sfx.Click, 0.6f); }, ButtonKind.Primary, 15f);
                next.style.marginTop = 6f;
                c.Add(Padded(next));
            }
            else
            {
                c.Add(Section("Маршруты фур"));
                c.Add(Padded(Wrap("Выберите фуру. На хабе — возьмите груз, и фура повезёт его к получателю. Или кликните станцию на карте — фура поедет туда. " +
                                  $"За раунд фура проезжает {_game.Config.TruckSpeed} участков; маршрут продолжится в следующем раунде.")));
                foreach (var truck in _game.Trucks.Where(t => t.Owner == player.Index)) c.Add(TruckCard(truck));
                bool last = _game.IsLastTurnInRound;
                var done = Ui.Button(last ? "Готово — фуры поедут" : "Готово — передать ход", () =>
                {
                    int round = _game.Round;
                    _game.FinishPlanning();
                    _app.Sound.Play(Sfx.Ticket, 0.6f);
                    AfterTurn(round);
                }, ButtonKind.Primary, 15f);
                done.style.marginTop = 8f;
                c.Add(Padded(done));
            }
            AddCargoBoard(c);
            AddRules(c);
            AddLog(c);
        }

        VisualElement TruckCard(Truck truck)
        {
            var card = Ui.Card(10f);
            card.style.marginLeft = 14f;
            card.style.marginRight = 14f;
            card.style.marginBottom = 8f;
            bool selected = truck.Id == _selectedTruck;
            card.style.backgroundColor = selected ? Theme.PanelHover : Theme.PanelLight;
            Ui.Border(card, selected ? 2f : 1f, selected ? GameColors.Accent : Theme.Line);
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedTruck = selected ? -1 : truck.Id;
                _dirty = true;
            });

            int number = _game.Trucks.Where(t => t.Owner == truck.Owner).ToList().IndexOf(truck) + 1;
            var cargo = _game.GetCargo(truck.Cargo);
            var planned = _game.GetCargo(truck.PlannedCargo);
            string where = _game.Map.LocationName(truck.Location);
            card.Add(Ui.Text($"Фура {number} · {where}", 15f, Theme.Text, true));
            string status = cargo != null ? $"Везёт: {LogisticsGame.KindTitle(cargo.Kind).ToLowerInvariant()} → {_game.Map.LocationName(cargo.To)} ({_game.ValueIfDelivered(cargo, _game.RouteRounds(truck.Route))} очк.)"
                : planned != null ? $"Берёт: {LogisticsGame.KindTitle(planned.Kind).ToLowerInvariant()} → {_game.Map.LocationName(planned.To)}"
                : truck.Target != null ? $"Пустая → {_game.Map.LocationName(truck.Target)}"
                : "Стоит, ждёт задания";
            card.Add(Wrap(status, 13f));
            if (truck.Route.Count > 0)
            {
                int length = _game.RouteLength(truck.Route);
                bool center = cargo?.Kind == CargoKind.Hazard || planned?.Kind == CargoKind.Hazard
                    ? _game.RouteEntersBribeZone(truck.Location, truck.Route) : false;
                card.Add(Wrap($"Маршрут: {length} участков, ~{_game.RouteRounds(truck.Route)} р." + (center ? $" · через Центр: взятка {_game.Config.HazardBribe}" : ""), 12f, Theme.TextDim));
            }
            if (!selected) return card;

            // Выбрана: грузы на её хабе и сброс маршрута.
            if (cargo == null)
            {
                var waiting = _game.WaitingAt(truck.Location).ToList();
                if (waiting.Count == 0) card.Add(Wrap("Здесь нет грузов. Кликните хаб на карте (квадратики — грузы), чтобы поехать за грузом.", 12f, Theme.TextDim));
                foreach (var item in waiting)
                {
                    var row = CargoRow(item, true);
                    row.RegisterCallback<ClickEvent>(e =>
                    {
                        e.StopPropagation();
                        var error = _game.PlanTruck(truck.Id, item.Id, null);
                        if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); }
                        else _app.Sound.Play(Sfx.Card, 0.6f);
                    });
                    card.Add(row);
                }
            }
            if (truck.Route.Count > 0 || truck.PlannedCargo != null)
            {
                var clear = Ui.Button("Отменить маршрут", () => _game.ClearTruck(truck.Id), ButtonKind.Ghost, 12f);
                clear.style.marginTop = 4f;
                card.Add(clear);
            }
            return card;
        }

        VisualElement CargoRow(Cargo cargo, bool clickable)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            Ui.Padding(row, 4f, 6f);
            Ui.Radius(row, 6f);
            row.style.backgroundColor = clickable ? new Color(1f, 1f, 1f, 0.06f) : Color.clear;
            if (clickable) row.pickingMode = PickingMode.Position;
            var chip = Ui.Text(KindLetter(cargo.Kind), 12f, new Color(0.08f, 0.08f, 0.1f), true);
            chip.style.width = 20f;
            chip.style.height = 20f;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.style.backgroundColor = KindColor(cargo.Kind);
            Ui.Radius(chip, 4f);
            chip.style.marginRight = 8f;
            row.Add(chip);
            int roundsLeft = cargo.Kind == CargoKind.Perishable ? Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round) : -1;
            var text = Ui.Text($"{LogisticsGame.KindTitle(cargo.Kind)} → {_game.Map.LocationName(cargo.To)} · {cargo.Value} очк." +
                               (roundsLeft >= 0 ? $" · норма ещё {roundsLeft} р." : ""), 12f, Theme.Text);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexShrink = 1f;
            row.Add(text);
            return row;
        }

        void AddCargoBoard(VisualElement c)
        {
            c.Add(Section("Грузы на хабах"));
            bool any = false;
            foreach (var hub in _game.Hubs)
            {
                var waiting = _game.WaitingAt(hub.LocationId).ToList();
                if (waiting.Count == 0) continue;
                any = true;
                var title = Ui.Text(_game.Map.LocationName(hub.LocationId), 13f, Theme.TextDim, true);
                title.style.marginLeft = 16f;
                title.style.marginTop = 4f;
                c.Add(title);
                foreach (var cargo in waiting)
                {
                    var row = CargoRow(cargo, false);
                    row.style.marginLeft = 12f;
                    row.style.marginRight = 12f;
                    c.Add(row);
                }
            }
            if (!any) c.Add(Padded(Ui.Text("Пока пусто — новые грузы появятся в следующем раунде.", 13f, Theme.TextDim)));
        }

        void AddRules(VisualElement c)
        {
            c.Add(Section("Правила коротко"));
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
            {
                var row = Ui.Row();
                row.style.marginLeft = 14f;
                row.style.marginRight = 14f;
                row.style.marginBottom = 4f;
                var chip = Ui.Text(KindLetter(kind), 11f, new Color(0.08f, 0.08f, 0.1f), true);
                chip.style.width = 18f;
                chip.style.height = 18f;
                chip.style.unityTextAlign = TextAnchor.MiddleCenter;
                chip.style.backgroundColor = KindColor(kind);
                Ui.Radius(chip, 4f);
                chip.style.marginRight = 8f;
                row.Add(chip);
                var text = Wrap($"{LogisticsGame.KindTitle(kind)}: {LogisticsGame.KindRule(kind, _game.Config)}", 12f);
                text.style.flexShrink = 1f;
                row.Add(text);
                c.Add(row);
            }
            c.Add(Padded(Wrap($"Пропускная способность за раунд: улица {_game.Config.CapacityStreet}, широкая дорога и мост {_game.Config.CapacityWide}, " +
                              $"магистраль {_game.Config.CapacityHighway}. Больше фур — пробка: все они стоят, каждая −{_game.Config.JamPenalty}. " +
                              "Серые дороги — городские: общие и бесплатные.", 12f, Theme.TextDim)));
        }

        void AddLog(VisualElement c)
        {
            c.Add(Section("Журнал"));
            foreach (var line in _log.Skip(Math.Max(0, _log.Count - 10)).Reverse())
                c.Add(Padded(Wrap(line, 12f, Theme.TextDim)));
        }

        void RefreshPlayers()
        {
            _players.Clear();
            foreach (var p in _game.Players)
            {
                var chip = Ui.Row();
                chip.style.alignItems = Align.Center;
                chip.style.marginRight = 14f;
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
                var extra = Ui.Text($"  влияние {p.Influence}" + (current ? $" · стройка {p.BuildPoints}" : ""), 12f, Theme.TextDim);
                chip.Add(extra);
                _players.Add(chip);
            }
        }

        /// <summary>Подсветка на карте: в стройку — доступные дороги, в план — маршруты своих фур (выбранная ярче).</summary>
        void RefreshHighlights()
        {
            _map.Affordable.Clear();
            _map.PathRoads.Clear();
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
        }

        static Label Section(string text)
        {
            var label = Ui.Text(text, 15f, Theme.Text, true);
            label.style.marginTop = 16f;
            label.style.marginBottom = 6f;
            label.style.marginLeft = 16f;
            return label;
        }

        static VisualElement Padded(VisualElement e)
        {
            e.style.marginLeft = 16f;
            e.style.marginRight = 16f;
            e.style.marginBottom = 6f;
            return e;
        }

        static Label Wrap(string text, float size = 13f, Color? color = null)
        {
            var label = Ui.Text(text, size, color ?? Theme.Text);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        // ================================================================== Итоги

        void ShowResults()
        {
            _results.Clear();
            var card = Ui.Card(26f);
            card.style.backgroundColor = Theme.Panel;
            card.style.minWidth = 560f;
            var ranking = _game.Players.OrderByDescending(p => p.Score).ToList();
            card.Add(Ui.Text($"Победил {ranking[0].Name}!", 28f, Palette.PlayerColor(ranking[0].Index), true));
            var sub = Ui.Text($"{_game.Map.Name}, {_game.Config.Rounds} раундов", 14f, Theme.TextDim);
            sub.style.marginBottom = 16f;
            card.Add(sub);
            int place = 1;
            foreach (var p in ranking)
            {
                var row = Ui.Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 6f;
                row.Add(Ui.Text($"{place++}. {p.Name}", 17f, Palette.PlayerColor(p.Index), true));
                row.Add(Ui.Text($"{p.Score} очк. · доставок {p.Delivered} · пробок {p.Jams} · проезд +{p.TollIn}/−{p.TollOut}" +
                                (p.Confiscated > 0 ? $" · конфискаций {p.Confiscated}" : ""), 14f, Theme.Text));
                card.Add(row);
            }
            var buttons = Ui.Row();
            buttons.style.marginTop = 18f;
            var again = Ui.Button("Новый город", () => _app.StartGame(_setup), ButtonKind.Primary, 16f);
            again.style.marginRight = 10f;
            buttons.Add(again);
            buttons.Add(Ui.Button("В меню", () => _app.ShowMenu(), ButtonKind.Secondary, 16f));
            card.Add(buttons);
            _results.Add(card);
            Ui.Show(_results, true);
            _app.Sound.Play(Sfx.Fanfare, 0.7f);
        }

        // ================================================================== 3D-город

        public void Setup3D()
        {
            Release3D();
            var display = _app.Display;
            _map.CameraTiltDegrees = display != null ? display.tiltDegrees : 38f;
            if (display == null || display.city3D)
                _city3D = City3DScene.TryCreate(_game.Map, display != null ? Mathf.Clamp(display.city3DQuality, 0, 2) : 2);
            bool on = _city3D != null;
            _map.Use3D = on;
            var city = _city3D;
            _map.RoadHidden = null;
            if (on) _map.RoadHidden = id => city.IsUnderConstruction(id);
            Root.style.backgroundColor = on ? Color.clear : Theme.Background;
            _app.SetSceneBehind(on);
        }

        public void Release3D()
        {
            if (_city3D == null) return;
            _city3D.Dispose();
            _city3D = null;
            _map.RoadHidden = null;
            _map.Use3D = false;
            Root.style.backgroundColor = Theme.Background;
            _app.SetSceneBehind(false);
        }

        void Sync3D(float deltaTime)
        {
            if (_city3D == null || _map.panel == null) return;
            var panelSize = _map.panel.visualTree.layout.size;
            if (float.IsNaN(panelSize.x) || panelSize.x < 10f) return;
            var board = _game.Board;
            _city3D.Sync(_map.Layout, panelSize, _map.worldBound.position, _map.Light, id => board.IsDistrictOpen(id), deltaTime);
            _city3D.SyncRoads(board);
        }
    }
}
