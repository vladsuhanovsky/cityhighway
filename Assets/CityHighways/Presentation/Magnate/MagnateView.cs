using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Экран «Логистического магната»: карта (MapView по доске LogisticsGame.Board, с 3D-городом и мини-картой),
    /// сверху — раунд и компании с репутацией, справа — панель хода: событие раунда, стройка (дорога — купить,
    /// своя — магистраль, хаб — развить), маршруты фур с предпросмотром, заказы мэрии, грузы с фильтрами, правила,
    /// журнал. Поверх карты — фуры (кабина и кузов цвета компании, груз — цветом вида), очереди в пробках
    /// с часами, перекрытые дороги, уровни хабов. Все фуры едут разом: скоропорт быстрее, тяжёлые — медленнее.
    /// </summary>
    public sealed class MagnateView
    {
        const float TopBarHeight = 56f, PanelWidth = 400f, AnimationSeconds = 2.2f;

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly LogisticsGame _game;
        readonly System.Random _botRandom = new System.Random(17);

        public VisualElement Root { get; }
        readonly VisualElement _mapArea, _players, _panel, _handoff, _results;
        readonly ScrollView _panelScroll;
        readonly Label _roundLabel, _tooltip, _banner;
        readonly MapView _map;
        readonly MiniMapView _minimap;
        readonly List<string> _log = new List<string>();
        readonly HashSet<CargoKind> _hiddenKinds = new HashSet<CargoKind>();

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

            // Мини-карта: грузы на хабах (цвет вида) и фуры (цвет компании).
            _minimap = new MiniMapView(_map, 236f, 150f);
            _minimap.style.position = Position.Absolute;
            _minimap.style.left = 14f;
            _minimap.style.bottom = 14f;
            _minimap.Extra = DrawMinimap;
            _mapArea.Add(_minimap);

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
            _roundLabel.style.marginRight = 18f;
            top.Add(_roundLabel);
            _players = Ui.Row();
            _players.style.flexGrow = 1f;
            _players.style.overflow = Overflow.Hidden;
            top.Add(_players);
            var menu = Ui.Button("Меню", () => _app.ShowMenu(), ButtonKind.Secondary, 14f);
            menu.style.width = 96f;
            top.Add(menu);

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
            _minimap.SetGame(_game.Board);
            Ui.Show(_minimap, _app.Display == null || _app.Display.showMinimap);
            _log.Add($"Город {_game.Map.Name}, сложность «{MagnateConfig.Title(_game.Config.Difficulty)}». " +
                     $"Хабы: {string.Join(", ", _game.Hubs.Select(h => _game.Map.LocationName(h.LocationId)))}.");
            StartTurn();
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
                    p.Arc(at, 15f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Stroke();
                }
                for (int l = 0; l < hub.Level; l++)
                    Square(p, at + new Vector2(-10f + l * 5f, 16f), 1.7f, l == 0 ? new Color(1f, 1f, 1f, 0.5f) : Theme.Gold);
                int k = 0;
                foreach (var waiting in _game.WaitingAt(hub.LocationId))
                {
                    if (_hiddenKinds.Contains(waiting.Kind)) continue;
                    var c = at + new Vector2(14f + k * 11f, -16f);
                    Square(p, c, 4.8f, new Color(0f, 0f, 0f, 0.6f));
                    Square(p, c, 4f, KindColor(waiting.Kind));
                    k++;
                }
            }

            // Фуры, а над очередями в пробках — часы.
            var fan = new Dictionary<string, int>();
            var clocks = new HashSet<string>();
            foreach (var truck in _game.Trucks)
            {
                fan.TryGetValue(truck.Location, out int index);
                fan[truck.Location] = index + 1;
                TruckPose(truck, index, out var position, out var heading);
                var move = MoveOf(truck);
                var kind = _game.GetCargo(truck.Cargo)?.Kind ?? (_animating != null ? move?.CargoKind : null);
                DrawTruck(p, position, heading, Palette.PlayerColor(truck.Owner), kind, truck.Id == _selectedTruck);
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
            if (_selectedTruck < 0) { Toast("Сначала выберите фуру в панели справа."); return; }
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

        // ================================================================== Панель

        void Refresh()
        {
            _roundLabel.text = $"{_game.Map.Name} · раунд {_game.Round}/{_game.Config.Rounds} · {MagnateConfig.Title(_game.Config.Difficulty)}";
            RefreshPlayers();
            RefreshHighlights();
            SyncHubs();
            _panelScroll.Clear();
            var c = _panelScroll.contentContainer;
            var player = _game.CurrentPlayer;

            if (_game.Phase == MagnatePhase.GameOver) { c.Add(Section("Партия окончена")); return; }
            if (_animating != null) { c.Add(Section("Фуры в пути…")); AddLog(c); return; }

            AddEvents(c);
            var head = Ui.Text(player.Name, 22f, Palette.PlayerColor(player.Index), true);
            head.style.marginTop = 12f;
            c.Add(Padded(head));
            if (player.IsBot)
            {
                c.Add(Padded(Ui.Text(_game.Phase == MagnatePhase.Build ? "Строит дороги и развивает хабы…" : "Планирует маршруты…", 15f, Theme.TextDim)));
                AddContracts(c);
                AddCargoBoard(c);
                AddLog(c);
                return;
            }
            if (_awaitingHandoff) return;

            if (_game.Phase == MagnatePhase.Build)
            {
                c.Add(Section($"Стройка · очков: {player.BuildPoints}"));
                c.Add(Padded(Wrap($"Клик по свободной дороге — купить (цена = длина, мост +{_game.Config.BridgeExtraCost}). Клик по своей — магистраль: " +
                                  "пропускает больше фур. Клик по хабу — развить: больше и дороже грузы, плата с соперников за погрузку, вы грузите первым. " +
                                  $"Каждый раунд +{_game.Config.BuildPointsPerRound} очков стройки (до {_game.Config.MaxBuildPoints}).")));
                var next = Ui.Button("Дальше: маршруты фур →", () => { _game.FinishBuild(); _app.Sound.Play(Sfx.Click, 0.6f); }, ButtonKind.Primary, 15f);
                next.style.marginTop = 6f;
                c.Add(Padded(next));
            }
            else
            {
                c.Add(Section("Маршруты фур"));
                c.Add(Padded(Wrap("Выберите фуру. На хабе — возьмите груз, и фура повезёт его к получателю. Или наведите на станцию — " +
                                  "увидите маршрут, время, плату и риск пробок; клик — ехать туда.")));
                foreach (var truck in _game.Trucks.Where(t => t.Owner == player.Index)) c.Add(TruckCard(truck));
                var done = Ui.Button(_game.IsLastTurnInRound ? "Готово — фуры поедут" : "Готово — передать ход", () =>
                {
                    _game.FinishPlanning();
                    _app.Sound.Play(Sfx.Ticket, 0.6f);
                    AfterTurn();
                }, ButtonKind.Primary, 15f);
                done.style.marginTop = 8f;
                c.Add(Padded(done));
            }
            AddContracts(c);
            AddCargoBoard(c);
            AddRules(c);
            AddLog(c);
        }

        void AddEvents(VisualElement c)
        {
            foreach (var e in _game.Events)
            {
                var card = Ui.Card(10f);
                card.style.marginLeft = 14f;
                card.style.marginRight = 14f;
                card.style.marginTop = 12f;
                card.style.backgroundColor = new Color(0.30f, 0.20f, 0.12f);
                Ui.Border(card, 1f, Theme.Gold);
                card.Add(Ui.Text($"Событие: {e.Title}" + (e.EndRound > _game.Round ? $" (до раунда {e.EndRound})" : ""), 14f, Theme.Gold, true));
                card.Add(Wrap(e.Description, 12f));
                c.Add(card);
            }
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
                _preview = null;
                _dirty = true;
            });

            int number = _game.Trucks.Where(t => t.Owner == truck.Owner).ToList().IndexOf(truck) + 1;
            var cargo = _game.GetCargo(truck.Cargo);
            var planned = _game.GetCargo(truck.PlannedCargo);
            var kind = cargo?.Kind ?? planned?.Kind;
            card.Add(Ui.Text($"Фура {number} · {_game.Map.LocationName(truck.Location)} · скорость {_game.SpeedOf(kind)}", 15f, Theme.Text, true));
            string status = cargo != null
                ? $"Везёт: {LogisticsGame.KindTitle(cargo.Kind).ToLowerInvariant()} → {_game.Map.LocationName(cargo.To)} " +
                  $"(~{_game.ValueIfDelivered(cargo, _game.RouteRounds(truck.Route, cargo.Kind), truck.Owner)} очк.)" +
                  (cargo.Kind == CargoKind.Valuable ? (cargo.Escorted ? " · с охраной" : " · без охраны!") : "")
                : planned != null ? $"Берёт: {LogisticsGame.KindTitle(planned.Kind).ToLowerInvariant()} → {_game.Map.LocationName(planned.To)}"
                : truck.Target != null ? $"Пустая → {_game.Map.LocationName(truck.Target)}"
                : "Стоит, ждёт задания";
            card.Add(Wrap(status, 13f));
            if (truck.Route.Count > 0)
            {
                bool center = kind == CargoKind.Hazard && _game.RouteEntersBribeZone(truck.Location, truck.Route);
                card.Add(Wrap($"Маршрут: {_game.RouteLength(truck.Route)} участков, ~{_game.RouteRounds(truck.Route, kind)} р. · проезд −{_game.RouteTolls(truck.Owner, truck.Route)}" +
                              $" · забитых дорог: {_game.RouteRisk(truck.Route)}" + (center ? $" · через Центр: взятка {_game.Config.HazardBribe}" : ""), 12f, Theme.TextDim));
            }
            if (!selected) return card;

            // Выбрана: грузы на её хабе (с расчётом пути) и сброс маршрута.
            if (cargo == null)
            {
                var waiting = _game.WaitingAt(truck.Location).ToList();
                if (waiting.Count == 0) card.Add(Wrap("Здесь нет грузов. Наведите на хаб (квадратики над ним — грузы) и кликните, чтобы поехать.", 12f, Theme.TextDim));
                foreach (var item in waiting)
                {
                    var path = _game.FindRoute(truck.Owner, truck.Location, item.To, item.Kind);
                    var row = CargoRow(item, true, path);
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

        VisualElement CargoRow(Cargo cargo, bool clickable, List<string> route = null)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            Ui.Padding(row, 4f, 6f);
            Ui.Radius(row, 6f);
            row.style.backgroundColor = clickable ? new Color(1f, 1f, 1f, 0.06f) : Color.clear;
            if (clickable) row.pickingMode = PickingMode.Position;
            row.Add(Chip(cargo.Kind, 20f));
            int roundsLeft = cargo.Kind == CargoKind.Perishable ? Mathf.Max(0, cargo.SpawnRound + cargo.Norm - _game.Round) : -1;
            string extra = "";
            if (clickable)
                extra = route != null
                    ? $" · {_game.RouteRounds(route, cargo.Kind)} р., проезд −{_game.RouteTolls(_game.CurrentPlayerIndex, route)}"
                    : " · сейчас не проехать";
            var text = Ui.Text($"{LogisticsGame.KindTitle(cargo.Kind)} → {_game.Map.LocationName(cargo.To)} · {cargo.Value} очк." +
                               (roundsLeft >= 0 ? $" · норма ещё {roundsLeft} р." : "") + extra, 12f, Theme.Text);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexShrink = 1f;
            row.Add(text);
            return row;
        }

        static Label Chip(CargoKind kind, float size)
        {
            var chip = Ui.Text(KindLetter(kind), size * 0.6f, new Color(0.08f, 0.08f, 0.1f), true);
            chip.style.width = size;
            chip.style.height = size;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.style.backgroundColor = KindColor(kind);
            Ui.Radius(chip, 4f);
            chip.style.marginRight = 8f;
            chip.style.flexShrink = 0f;
            return chip;
        }

        void AddContracts(VisualElement c)
        {
            if (_game.Contracts.Count == 0) return;
            c.Add(Section("Заказы мэрии"));
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
                var title = Ui.Text(contract.Title, 14f, Theme.Text, true);
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.flexShrink = 1f;
                titleRow.Add(title);
                card.Add(titleRow);
                card.Add(Wrap($"Первому: +{contract.RewardPoints} очков, +{contract.RewardInfluence} влияния и репутация. Срок — до конца раунда {contract.EndRound}.", 12f, Theme.TextDim));
                foreach (var company in _game.Players)
                {
                    var row = Ui.Row();
                    row.style.alignItems = Align.Center;
                    row.style.marginTop = 3f;
                    var name = Ui.Text(company.Name, 11f, Theme.TextDim);
                    name.style.width = 90f;
                    row.Add(name);
                    var track = new VisualElement();
                    track.style.flexGrow = 1f;
                    track.style.height = 6f;
                    track.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
                    Ui.Radius(track, 3f);
                    var fill = new VisualElement();
                    fill.style.height = Length.Percent(100f);
                    fill.style.width = Length.Percent(100f * Mathf.Clamp01(contract.Progress[company.Index] / (float)Math.Max(1, contract.Needed)));
                    fill.style.backgroundColor = Palette.PlayerColor(company.Index);
                    Ui.Radius(fill, 3f);
                    track.Add(fill);
                    row.Add(track);
                    var count = Ui.Text($" {contract.Progress[company.Index]}/{contract.Needed}", 11f, Theme.TextDim);
                    count.style.width = 34f;
                    row.Add(count);
                    card.Add(row);
                }
                c.Add(card);
            }
        }

        void AddCargoBoard(VisualElement c)
        {
            c.Add(Section("Грузы на хабах"));
            // Фильтры: какие виды показывать на карте, мини-карте и в списке.
            var filters = Ui.Row();
            filters.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            filters.style.marginLeft = 14f;
            filters.style.marginRight = 14f;
            filters.style.marginBottom = 4f;
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
            c.Add(filters);
            bool any = false;
            foreach (var hub in _game.Hubs)
            {
                var waiting = _game.WaitingAt(hub.LocationId).Where(x => !_hiddenKinds.Contains(x.Kind)).ToList();
                if (waiting.Count == 0) continue;
                any = true;
                var title = Ui.Text($"{_game.Map.LocationName(hub.LocationId)} · ур. {hub.Level}", 13f, Theme.TextDim, true);
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
            if (!any) c.Add(Padded(Ui.Text(_hiddenKinds.Count > 0 ? "Нет грузов выбранных видов." : "Пусто — новые грузы появятся в следующем раунде.", 13f, Theme.TextDim)));
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
                row.Add(Chip(kind, 18f));
                var text = Wrap($"{LogisticsGame.KindTitle(kind)}: {LogisticsGame.KindRule(kind, _game.Config)}", 12f);
                text.style.flexShrink = 1f;
                row.Add(text);
                c.Add(row);
            }
            c.Add(Padded(Wrap($"Пропускная способность за раунд: улица {_game.Config.CapacityStreet}, широкая дорога и мост {_game.Config.CapacityWide}, " +
                              $"магистраль {_game.Config.CapacityHighway}. Больше фур — пробка: все они стоят, каждая −{_game.Config.JamPenalty}. " +
                              "Серые дороги — городские: общие и бесплатные. Репутация растёт за доставки вовремя и заказы мэрии, " +
                              "падает за пробки и потери; с уровнем растут очки за доставку (до ×1,5).", 12f, Theme.TextDim)));
        }

        void AddLog(VisualElement c)
        {
            c.Add(Section("Журнал"));
            foreach (var line in _log.Skip(Math.Max(0, _log.Count - 12)).Reverse())
                c.Add(Padded(Wrap(line, 12f, Theme.TextDim)));
        }

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

        /// <summary>Подсветка: в стройку — доступные дороги, в план — маршруты своих фур (выбранная ярче) и предпросмотр.</summary>
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
            if (_preview != null)
                foreach (var id in _preview) _map.PathRoads[id] = new Color(0.45f, 0.9f, 1f, 0.9f);
        }

        void SyncHubs()
        {
            if (_city3D == null) return;
            foreach (var hub in _game.Hubs)
                _city3D.SetHub(hub.LocationId, hub.Kind == HubKind.Airport ? 0 : hub.Kind == HubKind.Station ? 1 : 2, hub.Level);
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
            if (_resultsShown) return;
            _resultsShown = true;
            var credits = Profiles.CreditAfterMagnate(_game, _setup);
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
                _city3D = City3DScene.TryCreate(_game.Map, display != null ? Mathf.Clamp(display.city3DQuality, 0, 2) : 2);
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
        }
    }
}
