using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Экран «Логистического магната». Карта на всю ширину между верхней строкой (город, раунд, событие, компании
    /// с репутацией) и нижней панелью (MagnateView.Panel.cs: контекстный раздел, «Грузы», «Заказы мэрии», «Действия»
    /// и всплывающие окна над ними). Карта спокойная: одно покрытие дорог, крупные фишки, значки хабов, фокус под
    /// курсором, слои; мини-карта слева внизу. Поверх карты — фуры (кабина и кузов цвета компании, груз — цветом
    /// вида), очереди в пробках с часами, перекрытые дороги, уровни хабов. Все фуры едут разом: скоропорт быстрее,
    /// тяжёлые — медленнее. Обучение — MagnateView.Coach.cs.
    /// </summary>
    public sealed partial class MagnateView
    {
        const float TopBarHeight = 56f, AnimationSeconds = 2.2f;

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly LogisticsGame _game;
        readonly System.Random _botRandom = new System.Random(17);

        public VisualElement Root { get; }
        readonly VisualElement _mapArea, _players, _handoff, _results;
        readonly Label _roundLabel, _eventChip, _tooltip, _banner;
        readonly VisualElement _eventIcon;
        readonly MapView _map;
        readonly MiniMapView _minimap;
        readonly List<string> _log = new List<string>();
        /// <summary>Кнопки «Правила» и «Журнал» в верхней строке (к их правому краю прижимаются их окна).</summary>
        VisualElement _topActions;
        readonly HashSet<CargoKind> _hiddenKinds = new HashSet<CargoKind>();
        VisualElement _layersPanel;
        readonly VisualElement _layersList;

        City3DScene _city3D;
        bool _dirty = true;
        int _selectedTruck = -1;
        float _botTimer = -1f;
        bool _awaitingHandoff, _resultsShown;
        RoundReport _animating;
        float _animationTime;
        RoundReport _shownReport;
        int _roundShown;
        List<string> _preview;
        float _time;

        public MagnateView(CityHighwaysApp app, GameSetup setup, LogisticsGame game)
        {
            _app = app;
            _setup = setup;
            _game = game;
            _game.StateChanged += () => _dirty = true;
            _game.Log += message =>
            {
                _log.Add(message);
                if (_log.Count > 60) _log.RemoveAt(0);
                _dirty = true;
            };

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.backgroundColor = Theme.Background;

            _mapArea = Ui.Box();
            _mapArea.style.position = Position.Absolute;
            _mapArea.style.left = 0f;
            _mapArea.style.right = 0f;
            _mapArea.style.top = TopBarHeight;
            _mapArea.style.bottom = BottomHeight;
            Root.Add(_mapArea);

            _map = new MapView();
            Ui.Fill(_map);
            _map.Hovered += OnRoadHover;
            _map.LocationHovered += OnLocationHover;
            _map.RoadClicked += OnRoadClicked;
            _map.LocationClicked += OnLocationClicked;
            _map.Overlay = DrawOverlay;
            _mapArea.Add(_map);

            // Мини-карта: грузы на хабах (цвет вида) и фуры (цвет компании).
            _minimap = new MiniMapView(_map, 200f, 150f);
            _minimap.style.position = Position.Absolute;
            _minimap.style.left = 14f;
            _minimap.style.bottom = 14f;
            _minimap.Extra = DrawMinimap;
            _mapArea.Add(_minimap);

            // Слои карты: кнопка в правом верхнем углу карты, по клику — список переключателей.
            _layersPanel = new VisualElement();
            _layersPanel.style.position = Position.Absolute;
            _layersPanel.style.right = 14f;
            _layersPanel.style.top = 14f;
            _layersPanel.style.alignItems = Align.FlexEnd;
            var layersButton = Ui.Button("Слои карты", () =>
            {
                bool open = _layersList.style.display == DisplayStyle.None;
                // Справа под кнопкой — и панель грузов: открываем слои — грузы закрываются.
                if (open && (_popupKind == "cargo" || _popupKind == "cargoDetail")) ClosePopup(false);
                Ui.Show(_layersList, open);
                _app.Sound.Play(Sfx.Click, 0.4f);
            }, ButtonKind.Secondary, 13f);
            layersButton.style.width = 130f;
            _layersPanel.Add(layersButton);
            _layersList = new VisualElement();
            _layersList.style.marginTop = 6f;
            _layersList.style.backgroundColor = Theme.PanelGlass;
            _layersList.style.width = 220f;
            Ui.Padding(_layersList, 8f, 10f);
            Ui.Radius(_layersList, 10f);
            Ui.Border(_layersList, 1f, Theme.Line);
            Ui.Show(_layersList, false);
            _layersPanel.Add(_layersList);
            _mapArea.Add(_layersPanel);
            BuildLayersList();

            _banner = Ui.Text("", 16f, Theme.Text, true);
            _banner.style.position = Position.Absolute;
            _banner.style.top = 12f;
            _banner.style.left = Length.Percent(50f);
            _banner.style.translate = new Translate(Length.Percent(-50f), new Length(0f));
            _banner.style.backgroundColor = Theme.PanelGlass;
            _banner.style.maxWidth = 640f;
            _banner.style.whiteSpace = WhiteSpace.Normal;
            _banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            Ui.Padding(_banner, 8f, 16f);
            Ui.Radius(_banner, 10f);
            _banner.pickingMode = PickingMode.Ignore;
            Ui.Show(_banner, false);
            _mapArea.Add(_banner);

            _tooltip = Ui.Text("", 13f, Theme.Text);
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.backgroundColor = new Color(0.08f, 0.10f, 0.13f, 0.95f);
            _tooltip.style.whiteSpace = WhiteSpace.Normal;
            _tooltip.style.maxWidth = 320f;
            Ui.Padding(_tooltip, 6f, 10f);
            Ui.Radius(_tooltip, 6f);
            _tooltip.pickingMode = PickingMode.Ignore;
            Ui.Show(_tooltip, false);
            _mapArea.Add(_tooltip);

            // Верхняя панель: город, раунд, сложность, компании, меню.
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
            _roundLabel.style.marginRight = 12f;
            top.Add(_roundLabel);
            // Событие раунда — плашка в верхней строке (подробности во всплывающей подсказке).
            _eventChip = Ui.Text("", 12f, Theme.Gold, true);
            _eventChip.style.backgroundColor = new Color(0.30f, 0.20f, 0.12f);
            Ui.Padding(_eventChip, 3f, 8f);
            Ui.Radius(_eventChip, 6f);
            Ui.Border(_eventChip, 1f, Theme.Gold);
            _eventChip.style.marginRight = 14f;
            _eventChip.pickingMode = PickingMode.Position;
            Ui.Show(_eventChip, false);
            _eventIcon = new VisualElement();
            _eventIcon.style.marginRight = 6f;
            top.Add(_eventIcon);
            top.Add(_eventChip);
            _players = Ui.Row();
            _players.style.flexGrow = 1f;
            _players.style.overflow = Overflow.Hidden;
            top.Add(_players);
            // Правила и журнал — в верхней строке, слева от «Обучения»; окна открываются под строкой.
            _topActions = Ui.Row();
            _topActions.style.alignItems = Align.Center;
            var rules = Ui.Button("Правила", () => TogglePopup("rules", TopSection), ButtonKind.Secondary, 13f);
            rules.style.width = 96f;
            rules.style.marginRight = 8f;
            _topActions.Add(rules);
            var journal = Ui.Button("Журнал", () => TogglePopup("log", TopSection), ButtonKind.Secondary, 13f);
            journal.style.width = 96f;
            journal.style.marginRight = 8f;
            _topActions.Add(journal);
            top.Add(_topActions);
            var help = Ui.Button("? Обучение", () => StartCoach(), ButtonKind.Ghost, 13f);
            help.style.width = 110f;
            help.style.marginRight = 8f;
            top.Add(help);
            var menu = Ui.Button("Меню", () => _app.ShowMenu(), ButtonKind.Secondary, 14f);
            menu.style.width = 96f;
            top.Add(menu);

            BuildBottomBar();
            BuildCoach();

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
            // Спокойная карта магната: одно покрытие дорог, крупные фишки, значки хабов, фокус под курсором.
            _map.CalmRoads = true;
            // Дороги лежат на наклонённой земле, городские — без подсветки, ещё не построенные — пунктиром.
            _map.GroundRoads = true;
            _map.PlainPublicRoads = true;
            _map.DashedFreeRoads = true;
            _map.LocationScale = 1.3f;
            _map.HubIcons = true;
            _map.FocusExempt = id => _game.HubAt(id) != null;
            // Цена свободной улицы в очках стройки — плашкой на дороге, только в свою стройку.
            _map.RoadBadge = road => Has(Layer.RoadCost) && _game.Phase == MagnatePhase.Build && HumanTurn && !_game.IsBuilt(road.Id)
                ? _game.BuildCost(road).ToString() : null;
            _map.RoadBadgeMuted = road => _game.BuildCost(road) > _game.CurrentPlayer.BuildPoints;
            _map.SetGame(_game.Board);
            _minimap.SetGame(_game.Board);
            Ui.Show(_minimap, _app.Display == null || _app.Display.showMinimap);
            _log.Add($"Город {_game.Map.Name}, сложность «{MagnateConfig.Title(_game.Config.Difficulty)}». " +
                     $"Хабы: {string.Join(", ", _game.Hubs.Select(h => _game.Map.LocationName(h.LocationId)))}.");
            ApplyLayers();
            if (_game.Config.RandomFirstPlayer)
            {
                var first = _game.CurrentPlayer;
                _log.Add($"Жребий: первым ходит {first.Name}.");
                Toast(first.IsBot ? $"Жребий: первым ходит {first.Name}." : $"Жребий: вы ходите первым, {first.Name}!", 3.5f);
            }
            StartTurn();
            if (_setup.forceMagnateTutorial || (!_setup.magnateTutorialDone && _game.Players.Any(p => !p.IsBot))) StartCoach();
            _setup.forceMagnateTutorial = false;
            _dirty = true;
        }

        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            _map.Tick(deltaTime);
            Sync3D(deltaTime);
            _minimap.Refresh();

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
            UpdateMapEmphasis();
            CoachTick();
        }

        // ================================================================== Слои карты

        [Flags]
        public enum Layer
        {
            StationNames = 1, DistrictNames = 2, Cargo = 4, Trucks = 8, Traffic = 16, FreeRoads = 32, Focus = 64,
            /// <summary>Цены свободных дорог. Бит хранится наоборот (1 — выключено), чтобы у старых настроек слой был включён.</summary>
            RoadCost = 128
        }

        static readonly (Layer layer, string title, string hint)[] LayerItems =
        {
            (Layer.StationNames, "Названия станций", "Подписи под станциями"),
            (Layer.DistrictNames, "Названия районов", "Крупные подписи районов"),
            (Layer.Cargo, "Грузы", "Квадратики грузов над хабами"),
            (Layer.Trucks, "Фуры", "Фуры компаний на карте"),
            (Layer.Traffic, "Загрузка дорог", "Цвет по прошлому раунду: зелёный — свободно, жёлтый — почти полно, красный — пробка"),
            (Layer.FreeRoads, "Свободные дороги", "Ничьи дороги. В фазе маршрутов они бледные: фуры по ним не ездят"),
            (Layer.Focus, "Фокус под курсором", "Вдали от курсора карта тускнеет"),
            (Layer.RoadCost, "Цены дорог", "В свою стройку: сколько очков стройки стоит свободная улица (серая — пока не хватает очков)"),
        };

        bool Has(Layer layer)
        {
            bool bit = ((Layer)_setup.magnateLayers & layer) != 0;
            return layer == Layer.RoadCost ? !bit : bit;
        }

        void BuildLayersList()
        {
            _layersList.Clear();
            var caption = Ui.Text("Что показывать на карте", 12f, Theme.TextDim, true);
            caption.style.marginBottom = 6f;
            _layersList.Add(caption);
            foreach (var item in LayerItems)
            {
                bool on = Has(item.layer);
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4f;
                row.pickingMode = PickingMode.Position;
                row.tooltip = item.hint;
                var box = Ui.Text(on ? "✓" : "", 12f, new Color(0.08f, 0.08f, 0.1f), true);
                box.style.width = 16f;
                box.style.height = 16f;
                box.style.unityTextAlign = TextAnchor.MiddleCenter;
                box.style.backgroundColor = on ? GameColors.Accent : new Color(1f, 1f, 1f, 0.08f);
                Ui.Radius(box, 4f);
                box.style.marginRight = 8f;
                row.Add(box);
                row.Add(Ui.Text(item.title, 13f, on ? Theme.Text : Theme.TextDim));
                var layer = item.layer;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _setup.magnateLayers ^= (int)layer;
                    SaveSetup();
                    ApplyLayers();
                    BuildLayersList();
                });
                _layersList.Add(row);
            }
        }

        void ApplyLayers()
        {
            _map.SetLabelLayers(Has(Layer.StationNames), Has(Layer.DistrictNames));
            _map.FocusRadius = Has(Layer.Focus) ? 230f : 0f;
            UpdateMapEmphasis();
        }

        /// <summary>
        /// Иерархия карты по фазе: в стройку свободные дороги видны (их покупают), в маршруты — бледные
        /// (фуры ездят только по построенным), выключенный слой — не видны вовсе.
        /// </summary>
        void UpdateMapEmphasis()
        {
            float free = !Has(Layer.FreeRoads) ? 0f : _game.Phase == MagnatePhase.Build && HumanTurn ? 1f : 0.3f;
            _map.FreeRoadAlpha = free;
        }

        void StartTurn()
        {
            if (_game.Phase == MagnatePhase.GameOver)
            {
                ShowResults();
                return;
            }
            _selectedTruck = -1;
            _preview = null;
            if (_game.Round != _roundShown)
            {
                _roundShown = _game.Round;
                var fresh = _game.Events.FirstOrDefault(ev => ev.StartRound == _game.Round);
                if (fresh != null) Toast($"Событие: {fresh.Title}. {fresh.Description}", 4.5f);
            }
            var player = _game.CurrentPlayer;
            if (player.IsBot)
            {
                _botTimer = 0.6f;
                _dirty = true;
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
            LogisticsBot.Plan(_game, _botRandom);
            AfterTurn();
        }

        /// <summary>После «Готово»: следующий игрок или (если раунд закрылся) анимация движения.</summary>
        void AfterTurn()
        {
            _botTimer = -1f;
            _selectedTruck = -1;
            _preview = null;
            if (_game.LastReport != null && _game.LastReport != _shownReport) return; // Tick запустит анимацию
            StartTurn();
        }

        // ================================================================== Анимация раунда

        void StartAnimation(RoundReport report)
        {
            _animating = report;
            _shownReport = report;
            _animationTime = 0f;
            Toast($"Раунд {report.Round}: фуры в пути…", AnimationSeconds);
            if (report.Jams.Count > 0) _app.Sound.Play(Sfx.Horn, 0.6f);
            _dirty = true;
        }

        void FinishAnimation()
        {
            var report = _animating;
            _animating = null;
            int delivered = report.Moves.Count(m => m.Delivered != null);
            Toast(delivered > 0 || report.Jams.Count > 0
                ? $"Доставлено: {delivered}" + (report.Jams.Count > 0 ? $" · пробок: {report.Jams.Count}" : "")
                : "Раунд прошёл спокойно", 2.2f);
            if (delivered > 0) _app.Sound.Play(Sfx.Coin, 0.7f);
            StartTurn();
        }

        /// <summary>Доля анимации, за которую фура проходит путь: скоропорт быстрее, тяжёлые и негабарит — дольше.</summary>
        static float TravelShare(CargoKind? kind) =>
            kind == CargoKind.Perishable ? 0.62f : kind == CargoKind.Heavy || kind == CargoKind.Oversized ? 0.95f : 0.8f;

        static bool Queued(TruckMove move) =>
            move.StoppedBefore != null && (move.Reason == StopReason.Jam || move.Reason == StopReason.Oversized);

        TruckMove MoveOf(Truck truck) => (_animating ?? _game.LastReport)?.Moves.FirstOrDefault(m => m.Truck == truck.Id);

        /// <summary>Положение и направление фуры на экране.</summary>
        void TruckPose(Truck truck, int fanIndex, out Vector2 position, out Vector2 heading)
        {
            heading = Vector2.right;
            var move = MoveOf(truck);
            if (_animating != null && move != null && move.Stops.Count > 1)
            {
                float share = TravelShare(move.CargoKind);
                float k = Ui.EaseInOutCubic(Mathf.Clamp01(_animationTime / (AnimationSeconds * share)));
                float u = k * (move.Stops.Count - 1);
                int i = Mathf.Min(move.Stops.Count - 2, Mathf.FloorToInt(u));
                var a = _map.ScreenPositionOf(move.Stops[i]);
                var b = _map.ScreenPositionOf(move.Stops[i + 1]);
                if ((b - a).sqrMagnitude > 0.01f) heading = (b - a).normalized;
                position = Vector2.Lerp(a, b, u - i) + new Vector2(-heading.y, heading.x) * 3f; // правая полоса
                // Доехала до пробки — встаёт в очередь.
                if (k >= 1f && Queued(move)) QueuePose(truck, move, out position, out heading);
                return;
            }
            // Стоит в очереди перед пробкой (всю фазу планирования следующего раунда — тоже, пока фура там).
            if (move != null && Queued(move) && truck.Location == move.Stops[move.Stops.Count - 1])
            {
                QueuePose(truck, move, out position, out heading);
                return;
            }
            var at = _map.ScreenPositionOf(truck.Location);
            float angle = fanIndex * 2.1f + 0.7f;
            position = at + new Vector2(-15f, -13f) + (fanIndex == 0 ? Vector2.zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 12f);
            var first = truck.Route.Count > 0 ? _game.Map.GetRoad(truck.Route[0]) : null;
            if (first != null)
            {
                var other = _map.ScreenPositionOf(first.A == truck.Location ? first.B : first.A);
                if ((other - at).sqrMagnitude > 1f) heading = (other - at).normalized;
            }
        }

        /// <summary>Очередь у въезда на дорогу в пробке: каждая следующая застрявшая фура — на корпус дальше.</summary>
        void QueuePose(Truck truck, TruckMove move, out Vector2 position, out Vector2 heading)
        {
            var road = _game.Map.GetRoad(move.StoppedBefore);
            string from = move.Stops[move.Stops.Count - 1];
            string to = road.A == from ? road.B : road.A;
            var a = _map.ScreenPositionOf(from);
            var b = _map.ScreenPositionOf(to);
            heading = (b - a).sqrMagnitude > 0.01f ? (b - a).normalized : Vector2.right;
            var report = _animating ?? _game.LastReport;
            int index = report.Moves
                .Where(m => Queued(m) && m.StoppedBefore == move.StoppedBefore && m.Stops[m.Stops.Count - 1] == from)
                .TakeWhile(m => m.Truck != truck.Id).Count();
            float shake = _animating != null ? Mathf.Sin(_time * 28f + truck.Id) * 0.6f : 0f;
            position = a + heading * (34f - index * 17f + shake) + new Vector2(-heading.y, heading.x) * 3f;
        }

        // ================================================================== Рисунок поверх карты

        static readonly Color RegularColor = new Color(0.72f, 0.52f, 0.32f);
        static readonly Color PerishableColor = new Color(0.36f, 0.80f, 0.36f);
        static readonly Color HeavyColor = new Color(1.00f, 0.60f, 0.18f);
        static readonly Color HazardColor = new Color(1.00f, 0.86f, 0.10f);
        static readonly Color FragileColor = new Color(0.55f, 0.82f, 1.00f);
        static readonly Color ValuableColor = new Color(0.86f, 0.62f, 1.00f);
        static readonly Color OversizedColor = new Color(0.95f, 0.36f, 0.30f);

        public static Color KindColor(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Perishable: return PerishableColor;
                case CargoKind.Heavy: return HeavyColor;
                case CargoKind.Hazard: return HazardColor;
                case CargoKind.Fragile: return FragileColor;
                case CargoKind.Valuable: return ValuableColor;
                case CargoKind.Oversized: return OversizedColor;
                default: return RegularColor;
            }
        }

        public static string KindLetter(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Perishable: return "С";
                case CargoKind.Heavy: return "Т";
                case CargoKind.Hazard: return "!";
                case CargoKind.Fragile: return "Х";
                case CargoKind.Valuable: return "Ц";
                case CargoKind.Oversized: return "Н";
                default: return "О";
            }
        }

        void DrawOverlay(Painter2D p)
        {
            // Перекрытые событиями дороги: красно-белый шлагбаум поперёк и гаечный ключ рядом.
            foreach (var road in _game.Map.Roads)
            {
                if (!_game.IsClosed(road.Id)) continue;
                var a = _map.ScreenPositionOf(road.A);
                var b = _map.ScreenPositionOf(road.B);
                var mid = (a + b) * 0.5f;
                var dir = (b - a).sqrMagnitude > 0.01f ? (b - a).normalized : Vector2.right;
                var n = new Vector2(-dir.y, dir.x);
                for (int s = -2; s <= 2; s++)
                    Rotated(p, mid + n * s * 3.4f, n, 1.8f, 7f, s % 2 == 0 ? new Color(0.92f, 0.18f, 0.16f) : Color.white);
                Wrench(p, mid + n * 18f);
            }

            // Хабы: кольцо цвета владельца, точки уровня, грузы — квадратики цвета вида (с учётом фильтра).
            foreach (var hub in _game.Hubs)
            {
                var at = _map.ScreenPositionOf(hub.LocationId);
                if (hub.Owner >= 0)
                {
                    p.strokeColor = Palette.PlayerColor(hub.Owner);
                    p.lineWidth = 2.4f;
                    p.BeginPath();
                    p.Arc(at, 18f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Stroke();
                }
                for (int l = 0; l < hub.Level; l++)
                    Square(p, at + new Vector2(-10f + l * 5f, 20f), 1.8f, l == 0 ? new Color(1f, 1f, 1f, 0.5f) : Theme.Gold);
                // Обучение: хабы пульсируют, пока о них рассказывает подсказка.
                if (_coachStep == CoachHubs)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
                    p.strokeColor = new Color(1f, 0.85f, 0.3f, 0.45f + 0.5f * pulse);
                    p.lineWidth = 3f;
                    p.BeginPath();
                    p.Arc(at, 20f + 6f * pulse, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Stroke();
                }
                if (!Has(Layer.Cargo)) continue;
                int k = 0;
                foreach (var waiting in _game.WaitingAt(hub.LocationId))
                {
                    if (_hiddenKinds.Contains(waiting.Kind)) continue;
                    var c = at + new Vector2(16f + k * 13f, -18f);
                    // Срочный скоропорт — пульсирующая красная рамка; груз для заказа мэрии — золотая точка.
                    if (Urgent(waiting))
                        Square(p, c, 6.6f + 0.8f * Mathf.Sin(_time * 6f), new Color(0.95f, 0.2f, 0.18f, 0.9f));
                    Square(p, c, 5.6f, new Color(0f, 0f, 0f, 0.65f));
                    Square(p, c, 4.7f, KindColor(waiting.Kind));
                    if (ForContract(waiting)) Disc(p, c + new Vector2(4.5f, -4.5f), 2.2f, Theme.Gold);
                    k++;
                }
            }

            if (Has(Layer.Traffic)) DrawTraffic(p);

            // Фуры, а над очередями в пробках — часы.
            var fan = new Dictionary<string, int>();
            var clocks = new HashSet<string>();
            foreach (var truck in _game.Trucks)
            {
                if (!Has(Layer.Trucks)) break;
                fan.TryGetValue(truck.Location, out int index);
                fan[truck.Location] = index + 1;
                TruckPose(truck, index, out var position, out var heading);
                var move = MoveOf(truck);
                var kind = _game.GetCargo(truck.Cargo)?.Kind ?? (_animating != null ? move?.CargoKind : null);
                // В 3D фура — модель сцены; поверх — только кольцо выбранной фуры.
                if (_city3D != null && _city3D.HasTrucks)
                {
                    if (truck.Id == _selectedTruck)
                    {
                        p.strokeColor = Color.white;
                        p.lineWidth = 2.2f;
                        p.BeginPath();
                        p.Arc(position, 15f, Angle.Degrees(0f), Angle.Degrees(360f));
                        p.Stroke();
                    }
                }
                else DrawTruck(p, position, heading, Palette.PlayerColor(truck.Owner), kind, truck.Id == _selectedTruck);
                if (move != null && Queued(move) && truck.Location == move.Stops[move.Stops.Count - 1])
                    clocks.Add(move.StoppedBefore + "|" + truck.Location);
            }
            foreach (var key in clocks)
            {
                var parts = key.Split('|');
                var jammed = _game.Map.GetRoad(parts[0]);
                var from = _map.ScreenPositionOf(parts[1]);
                var to = _map.ScreenPositionOf(jammed.A == parts[1] ? jammed.B : jammed.A);
                var dir = (to - from).sqrMagnitude > 0.01f ? (to - from).normalized : Vector2.right;
                Clock(p, from + dir * 34f + new Vector2(0f, -17f));
            }
        }

        /// <summary>Скоропорт, которому до конца нормы остался раунд или меньше.</summary>
        bool Urgent(Cargo cargo) => cargo.Kind == CargoKind.Perishable && cargo.SpawnRound + cargo.Norm - _game.Round <= 1;

        /// <summary>Груз засчитывается в действующий заказ мэрии.</summary>
        bool ForContract(Cargo cargo) => _game.Contracts.Any(c => c.Winner < 0 && (!c.Kind.HasValue || c.Kind == cargo.Kind));

        /// <summary>Загрузка дорог по прошлому раунду: линия по оси построенной дороги — зелёная, жёлтая, красная.</summary>
        void DrawTraffic(Painter2D p)
        {
            foreach (var road in _game.Map.Roads)
            {
                if (!_game.IsBuilt(road.Id)) continue;
                int traffic = _game.LastTraffic(road.Id);
                if (traffic == 0) continue;
                float load = traffic / (float)Math.Max(1, _game.Capacity(road));
                var color = load > 1f ? new Color(0.95f, 0.22f, 0.18f, 0.95f) : load >= 0.75f ? new Color(1f, 0.78f, 0.2f, 0.9f) : new Color(0.35f, 0.85f, 0.4f, 0.85f);
                p.strokeColor = color;
                p.lineWidth = 2.6f;
                p.BeginPath();
                p.MoveTo(_map.ScreenPositionOf(road.A));
                p.LineTo(_map.ScreenPositionOf(road.B));
                p.Stroke();
            }
        }

        /// <summary>Фура сверху: кузов цвета компании с пятном цвета груза, кабина посветлее, стекло.</summary>
        static void DrawTruck(Painter2D p, Vector2 c, Vector2 dir, Color company, CargoKind? kind, bool selected)
        {
            var trailer = c - dir * 2.5f;
            var cab = c + dir * 6.5f;
            var outline = new Color(0.05f, 0.06f, 0.08f, 0.95f);
            if (selected) Rotated(p, c + dir * 1.5f, dir, 11.5f, 6.6f, Color.white);
            Rotated(p, trailer, dir, 7.6f, 4.6f, outline);
            Rotated(p, cab, dir, 3.4f, 4.2f, outline);
            Rotated(p, trailer, dir, 6.8f, 3.8f, company);
            Rotated(p, cab, dir, 2.7f, 3.5f, Color.Lerp(company, Color.white, 0.25f));
            Rotated(p, cab + dir * 1.2f, dir, 0.9f, 2.6f, new Color(0.55f, 0.75f, 0.95f));
            if (kind.HasValue)
            {
                Rotated(p, trailer, dir, 4.6f, 2.3f, new Color(0f, 0f, 0f, 0.6f));
                Rotated(p, trailer, dir, 4f, 1.8f, KindColor(kind.Value));
            }
        }

        /// <summary>Прямоугольник, повёрнутый по направлению dir: halfLength вдоль, halfWidth поперёк.</summary>
        static void Rotated(Painter2D p, Vector2 c, Vector2 dir, float halfLength, float halfWidth, Color color)
        {
            var n = new Vector2(-dir.y, dir.x);
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(c - dir * halfLength - n * halfWidth);
            p.LineTo(c + dir * halfLength - n * halfWidth);
            p.LineTo(c + dir * halfLength + n * halfWidth);
            p.LineTo(c - dir * halfLength + n * halfWidth);
            p.ClosePath();
            p.Fill();
        }

        static void Square(Painter2D p, Vector2 c, float half, Color color) => Rotated(p, c, Vector2.right, half, half, color);

        static void Disc(Painter2D p, Vector2 c, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
        }

        /// <summary>Часы задержки: жёлтый циферблат с бегущей стрелкой.</summary>
        void Clock(Painter2D p, Vector2 c)
        {
            Disc(p, c, 8.5f, new Color(0.05f, 0.06f, 0.08f, 0.9f));
            Disc(p, c, 7f, new Color(1f, 0.85f, 0.3f));
            p.strokeColor = new Color(0.1f, 0.1f, 0.12f);
            p.lineWidth = 1.5f;
            float spin = _time * 2.5f;
            p.BeginPath();
            p.MoveTo(c);
            p.LineTo(c + new Vector2(Mathf.Sin(spin), -Mathf.Cos(spin)) * 5f);
            p.MoveTo(c);
            p.LineTo(c + new Vector2(3.5f, 0f));
            p.Stroke();
        }

        /// <summary>Гаечный ключ на тёмном кружке — ДТП, ремонт или праздник перекрыли дорогу.</summary>
        static void Wrench(Painter2D p, Vector2 c)
        {
            var steel = new Color(0.85f, 0.88f, 0.92f);
            Disc(p, c, 9f, new Color(0.05f, 0.06f, 0.08f, 0.9f));
            var d = new Vector2(0.707f, -0.707f);
            Rotated(p, c, d, 5.5f, 1.4f, steel);
            Disc(p, c + d * 5f, 3f, steel);
            Disc(p, c + d * 6.2f, 1.4f, new Color(0.05f, 0.06f, 0.08f, 1f));
        }

        void DrawMinimap(Painter2D p, Func<float, float, Vector2> toMini)
        {
            foreach (var hub in _game.Hubs)
            {
                if (!Has(Layer.Cargo)) break;
                var location = _game.Map.GetLocation(hub.LocationId);
                if (location == null) continue;
                int k = 0;
                foreach (var cargo in _game.WaitingAt(hub.LocationId))
                {
                    if (_hiddenKinds.Contains(cargo.Kind)) continue;
                    Square(p, toMini(location.X, location.Y) + new Vector2(4f + k * 4f, -4f), 1.6f, KindColor(cargo.Kind));
                    k++;
                }
            }
            foreach (var truck in _game.Trucks)
            {
                if (!Has(Layer.Trucks)) break;
                var location = _game.Map.GetLocation(truck.Location);
                if (location == null) continue;
                Disc(p, toMini(location.X, location.Y) + new Vector2((truck.Id % 3 - 1) * 2.5f, 3f), 1.8f, Palette.PlayerColor(truck.Owner));
            }
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
                          (_game.IsWide(road) ? "\nТяжеловес и негабарит — можно" : "\nТяжеловес и негабарит — нельзя");
            var closing = _game.Events.FirstOrDefault(e => e.Road == road.Id);
            if (_game.IsClosed(road.Id)) text += $"\nЗакрыта: {closing?.Title ?? "событие"}";
            if (HumanTurn && _game.Phase == MagnatePhase.Build)
            {
                if (!owner.HasValue) text += $"\nКупить: {_game.BuildCost(road)} очков стройки";
                else if (owner == _game.CurrentPlayerIndex && !_game.IsHighway(road.Id)) text += $"\nУлучшить до магистрали: {_game.UpgradeCost(road)}";
            }
            ShowTooltip(text, position);
        }

        void OnLocationHover(Location location, Vector2 position)
        {
            _preview = null;
            if (location == null)
            {
                Ui.Show(_tooltip, false);
                RefreshHighlights();
                return;
            }
            var hub = _game.HubAt(location.Id);
            string text = location.Name + " · " + _game.Map.DistrictName(location.DistrictId);
            if (hub != null)
            {
                text += "\n" + (hub.Kind == HubKind.Airport ? "Аэропорт" : hub.Kind == HubKind.Port ? "Порт" : "Вокзал") +
                        $" · уровень {hub.Level}" + (hub.Owner >= 0 ? $" · развивает {_game.Players[hub.Owner].Name}" : " · городской");
                foreach (var cargo in _game.WaitingAt(location.Id))
                    text += $"\n• {LogisticsGame.KindTitle(cargo.Kind)} → {_game.Map.LocationName(cargo.To)}: {cargo.Value} очк.";
                if (HumanTurn && _game.Phase == MagnatePhase.Build && hub.Level < 5 && (hub.Owner < 0 || hub.Owner == _game.CurrentPlayerIndex))
                    text += $"\nКлик — развить до уровня {hub.Level + 1} за {_game.HubUpgradeCost(hub)}: больше грузов, " +
                            $"с чужих фур {_game.Config.HubFee} за погрузку, вы грузите первым";
            }
            if (location.DistrictId == _game.Config.BribeDistrict) text += $"\nОпасный груз здесь — взятка {_game.Config.HazardBribe} влияния";

            // Предпросмотр маршрута выбранной пустой фуры: путь на карте, время, плата, риск.
            if (HumanTurn && _game.Phase == MagnatePhase.Plan && _selectedTruck >= 0)
            {
                var truck = _game.GetTruck(_selectedTruck);
                if (truck != null && truck.Cargo == null && truck.Location != location.Id)
                {
                    var route = _game.FindRoute(truck.Owner, truck.Location, location.Id, null);
                    if (route != null)
                    {
                        _preview = route;
                        text += $"\nМаршрут: {_game.RouteLength(route)} участков, ~{_game.RouteRounds(route)} р." +
                                $" · проезд −{_game.RouteTolls(truck.Owner, route)} · забитых дорог: {_game.RouteRisk(route)}\nКлик — ехать сюда";
                    }
                    else text += "\nТуда сейчас не проехать";
                }
            }
            RefreshHighlights();
            ShowTooltip(text, position);
        }

        void ShowTooltip(string text, Vector2 position)
        {
            _tooltip.text = text;
            _tooltip.style.left = Mathf.Min(position.x + 16f, _mapArea.layout.width - 330f);
            _tooltip.style.top = Mathf.Min(position.y + 16f, _mapArea.layout.height - 160f);
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
            if (!HumanTurn) return;
            if (_game.Phase == MagnatePhase.Build)
            {
                if (_game.HubAt(location.Id) == null) return;
                var error = _game.UpgradeHub(location.Id);
                if (error != null) { Toast(error); _app.Sound.Play(Sfx.Error, 0.5f); }
                else _app.Sound.Play(Sfx.Bell, 0.6f);
                OnLocationHover(location, position);
                return;
            }
            if (_selectedTruck < 0) { Toast("Сначала выберите фуру в разделе «Маршруты фур» внизу."); return; }
            var plan = _game.PlanTruck(_selectedTruck, null, location.Id);
            if (plan != null) { Toast(plan); _app.Sound.Play(Sfx.Error, 0.5f); }
            else _app.Sound.Play(Sfx.Click, 0.6f);
            _preview = null;
        }

        void Toast(string text, float seconds = 2.4f)
        {
            _banner.text = text;
            Ui.Show(_banner, true);
            _app.Tweener.After(seconds, () => { if (_banner.text == text) Ui.Show(_banner, false); });
        }

        // ================================================================== Итоги

        void ShowResults()
        {
            if (_resultsShown) return;
            _resultsShown = true;
            var credits = Profiles.CreditAfterMagnate(_game, _setup);
            // Журнал партий (Счётчик партий → вкладка «Логистический магнат»).
            var unlocked = credits.Where(c => c.Unlocked != null && c.Unlocked.Count > 0)
                .GroupBy(c => c.Profile).ToDictionary(g => g.Key, g => g.SelectMany(c => c.Unlocked).Select(a => a.Title).ToList());
            StatsLog.AppendMagnate(_game, _setup, _time, unlocked);
            _results.Clear();
            var card = Ui.Card(26f);
            card.style.backgroundColor = Theme.Panel;
            card.style.minWidth = 620f;
            var ranking = _game.Players.OrderByDescending(p => p.Score).ToList();
            card.Add(Ui.Text($"Победил {ranking[0].Name}!", 28f, Palette.PlayerColor(ranking[0].Index), true));
            var sub = Ui.Text($"{_game.Map.Name}, {_game.Config.Rounds} раундов, сложность «{MagnateConfig.Title(_game.Config.Difficulty)}»", 14f, Theme.TextDim);
            sub.style.marginBottom = 16f;
            card.Add(sub);
            int place = 1;
            foreach (var p in ranking)
            {
                var row = Ui.Column();
                row.style.marginBottom = 8f;
                row.Add(Ui.Text($"{place++}. {p.Name} — {p.Score} очк. · {LogisticsGame.LevelTitle(p.Level)}", 17f, Palette.PlayerColor(p.Index), true));
                row.Add(Ui.Text($"доставок {p.Delivered} · заказов мэрии {p.ContractsWon} · пробок {p.Jams} · проезд +{p.TollIn}/−{p.TollOut} · хабы +{p.HubFees}" +
                                (p.Confiscated + p.Thefts > 0 ? $" · потерь {p.Confiscated + p.Thefts}" : ""), 13f, Theme.TextDim));
                card.Add(row);
            }
            foreach (var credit in credits)
            {
                if (credit.Unlocked == null) continue;
                foreach (var achievement in credit.Unlocked)
                {
                    var line = Ui.Text($"Достижение «{achievement.Title}» — {credit.Profile} (+{achievement.Reward} влияния в кошелёк)", 14f, Theme.Gold, true);
                    line.style.marginTop = 4f;
                    card.Add(line);
                }
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
                _city3D = City3DScene.TryCreate(_game.Map, display != null ? Mathf.Clamp(display.city3DQuality, 0, 2) : 2,
                    _game.Hubs.Select(h => h.LocationId));
            bool on = _city3D != null;
            _map.Use3D = on;
            var city = _city3D;
            _map.RoadHidden = null;
            if (on) _map.RoadHidden = id => city.IsUnderConstruction(id);
            Root.style.backgroundColor = on ? Color.clear : Theme.Background;
            _app.SetSceneBehind(on);
            SyncHubs();
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
            // Этап 2 полноценного 3D: бордюры, разметка, машины и фуры — в сцене.
            _city3D.SyncRoadDetails(board, Palette.PlayerColor, _game.PublicOwner);
            _city3D.SyncTraffic(board, _map.PulseTime);
            _city3D.SyncPeople(board, _map.PulseTime);
            SyncTrucks3D();
        }

        readonly List<City3DScene.TruckView> _truckViews3D = new List<City3DScene.TruckView>();
        readonly Dictionary<string, int> _truckFan3D = new Dictionary<string, int>();

        /// <summary>
        /// Фуры в 3D: та же поза, что у плоской фуры (TruckPose, в пикселях карты), переводится на землю сцены.
        /// Слой «Фуры» выключен — фур нет и в 3D.
        /// </summary>
        void SyncTrucks3D()
        {
            _truckViews3D.Clear();
            _truckFan3D.Clear();
            var layout = _map.Layout;
            if (Has(Layer.Trucks) && layout != null)
                foreach (var truck in _game.Trucks)
                {
                    _truckFan3D.TryGetValue(truck.Location, out int index);
                    _truckFan3D[truck.Location] = index + 1;
                    TruckPose(truck, index, out var position, out var heading);
                    var here = layout.ScreenToMap(position);
                    var ahead = layout.ScreenToMap(position + heading * 6f);
                    var world = BoardLayout.World(here.x, here.y);
                    var move = MoveOf(truck);
                    var kind = _game.GetCargo(truck.Cargo)?.Kind ?? (_animating != null ? move?.CargoKind : null);
                    _truckViews3D.Add(new City3DScene.TruckView
                    {
                        Id = truck.Id,
                        Position = world,
                        Heading = BoardLayout.World(ahead.x, ahead.y) - world,
                        Company = Palette.PlayerColor(truck.Owner),
                        Cargo = kind.HasValue ? KindColor(kind.Value) : (Color?)null
                    });
                }
            _city3D.SyncTrucks(_truckViews3D);
        }
    }
}
