using System;
using System.Collections.Generic;
using System.Linq;
using RailGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RailGame.Presentation.App
{
    /// <summary>
    /// Игровой экран на UI Toolkit. Правила — в Game, здесь только отображение и ввод.
    /// Анимации строятся по разнице состояний до и после каждого хода, поэтому
    /// одинаково работают и для людей, и для ботов.
    /// </summary>
    public sealed class GameView
    {
        const float SidebarWidth = 400f;
        const float BottomHeight = 200f;
        const float BotDelay = 0.55f;
        const int LogLines = 8;
        const int ColorCount = 9; // 8 цветов + локомотив

        enum OverlayKind
        {
            None,
            Payment,
            Handoff,
            GameOver,
            ConfirmExit
        }

        sealed class PlayerRow
        {
            public VisualElement Root;
            public Label Name;
            public Label Stats;
        }

        sealed class Snapshot
        {
            public int[,] Hands;
            public CardColor?[] FaceUp;
            public readonly Dictionary<string, int> Owners = new Dictionary<string, int>();
            public readonly HashSet<string> Completed = new HashSet<string>();
            public int Acting;
            public GamePhase Phase;
            public int TicketDeck;
        }

        readonly RailGameApp _app;
        readonly GameSetup _setup;
        public VisualElement Root { get; }

        Game _game;
        IBot[] _bots;
        readonly MapView _map;

        // Правая панель
        readonly Label _turnLabel;
        readonly VisualElement _faceUpRow;
        CardView[] _faceUp = new CardView[0];
        readonly UnityEngine.UIElements.Button _deckButton;
        readonly UnityEngine.UIElements.Button _ticketsButton;
        readonly VisualElement _playersBox;
        PlayerRow[] _playerRows = new PlayerRow[0];
        readonly VisualElement _ticketsBox;
        readonly VisualElement _logBox;

        // Нижняя панель
        readonly VisualElement _statusChip;
        readonly Label _statusName;
        readonly Label _statusText;
        readonly Label _finalRound;
        readonly Label _flashLabel;
        readonly UnityEngine.UIElements.Button _passButton;
        readonly Label _handTitle;
        readonly VisualElement _handRow;
        readonly Dictionary<CardColor, CardView> _hand = new Dictionary<CardColor, CardView>();

        // Слои поверх всего
        readonly VisualElement _fx;
        readonly VisualElement _overlay;
        readonly VisualElement _tooltip;
        readonly Label _tooltipText;
        readonly VisualElement _toastHolder;
        readonly Label _toast;
        int _toastVersion;
        OverlayKind _overlayKind;
        int _overlayPlayer = -1;

        // Состояние экрана
        readonly List<string> _log = new List<string>();
        string _flash;
        float _flashUntil;
        int _revealedFor = -1;
        float _botTimer;
        bool _dirty = true;
        readonly HashSet<string> _selectedTickets = new HashSet<string>();
        string _selectionKey;
        Snapshot _snapshot;

        public GameView(RailGameApp app, GameSetup setup)
        {
            _app = app;
            _setup = setup;

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.flexDirection = FlexDirection.Row;

            // ---------- Левая часть: карта и нижняя панель
            var left = Ui.Column();
            left.style.flexGrow = 1f;
            Root.Add(left);

            _map = new MapView();
            _map.Hovered += OnMapHover;
            _map.RouteClicked += OnRouteClicked;
            left.Add(_map);

            var bottom = Ui.Row();
            bottom.style.height = BottomHeight;
            bottom.style.flexShrink = 0f;
            bottom.style.alignItems = Align.Stretch;
            bottom.style.backgroundColor = Theme.Panel;
            bottom.style.borderTopWidth = 1f;
            bottom.style.borderTopColor = Theme.Line;
            Ui.Padding(bottom, 16f, 20f);
            left.Add(bottom);

            var status = Ui.Column();
            status.style.width = 430f;
            status.style.flexShrink = 0f;
            status.style.paddingRight = 20f;
            bottom.Add(status);

            var nameRow = Ui.Row();
            _statusChip = Ui.Box();
            _statusChip.style.width = 18f;
            _statusChip.style.height = 18f;
            _statusChip.style.marginRight = 10f;
            Ui.Radius(_statusChip, 4f);
            nameRow.Add(_statusChip);
            _statusName = Ui.Text("", 22f, null, true);
            nameRow.Add(_statusName);
            status.Add(nameRow);

            _statusText = Ui.Text("", 15f, Theme.Text);
            _statusText.style.marginTop = 8f;
            status.Add(_statusText);
            _finalRound = Ui.Text("Последний круг!", 15f, Theme.Accent, true);
            _finalRound.style.marginTop = 6f;
            status.Add(_finalRound);
            _flashLabel = Ui.Text("", 13f, Theme.Danger);
            _flashLabel.style.marginTop = 6f;
            status.Add(_flashLabel);
            _passButton = Ui.Button("Пропустить ход", () => Act(() => _game.Pass()), ButtonKind.Secondary, 14f);
            _passButton.style.marginTop = 8f;
            _passButton.style.alignSelf = Align.FlexStart;
            status.Add(_passButton);

            var handColumn = Ui.Column();
            handColumn.style.flexGrow = 1f;
            bottom.Add(handColumn);
            _handTitle = Ui.SectionTitle("Рука");
            handColumn.Add(_handTitle);
            _handRow = Ui.Row();
            handColumn.Add(_handRow);
            foreach (var color in CardColors.Regular.Concat(new[] { CardColor.Locomotive }))
            {
                var card = new CardView(76f, 124f, true);
                card.pickingMode = PickingMode.Ignore;
                card.style.marginRight = 8f;
                _hand[color] = card;
                _handRow.Add(card);
            }

            // ---------- Правая панель
            var side = Ui.Column();
            side.style.width = SidebarWidth;
            side.style.flexShrink = 0f;
            side.style.backgroundColor = Theme.Panel;
            side.style.borderLeftWidth = 1f;
            side.style.borderLeftColor = Theme.Line;
            Ui.Padding(side, 16f, 18f);
            Root.Add(side);

            var header = Ui.Row();
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 14f;
            _turnLabel = Ui.Text("", 20f, null, true);
            header.Add(_turnLabel);
            header.Add(Ui.Button("Меню", ShowConfirmExit, ButtonKind.Ghost, 13f));
            side.Add(header);

            side.Add(Ui.SectionTitle("Открытые карты"));
            _faceUpRow = Ui.Row();
            _faceUpRow.style.justifyContent = Justify.SpaceBetween;
            side.Add(_faceUpRow);

            var drawRow = Ui.Row();
            drawRow.style.marginTop = 10f;
            _deckButton = Ui.Button("Колода", () => Act(() => _game.DrawCardFromDeck()), ButtonKind.Secondary, 14f);
            _deckButton.style.flexGrow = 1f;
            _deckButton.style.marginRight = 8f;
            drawRow.Add(_deckButton);
            _ticketsButton = Ui.Button("Задания", () => Act(() => _game.DrawTickets()), ButtonKind.Secondary, 14f);
            _ticketsButton.style.flexGrow = 1f;
            drawRow.Add(_ticketsButton);
            side.Add(drawRow);

            var playersTitle = Ui.SectionTitle("Игроки");
            playersTitle.style.marginTop = 18f;
            side.Add(playersTitle);
            _playersBox = Ui.Column();
            side.Add(_playersBox);

            _ticketsBox = Ui.Column();
            _ticketsBox.style.flexGrow = 1f;
            _ticketsBox.style.flexShrink = 1f;
            _ticketsBox.style.overflow = Overflow.Hidden;
            _ticketsBox.style.marginTop = 12f;
            side.Add(_ticketsBox);

            side.Add(Ui.SectionTitle("Журнал"));
            _logBox = Ui.Column();
            _logBox.style.height = LogLines * 19f;
            _logBox.style.flexShrink = 0f;
            _logBox.style.overflow = Overflow.Hidden;
            side.Add(_logBox);

            // ---------- Оверлеи и эффекты
            _overlay = new VisualElement();
            Ui.Fill(_overlay);
            _overlay.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == _overlay && _overlayKind == OverlayKind.Payment) HideOverlay();
            });
            Ui.Show(_overlay, false);
            Root.Add(_overlay);

            _fx = Ui.Box();
            Ui.Fill(_fx);
            Root.Add(_fx);

            _tooltip = Ui.Column();
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.width = 300f;
            _tooltip.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.95f);
            Ui.Radius(_tooltip, 8f);
            Ui.Border(_tooltip, 1f, Theme.Line);
            Ui.Padding(_tooltip, 10f, 12f);
            _tooltipText = Ui.Text("", 13f);
            _tooltip.Add(_tooltipText);
            Ui.Show(_tooltip, false);
            _fx.Add(_tooltip);

            _toastHolder = Ui.Row();
            _toastHolder.style.position = Position.Absolute;
            _toastHolder.style.left = 0f;
            _toastHolder.style.right = SidebarWidth;
            _toastHolder.style.top = 70f;
            _toastHolder.style.justifyContent = Justify.Center;
            _toast = Ui.Text("", 20f, Theme.Accent, true);
            _toast.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
            _toast.style.whiteSpace = WhiteSpace.NoWrap;
            Ui.Radius(_toast, 22f);
            Ui.Padding(_toast, 10f, 24f);
            _toastHolder.Add(_toast);
            Ui.Show(_toastHolder, false);
            _fx.Add(_toastHolder);
        }

        // ================================================================== Партия

        public void StartNewGame()
        {
            try
            {
                var map = MapLoader.LoadFromResources(_setup.mapResourcePath);
                int seed = UnityEngine.Random.Range(0, int.MaxValue);
                _game = new Game(map, _setup.PlayerNames(), new GameConfig { TrainsPerPlayer = _setup.trainsPerPlayer }, seed);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError(e.Message);
                return;
            }

            _game.Log += AddLog;
            _game.StateChanged += OnStateChanged;

            _bots = new IBot[_game.Players.Count];
            for (int i = 0; i < _bots.Length; i++)
                if (IsBot(i)) _bots[i] = Bots.Create(_setup.players[i].botLevel, UnityEngine.Random.Range(0, int.MaxValue));

            _map.SetGame(_game);
            BuildFaceUpRow();
            BuildPlayerRows();

            _log.Clear();
            AddLog($"Новая партия на карте «{_game.Map.Name}».");
            _revealedFor = -1;
            _selectionKey = null;
            _selectedTickets.Clear();
            _flash = null;
            HideOverlay();
            _snapshot = TakeSnapshot();
            _dirty = true;

            _app.Tweener.After(0.4f, OnTurnStarted);
        }

        public void Tick(float deltaTime)
        {
            if (_game == null) return;

            _map.PulseTime += deltaTime;
            _map.MarkDirtyRepaint();

            if (_flash != null && Time.time > _flashUntil)
            {
                _flash = null;
                _dirty = true;
            }

            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }

            TickBots(deltaTime);
        }

        void TickBots(float deltaTime)
        {
            if (_game.Phase == GamePhase.GameOver || _overlayKind == OverlayKind.ConfirmExit) return;

            int acting = _game.ActingPlayerIndex;
            if (!IsBot(acting))
            {
                _botTimer = 0f;
                return;
            }
            // Боты ждут, пока доиграют анимации, — иначе за их ходами не уследить.
            if (_app.Tweener.BlockingCount > 0) return;

            _botTimer += deltaTime;
            if (_botTimer < BotDelay) return;
            _botTimer = 0f;

            var result = _bots[acting].Act(_game);
            if (!result.Success) Flash("Бот: " + result.Error);
        }

        // ================================================================== Кто ходит и кто смотрит

        bool IsBot(int index) => index >= 0 && index < _setup.players.Count && _setup.players[index].isBot;

        int HumanCount => _setup.players.Count(p => !p.isBot);

        bool NeedsHandoff
        {
            get
            {
                int acting = _game.ActingPlayerIndex;
                return _setup.hideCardsBetweenTurns && HumanCount > 1 && acting >= 0 && !IsBot(acting) && _revealedFor != acting;
            }
        }

        int Viewer
        {
            get
            {
                int acting = _game.ActingPlayerIndex;
                if (acting >= 0 && !IsBot(acting)) return acting;
                if (_revealedFor >= 0 && !IsBot(_revealedFor)) return _revealedFor;
                for (int i = 0; i < _game.Players.Count; i++)
                    if (!IsBot(i)) return i;
                return Math.Max(acting, 0);
            }
        }

        bool HumanToAct
        {
            get
            {
                int acting = _game.ActingPlayerIndex;
                return acting >= 0 && !IsBot(acting) && !NeedsHandoff && _overlayKind != OverlayKind.ConfirmExit;
            }
        }

        string BotTag(int index)
        {
            if (!IsBot(index)) return "";
            return _setup.players[index].botLevel == BotLevel.Random ? " · случайный бот" : " · бот";
        }

        // ================================================================== Действия игрока

        void Act(Func<ActionResult> action)
        {
            if (_game == null) return;
            if (!HumanToAct)
            {
                Flash(IsBot(_game.ActingPlayerIndex) ? "Сейчас ходит бот." : "Сейчас нельзя.");
                return;
            }
            var result = action();
            if (!result.Success) Flash(result.Error);
        }

        void OnRouteClicked(Route route, Vector2 position)
        {
            if (_overlayKind != OverlayKind.None) return;
            if (!HumanToAct)
            {
                Flash("Сейчас ходит бот.");
                return;
            }

            switch (_game.Phase)
            {
                case GamePhase.SetupTickets:
                    Flash("Сначала выберите стартовые задания.");
                    return;
                case GamePhase.DrawingSecondCard:
                    Flash("Сначала возьмите вторую карту.");
                    return;
                case GamePhase.ChoosingTickets:
                    Flash("Сначала выберите задания.");
                    return;
            }

            var options = _game.GetClaimOptions(route.Id);
            if (options.Count == 0)
            {
                Flash(_game.GetClaimBlocker(route.Id) ?? "Этот маршрут сейчас занять нельзя.");
                return;
            }
            if (options.Count == 1)
            {
                var only = options[0];
                Act(() => _game.ClaimRoute(route.Id, only.Color, only.Locomotives));
                return;
            }
            ShowPayment(route, options, position);
        }

        void OnMapHover(Route route, Vector2 position)
        {
            if (route == null || _overlayKind != OverlayKind.None || _game == null)
            {
                Ui.Show(_tooltip, false);
                return;
            }

            _tooltipText.text = RouteTooltip(route);
            var local = _fx.WorldToLocal(_map.LocalToWorld(position));
            var mapBounds = _map.worldBound;
            var mapMax = _fx.WorldToLocal(new Vector2(mapBounds.xMax, mapBounds.yMax));
            _tooltip.style.left = Mathf.Min(local.x + 18f, mapMax.x - 310f);
            _tooltip.style.top = Mathf.Min(local.y + 18f, mapMax.y - 110f);
            Ui.Show(_tooltip, true);
        }

        string RouteTooltip(Route route)
        {
            string color = route.Color.HasValue ? Palette.CardTitle(route.Color.Value) : "Серый — любой один цвет";
            string text = $"<b>{CityName(route.CityA)} — {CityName(route.CityB)}</b>\n" +
                          $"Длина {route.Length} · {color} · {_game.Config.PointsForRouteLength(route.Length)} очк.";

            int? owner = _game.GetRouteOwner(route.Id);
            if (owner.HasValue) return text + "\nЗанят: " + _game.Players[owner.Value].Name;
            if (!_game.IsRouteOpenFor(_game.CurrentPlayerIndex, route.Id) && route.ParallelRouteId != null)
                return text + "\nЗакрыт: параллельный путь уже занят";
            if (HumanToAct && _game.Phase == GamePhase.AwaitingAction)
                return text + "\n" + (_game.GetClaimBlocker(route.Id) ?? "<color=#7FD88A>Кликните, чтобы занять</color>");
            return text;
        }

        // ================================================================== Анимации по изменениям

        void OnStateChanged()
        {
            var now = TakeSnapshot();
            if (_snapshot != null) AnimateChanges(_snapshot, now);
            _snapshot = now;
            _dirty = true;
        }

        Snapshot TakeSnapshot()
        {
            var s = new Snapshot
            {
                Hands = new int[_game.Players.Count, ColorCount],
                FaceUp = _game.FaceUpCards.ToArray(),
                Acting = _game.ActingPlayerIndex,
                Phase = _game.Phase,
                TicketDeck = _game.TicketDeckCount
            };

            foreach (var player in _game.Players)
            {
                for (int c = 0; c < ColorCount; c++) s.Hands[player.Index, c] = player.CardCount((CardColor)c);
                foreach (var ticket in player.Tickets)
                    if (Scoring.IsTicketCompleted(ticket, player.ClaimedRoutes)) s.Completed.Add(player.Index + ":" + ticket.Id);
            }

            foreach (var route in _game.Map.Routes)
            {
                var owner = _game.GetRouteOwner(route.Id);
                if (owner.HasValue) s.Owners[route.Id] = owner.Value;
            }
            return s;
        }

        void AnimateChanges(Snapshot before, Snapshot now)
        {
            // Новые маршруты — прокладываем вагоны.
            float claimDuration = 0f;
            foreach (var pair in now.Owners)
            {
                if (before.Owners.ContainsKey(pair.Key)) continue;
                var route = _game.Map.GetRoute(pair.Key);
                AnimateClaim(route);
                claimDuration = Mathf.Max(claimDuration, ClaimDuration(route));
            }

            // Новые карты в руках — летят из открытого ряда или из колоды.
            var changedSlots = new List<int>();
            for (int i = 0; i < now.FaceUp.Length && i < before.FaceUp.Length; i++)
                if (!Nullable.Equals(before.FaceUp[i], now.FaceUp[i])) changedSlots.Add(i);

            int flight = 0;
            for (int p = 0; p < _game.Players.Count; p++)
            {
                for (int c = 0; c < ColorCount; c++)
                {
                    int gained = now.Hands[p, c] - before.Hands[p, c];
                    for (int k = 0; k < gained; k++)
                    {
                        int slot = -1;
                        foreach (int index in changedSlots)
                        {
                            if (before.FaceUp[index] == (CardColor)c)
                            {
                                slot = index;
                                break;
                            }
                        }
                        if (slot >= 0) changedSlots.Remove(slot);
                        FlyCard(p, (CardColor)c, slot, flight++ * 0.15f);
                    }
                }
            }

            if (now.TicketDeck < before.TicketDeck) _app.Sound.Play(Sfx.Swish);

            // Выполненные задания того, кто сидит за экраном.
            foreach (var key in now.Completed)
            {
                if (before.Completed.Contains(key)) continue;
                int separator = key.IndexOf(':');
                int playerIndex = int.Parse(key.Substring(0, separator));
                string ticketId = key.Substring(separator + 1);
                if (playerIndex != Viewer || IsBot(playerIndex)) continue;

                var ticket = _game.Players[playerIndex].Tickets.FirstOrDefault(t => t.Id == ticketId);
                if (ticket == null) continue;
                _app.Tweener.After(claimDuration, () =>
                {
                    Toast($"Задание выполнено: {CityName(ticket.CityA)} — {CityName(ticket.CityB)}  +{ticket.Points}", Theme.Success);
                    _app.Sound.Play(Sfx.Ticket);
                });
            }

            if (now.Phase == GamePhase.GameOver && before.Phase != GamePhase.GameOver)
            {
                _app.Tweener.After(claimDuration + 0.4f, () => _app.Sound.Play(Sfx.Fanfare));
                return;
            }

            bool turnChanged = now.Acting != before.Acting
                               || (before.Phase == GamePhase.SetupTickets && now.Phase != GamePhase.SetupTickets);
            if (turnChanged) _app.Tweener.After(claimDuration + 0.1f, OnTurnStarted);
        }

        void OnTurnStarted()
        {
            if (_game == null || _game.Phase == GamePhase.GameOver) return;
            int acting = _game.ActingPlayerIndex;
            if (acting < 0 || IsBot(acting)) return;

            _app.Sound.Play(Sfx.Bell, 0.6f);
            if (NeedsHandoff) return; // покажется окно передачи хода

            var name = _game.Players[acting].Name;
            if (_game.Phase == GamePhase.SetupTickets) Toast($"{name}, выберите стартовые задания", Theme.Accent);
            else Toast(HumanCount > 1 ? "Ходит " + name : "Ваш ход", Theme.Accent);
        }

        static float ClaimDuration(Route route) => route == null ? 0f : 0.1f * route.Length + 0.1f;

        void AnimateClaim(Route route)
        {
            if (route == null) return;
            _map.ClaimProgress[route.Id] = 0f;
            int segments = route.Length;
            int played = 0;

            _app.Tweener.Add(ClaimDuration(route), k =>
            {
                _map.ClaimProgress[route.Id] = k;
                int shown = Mathf.FloorToInt(k * segments + 0.001f);
                if (shown > played)
                {
                    played = shown;
                    _app.Sound.Play(Sfx.Clack, 0.8f);
                }
            }, () =>
            {
                _map.ClaimProgress.Remove(route.Id);
                _app.Sound.Play(Sfx.Whistle, 0.6f);
            });
        }

        void FlyCard(int playerIndex, CardColor color, int faceUpSlot, float delay)
        {
            VisualElement from = faceUpSlot >= 0 && faceUpSlot < _faceUp.Length ? (VisualElement)_faceUp[faceUpSlot] : _deckButton;
            bool toHand = playerIndex == Viewer && !IsBot(playerIndex) && !NeedsHandoff;
            VisualElement to = toHand ? (VisualElement)_hand[color] : _playerRows[playerIndex].Root;
            if (!IsLaidOut(from) || !IsLaidOut(to)) return;

            // Цвет видно, если карта была открытой или летит в руку того, кто смотрит на экран.
            bool reveal = faceUpSlot >= 0 || toHand;
            const float width = 58f;
            const float height = 84f;
            var start = _fx.WorldToLocal(from.worldBound.center) - new Vector2(width / 2f, height / 2f);
            var end = _fx.WorldToLocal(to.worldBound.center) - new Vector2(width / 2f, height / 2f);

            var card = new CardView(width, height, false);
            card.pickingMode = PickingMode.Ignore;
            if (reveal) card.Set(color);
            else card.ShowBack();
            card.style.position = Position.Absolute;
            card.style.left = start.x;
            card.style.top = start.y;
            card.style.opacity = 0f;
            _fx.Add(card);

            _app.Tweener.After(delay, () => _app.Sound.Play(Sfx.Card));
            _app.Tweener.Add(0.5f, k =>
            {
                float eased = Ui.EaseInOutCubic(k);
                var position = Vector2.Lerp(start, end, eased);
                position.y -= Mathf.Sin(k * Mathf.PI) * 50f;
                card.style.left = position.x;
                card.style.top = position.y;
                card.style.opacity = Mathf.Clamp01(k * 8f) * (k > 0.85f ? (1f - k) / 0.15f : 1f);
            }, () =>
            {
                card.RemoveFromHierarchy();
                if (to is CardView target) _app.Tweener.Add(0.3f, target.Pulse, () => target.Pulse(0f), 0f, false);
            }, delay);
        }

        static bool IsLaidOut(VisualElement e) =>
            e != null && e.panel != null && !float.IsNaN(e.worldBound.width) && e.worldBound.width > 1f;

        void Toast(string text, Color color)
        {
            _toast.text = text;
            _toast.style.color = color;
            int version = ++_toastVersion;
            Ui.Show(_toastHolder, true);

            _app.Tweener.Add(2.4f, k =>
            {
                if (version != _toastVersion) return;
                float appear = Mathf.Clamp01(k / 0.1f);
                float alpha = k > 0.8f ? (1f - k) / 0.2f : appear;
                _toastHolder.style.opacity = alpha;
                _toastHolder.style.top = 70f - 14f * (1f - Ui.EaseOutCubic(appear));
            }, () =>
            {
                if (version == _toastVersion) Ui.Show(_toastHolder, false);
            }, 0f, false);
        }

        void Flash(string message)
        {
            _flash = message;
            _flashUntil = Time.time + 3.5f;
            _app.Sound.Play(Sfx.Error, 0.7f);
            _dirty = true;
        }

        void AddLog(string message)
        {
            _log.Add(message);
            if (_log.Count > 200) _log.RemoveAt(0);
            _dirty = true;
        }

        // ================================================================== Обновление экрана

        void Refresh()
        {
            if (_game == null) return;
            _turnLabel.text = _game.Phase == GamePhase.SetupTickets ? "Подготовка" : "Ход " + _game.TurnNumber;
            RefreshFaceUp();
            RefreshPlayers();
            RefreshTickets();
            RefreshLog();
            RefreshStatus();
            RefreshHand();
            RefreshOverlays();
            RefreshMap();
        }

        void BuildFaceUpRow()
        {
            _faceUpRow.Clear();
            _faceUp = new CardView[_game.FaceUpCards.Count];
            for (int i = 0; i < _faceUp.Length; i++)
            {
                int slot = i;
                var card = new CardView(64f, 92f, false);
                card.RegisterCallback<ClickEvent>(_ => Act(() => _game.DrawFaceUpCard(slot)));
                card.RegisterCallback<PointerEnterEvent>(_ => card.style.top = -5f);
                card.RegisterCallback<PointerLeaveEvent>(_ => card.style.top = 0f);
                _faceUp[i] = card;
                _faceUpRow.Add(card);
            }
        }

        void RefreshFaceUp()
        {
            bool canDraw = HumanToAct &&
                           (_game.Phase == GamePhase.AwaitingAction || _game.Phase == GamePhase.DrawingSecondCard);
            for (int i = 0; i < _faceUp.Length; i++)
            {
                var card = _game.FaceUpCards[i];
                bool forbidden = _game.Phase == GamePhase.DrawingSecondCard && card == CardColor.Locomotive;
                _faceUp[i].Set(card, null, card.HasValue && forbidden);
            }
            _faceUpRow.style.opacity = canDraw ? 1f : 0.7f;

            _deckButton.text = "Колода · " + _game.DeckCount;
            Ui.SetEnabled(_deckButton, canDraw && _game.DeckCount + _game.DiscardCount > 0);
            _ticketsButton.text = "Задания · " + _game.TicketDeckCount;
            Ui.SetEnabled(_ticketsButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction && _game.TicketDeckCount > 0);
        }

        void BuildPlayerRows()
        {
            _playersBox.Clear();
            _playerRows = new PlayerRow[_game.Players.Count];
            foreach (var player in _game.Players)
            {
                var row = Ui.Row();
                row.style.marginBottom = 6f;
                Ui.Padding(row, 8f, 10f);
                Ui.Radius(row, 8f);

                var chip = Ui.Box();
                chip.style.width = 16f;
                chip.style.height = 16f;
                chip.style.marginRight = 10f;
                chip.style.backgroundColor = Palette.PlayerColor(player.Index);
                Ui.Radius(chip, 4f);
                row.Add(chip);

                var texts = Ui.Column();
                texts.style.flexGrow = 1f;
                var name = Ui.Text("", 15f, null, true);
                var stats = Ui.Text("", 12f, Theme.TextDim);
                texts.Add(name);
                texts.Add(stats);
                row.Add(texts);

                _playersBox.Add(row);
                _playerRows[player.Index] = new PlayerRow { Root = row, Name = name, Stats = stats };
            }
        }

        void RefreshPlayers()
        {
            int acting = _game.ActingPlayerIndex;
            foreach (var player in _game.Players)
            {
                var row = _playerRows[player.Index];
                bool active = player.Index == acting;
                row.Name.text = player.Name + BotTag(player.Index) + (active ? " — ходит" : "");
                row.Stats.text = $"{player.RoutePoints} очк. · {player.TrainsLeft} ваг. · {player.HandSize} карт · {player.Tickets.Count} зад.";
                row.Root.style.backgroundColor = active ? Theme.PanelHover : Theme.PanelLight;
                Ui.Border(row.Root, active ? 2f : 1f, active ? Palette.PlayerColor(player.Index) : Theme.Line);
            }
        }

        void RefreshTickets()
        {
            _ticketsBox.Clear();
            _map.HighlightCities.Clear();

            if (TryGetTicketChooser(out var chooser)) BuildTicketChoice(chooser);
            else BuildTicketList();
        }

        bool TryGetTicketChooser(out PlayerState chooser)
        {
            chooser = null;
            if (!HumanToAct) return false;
            if (_game.Phase != GamePhase.SetupTickets && _game.Phase != GamePhase.ChoosingTickets) return false;
            var player = _game.Players[_game.ActingPlayerIndex];
            if (player.PendingTickets.Count == 0) return false;
            chooser = player;
            return true;
        }

        void BuildTicketChoice(PlayerState player)
        {
            bool setup = _game.Phase == GamePhase.SetupTickets;
            int minKeep = setup ? _game.Config.InitialTicketsMinKeep : _game.Config.TicketsMinKeep;
            int required = Math.Min(minKeep, player.PendingTickets.Count);

            string key = player.Index + ":" + string.Join(",", player.PendingTickets.Select(t => t.Id));
            if (key != _selectionKey)
            {
                _selectionKey = key;
                _selectedTickets.Clear();
                if (setup)
                    foreach (var t in player.PendingTickets) _selectedTickets.Add(t.Id);
            }

            var card = Ui.Card(12f);
            Ui.Border(card, 2f, Theme.Accent);
            card.Add(Ui.Text(setup ? "Стартовые задания" : "Новые задания", 17f, Theme.Accent, true));
            var hint = Ui.Text($"Оставьте хотя бы {required}. Наведите — города подсветятся на карте. Невыполненное задание в конце отнимает очки.", 12f, Theme.TextDim);
            hint.style.marginTop = 4f;
            hint.style.marginBottom = 8f;
            card.Add(hint);

            foreach (var ticket in player.PendingTickets)
            {
                bool selected = _selectedTickets.Contains(ticket.Id);
                var row = TicketRow(ticket, selected ? new Color(0.27f, 0.36f, 0.22f) : Theme.Panel, selected, false);
                row.pickingMode = PickingMode.Position;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_selectedTickets.Remove(ticket.Id)) _selectedTickets.Add(ticket.Id);
                    _app.Sound.Play(Sfx.Click, 0.5f);
                    _dirty = true;
                });
                card.Add(row);
            }

            var confirm = Ui.Button($"Оставить выбранные ({_selectedTickets.Count})", () =>
            {
                var ids = _selectedTickets.ToList();
                if (ids.Count < required)
                {
                    Flash($"Нужно оставить хотя бы {required}.");
                    return;
                }
                Act(() => setup ? _game.KeepInitialTickets(player.Index, ids) : _game.KeepTickets(ids));
            }, ButtonKind.Primary, 15f);
            confirm.style.marginTop = 8f;
            Ui.SetEnabled(confirm, _selectedTickets.Count >= required);
            card.Add(confirm);

            _ticketsBox.Add(card);
        }

        void BuildTicketList()
        {
            var viewer = _game.Players[Viewer];
            _ticketsBox.Add(Ui.SectionTitle("Задания · " + viewer.Name));

            if (NeedsHandoff)
            {
                _ticketsBox.Add(Ui.Text("Скрыто до передачи хода.", 13f, Theme.TextDim));
                return;
            }
            if (viewer.Tickets.Count == 0)
            {
                _ticketsBox.Add(Ui.Text("Заданий пока нет.", 13f, Theme.TextDim));
                return;
            }

            foreach (var ticket in viewer.Tickets.OrderBy(t => Scoring.IsTicketCompleted(t, viewer.ClaimedRoutes)))
            {
                bool done = Scoring.IsTicketCompleted(ticket, viewer.ClaimedRoutes);
                _ticketsBox.Add(TicketRow(ticket, Theme.PanelLight, done, true));
            }
        }

        /// <summary>Строка задания. checkbox=false — квадратик выбора, true — кружок «выполнено».</summary>
        VisualElement TicketRow(Ticket ticket, Color background, bool marked, bool completionStyle)
        {
            var row = Ui.Row();
            row.pickingMode = PickingMode.Position;
            row.style.backgroundColor = background;
            row.style.marginBottom = 6f;
            Ui.Radius(row, 7f);
            Ui.Padding(row, 8f, 10f);

            var mark = Ui.Box();
            mark.style.width = 14f;
            mark.style.height = 14f;
            mark.style.marginRight = 10f;
            Ui.Radius(mark, completionStyle ? 7f : 3f);
            if (marked)
            {
                mark.style.backgroundColor = Theme.Success;
            }
            else
            {
                Ui.Border(mark, 2f, Theme.TextDim);
            }
            row.Add(mark);

            var cities = Ui.Text($"{CityName(ticket.CityA)} — {CityName(ticket.CityB)}", 14f, completionStyle && marked ? Theme.TextDim : Theme.Text);
            cities.style.flexGrow = 1f;
            row.Add(cities);
            var points = Ui.Text((completionStyle && marked ? "+" : "") + ticket.Points, 14f, completionStyle && marked ? Theme.Success : Theme.Accent, true);
            row.Add(points);

            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _map.HighlightCities.Clear();
                _map.HighlightCities.Add(ticket.CityA);
                _map.HighlightCities.Add(ticket.CityB);
                row.style.borderLeftWidth = 3f;
                row.style.borderLeftColor = Theme.Accent;
            });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _map.HighlightCities.Clear();
                row.style.borderLeftWidth = 0f;
            });
            return row;
        }

        void RefreshLog()
        {
            _logBox.Clear();
            int start = Math.Max(0, _log.Count - LogLines);
            for (int i = start; i < _log.Count; i++)
            {
                var line = Ui.Text(_log[i], 12f, i == _log.Count - 1 ? Theme.Text : Theme.TextDim);
                line.style.whiteSpace = WhiteSpace.NoWrap;
                line.style.overflow = Overflow.Hidden;
                line.style.textOverflow = TextOverflow.Ellipsis;
                line.style.height = 19f;
                _logBox.Add(line);
            }
        }

        void RefreshStatus()
        {
            int acting = _game.ActingPlayerIndex;
            if (acting >= 0)
            {
                _statusChip.style.backgroundColor = Palette.PlayerColor(acting);
                _statusName.text = _game.Players[acting].Name + BotTag(acting);
            }
            else
            {
                _statusChip.style.backgroundColor = Theme.Accent;
                _statusName.text = "Партия окончена";
            }

            _statusText.text = Instruction(acting);
            Ui.Show(_finalRound, _game.IsFinalRound && _game.Phase != GamePhase.GameOver);
            _flashLabel.text = _flash ?? "";
            Ui.Show(_flashLabel, _flash != null);
            Ui.Show(_passButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction && !_game.HasAnyLegalAction());
        }

        string Instruction(int acting)
        {
            if (acting < 0) return "Итоги — в окне по центру.";
            if (IsBot(acting)) return "Бот думает над ходом…";
            if (NeedsHandoff) return "Передайте компьютер этому игроку.";

            switch (_game.Phase)
            {
                case GamePhase.SetupTickets:
                    return "Выберите стартовые задания на панели справа.";
                case GamePhase.AwaitingAction:
                    return "Возьмите 2 карты, займите маршрут кликом по карте (доступные мерцают) или возьмите задания.";
                case GamePhase.DrawingSecondCard:
                    return "Возьмите вторую карту. Открытый локомотив сейчас брать нельзя.";
                case GamePhase.ChoosingTickets:
                    return "Отметьте справа задания, которые оставляете.";
                default:
                    return "";
            }
        }

        void RefreshHand()
        {
            var viewer = _game.Players[Viewer];
            bool hidden = NeedsHandoff;
            _handTitle.text = hidden
                ? "РУКА СКРЫТА"
                : $"РУКА · {viewer.Name.ToUpperInvariant()} · ВАГОНОВ: {viewer.TrainsLeft}";
            Ui.Show(_handRow, !hidden);

            foreach (var pair in _hand)
            {
                int count = viewer.CardCount(pair.Key);
                pair.Value.Set(pair.Key, count, count == 0);
            }
        }

        void RefreshMap()
        {
            _map.Affordable.Clear();
            if (HumanToAct && _game.Phase == GamePhase.AwaitingAction && _overlayKind == OverlayKind.None)
            {
                foreach (var route in _game.Map.Routes)
                    if (_game.GetClaimOptions(route.Id).Count > 0) _map.Affordable.Add(route.Id);
            }
            _map.Interactive = _overlayKind == OverlayKind.None;
        }

        // ================================================================== Оверлеи

        void RefreshOverlays()
        {
            if (_overlayKind == OverlayKind.ConfirmExit) return;

            if (_game.Phase == GamePhase.GameOver)
            {
                if (_overlayKind != OverlayKind.GameOver)
                {
                    // Даём доиграть последнюю анимацию, потом показываем итоги.
                    _overlayKind = OverlayKind.GameOver;
                    _app.Tweener.After(1.2f, ShowGameOver);
                }
                return;
            }

            if (NeedsHandoff)
            {
                int acting = _game.ActingPlayerIndex;
                if (_overlayKind != OverlayKind.Handoff || _overlayPlayer != acting) ShowHandoff(acting);
                return;
            }

            if (_overlayKind == OverlayKind.Handoff) HideOverlay();
            if (_overlayKind == OverlayKind.Payment && (!HumanToAct || _game.Phase != GamePhase.AwaitingAction)) HideOverlay();
        }

        void ShowOverlay(OverlayKind kind, VisualElement content, Color background)
        {
            _overlay.Clear();
            _overlay.style.backgroundColor = background;
            _overlay.Add(content);
            Ui.Show(_overlay, true);
            Ui.Show(_tooltip, false);
            _overlayKind = kind;
            _map.Interactive = false;
            _map.Affordable.Clear();
        }

        void HideOverlay()
        {
            _overlay.Clear();
            Ui.Show(_overlay, false);
            _overlayKind = OverlayKind.None;
            _overlayPlayer = -1;
            _map.Interactive = true;
            _dirty = true;
        }

        static VisualElement Centered(VisualElement box)
        {
            var holder = Ui.Column();
            Ui.Fill(holder);
            holder.style.alignItems = Align.Center;
            holder.style.justifyContent = Justify.Center;
            holder.Add(box);
            return holder;
        }

        VisualElement DialogBox(float width)
        {
            var box = Ui.Card(28f);
            box.pickingMode = PickingMode.Position;
            box.style.width = width;
            box.style.backgroundColor = Theme.Panel;
            Ui.Radius(box, 14f);
            return box;
        }

        void ShowPayment(Route route, IReadOnlyList<ClaimOption> options, Vector2 mapPosition)
        {
            var box = Ui.Card(14f);
            box.pickingMode = PickingMode.Position;
            box.style.position = Position.Absolute;
            box.style.width = 340f;
            box.style.backgroundColor = Theme.Panel;
            Ui.Border(box, 2f, Theme.Accent);

            box.Add(Ui.Text($"{CityName(route.CityA)} — {CityName(route.CityB)}: чем платить?", 15f, null, true));
            foreach (var option in options)
            {
                var chosen = option;
                var button = Ui.Button(PaymentLabel(route, option), () =>
                {
                    HideOverlay();
                    Act(() => _game.ClaimRoute(route.Id, chosen.Color, chosen.Locomotives));
                }, ButtonKind.Secondary, 14f);
                button.style.marginTop = 8f;
                box.Add(button);
            }
            var cancel = Ui.Button("Отмена", HideOverlay, ButtonKind.Ghost, 14f);
            cancel.style.marginTop = 8f;
            box.Add(cancel);

            float estimatedHeight = 70f + 46f * (options.Count + 1);
            var local = _overlay.WorldToLocal(_map.LocalToWorld(mapPosition));
            var mapBounds = _map.worldBound;
            var mapMax = _overlay.WorldToLocal(new Vector2(mapBounds.xMax, mapBounds.yMax));
            box.style.left = Mathf.Max(10f, Mathf.Min(local.x + 14f, mapMax.x - 350f));
            box.style.top = Mathf.Max(10f, Mathf.Min(local.y + 14f, mapMax.y - estimatedHeight));

            ShowOverlay(OverlayKind.Payment, box, new Color(0f, 0f, 0f, 0.15f));
        }

        void ShowHandoff(int playerIndex)
        {
            var player = _game.Players[playerIndex];
            var box = DialogBox(560f);
            box.style.alignItems = Align.Center;
            Ui.Border(box, 3f, Palette.PlayerColor(playerIndex));

            box.Add(Ui.Text("Передайте компьютер", 14f, Theme.TextDim, true));
            var title = Ui.Text(player.Name, 36f, Palette.PlayerColor(playerIndex), true);
            title.style.marginTop = 6f;
            box.Add(title);
            var text = Ui.Text("Остальные — отвернитесь: сейчас откроются карты и задания этого игрока.", 15f, Theme.Text);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 14f;
            text.style.marginBottom = 22f;
            box.Add(text);
            box.Add(Ui.Button("Показать мои карты", () =>
            {
                _revealedFor = playerIndex;
                HideOverlay();
                OnTurnStarted();
            }, ButtonKind.Primary, 18f));

            ShowOverlay(OverlayKind.Handoff, Centered(box), Theme.ShadeOpaque);
            _overlayPlayer = playerIndex;
        }

        void ShowGameOver()
        {
            if (_game == null || _game.Result == null) return;
            var result = _game.Result;
            var box = DialogBox(860f);

            var title = Ui.Text("Победа: " + string.Join(", ", result.Winners.Select(w => w.PlayerName)), 34f, Theme.Accent, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(title);
            var subtitle = Ui.Text("Очки за маршруты + задания (выполненные плюсом, невыполненные минусом) + бонус за самый длинный путь", 13f, Theme.TextDim);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 20f;
            box.Add(subtitle);

            float[] widths = { 300f, 110f, 170f, 130f, 90f };
            box.Add(ResultRow(new[] { "Игрок", "Маршруты", "Задания", "Путь", "Итого" }, widths, Theme.TextDim, -1, false));

            int place = 1;
            foreach (var s in result.Ranking)
            {
                string tickets = $"{s.TicketPoints:+#;-#;0} ({s.CompletedTickets} из {s.CompletedTickets + s.FailedTickets})";
                string path = s.LongestPath + (s.LongestPathBonus > 0 ? $" (+{s.LongestPathBonus})" : "");
                var cells = new[] { $"{place}. {s.PlayerName}", s.RoutePoints.ToString(), tickets, path, s.Total.ToString() };
                box.Add(ResultRow(cells, widths, Theme.Text, s.PlayerIndex, place == 1));
                place++;
            }

            var buttons = Ui.Row();
            buttons.style.justifyContent = Justify.Center;
            buttons.style.marginTop = 24f;
            var again = Ui.Button("Сыграть ещё", () => _app.StartGame(_setup), ButtonKind.Primary, 17f);
            again.style.marginRight = 12f;
            buttons.Add(again);
            buttons.Add(Ui.Button("В главное меню", _app.ShowMenu, ButtonKind.Secondary, 17f));
            box.Add(buttons);

            ShowOverlay(OverlayKind.GameOver, Centered(box), Theme.Shade);
        }

        VisualElement ResultRow(string[] cells, float[] widths, Color color, int playerIndex, bool winner)
        {
            var row = Ui.Row();
            Ui.Padding(row, 9f, 12f);
            row.style.marginBottom = 6f;
            Ui.Radius(row, 8f);
            if (playerIndex >= 0)
            {
                row.style.backgroundColor = winner ? new Color(0.30f, 0.25f, 0.14f) : Theme.PanelLight;
                row.style.borderLeftWidth = 4f;
                row.style.borderLeftColor = Palette.PlayerColor(playerIndex);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                bool total = i == cells.Length - 1 && playerIndex >= 0;
                var cell = Ui.Text(cells[i], total ? 20f : 15f, total ? Theme.Accent : color, total || i == 0);
                cell.style.width = widths[i];
                cell.style.whiteSpace = WhiteSpace.NoWrap;
                row.Add(cell);
            }
            return row;
        }

        void ShowConfirmExit()
        {
            var box = DialogBox(480f);
            box.Add(Ui.Text("Выйти в главное меню?", 22f, null, true));
            var text = Ui.Text("Текущая партия не сохранится.", 15f, Theme.TextDim);
            text.style.marginTop = 8f;
            text.style.marginBottom = 20f;
            box.Add(text);

            var buttons = Ui.Row();
            var exit = Ui.Button("Выйти", _app.ShowMenu, ButtonKind.Primary, 16f);
            exit.style.marginRight = 10f;
            buttons.Add(exit);
            buttons.Add(Ui.Button("Остаться", HideOverlay, ButtonKind.Secondary, 16f));
            box.Add(buttons);

            ShowOverlay(OverlayKind.ConfirmExit, Centered(box), Theme.Shade);
        }

        void ShowError(string message)
        {
            var box = DialogBox(640f);
            box.Add(Ui.Text("Не удалось начать партию", 22f, Theme.Danger, true));
            var text = Ui.Text(message, 14f, Theme.Text);
            text.style.marginTop = 10f;
            text.style.marginBottom = 20f;
            box.Add(text);
            box.Add(Ui.Button("В главное меню", _app.ShowMenu, ButtonKind.Primary, 16f));
            ShowOverlay(OverlayKind.ConfirmExit, Centered(box), Theme.ShadeOpaque);
        }

        // ================================================================== Мелочи

        string CityName(string cityId) => _game.Map.GetCity(cityId)?.Name ?? cityId;

        static string PaymentLabel(Route route, ClaimOption option)
        {
            if (option.Color == CardColor.Locomotive) return $"Только локомотивы × {option.Locomotives}";
            string main = $"{Palette.CardTitle(option.Color)} × {route.Length - option.Locomotives}";
            return option.Locomotives == 0 ? main : $"{main} + локомотивы × {option.Locomotives}";
        }
    }
}
