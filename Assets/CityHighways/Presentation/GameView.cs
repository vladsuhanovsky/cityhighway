using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
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
        const int LogLines = 60;
        const int ResourceCount = ResourceInfo.Count;

        enum OverlayKind
        {
            None,
            Payment,
            Handoff,
            GameOver,
            ConfirmExit,
            Tutorial,
            LuckChoice,
            TradeCompose,
            TradeRespond,
            Upgrade,
            Influence,
            Auction
        }

        sealed class PlayerRow
        {
            public VisualElement Root;
            public Label Name;
            public Label Stats;
            public CharacterIcon Icon;
            public Label Ability;
            public ProfileBadge Badge;
            public Label Title;
        }

        /// <summary>Оформление игроков из профилей (эмблема, титул, рамка): за одним компьютером — по имени, в сети — от хоста.</summary>
        PlayerLook[] _looks = new PlayerLook[0];
        /// <summary>Сколько влияния ушло в профили после партии — показывается в итогах.</summary>
        List<Profiles.Credit> _profileCredits;

        PlayerLook LookOf(int index) => index >= 0 && index < _looks.Length ? _looks[index] : PlayerLook.None;

        void BuildLooks()
        {
            _looks = new PlayerLook[_game.Players.Count];
            for (int i = 0; i < _looks.Length; i++)
            {
                if (Online)
                {
                    var seat = i < _net.Seats.Count ? _net.Seats[i] : null;
                    _looks[i] = seat == null ? PlayerLook.None : new PlayerLook(seat.emblem, seat.title, seat.frame);
                }
                else
                {
                    bool bot = i < _setup.players.Count && _setup.players[i].isBot;
                    _looks[i] = bot ? PlayerLook.None : Profiles.LookFor(_game.Players[i].Name);
                }
            }
        }

        sealed class Snapshot
        {
            public int[,] Hands;
            public Resource?[] FaceUp;
            public readonly Dictionary<string, int> Owners = new Dictionary<string, int>();
            public readonly HashSet<string> Completed = new HashSet<string>();
            public int Acting;
            public GamePhase Phase;
            public int ContractDeck;
            public int EventNumber;
            public int LuckNumber;
            public readonly Dictionary<string, int> GoalOwners = new Dictionary<string, int>();
        }

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        public VisualElement Root { get; }

        Game _game;
        IBot[] _bots;
        readonly MapView _map;

        // Правая панель
        readonly Label _turnLabel;
        readonly VisualElement _eventCard;
        readonly VisualElement _eventChip;
        readonly Label _eventTitle;
        readonly Label _eventText;
        readonly VisualElement _faceUpRow;
        CardView[] _faceUp = new CardView[0];
        readonly UnityEngine.UIElements.Button _deckButton;
        readonly UnityEngine.UIElements.Button _luckButton;
        readonly UnityEngine.UIElements.Button _tradeButton;
        readonly UnityEngine.UIElements.Button _influenceButton;

        enum SideTab { Contracts, Goals, Log }
        SideTab _sideTab = SideTab.Contracts;
        readonly UnityEngine.UIElements.Button _tabContracts, _tabGoals, _tabLog;
        readonly ScrollView _contractsPage, _goalsPage, _logPage;
        int _seenGoalOwners;   // сколько целей было взято, когда вкладку «Цели» открывали последний раз
        readonly Label _luckOdds;
        readonly VisualElement _goalsBox;
        readonly UnityEngine.UIElements.Button _contractsButton;
        readonly VisualElement _playersBox;
        PlayerRow[] _playerRows = new PlayerRow[0];
        readonly VisualElement _contractsBox;
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
        readonly Dictionary<Resource, CardView> _hand = new Dictionary<Resource, CardView>();

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

        // Горячий стул: таймер хода и «пока вас не было».
        float _turnTimeLeft;
        int _timerPlayer = -1;
        int _timerTurn = -1;
        GamePhase _timerPhase;
        bool _timerExpired;
        SmartBot _timeoutBot;
        int _logTotal;
        int[] _seenLog;
        bool _dirty = true;
        readonly HashSet<string> _selectedContracts = new HashSet<string>();
        /// <summary>Закреплённая подсветка: игрок → id контракта. Держится, пока игрок сам её не снимет.</summary>
        readonly Dictionary<int, string> _pinnedContracts = new Dictionary<int, string>();
        string _selectionKey;
        Snapshot _snapshot;
        float _playSeconds;

        public const string TutorialPrefsKey = "CityHighways.TutorialDone.v1";

        /// <summary>Сетевая сессия; null — игра за одним компьютером.</summary>
        readonly NetSession _net;
        bool Online => _net != null;

        public GameView(CityHighwaysApp app, GameSetup setup, NetSession net = null)
        {
            _app = app;
            _setup = setup;
            _net = net;

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.flexDirection = FlexDirection.Row;

            // ---------- Левая часть: карта и нижняя панель
            var left = Ui.Column();
            left.style.flexGrow = 1f;
            Root.Add(left);

            _map = new MapView();
            _map.Hovered += OnMapHover;
            _map.RoadClicked += OnRoadClicked;
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
            foreach (var color in ResourceInfo.Building.Concat(new[] { Resource.Machinery }))
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

            // Шапка: номер хода и меню.
            var header = Ui.Row();
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8f;
            _turnLabel = Ui.Text("", 20f, null, true);
            header.Add(_turnLabel);
            header.Add(Ui.Button("Меню", ShowConfirmExit, ButtonKind.Ghost, 13f));
            side.Add(header);

            // Событие круга — компактная строка: цветная точка, название, описание мелко.
            _eventCard = Ui.Column();
            _eventCard.style.marginBottom = 10f;
            Ui.Padding(_eventCard, 6f, 10f);
            Ui.Radius(_eventCard, 8f);
            _eventCard.style.backgroundColor = Theme.PanelLight;
            var eventHeader = Ui.Row();
            eventHeader.style.alignItems = Align.Center;
            _eventChip = Ui.Box();
            _eventChip.style.width = 10f;
            _eventChip.style.height = 10f;
            _eventChip.style.marginRight = 8f;
            Ui.Radius(_eventChip, 5f);
            eventHeader.Add(_eventChip);
            _eventTitle = Ui.Text("", 14f, null, true);
            eventHeader.Add(_eventTitle);
            _eventCard.Add(eventHeader);
            _eventText = Ui.Text("", 11f, Theme.TextDim);
            _eventText.style.marginTop = 2f;
            _eventText.style.whiteSpace = WhiteSpace.Normal;
            _eventCard.Add(_eventText);
            Ui.Show(_eventCard, false);
            side.Add(_eventCard);

            // Игроки — по строке на каждого; способность видна только у того, кто ходит.
            _playersBox = Ui.Column();
            _playersBox.style.marginBottom = 10f;
            side.Add(_playersBox);

            // Взять карты: открытый ряд и одна строка действий.
            _faceUpRow = Ui.Row();
            _faceUpRow.style.justifyContent = Justify.SpaceBetween;
            side.Add(_faceUpRow);

            var drawRow = Ui.Row();
            drawRow.style.marginTop = 8f;
            UnityEngine.UIElements.Button ActionButton(string text, Action onClick, bool last = false)
            {
                var button = Ui.Button(text, onClick, ButtonKind.Secondary, 13f);
                button.style.flexGrow = 1f;
                button.style.flexBasis = 0f;
                button.style.paddingLeft = 4f;
                button.style.paddingRight = 4f;
                if (!last) button.style.marginRight = 6f;
                drawRow.Add(button);
                return button;
            }
            _deckButton = ActionButton("Колода", () => Act(DrawFromDeck));
            _luckButton = ActionButton("Удача", () => Act(() => _game.DrawLuckCard()));
            _contractsButton = ActionButton("Контракты", () => Act(() => _game.DrawContracts()));
            _influenceButton = ActionButton("Влияние", ShowInfluenceMenu);
            _tradeButton = ActionButton("Сделка", ShowTradeComposer, true);
            side.Add(drawRow);
            _luckOdds = Ui.Text("", 11f, Theme.TextDim);
            _luckOdds.style.marginTop = 4f;
            side.Add(_luckOdds);

            // Вкладки: контракты, цели мэрии, журнал — видна одна, ей достаётся всё оставшееся место.
            var tabs = Ui.Row();
            tabs.style.marginTop = 12f;
            tabs.style.borderBottomWidth = 1f;
            tabs.style.borderBottomColor = Theme.Line;
            side.Add(tabs);
            UnityEngine.UIElements.Button Tab(SideTab tab)
            {
                var button = Ui.Button("", () => SelectTab(tab), ButtonKind.Ghost, 13f);
                button.style.flexGrow = 1f;
                button.style.flexBasis = 0f;
                button.style.borderBottomLeftRadius = 0f;
                button.style.borderBottomRightRadius = 0f;
                tabs.Add(button);
                return button;
            }
            _tabContracts = Tab(SideTab.Contracts);
            _tabGoals = Tab(SideTab.Goals);
            _tabLog = Tab(SideTab.Log);

            var tabHost = Ui.Column();
            tabHost.style.flexGrow = 1f;
            tabHost.style.flexShrink = 1f;
            tabHost.style.overflow = Overflow.Hidden;
            tabHost.style.marginTop = 8f;
            side.Add(tabHost);

            ScrollView Page(VisualElement content)
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.flexGrow = 1f;
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.Add(content);
                tabHost.Add(scroll);
                return scroll;
            }
            _contractsBox = Ui.Column();
            _contractsPage = Page(_contractsBox);
            _goalsBox = Ui.Column();
            _goalsPage = Page(_goalsBox);
            _logBox = Ui.Column();
            _logPage = Page(_logBox);
            SelectTab(SideTab.Contracts);

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
            Game game;
            try
            {
                var map = CityHighwaysApp.LoadMap(_setup.mapResourcePath);
                int seed = UnityEngine.Random.Range(0, int.MaxValue);
                var characters = _setup.ResolveCharacters(new System.Random(seed ^ 0x5bd1e995));
                game = new Game(map, _setup.PlayerNames(), new GameConfig { RoadPiecesOverride = _setup.roadPieces }, seed, characters);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError(e.Message);
                return;
            }
            Begin(game, 0f, $"Новая партия на карте «{game.Map.Name}».");
        }

        /// <summary>Сетевая партия: Game уже собран сессией (у хоста — новый, у клиента — по данным хоста).</summary>
        public void StartOnline()
        {
            _net.SeatsChanged += () => _dirty = true;
            _net.Notice += text => Toast(text, Theme.Accent);
            _net.Resynced += () =>
            {
                if (_game != null)
                {
                    _game.Log -= AddLog;
                    _game.StateChanged -= OnStateChanged;
                }
                Begin(_net.Game, _playSeconds, "Партия синхронизирована с хостом.");
            };
            _net.Closed += text =>
            {
                if (_game != null && _game.Phase == GamePhase.GameOver) return;
                ShowConnectionLost(text);
            };
            string role = _net.IsHost ? "вы хост" : "хост — " + _net.Seats[0].name;
            Begin(_net.Game, 0f, $"Сетевая партия ({_net.Transport.Kind}, {role}) на карте «{_net.Game.Map.Name}».");
        }

        void ShowConnectionLost(string text)
        {
            var box = DialogBox(560f);
            box.Add(Ui.Text("Сетевая партия прервана", 22f, Theme.Danger, true));
            var label = Ui.Text(text, 15f, Theme.Text);
            label.style.marginTop = 10f;
            label.style.marginBottom = 20f;
            box.Add(label);
            box.Add(Ui.Button("В главное меню", _app.ShowMenu, ButtonKind.Primary, 16f));
            ShowOverlay(OverlayKind.ConfirmExit, Centered(box), Theme.ShadeOpaque);
        }

        /// <summary>Продолжить сохранённую партию.</summary>
        public void ContinueGame(SaveFile file)
        {
            Game game;
            try
            {
                var map = CityHighwaysApp.LoadMap(_setup.mapResourcePath);
                game = Game.Load(map, file.game); // по истории команд — точь-в-точь, иначе по снимку
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SaveSystem.Delete();
                ShowError("Сохранение повреждено или не подходит к карте, поэтому удалено.\n" + e.Message);
                return;
            }
            Begin(game, file.playSeconds, $"Партия продолжена (сохранена {file.savedAt}).");
        }

        void Begin(Game game, float playSeconds, string firstLogLine)
        {
            _game = game;
            _playSeconds = playSeconds;
            _game.Log += AddLog;
            _lastTradeShown = _game.TradeNumber;
            _lastAuctionShown = _game.AuctionNumber;
            _lastWhisperShown = _game.WhisperNumber;
            _lastAccusationShown = _game.AccusationNumber;
            _game.StateChanged += OnStateChanged;

            _bots = new IBot[_game.Players.Count];
            for (int i = 0; i < _bots.Length; i++)
                if (IsRealBot(i)) _bots[i] = _setup.players[i].CreateBot(UnityEngine.Random.Range(0, int.MaxValue));

            _map.SetGame(_game);
            BuildFaceUpRow();
            BuildPlayerRows();

            _log.Clear();
            AddLog(firstLogLine);
            _revealedFor = -1;
            _selectionKey = null;
            _selectedContracts.Clear();
            _pinnedContracts.Clear();
            _flash = null;
            HideOverlay();
            _snapshot = TakeSnapshot();
            _dirty = true;
            if (!Online) SaveSystem.Save(_setup, _game, _playSeconds);

            bool showTutorial = !Online && HumanCount > 0 && PlayerPrefs.GetInt(TutorialPrefsKey, 0) == 0;
            if (showTutorial) _app.Tweener.After(0.5f, () => ShowTutorialStep(0));
            else _app.Tweener.After(0.4f, OnTurnStarted);
        }

        public void Tick(float deltaTime)
        {
            if (_game == null) return;

            _map.PulseTime += deltaTime;
            _map.Repaint();
            if (_game.Phase != GamePhase.GameOver) _playSeconds += deltaTime;

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

            TickTurnTimer(deltaTime);
            TickBots(deltaTime);
        }

        bool TimerEnabled => _setup.turnSeconds > 0;
        /// <summary>Клиент сетевой партии отправил ход и ждёт, пока хост его примет.</summary>
        bool AwaitingHost => Online && _net.AwaitingHost;

        /// <summary>
        /// Таймер хода человека. Идёт, только когда человек может действовать (не во время передачи хода,
        /// анимаций и обучения). Новый отсчёт — с каждым новым ходом или сменой игрока. Время вышло —
        /// ход доигрывает умный бот.
        /// </summary>
        void TickTurnTimer(float deltaTime)
        {
            if (!TimerEnabled || _game.Phase == GamePhase.GameOver) return;
            int acting = _game.ActingPlayerIndex;
            if (acting != _timerPlayer || _game.TurnNumber != _timerTurn
                || (_timerPhase == GamePhase.SetupContracts) != (_game.Phase == GamePhase.SetupContracts))
            {
                _timerPlayer = acting;
                _timerTurn = _game.TurnNumber;
                _timerPhase = _game.Phase;
                _turnTimeLeft = _setup.turnSeconds;
                _timerExpired = false;
            }
            if (!HumanToAct || _timerExpired) return;
            if (_overlayKind != OverlayKind.None && _overlayKind != OverlayKind.Payment && _overlayKind != OverlayKind.LuckChoice) return;
            if (_app.Tweener.BlockingCount > 0) return;

            float before = _turnTimeLeft;
            _turnTimeLeft -= deltaTime;
            if (Mathf.CeilToInt(before) != Mathf.CeilToInt(_turnTimeLeft)) _dirty = true;
            if (before > 10f && _turnTimeLeft <= 10f) _app.Sound.Play(Sfx.Bell, 0.4f);
            if (_turnTimeLeft > 0f) return;

            _timerExpired = true;
            string name = _game.Players[acting].Name;
            if (_overlayKind == OverlayKind.Payment || _overlayKind == OverlayKind.LuckChoice) HideOverlay();
            _timeoutBot = _timeoutBot ?? new SmartBot(UnityEngine.Random.Range(0, int.MaxValue));
            int turn = _timerTurn;
            // Бот доигрывает ход этого игрока: до смены игрока или хода (не больше нескольких действий).
            // В сетевой партии — на копии у клиента, с отправкой хосту (Perform), у хоста — сразу.
            Perform(game =>
            {
                var last = ActionResult.Ok();
                for (int i = 0; i < 6 && game.ActingPlayerIndex == acting && game.TurnNumber == turn
                                     && game.Phase != GamePhase.GameOver; i++)
                {
                    last = _timeoutBot.Act(game);
                    if (!last.Success) break;
                }
                return last;
            });
            Toast($"Время вышло — за {name} походил бот", Theme.Danger);
            _dirty = true;
        }

        string TimerText()
        {
            if (!TimerEnabled || !HumanToAct || _timerExpired) return "";
            int seconds = Mathf.Max(0, Mathf.CeilToInt(_turnTimeLeft));
            return $"  ·  осталось {seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>Отметить, что игрок видел журнал до этого места (пока его карты открыты).</summary>
        void MarkLogSeen()
        {
            if (_seenLog == null || _seenLog.Length != _game.Players.Count) _seenLog = new int[_game.Players.Count];
            int acting = _game.ActingPlayerIndex;
            if (acting >= 0 && HumanToAct) _seenLog[acting] = _logTotal;
        }

        /// <summary>Записи журнала, которых игрок ещё не видел (ходы соперников с его прошлого хода).</summary>
        List<string> UnseenLog(int playerIndex)
        {
            if (_seenLog == null || _seenLog.Length != _game.Players.Count) _seenLog = new int[_game.Players.Count];
            int unseen = Mathf.Clamp(_logTotal - _seenLog[playerIndex], 0, _log.Count);
            return _log.Skip(_log.Count - unseen).ToList();
        }

        void TickBots(float deltaTime)
        {
            if (_game.Phase == GamePhase.GameOver
                || _overlayKind == OverlayKind.ConfirmExit
                || _overlayKind == OverlayKind.Tutorial) return;

            // В сетевой партии ботами управляет только хост; клиенты видят их ходы командами.
            if (Online && !_net.IsHost) return;
            int acting = _game.ActingPlayerIndex;
            if (!IsRealBot(acting))
            {
                _botTimer = 0f;
                return;
            }
            // Игрок отключился посреди партии — за его место играет новый бот.
            if (_bots[acting] == null) _bots[acting] = new SmartBot(UnityEngine.Random.Range(0, int.MaxValue));
            // Боты ждут, пока доиграют анимации, — иначе за их ходами не уследить.
            if (_app.Tweener.BlockingCount > 0) return;

            _botTimer += deltaTime;
            if (_botTimer < BotDelay) return;
            _botTimer = 0f;

            var result = _bots[acting].Act(_game);
            if (!result.Success) Flash("Бот: " + result.Error);
        }

        // ================================================================== Кто ходит и кто смотрит

        /// <summary>
        /// «Не человек за этим экраном». За одним компьютером — бот; в сетевой партии — любое чужое место
        /// (бот или игрок на другом компьютере): его карт не показываем и за него не ходим.
        /// </summary>
        bool IsBot(int index)
        {
            if (Online) return index != _net.LocalSeat;
            return index >= 0 && index < _setup.players.Count && _setup.players[index].isBot;
        }

        /// <summary>Настоящий бот (в сетевой партии — и место отключившегося игрока).</summary>
        bool IsRealBot(int index)
        {
            if (Online) return _net.IsBotSeat(index);
            return index >= 0 && index < _setup.players.Count && _setup.players[index].isBot;
        }

        int HumanCount => Online ? 1 : _setup.players.Count(p => !p.isBot);

        bool NeedsHandoff
        {
            get
            {
                int acting = _game.ActingPlayerIndex;
                return !Online && _setup.hideCardsBetweenTurns && HumanCount > 1 && acting >= 0 && !IsBot(acting) && _revealedFor != acting;
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
                return acting >= 0 && !IsBot(acting) && !NeedsHandoff && !AwaitingHost
                       && _overlayKind != OverlayKind.ConfirmExit && _overlayKind != OverlayKind.Tutorial;
            }
        }

        string BotTag(int index)
        {
            if (Online && !IsRealBot(index)) return index == _net.LocalSeat ? " · вы" : " · по сети";
            if (!IsRealBot(index)) return "";
            return index < _setup.players.Count && _setup.players[index].botLevel == BotLevel.Random ? " · случайный бот" : " · бот";
        }

        string WhoActsText(int acting)
        {
            if (AwaitingHost) return "Ход отправлен хосту…";
            if (IsRealBot(acting)) return "Сейчас ходит бот.";
            if (Online) return "Сейчас ходит " + _game.Players[acting].Name + ".";
            return "Сейчас нельзя.";
        }

        /// <summary>
        /// Выполнить действие человека за этим экраном. За одним компьютером — сразу. В сетевой партии
        /// действие уходит в сессию: хост выполняет его на настоящей партии, клиент — на копии (_game на
        /// время вызова подменяется копией, поэтому лямбды вида () => _game.BuildRoad(...) работают как есть).
        /// </summary>
        ActionResult Perform(Func<Game, ActionResult> action)
        {
            if (!Online) return action(_game);
            var real = _game;
            return _net.Perform(copy =>
            {
                _game = copy;
                try
                {
                    return action(copy);
                }
                finally
                {
                    _game = real;
                }
            });
        }

        // ================================================================== Действия игрока

        void Act(Func<ActionResult> action)
        {
            if (_game == null) return;
            if (!HumanToAct)
            {
                Flash(WhoActsText(_game.ActingPlayerIndex));
                return;
            }
            var result = Perform(_ => action());
            if (!result.Success) Flash(result.Error);
            else if (AwaitingHost) _dirty = true;
        }

        void OnRoadClicked(Road route, Vector2 position)
        {
            if (_overlayKind != OverlayKind.None) return;
            if (!HumanToAct)
            {
                Flash(WhoActsText(_game.ActingPlayerIndex));
                return;
            }

            switch (_game.Phase)
            {
                case GamePhase.SetupContracts:
                    Flash("Сначала выберите стартовые контракты.");
                    return;
                case GamePhase.DrawingSecondCard:
                    Flash("Сначала возьмите вторую карту.");
                    return;
                case GamePhase.ChoosingContracts:
                    Flash("Сначала выберите контракты.");
                    return;
                case GamePhase.ChoosingLuck:
                    Flash("Сначала выберите карту удачи.");
                    return;
                case GamePhase.RespondingTrade:
                    Flash("Сначала ответьте на сделку.");
                    return;
                case GamePhase.Bidding:
                    Flash("Сначала сделайте ставку на аукционе мэрии.");
                    return;
            }

            // Своя дорога — предлагаем улучшить её до магистрали.
            if (_game.GetRoadOwner(route.Id) == _game.CurrentPlayerIndex)
            {
                var blocker = _game.GetUpgradeBlocker(route.Id);
                if (blocker != null)
                {
                    Flash(blocker);
                    return;
                }
                ShowUpgrade(route, position);
                return;
            }

            var options = _game.GetPaymentOptions(route.Id);
            if (_game.CanBuildRoadFree(route.Id))
            {
                // Прорабу всегда предлагаем выбор: бесплатно или картами.
                ShowPayment(route, options, position, true);
                return;
            }
            if (options.Count == 0)
            {
                Flash(_game.GetBuildBlocker(route.Id) ?? "Эту дорогу сейчас построить нельзя.");
                return;
            }
            if (options.Count == 1)
            {
                var only = options[0];
                Act(() => _game.BuildRoad(route.Id, only.Resource, only.Machinery));
                return;
            }
            ShowPayment(route, options, position);
        }

        void OnMapHover(Road route, Vector2 position)
        {
            if (route == null || _overlayKind != OverlayKind.None || _game == null)
            {
                Ui.Show(_tooltip, false);
                return;
            }

            _tooltipText.text = RoadTooltip(route);
            var local = _fx.WorldToLocal(_map.LocalToWorld(position));
            var mapBounds = _map.worldBound;
            var mapMax = _fx.WorldToLocal(new Vector2(mapBounds.xMax, mapBounds.yMax));
            _tooltip.style.left = Mathf.Min(local.x + 18f, mapMax.x - 310f);
            _tooltip.style.top = Mathf.Min(local.y + 18f, mapMax.y - 110f);
            Ui.Show(_tooltip, true);
        }

        string RoadTooltip(Road road)
        {
            string kind = ResourceInfo.RoadKind(road.Resource);
            string resource = road.Resource.HasValue ? Palette.CardTitle(road.Resource.Value) : "любой один ресурс";
            string text = $"<b>{LocationName(road.A)} — {LocationName(road.B)}</b>\n" +
                          $"{char.ToUpper(kind[0]) + kind.Substring(1)} · длина {road.Length} · {resource} · {_game.Config.PointsForLength(road.Length)} очк.";

            int? owner = _game.GetRoadOwner(road.Id);
            if (owner.HasValue)
            {
                text += "\nПостроил: " + _game.Players[owner.Value].Name;
                if (_game.IsRushHourRoad(road.Id))
                    text += $"\n<color=#FF9A33>Загруженная магистраль (час пик): проезд ×{_game.Config.RushHourTollMultiplier}</color>";
                int viewer = Viewer;
                if (owner.Value != viewer && !IsBot(viewer))
                    text += $"\nПроезд для вас: −{_game.TollFor(road.Id)} очк. владельцу";
                return text;
            }
            if (_game.IsUnderRepair(road.Id)) text += "\n<color=#FF6B6B>Ремонт: закрыт до конца круга</color>";
            if (_game.IsParadeClosed(road.Id)) text += "\n<color=#CC80FF>Парад: закрыт до конца круга</color>";
            if (road.Resource == Resource.Gravel && _game.CurrentEvent?.Kind == CityEventKind.Snow)
                text += $"\n<color=#D9EBFF>Снегопад: +{_game.Config.SnowExtraCost} карта к цене</color>";
            if (_game.CurrentEvent?.Kind == CityEventKind.Investor)
                text += $"\n<color=#FFD94D>Инвестор: +{_game.Config.InvestorBonus} очка за постройку</color>";
            int cost = _game.BuildCost(road);
            if (cost < road.Length) text += $"\n<color=#7FC8FF>Субсидия мэрии: сейчас стоит {cost} карт(ы)</color>";
            if (_game.CurrentEvent != null && _game.CurrentEvent.Kind == CityEventKind.RushHour)
                text += "\n<color=#FF9A33>Час пик: построенная сейчас дорога станет платной ×2</color>";
            if (HumanToAct && _game.Phase == GamePhase.AwaitingAction)
                return text + "\n" + (_game.GetBuildBlocker(road.Id) ?? "<color=#7FD88A>Кликните, чтобы построить</color>");
            return text;
        }

        // ================================================================== Анимации по изменениям

        void OnStateChanged()
        {
            var now = TakeSnapshot();
            bool finished = now.Phase == GamePhase.GameOver && (_snapshot == null || _snapshot.Phase != GamePhase.GameOver);
            if (_snapshot != null) AnimateChanges(_snapshot, now);
            _snapshot = now;
            _dirty = true;

            // Автосохранение после каждого хода; по окончании партии — запись статистики.
            if (finished)
            {
                if (!Online) SaveSystem.Delete();
                StatsLog.Append(_game, _setup, _playSeconds);
                _profileCredits = Profiles.CreditAfterGame(_game, _setup, Online ? _net.LocalSeat : (int?)null);
            }
            else if (now.Phase != GamePhase.GameOver && !Online)
            {
                SaveSystem.Save(_setup, _game, _playSeconds);
            }
        }

        Snapshot TakeSnapshot()
        {
            var s = new Snapshot
            {
                Hands = new int[_game.Players.Count, ResourceCount],
                FaceUp = _game.FaceUpCards.ToArray(),
                Acting = _game.ActingPlayerIndex,
                Phase = _game.Phase,
                ContractDeck = _game.ContractDeckCount,
                EventNumber = _game.EventNumber,
                LuckNumber = _game.LuckNumber
            };
            foreach (var goal in _game.ActiveGoals)
            {
                var goalOwner = _game.GoalOwner(goal.Id);
                if (goalOwner.HasValue) s.GoalOwners[goal.Id] = goalOwner.Value;
            }

            foreach (var player in _game.Players)
            {
                for (int c = 0; c < ResourceCount; c++) s.Hands[player.Index, c] = player.CardCount((Resource)c);
                foreach (var contract in player.Contracts)
                    if (_game.CheckContract(player.Index, contract).Completed) s.Completed.Add(player.Index + ":" + contract.Id);
            }

            foreach (var route in _game.Map.Roads)
            {
                var owner = _game.GetRoadOwner(route.Id);
                if (owner.HasValue) s.Owners[route.Id] = owner.Value;
            }
            return s;
        }

        void AnimateChanges(Snapshot before, Snapshot now)
        {
            // Новые дороги — укладываем участки по одному.
            float claimDuration = 0f;
            foreach (var pair in now.Owners)
            {
                if (before.Owners.ContainsKey(pair.Key)) continue;
                var route = _game.Map.GetRoad(pair.Key);
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
                for (int c = 0; c < ResourceCount; c++)
                {
                    int gained = now.Hands[p, c] - before.Hands[p, c];
                    for (int k = 0; k < gained; k++)
                    {
                        int slot = -1;
                        foreach (int index in changedSlots)
                        {
                            if (before.FaceUp[index] == (Resource)c)
                            {
                                slot = index;
                                break;
                            }
                        }
                        if (slot >= 0) changedSlots.Remove(slot);
                        FlyCard(p, (Resource)c, slot, flight++ * 0.15f);
                    }
                }
            }

            if (now.ContractDeck < before.ContractDeck && now.EventNumber == before.EventNumber) _app.Sound.Play(Sfx.Swish);

            // Новое городское событие.
            if (now.EventNumber > before.EventNumber && _game.CurrentEvent != null)
            {
                var cityEvent = _game.CurrentEvent;
                _app.Tweener.After(claimDuration + 0.3f, () =>
                {
                    // Спокойная подача: тост, тихий звук, мягкое свечение краёв карты и плавная подсветка карточки события.
                    var color = EventColor(cityEvent.Kind);
                    Toast("Событие: " + cityEvent.Title, color);
                    _app.Sound.Play(cityEvent.Kind == CityEventKind.NewDistrict ? Sfx.Fanfare : Sfx.Swish, 0.3f);
                    _map.Flash(color);
                    _map.Repaint();
                    GlowEventCard(color);
                });
            }

            // Взятки: шёпот в мэрии и итог обвинения — видно всем.
            if (_game.WhisperNumber > _lastWhisperShown)
            {
                _lastWhisperShown = _game.WhisperNumber;
                Toast("В мэрии кто-то шепчется…", Theme.TextDim);
            }
            if (_game.AccusationNumber > _lastAccusationShown && _game.LastAccusation != null)
            {
                _lastAccusationShown = _game.AccusationNumber;
                var a = _game.LastAccusation;
                string accuser = _game.Players[a.Accuser].Name, accused = _game.Players[a.Accused].Name;
                Toast(a.Correct
                        ? $"{accuser} разоблачает {accused}: взятка! −{_game.Config.BribeFine * a.Exposed}"
                        : $"{accuser} ошибся: {accused} чист. −{_game.Config.FalseAccusationFine} обвинителю",
                    a.Correct ? Theme.Danger : Theme.TextDim);
                _app.Sound.Play(a.Correct ? Sfx.Horn : Sfx.Swish, 0.35f);
            }

            // Аукцион мэрии: начало и итог.
            if (_game.AuctionNumber > _lastAuctionShown)
            {
                _lastAuctionShown = _game.AuctionNumber;
                if (_game.Phase == GamePhase.Bidding && _game.AuctionContract != null)
                {
                    Toast("Аукцион мэрии: " + ContractTitle(_game.AuctionContract), Theme.Accent);
                    _app.Sound.Play(Sfx.Fanfare, 0.3f);
                }
                else if (_game.LastAuction != null)
                {
                    var auction = _game.LastAuction;
                    Toast(auction.Winner >= 0
                        ? $"Аукцион: {_game.Players[auction.Winner].Name} забирает контракт за {auction.Price} вл."
                        : "Аукцион: ставок нет, контракт ушёл под колоду", auction.Winner >= 0 ? Theme.Success : Theme.TextDim);
                }
            }

            // Итог сделки.
            if (_game.TradeNumber > _lastTradeShown && _game.LastTrade != null)
            {
                _lastTradeShown = _game.TradeNumber;
                var trade = _game.LastTrade;
                string answer = _game.LastTradeAccepted ? "согласился" : "отказался";
                Toast($"Сделка: {_game.Players[trade.To].Name} {answer}", _game.LastTradeAccepted ? Theme.Success : Theme.TextDim);
            }

            // Открытая карта удачи.
            if (now.LuckNumber > before.LuckNumber && _game.LastLuckCard.HasValue)
            {
                ShowLuckCard(_game.LastLuckCard.Value, _game.LastLuckPlayer, _game.LastLuckResult);
            }

            // Цели мэрии, выполненные первым.
            foreach (var pair in now.GoalOwners)
            {
                if (before.GoalOwners.ContainsKey(pair.Key)) continue;
                var goal = _game.Map.GetGoal(pair.Key);
                string who = _game.Players[pair.Value].Name;
                _app.Tweener.After(claimDuration + 0.15f, () =>
                {
                    Toast($"Цель мэрии «{goal.Name}»: {who} первым, +{goal.Points}", Theme.Accent);
                    // Салют над серединой карты — цветом компании, взявшей цель.
                    _map.Fireworks(_map.contentRect.center + new Vector2(-80f, -40f), Palette.PlayerColor(pair.Value), 48);
                    _map.Fireworks(_map.contentRect.center + new Vector2(90f, 10f), Theme.Accent, 40);
                    _app.Sound.Play(Sfx.Fanfare, 0.7f);
                });
            }

            // Выполненные контракты того, кто сидит за экраном.
            foreach (var key in now.Completed)
            {
                if (before.Completed.Contains(key)) continue;
                int separator = key.IndexOf(':');
                int playerIndex = int.Parse(key.Substring(0, separator));
                string contractId = key.Substring(separator + 1);
                if (playerIndex != Viewer || IsBot(playerIndex)) continue;

                var contract = _game.Players[playerIndex].Contracts.FirstOrDefault(c => c.Id == contractId);
                if (contract == null) continue;
                int contractToll = _game.CheckContract(playerIndex, contract).Toll;
                _app.Tweener.After(claimDuration, () =>
                {
                    string toll = contractToll > 0 ? $"  (проезд −{contractToll})" : "";
                    Toast($"Контракт выполнен: {ContractTitle(contract)}  +{contract.Points}{toll}", Theme.Success);
                    // Салют над районами контракта.
                    foreach (var district in contract.Districts)
                        _map.Fireworks(_map.DistrictScreenCenter(district), Palette.PlayerColor(playerIndex), 30);
                    _app.Sound.Play(Sfx.Ticket);
                });
            }

            if (now.Phase == GamePhase.GameOver && before.Phase != GamePhase.GameOver)
            {
                _app.Tweener.After(claimDuration + 0.4f, () => _app.Sound.Play(Sfx.Fanfare));
                return;
            }

            bool turnChanged = now.Acting != before.Acting
                               || (before.Phase == GamePhase.SetupContracts && now.Phase != GamePhase.SetupContracts);
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
            if (_game.Phase == GamePhase.SetupContracts) Toast($"{name}, выберите стартовые контракты", Theme.Accent);
            else Toast(HumanCount > 1 ? "Ходит " + name : "Ваш ход", Theme.Accent);
        }

        static float ClaimDuration(Road road) => road == null ? 0f : 0.12f * road.Length + 0.1f;

        void AnimateClaim(Road route)
        {
            if (route == null) return;
            _map.BuildProgress[route.Id] = 0f;
            int segments = route.Length;
            int played = 0;

            _app.Tweener.Add(ClaimDuration(route), k =>
            {
                _map.BuildProgress[route.Id] = k;
                int shown = Mathf.FloorToInt(k * segments + 0.001f);
                if (shown > played)
                {
                    played = shown;
                    _app.Sound.Play(Sfx.Clack, 0.8f);
                }
            }, () =>
            {
                _map.BuildProgress.Remove(route.Id);
                _app.Sound.Play(Sfx.Horn, 0.6f);
            });
        }

        void FlyCard(int playerIndex, Resource color, int faceUpSlot, float delay)
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

        public static Color LuckColor(bool buff) => buff ? Theme.Success : Theme.Danger;

        /// <summary>
        /// Карта удачи: вылетает рубашкой из кнопки «Удача» в центр, переворачивается, показывает эффект
        /// и итог, затем улетает обратно в колоду. Пока карта на экране, боты ждут (анимация блокирующая).
        /// </summary>
        void ShowLuckCard(LuckCardKind kind, int playerIndex, string result)
        {
            if (!IsLaidOut(_luckButton) || !IsLaidOut(_fx)) return;
            bool buff = LuckInfo.IsBuff(kind);
            var color = LuckColor(buff);
            const float width = 230f;
            const float height = 310f;

            var deck = _fx.WorldToLocal(_luckButton.worldBound.center) - new Vector2(width / 2f, height / 2f);
            var center = _fx.WorldToLocal(_map.worldBound.center) - new Vector2(width / 2f, height / 2f);

            var card = Ui.Card(18f);
            card.pickingMode = PickingMode.Ignore;
            card.style.position = Position.Absolute;
            card.style.width = width;
            card.style.height = height;
            card.style.left = deck.x;
            card.style.top = deck.y;
            card.style.opacity = 0f;
            card.style.justifyContent = Justify.Center;
            card.style.alignItems = Align.Center;
            Ui.Radius(card, 16f);

            // Рубашка.
            var back = Ui.Column();
            back.style.alignItems = Align.Center;
            back.Add(Ui.Text("?", 96f, Theme.Accent, true));
            back.Add(Ui.Text("УДАЧА", 20f, Theme.Accent, true));
            card.Add(back);

            // Лицо.
            var face = Ui.Column();
            face.style.alignItems = Align.Center;
            face.Add(Ui.Text(buff ? "БАФ" : "ДЕБАФ", 14f, color, true));
            var title = Ui.Text(LuckInfo.Title(kind), 24f, Theme.Text, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginTop = 10f;
            face.Add(title);
            var effect = Ui.Text(LuckInfo.Describe(kind, _game.Config), 13f, Theme.TextDim);
            effect.style.unityTextAlign = TextAnchor.MiddleCenter;
            effect.style.whiteSpace = WhiteSpace.Normal;
            effect.style.marginTop = 12f;
            face.Add(effect);
            var outcome = Ui.Text(_game.Players[playerIndex].Name + ": " + result, 15f, color, true);
            outcome.style.unityTextAlign = TextAnchor.MiddleCenter;
            outcome.style.whiteSpace = WhiteSpace.Normal;
            outcome.style.marginTop = 18f;
            face.Add(outcome);
            Ui.Show(face, false);
            card.Add(face);

            void SetLook(bool showFace)
            {
                Ui.Show(back, !showFace);
                Ui.Show(face, showFace);
                card.style.backgroundColor = showFace ? Theme.Panel : Theme.PanelLight;
                Ui.Border(card, 3f, showFace ? color : Theme.Accent);
            }
            void SetFlip(float scaleX) => card.style.scale = new Scale(new Vector3(Mathf.Max(0.001f, scaleX), 1f, 1f));
            SetLook(false);
            _fx.Add(card);

            bool bot = IsBot(playerIndex);
            float hold = bot ? 1.0f : 1.8f;

            // 1. Из колоды в центр.
            _app.Sound.Play(Sfx.Swish, 0.6f);
            _app.Tweener.Add(0.35f, k =>
            {
                float e = Ui.EaseOutCubic(k);
                var p = Vector2.Lerp(deck, center, e);
                card.style.left = p.x;
                card.style.top = p.y;
                card.style.opacity = Mathf.Clamp01(k * 3f);
                SetFlip(0.4f + 0.6f * e);
            });
            // 2. Переворот: рубашка сжимается, лицо раскрывается.
            _app.Tweener.Add(0.4f, k =>
            {
                if (k < 0.5f) SetFlip(1f - k * 2f);
                else
                {
                    SetLook(true);
                    SetFlip((k - 0.5f) * 2f);
                }
            }, () =>
            {
                SetFlip(1f);
                _app.Sound.Play(buff ? Sfx.Bell : Sfx.Error, 0.7f);
            }, 0.35f);
            // 3. Пауза и обратно в колоду.
            _app.Tweener.Add(0.4f, k =>
            {
                float e = Ui.EaseInOutCubic(k);
                var p = Vector2.Lerp(center, deck, e);
                card.style.left = p.x;
                card.style.top = p.y;
                card.style.opacity = 1f - e;
                SetFlip(1f - 0.6f * e);
            }, () => card.RemoveFromHierarchy(), 0.75f + hold);
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
            _logTotal++;
            _log.Add(message);
            if (_log.Count > 200) _log.RemoveAt(0);
            _dirty = true;
        }

        // ================================================================== Обновление экрана

        void Refresh()
        {
            if (_game == null) return;
            _turnLabel.text = _game.Phase == GamePhase.SetupContracts ? "Подготовка" : "Ход " + _game.TurnNumber;
            RefreshEvent();
            RefreshGoals();
            RefreshTabs();
            _map.RefreshVisibility();
            RefreshFaceUp();
            RefreshPlayers();
            RefreshContracts();
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
                bool forbidden = _game.Phase == GamePhase.DrawingSecondCard && card == Resource.Machinery;
                _faceUp[i].Set(card, null, card.HasValue && forbidden);
            }
            _faceUpRow.style.opacity = canDraw ? 1f : 0.7f;

            bool firstCard = _game.Phase == GamePhase.AwaitingAction;
            _deckButton.text = (firstCard ? "Колода ×2" : "Колода ×1");
            Ui.SetEnabled(_deckButton, canDraw && _game.DeckCount + _game.DiscardCount > 0);
            Ui.Show(_luckButton, _game.Config.LuckEnabled);
            // На кнопке — сколько карт осталось до перемешивания; под кнопками — шансы.
            _luckButton.text = "Удача";
            Ui.Show(_luckOdds, _game.Config.LuckEnabled);
            int buffs = _game.LuckBuffsLeft, debuffs = _game.LuckDebuffsLeft;
            if (buffs + debuffs == 0) _luckOdds.text = "Колода удачи пуста.";
            else
            {
                int chance = (int)Math.Round(100.0 * buffs / (buffs + debuffs));
                int luckLeft = _game.LuckWillReshuffle ? _game.LuckDiscardCount : _game.LuckDeckCount;
                _luckOdds.text = $"В колоде {_game.DeckCount} · контрактов {_game.ContractDeckCount} · удача {luckLeft}: шанс бафа {chance}%"
                                 + (_game.LuckWillReshuffle ? " (перемешается)" : "")
                                 + (_game.TradeUsedThisTurn ? " · сделка была" : "")
                                 + (_game.InfluenceUsedThisTurn ? " · влияние потрачено" : "")
                                 + (HumanToAct && _game.BribeDiscountActive ? " · скидка −1 карта" : "")
                                 + (_game.ActiveBribes > 0 ? $" · шёпот в мэрии: {_game.ActiveBribes}" : "");
            }
            Ui.SetEnabled(_luckButton, HumanToAct && _game.CanDrawLuckCard());
            _contractsButton.text = "Контракты";
            Ui.SetEnabled(_contractsButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction && _game.ContractDeckCount > 0);
            Ui.Show(_tradeButton, _game.Config.TradesEnabled);
            _tradeButton.text = "Сделка";
            Ui.SetEnabled(_tradeButton, HumanToAct && _game.CanProposeTrade());
            Ui.Show(_influenceButton, _game.Config.InfluenceEnabled);
            _influenceButton.text = $"Влияние {_game.Players[Viewer].Influence}";
            Ui.SetEnabled(_influenceButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction);
        }

        /// <summary>Строка действия в окне влияния: название, описание (или причина, почему нельзя) и кнопка с ценой.</summary>
        VisualElement ActionLine(string title, string description, bool blocked, string price, Action onClick)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8f;
            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.style.flexShrink = 1f;
            texts.Add(Ui.Text(title, 15f, Theme.Text, true));
            var text = Ui.Text(description, 12f, blocked ? Theme.Danger : Theme.TextDim);
            text.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(text);
            row.Add(texts);
            var button = Ui.Button(price, onClick, ButtonKind.Primary, 15f);
            button.style.marginLeft = 12f;
            button.style.minWidth = 80f;
            Ui.SetEnabled(button, !blocked);
            row.Add(button);
            return row;
        }

        /// <summary>Траты влияния, взятки и разоблачения: у каждого — цена, описание и причина, если сейчас нельзя.</summary>
        void ShowInfluenceMenu()
        {
            if (!HumanToAct || _game.Phase != GamePhase.AwaitingAction) return;
            var me = _game.CurrentPlayer;
            var box = DialogBox(600f);
            Ui.Border(box, 2f, Theme.Accent);
            box.Add(Ui.Text($"Влияние: {me.Influence}", 26f, Theme.Accent, true));
            var note = Ui.Text("Раз в ход, до основного действия; ход не тратится (кроме «Контрактов с выбором» — это и есть ход). " +
                               "Влияние дают выполненные контракты, новые районы сети, цели мэрии, «Грант» и отставание от лидера. " +
                               "В очки оно не превращается.", 12f, Theme.TextDim);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginBottom = 12f;
            box.Add(note);

            void Group(string caption, bool city)
            {
                var header = Ui.Text(caption, 15f, Theme.Text, true);
                header.style.marginTop = 6f;
                header.style.marginBottom = 6f;
                box.Add(header);
                foreach (var action in InfluenceInfo.All.Where(a => InfluenceInfo.IsCityAction(a) == city))
                {
                    var chosen = action;
                    int cost = InfluenceInfo.Cost(action, _game.Config);
                    var blocker = _game.GetInfluenceBlocker(action);
                    var row = Ui.Row();
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 8f;
                    var texts = Ui.Column();
                    texts.style.flexGrow = 1f;
                    texts.style.flexShrink = 1f;
                    texts.Add(Ui.Text(InfluenceInfo.Title(action), 15f, Theme.Text, true));
                    var description = Ui.Text(blocker ?? InfluenceInfo.Describe(action, _game.Config), 12f, blocker != null ? Theme.Danger : Theme.TextDim);
                    description.style.whiteSpace = WhiteSpace.Normal;
                    texts.Add(description);
                    row.Add(texts);
                    var button = Ui.Button($"{cost} вл.", () =>
                    {
                        HideOverlay();
                        Act(() => _game.UseInfluence(chosen));
                    }, ButtonKind.Primary, 15f);
                    button.style.marginLeft = 12f;
                    button.style.minWidth = 80f;
                    Ui.SetEnabled(button, blocker == null);
                    row.Add(button);
                    box.Add(row);
                }
            }
            Group("Влияние на город", true);
            Group("Контроль рынка", false);

            if (_game.Config.BribesEnabled)
            {
                var bribeHeader = Ui.Text("Взятка чиновникам (тайно)", 15f, Theme.Text, true);
                bribeHeader.style.marginTop = 6f;
                box.Add(bribeHeader);
                var bribeNote = Ui.Text($"Все увидят только «в мэрии кто-то шепчется». Пока вы не начали следующий ход, соперник может вас обвинить: " +
                                        $"угадал — штраф −{_game.Config.BribeFine} за каждую взятку, ему +{_game.Config.BribeWhistleReward} влияния.", 12f, Theme.TextDim);
                bribeNote.style.whiteSpace = WhiteSpace.Normal;
                bribeNote.style.marginBottom = 6f;
                box.Add(bribeNote);
                foreach (var kind in BribeInfo.All)
                {
                    var chosen = kind;
                    var blocker = _game.GetBribeBlocker(kind);
                    box.Add(ActionLine(BribeInfo.Title(kind), blocker ?? BribeInfo.Describe(kind), blocker != null,
                        $"{BribeInfo.Cost(kind, _game.Config)} вл.", () =>
                        {
                            HideOverlay();
                            Act(() =>
                            {
                                var result = _game.Bribe(chosen);
                                if (result.Success) Flash("Взятка дана. Не попадитесь до следующего хода.");
                                return result;
                            });
                        }));
                }

                var accuseHeader = Ui.Text("Разоблачение", 15f, Theme.Text, true);
                accuseHeader.style.marginTop = 6f;
                box.Add(accuseHeader);
                var accuseNote = Ui.Text(_game.ActiveBribes > 0
                    ? $"Нераскрытых взяток сейчас: {_game.ActiveBribes}. Подозрительно, если у соперника рука выросла сильнее обычного или дорога обошлась дешевле. Ошибка — −{_game.Config.FalseAccusationFine} вам."
                    : $"Сейчас никто не шептался — обвинять некого. Ложное обвинение — −{_game.Config.FalseAccusationFine} вам.", 12f, Theme.TextDim);
                accuseNote.style.whiteSpace = WhiteSpace.Normal;
                accuseNote.style.marginBottom = 6f;
                box.Add(accuseNote);
                var accuseRow = Ui.Row();
                accuseRow.style.flexWrap = Wrap.Wrap;
                foreach (var other in _game.Players.Where(p => p.Index != me.Index))
                {
                    int target = other.Index;
                    var button = Ui.Button("Обвинить: " + other.Name, () =>
                    {
                        HideOverlay();
                        Act(() => _game.Accuse(target));
                    }, ButtonKind.Secondary, 13f);
                    button.style.marginRight = 8f;
                    button.style.marginBottom = 6f;
                    Ui.SetEnabled(button, _game.GetAccuseBlocker(target) == null);
                    accuseRow.Add(button);
                }
                box.Add(accuseRow);
            }

            var close = Ui.Button("Закрыть", HideOverlay, ButtonKind.Ghost, 15f);
            close.style.alignSelf = Align.FlexEnd;
            close.style.marginTop = 6f;
            box.Add(close);
            ShowOverlay(OverlayKind.Influence, Centered(box), Theme.Shade);
        }

        void BuildPlayerRows()
        {
            BuildLooks();
            _playersBox.Clear();
            _playerRows = new PlayerRow[_game.Players.Count];
            foreach (var player in _game.Players)
            {
                var row = Ui.Column();
                row.style.marginBottom = 4f;
                Ui.Padding(row, 4f, 8f);
                Ui.Radius(row, 8f);

                var line = Ui.Row();
                line.style.alignItems = Align.Center;
                var icon = new CharacterIcon(24f, player.Character, Palette.PlayerColor(player.Index));
                icon.style.marginRight = 8f;
                line.Add(icon);
                var badge = new ProfileBadge(14f);
                badge.style.marginRight = 5f;
                line.Add(badge);
                var name = Ui.Text("", 14f, null, true);
                name.style.flexGrow = 1f;
                name.style.flexShrink = 1f;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                name.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(name);
                var stats = Ui.Text("", 12f, Theme.TextDim);
                stats.style.flexShrink = 0f;
                line.Add(stats);
                row.Add(line);

                var title = Ui.Text("", 10f, new Color(0.98f, 0.78f, 0.25f), true);
                title.style.marginLeft = 32f;
                row.Add(title);

                var ability = Ui.Text("", 11f, Theme.TextDim);
                ability.style.whiteSpace = WhiteSpace.Normal;
                ability.style.marginLeft = 32f;
                row.Add(ability);

                _playersBox.Add(row);
                _playerRows[player.Index] = new PlayerRow { Root = row, Name = name, Stats = stats, Icon = icon, Ability = ability, Badge = badge, Title = title };
            }
        }

        void RefreshPlayers()
        {
            int acting = _game.ActingPlayerIndex;
            foreach (var player in _game.Players)
            {
                var row = _playerRows[player.Index];
                bool active = player.Index == acting;
                string character = player.Character != CharacterKind.None ? " · " + Characters.Title(player.Character) : "";
                row.Name.text = player.Name + character + BotTag(player.Index);
                // Способность — только у того, кто сейчас действует: остальным она пока не нужна.
                row.Ability.text = player.Character == CharacterKind.None ? "" : Characters.Ability(player.Character, _game.Config)
                    + (player.Character == CharacterKind.Foreman ? $" Осталось: {player.FreeRoadsLeft}." : "");
                Ui.Show(row.Ability, active && player.Character != CharacterKind.None);
                int points = player.RoadPoints + player.GoalPoints + player.LuckPoints;
                row.Stats.text = $"{points} очк · {player.RoadPieces} уч · {player.HandSize} карт"
                                 + (_game.Config.InfluenceEnabled ? $" · {player.Influence} вл" : "");
                row.Root.style.backgroundColor = active ? Theme.PanelHover : Theme.PanelLight;
                var look = LookOf(player.Index);
                var frame = Profiles.FrameColor(look.Frame);
                bool framed = frame.a > 0f;
                Ui.Border(row.Root, active || framed ? 2f : 1f, active ? Palette.PlayerColor(player.Index) : framed ? frame : Theme.Line);
                row.Badge.Set(look.Emblem, framed ? frame : new Color(0.98f, 0.78f, 0.25f));
                row.Title.text = Profiles.TitleText(look.Title);
                Ui.Show(row.Title, row.Title.text.Length > 0);
            }
        }

        void RefreshContracts()
        {
            _contractsBox.Clear();
            ClearContractHighlight();

            if (TryGetContractChooser(out var chooser)) BuildContractChoice(chooser);
            else
            {
                BuildContractList();
                RestorePinnedHighlight();
            }
        }

        bool TryGetContractChooser(out PlayerState chooser)
        {
            chooser = null;
            if (!HumanToAct) return false;
            if (_game.Phase != GamePhase.SetupContracts && _game.Phase != GamePhase.ChoosingContracts) return false;
            var player = _game.Players[_game.ActingPlayerIndex];
            if (player.PendingContracts.Count == 0) return false;
            chooser = player;
            return true;
        }

        void BuildContractChoice(PlayerState player)
        {
            bool setup = _game.Phase == GamePhase.SetupContracts;
            int minKeep = setup ? _game.Config.InitialContractsMinKeep : _game.Config.ContractsMinKeep;
            int required = Math.Min(minKeep, player.PendingContracts.Count);

            string key = player.Index + ":" + string.Join(",", player.PendingContracts.Select(c => c.Id));
            if (key != _selectionKey)
            {
                _selectionKey = key;
                _selectedContracts.Clear();
                if (setup)
                    foreach (var c in player.PendingContracts) _selectedContracts.Add(c.Id);
            }

            var card = Ui.Card(12f);
            Ui.Border(card, 2f, Theme.Accent);
            card.Add(Ui.Text(setup ? "Стартовые контракты" : "Новые контракты", 17f, Theme.Accent, true));
            var hint = Ui.Text($"Оставьте хотя бы {required}. Наведите — районы подсветятся на карте. Невыполненный контракт в конце отнимает очки.", 12f, Theme.TextDim);
            hint.style.marginTop = 4f;
            hint.style.marginBottom = 8f;
            card.Add(hint);

            foreach (var contract in player.PendingContracts)
            {
                bool selected = _selectedContracts.Contains(contract.Id);
                var row = ContractRow(player.Index, contract, selected ? new Color(0.27f, 0.36f, 0.22f) : Theme.Panel, selected, false);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_selectedContracts.Remove(contract.Id)) _selectedContracts.Add(contract.Id);
                    _app.Sound.Play(Sfx.Click, 0.5f);
                    _dirty = true;
                });
                card.Add(row);
            }

            var confirm = Ui.Button($"Взять выбранные ({_selectedContracts.Count})", () =>
            {
                var ids = _selectedContracts.ToList();
                if (ids.Count < required)
                {
                    Flash($"Нужно оставить хотя бы {required}.");
                    return;
                }
                Act(() => setup ? _game.KeepInitialContracts(player.Index, ids) : _game.KeepContracts(ids));
            }, ButtonKind.Primary, 15f);
            confirm.style.marginTop = 8f;
            Ui.SetEnabled(confirm, _selectedContracts.Count >= required);
            card.Add(confirm);

            _contractsBox.Add(card);
        }

        void BuildContractList()
        {
            var viewer = _game.Players[Viewer];
            // Заголовок не нужен: вкладка уже называется «Контракты».

            if (NeedsHandoff)
            {
                _contractsBox.Add(Ui.Text("Скрыто до передачи хода.", 13f, Theme.TextDim));
                return;
            }
            if (viewer.Contracts.Count == 0)
            {
                _contractsBox.Add(Ui.Text("Контрактов пока нет.", 13f, Theme.TextDim));
                return;
            }

            var tip = Ui.Text("Клик по контракту закрепляет его маршрут на карте, повторный клик снимает.", 11f, Theme.TextDim);
            tip.style.marginBottom = 6f;
            tip.style.whiteSpace = WhiteSpace.Normal;
            _contractsBox.Add(tip);

            // Сначала невыполненные — о них важнее помнить.
            var checks = viewer.Contracts.Select(c => _game.CheckContract(viewer.Index, c)).OrderBy(c => c.Completed).ToList();
            foreach (var check in checks)
                _contractsBox.Add(ContractRow(viewer.Index, check.Contract, Theme.PanelLight, check.Completed, true));
        }

        /// <summary>
        /// Строка контракта. selectionStyle=false — квадратик выбора; true — статус:
        /// зелёный кружок — выполнен своими дорогами, оранжевый — через чужие (с платой), пустой — ещё нет.
        /// </summary>
        VisualElement ContractRow(int playerIndex, Contract contract, Color background, bool marked, bool statusStyle)
        {
            var check = _game.CheckContract(playerIndex, contract);
            int foreign = check.ForeignRoads.Count;
            int toll = check.Toll;

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
            Ui.Radius(mark, statusStyle ? 7f : 3f);
            if (marked) mark.style.backgroundColor = statusStyle && foreign > 0 ? Theme.Accent : Theme.Success;
            else Ui.Border(mark, 2f, Theme.TextDim);
            row.Add(mark);

            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            if (contract.Id == _game.Players[playerIndex].MayorContractId)
                texts.Add(Ui.Text($"★ КОНТРАКТ МЭРИИ · выполнен — ещё +{_game.Config.AuctionBonus}", 10f, Theme.Accent, true));
            if (contract.IsComplex)
            {
                string kind = contract.Kind == ContractKind.Transit ? "ТРАНЗИТ" : "УЗЕЛ";
                if (contract.Kind == ContractKind.Hub && !check.Completed && check.LegsCompleted > 0)
                    kind += $" · готово {check.LegsCompleted} из {check.LegsTotal}";
                texts.Add(Ui.Text(kind, 10f, MapView.ThirdColor, true));
            }
            texts.Add(DistrictPair(contract, statusStyle && marked ? Theme.TextDim : Theme.Text));
            if (statusStyle && marked && foreign > 0)
                texts.Add(Ui.Text($"через {foreign} чужих уч. · проезд −{toll}", 11f, Theme.Accent));
            row.Add(texts);

            bool pinned = statusStyle && IsPinned(playerIndex, contract);
            if (pinned)
            {
                var pinLabel = Ui.Text("МАРШРУТ ЗАКРЕПЛЁН · клик — снять", 10f, Theme.Accent, true);
                pinLabel.style.marginTop = 2f;
                texts.Add(pinLabel);
            }

            string pointsText = statusStyle && marked ? "+" + contract.Points : contract.Points.ToString();
            row.Add(Ui.Text(pointsText, 14f, statusStyle && marked ? Theme.Success : Theme.Accent, true));

            void MarkRow(bool on)
            {
                row.style.borderLeftWidth = on ? 4f : 0f;
                row.style.borderLeftColor = Theme.Accent;
            }
            if (pinned)
            {
                MarkRow(true);
                row.style.backgroundColor = Theme.PanelHover;
            }

            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                ShowContractHighlight(playerIndex, contract);
                MarkRow(true);
            });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                // Уводим курсор — возвращается закреплённый маршрут (если есть), а не пустая карта.
                if (statusStyle) RestorePinnedHighlight();
                else ClearContractHighlight();
                MarkRow(pinned);
            });
            if (statusStyle)
            {
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (IsPinned(playerIndex, contract)) _pinnedContracts.Remove(playerIndex);
                    else _pinnedContracts[playerIndex] = contract.Id;
                    _app.Sound.Play(Sfx.Click, 0.5f);
                    _dirty = true;
                });
            }
            return row;
        }

        bool IsPinned(int playerIndex, Contract contract) =>
            _pinnedContracts.TryGetValue(playerIndex, out var id) && id == contract.Id;

        /// <summary>
        /// Вернуть закреплённую подсветку текущего зрителя. Путь пересчитывается заново — после постройки
        /// своих участков он сразу становится зелёным. Если контракта у игрока больше нет, закрепление снимается.
        /// </summary>
        void RestorePinnedHighlight()
        {
            ClearContractHighlight();
            if (_game == null || NeedsHandoff || TryGetContractChooser(out _)) return;
            var viewer = _game.Players[Viewer];
            if (!_pinnedContracts.TryGetValue(viewer.Index, out var id)) return;
            var contract = viewer.Contracts.FirstOrDefault(c => c.Id == id);
            if (contract == null)
            {
                _pinnedContracts.Remove(viewer.Index);
                return;
            }
            ShowContractHighlight(viewer.Index, contract);
        }

        /// <summary>
        /// «● Район А — ● Район Б»: метки тех же цветов, что и подсветка на карте (жёлтый, красный).
        /// Транзит: «… через ● В», узел: «… и ● В»; третий район фиолетовый.
        /// </summary>
        VisualElement DistrictPair(Contract contract, Color textColor)
        {
            var line = Ui.Row();
            line.style.flexWrap = Wrap.Wrap;
            line.style.alignItems = Align.Center;
            line.Add(DistrictChip(MapView.FromColor));
            line.Add(Ui.Text(DistrictName(contract.DistrictA), 14f, textColor));
            line.Add(Ui.Text(" — ", 14f, Theme.TextDim));
            line.Add(DistrictChip(MapView.ToColor));
            line.Add(Ui.Text(DistrictName(contract.DistrictB), 14f, textColor));
            if (contract.IsComplex)
            {
                line.Add(Ui.Text(contract.Kind == ContractKind.Transit ? " через " : " и ", 14f, Theme.TextDim));
                line.Add(DistrictChip(MapView.ThirdColor));
                line.Add(Ui.Text(DistrictName(contract.DistrictC), 14f, textColor));
            }
            return line;
        }

        /// <summary>Название контракта одной строкой: «А — Б», «А — Б через В» или «А — Б и В».</summary>
        string ContractTitle(Contract contract)
        {
            string title = $"{DistrictName(contract.DistrictA)} — {DistrictName(contract.DistrictB)}";
            if (contract.Kind == ContractKind.Transit) title += " через " + DistrictName(contract.DistrictC);
            else if (contract.Kind == ContractKind.Hub) title += " и " + DistrictName(contract.DistrictC);
            return title;
        }

        static VisualElement DistrictChip(Color color)
        {
            var chip = Ui.Box();
            chip.style.width = 9f;
            chip.style.height = 9f;
            chip.style.marginRight = 5f;
            chip.style.backgroundColor = color;
            Ui.Radius(chip, 4.5f);
            return chip;
        }

        /// <summary>Подсветить районы контракта и найденный путь: свои дороги — зелёным, чужие — оранжевым.</summary>
        void ShowContractHighlight(int playerIndex, Contract contract)
        {
            ClearContractHighlight();
            _map.SetContractHighlight(contract.DistrictA, contract.DistrictB, contract.DistrictC);

            var check = _game.CheckContract(playerIndex, contract);
            foreach (var road in check.Path)
            {
                bool mine = _game.GetRoadOwner(road.Id) == playerIndex;
                _map.PathRoads[road.Id] = mine ? new Color(0.30f, 0.85f, 0.40f, 0.95f) : new Color(1f, 0.55f, 0.10f, 0.95f);
            }
            _map.Repaint();
        }

        void ClearContractHighlight()
        {
            _map.ClearContractHighlight();
        }

        void RefreshLog()
        {
            _logBox.Clear();
            if (_sideTab != SideTab.Log) return; // журнал собираем, только когда его видно
            int start = Math.Max(0, _log.Count - LogLines);
            for (int i = start; i < _log.Count; i++)
            {
                var line = Ui.Text(_log[i], 12f, i == _log.Count - 1 ? Theme.Text : Theme.TextDim);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.marginBottom = 4f;
                _logBox.Add(line);
            }
            _logPage.schedule.Execute(() => _logPage.scrollOffset = new Vector2(0f, float.MaxValue));
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

            _statusText.text = Instruction(acting) + TimerText();
            _statusText.style.color = TimerEnabled && HumanToAct && !_timerExpired && _turnTimeLeft <= 10f ? Theme.Danger : Theme.Text;
            MarkLogSeen();
            Ui.Show(_finalRound, _game.IsFinalRound && _game.Phase != GamePhase.GameOver);
            _flashLabel.text = _flash ?? "";
            Ui.Show(_flashLabel, _flash != null);
            Ui.Show(_passButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction && !_game.HasAnyLegalAction());
        }

        string Instruction(int acting)
        {
            if (acting < 0) return "Итоги — в окне по центру.";
            if (AwaitingHost) return "Ход отправлен хосту…";
            if (IsRealBot(acting)) return "Бот думает над ходом…";
            if (IsBot(acting)) return $"Ходит {_game.Players[acting].Name} (по сети)…";
            if (NeedsHandoff) return "Передайте компьютер этому игроку.";

            switch (_game.Phase)
            {
                case GamePhase.SetupContracts:
                    return "Выберите стартовые контракты на панели справа.";
                case GamePhase.AwaitingAction:
                    return "Возьмите 2 карты, постройте дорогу кликом по карте (доступные мерцают) или возьмите контракты.";
                case GamePhase.DrawingSecondCard:
                    return "Возьмите вторую карту. Открытую технику сейчас брать нельзя.";
                case GamePhase.ChoosingContracts:
                    return "Отметьте справа контракты, которые берёте.";
                case GamePhase.ChoosingLuck:
                    return "Везунчик: выберите одну из двух карт удачи.";
                case GamePhase.RespondingTrade:
                    return "Вам предлагают сделку — примите или откажитесь.";
                case GamePhase.Bidding:
                    return "Аукцион мэрии: сделайте тайную ставку влиянием.";
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
                : $"РЕСУРСЫ · {viewer.Name.ToUpperInvariant()} · УЧАСТКОВ ДОРОГИ: {viewer.RoadPieces}";
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
                foreach (var route in _game.Map.Roads)
                    if (_game.GetPaymentOptions(route.Id).Count > 0 || _game.CanBuildRoadFree(route.Id)) _map.Affordable.Add(route.Id);
            }
            _map.Interactive = _overlayKind == OverlayKind.None;
        }

        // ================================================================== Оверлеи

        void RefreshOverlays()
        {
            if (_overlayKind == OverlayKind.ConfirmExit || _overlayKind == OverlayKind.Tutorial) return;

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
            if (_game.Phase == GamePhase.ChoosingLuck && HumanToAct)
            {
                if (_overlayKind != OverlayKind.LuckChoice) ShowLuckChoice();
                return;
            }
            if (_overlayKind == OverlayKind.LuckChoice) HideOverlay();
            if (_game.Phase == GamePhase.RespondingTrade && HumanToAct)
            {
                if (_overlayKind != OverlayKind.TradeRespond) ShowTradeResponse();
                return;
            }
            if (_overlayKind == OverlayKind.TradeRespond) HideOverlay();
            if (_game.Phase == GamePhase.Bidding && HumanToAct)
            {
                if (_overlayKind != OverlayKind.Auction || _overlayPlayer != _game.ActingPlayerIndex) ShowAuction();
                return;
            }
            if (_overlayKind == OverlayKind.Auction) HideOverlay();
            if ((_overlayKind == OverlayKind.TradeCompose || _overlayKind == OverlayKind.Upgrade)
                && (!HumanToAct || _game.Phase != GamePhase.AwaitingAction)) HideOverlay();
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

        void ShowPayment(Road route, IReadOnlyList<PaymentOption> options, Vector2 mapPosition, bool canBuildFree = false)
        {
            var box = Ui.Card(14f);
            box.pickingMode = PickingMode.Position;
            box.style.position = Position.Absolute;
            box.style.width = 340f;
            box.style.backgroundColor = Theme.Panel;
            Ui.Border(box, 2f, Theme.Accent);

            box.Add(Ui.Text($"{LocationName(route.A)} — {LocationName(route.B)}: чем платить?", 15f, null, true));
            if (canBuildFree)
            {
                var free = Ui.Button($"Бесплатно — Прораб (осталось {_game.CurrentPlayer.FreeRoadsLeft})", () =>
                {
                    HideOverlay();
                    Act(() => _game.BuildRoadFree(route.Id));
                }, ButtonKind.Primary, 14f);
                free.style.marginTop = 8f;
                box.Add(free);
            }
            foreach (var option in options)
            {
                var chosen = option;
                var button = Ui.Button(PaymentLabel(_game.BuildCost(route), option), () =>
                {
                    HideOverlay();
                    Act(() => _game.BuildRoad(route.Id, chosen.Resource, chosen.Machinery));
                }, ButtonKind.Secondary, 14f);
                button.style.marginTop = 8f;
                box.Add(button);
            }
            var cancel = Ui.Button("Отмена", HideOverlay, ButtonKind.Ghost, 14f);
            cancel.style.marginTop = 8f;
            box.Add(cancel);

            float estimatedHeight = 70f + 46f * (options.Count + 1 + (canBuildFree ? 1 : 0));
            var local = _overlay.WorldToLocal(_map.LocalToWorld(mapPosition));
            var mapBounds = _map.worldBound;
            var mapMax = _overlay.WorldToLocal(new Vector2(mapBounds.xMax, mapBounds.yMax));
            box.style.left = Mathf.Max(10f, Mathf.Min(local.x + 14f, mapMax.x - 350f));
            box.style.top = Mathf.Max(10f, Mathf.Min(local.y + 14f, mapMax.y - estimatedHeight));

            ShowOverlay(OverlayKind.Payment, box, new Color(0f, 0f, 0f, 0.15f));
        }

        /// <summary>Везунчик: две открытые карты удачи — кликните ту, которую оставляете.</summary>
        /// <summary>
        /// Колода: в начале хода одним нажатием берутся сразу две карты вслепую (это всё равно ход «взять карты»).
        /// Если первая карта уже взята (открытая или удача) — берётся одна, вторая.
        /// </summary>
        ActionResult DrawFromDeck()
        {
            int player = _game.CurrentPlayerIndex;
            var result = _game.DrawCardFromDeck();
            if (result.Success && _game.Phase == GamePhase.DrawingSecondCard && _game.CurrentPlayerIndex == player
                && _game.DeckCount + _game.DiscardCount > 0)
                result = _game.DrawCardFromDeck();
            return result;
        }

        void ShowLuckChoice()
        {
            var box = DialogBox(640f);
            box.style.alignItems = Align.Center;
            Ui.Border(box, 3f, Theme.Accent);
            box.Add(Ui.Text("Везунчик: какую карту оставить?", 24f, Theme.Accent, true));
            var note = Ui.Text("Обе карты уйдут в сброс, сработает только выбранная.", 13f, Theme.TextDim);
            note.style.marginTop = 4f;
            note.style.marginBottom = 16f;
            box.Add(note);

            var row = Ui.Row();
            var choice = _game.PendingLuckChoice;
            for (int i = 0; i < choice.Count; i++)
            {
                int index = i;
                var kind = choice[i];
                bool buff = LuckInfo.IsBuff(kind);
                var color = LuckColor(buff);

                var card = Ui.Card(16f);
                card.pickingMode = PickingMode.Position;
                card.style.width = 260f;
                card.style.minHeight = 220f;
                card.style.alignItems = Align.Center;
                card.style.justifyContent = Justify.Center;
                card.style.backgroundColor = Theme.PanelLight;
                if (i == 0) card.style.marginRight = 16f;
                Ui.Radius(card, 14f);
                Ui.Border(card, 3f, color);
                card.Add(Ui.Text(buff ? "БАФ" : "ДЕБАФ", 13f, color, true));
                var title = Ui.Text(LuckInfo.Title(kind), 22f, Theme.Text, true);
                title.style.marginTop = 8f;
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.whiteSpace = WhiteSpace.Normal;
                card.Add(title);
                bool insured = !buff && _game.CurrentPlayer.Character == CharacterKind.Insurer;
                var text = Ui.Text(insured ? $"Страховка: только −{_game.Config.InsurerFine} очко." : LuckInfo.Describe(kind, _game.Config), 13f, Theme.TextDim);
                text.style.marginTop = 10f;
                text.style.unityTextAlign = TextAnchor.MiddleCenter;
                text.style.whiteSpace = WhiteSpace.Normal;
                card.Add(text);
                var take = Ui.Button("Оставить", () =>
                {
                    HideOverlay();
                    Act(() => _game.ChooseLuckCard(index));
                }, buff ? ButtonKind.Primary : ButtonKind.Secondary, 15f);
                take.style.marginTop = 14f;
                card.Add(take);
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_overlayKind != OverlayKind.LuckChoice) return;
                    HideOverlay();
                    Act(() => _game.ChooseLuckCard(index));
                });
                row.Add(card);
            }
            box.Add(row);
            ShowOverlay(OverlayKind.LuckChoice, Centered(box), Theme.Shade);
        }

        int _lastTradeShown;
        int _lastAuctionShown;
        int _lastWhisperShown;
        int _lastAccusationShown;

        /// <summary>Тайная ставка на аукционе мэрии: контракт, бонус, своё влияние; кто уже поставил — без сумм.</summary>
        void ShowAuction()
        {
            var contract = _game.AuctionContract;
            if (contract == null) return;
            int bidder = _game.ActingPlayerIndex;
            var me = _game.Players[bidder];
            int bid = Math.Min(me.Influence, Math.Max(1, me.Influence / 3));

            var box = DialogBox(580f);
            box.style.alignItems = Align.Center;
            Ui.Border(box, 3f, Theme.Accent);
            box.Add(Ui.Text("АУКЦИОН МЭРИИ · РАЗ ЗА ПАРТИЮ", 13f, Theme.TextDim, true));
            var title = Ui.Text(ContractTitle(contract), 26f, Theme.Accent, true);
            title.style.marginTop = 6f;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            box.Add(title);
            var value = Ui.Text($"{contract.Points} очков, а выполнен к концу партии — ещё +{_game.Config.AuctionBonus}. Не выполнен — −{contract.Points}, как обычный контракт.", 15f, Theme.Text);
            value.style.whiteSpace = WhiteSpace.Normal;
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            value.style.marginTop = 8f;
            box.Add(value);
            var rules = Ui.Text("Каждый делает одну тайную ставку влиянием. Побеждает самая высокая (при равенстве — отстающий по очкам) и платит свою ставку; остальные не платят ничего.", 12f, Theme.TextDim);
            rules.style.whiteSpace = WhiteSpace.Normal;
            rules.style.unityTextAlign = TextAnchor.MiddleCenter;
            rules.style.marginTop = 8f;
            box.Add(rules);

            var done = _game.Players.Where(p => p.Index != bidder && _game.HasBid(p.Index)).Select(p => p.Name).ToList();
            var status = Ui.Text(done.Count > 0 ? "Уже поставили: " + string.Join(", ", done) : "Вы ставите первым.", 12f, Theme.TextDim);
            status.style.marginTop = 6f;
            box.Add(status);

            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 16f;
            var amount = Ui.Text("", 30f, Theme.Text, true);
            amount.style.width = 110f;
            amount.style.unityTextAlign = TextAnchor.MiddleCenter;
            UnityEngine.UIElements.Button place = null;
            void Refresh()
            {
                amount.text = bid + " вл.";
                place.text = bid > 0 ? $"Поставить {bid}" : "Не участвовать";
            }
            row.Add(Ui.Button("–", () => { bid = Math.Max(0, bid - 1); Refresh(); }, ButtonKind.Secondary, 20f));
            row.Add(amount);
            row.Add(Ui.Button("+", () => { bid = Math.Min(me.Influence, bid + 1); Refresh(); }, ButtonKind.Secondary, 20f));
            box.Add(row);
            box.Add(Ui.Text($"Ваше влияние: {me.Influence}", 13f, Theme.TextDim));

            place = Ui.Button("", () => { HideOverlay(); Act(() => _game.Bid(bid)); }, ButtonKind.Primary, 17f);
            place.style.marginTop = 16f;
            box.Add(place);
            Refresh();
            ShowOverlay(OverlayKind.Auction, Centered(box), Theme.Shade);
            _overlayPlayer = bidder;
        }

        // ================================================================== Сделки и улучшение

        /// <summary>Окно сделки: кому, что отдать и что попросить. Карты у вас — видны, у соперника — нет.</summary>
        void ShowTradeComposer()
        {
            if (!HumanToAct || !_game.CanProposeTrade()) return;
            var me = _game.CurrentPlayer;
            var others = _game.Players.Where(p => p.Index != me.Index).ToList();
            int to = others[0].Index;
            var resources = new[] { Resource.Asphalt, Resource.Concrete, Resource.Steel, Resource.Gravel, Resource.Machinery };
            var give = resources.FirstOrDefault(r => me.CardCount(r) > 0);
            var get = resources.First(r => r != give);
            int giveCount = 1, getCount = 1, giveInfluence = 0, getInfluence = 0;
            int max = _game.Config.TradeMaxCards;
            bool influenceOn = _game.Config.InfluenceEnabled;
            int maxInfluence = influenceOn ? _game.Config.TradeMaxInfluence : 0;
            int minCards = influenceOn ? 0 : 1; // с влиянием одна из сторон может быть без карт

            var box = DialogBox(560f);
            Ui.Border(box, 2f, Theme.Accent);
            box.Add(Ui.Text("Сделка", 26f, Theme.Accent, true));
            var note = Ui.Text(influenceOn
                ? "Одна сделка за ход, до основного действия. Ход она не тратит. Можно доплатить влиянием или купить карты за влияние."
                : "Одна сделка за ход, до основного действия. Ход она не тратит.", 13f, Theme.TextDim);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginBottom = 14f;
            box.Add(note);

            var summary = Ui.Text("", 15f, Theme.Text, true);
            summary.style.whiteSpace = WhiteSpace.Normal;
            var propose = Ui.Button("Предложить", () => { }, ButtonKind.Primary, 16f);

            void RefreshSummary()
            {
                summary.text = $"{_game.Players[to].Name} получит {TradeOffer.Side(giveCount, give, giveInfluence)}, " +
                               $"а вы — {TradeOffer.Side(getCount, get, getInfluence)}. У вас {Palette.CardTitle(give)}: {me.CardCount(give)}"
                               + (influenceOn ? $", влияния: {me.Influence}." : ".");
                bool valid = giveCount + giveInfluence > 0 && getCount + getInfluence > 0
                             && !(giveCount > 0 && getCount > 0 && give == get)
                             && me.CardCount(give) >= giveCount && me.Influence >= giveInfluence;
                Ui.SetEnabled(propose, valid);
            }

            VisualElement Line(string caption, VisualElement content)
            {
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 10f;
                var label = Ui.Text(caption, 14f, Theme.TextDim);
                label.style.width = 120f;
                row.Add(label);
                row.Add(content);
                return row;
            }

            UnityEngine.UIElements.Button Cycle(Func<string> text, Action next)
            {
                UnityEngine.UIElements.Button button = null;
                button = Ui.Button(text(), () =>
                {
                    next();
                    button.text = text();
                    RefreshSummary();
                }, ButtonKind.Secondary, 14f);
                button.style.width = 200f;
                return button;
            }

            VisualElement Counter(Func<int> value, Action<int> set, int low, int high)
            {
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginLeft = 10f;
                var label = Ui.Text(value().ToString(), 16f, Theme.Text, true);
                label.style.width = 28f;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                var minus = Ui.Button("–", () => { set(Math.Max(low, value() - 1)); label.text = value().ToString(); RefreshSummary(); }, ButtonKind.Secondary, 16f);
                var plus = Ui.Button("+", () => { set(Math.Min(high, value() + 1)); label.text = value().ToString(); RefreshSummary(); }, ButtonKind.Secondary, 16f);
                row.Add(minus);
                row.Add(label);
                row.Add(plus);
                return row;
            }

            Resource NextResource(Resource current) => resources[(Array.IndexOf(resources, current) + 1) % resources.Length];

            int othersIndex = 0;
            box.Add(Line("Кому", Cycle(() => _game.Players[to].Name, () =>
            {
                othersIndex = (othersIndex + 1) % others.Count;
                to = others[othersIndex].Index;
            })));
            var giveRow = Ui.Row();
            giveRow.Add(Cycle(() => Palette.CardTitle(give), () => give = NextResource(give)));
            giveRow.Add(Counter(() => giveCount, v => giveCount = v, minCards, max));
            box.Add(Line("Отдаю", giveRow));
            if (influenceOn) box.Add(Line("…и влияния", Counter(() => giveInfluence, v => giveInfluence = v, 0, maxInfluence)));
            var getRow = Ui.Row();
            getRow.Add(Cycle(() => Palette.CardTitle(get), () => get = NextResource(get)));
            getRow.Add(Counter(() => getCount, v => getCount = v, minCards, max));
            box.Add(Line("Прошу", getRow));
            if (influenceOn) box.Add(Line("…и влияния", Counter(() => getInfluence, v => getInfluence = v, 0, maxInfluence)));

            summary.style.marginTop = 6f;
            summary.style.marginBottom = 16f;
            box.Add(summary);

            var buttons = Ui.Row();
            buttons.style.justifyContent = Justify.FlexEnd;
            var cancel = Ui.Button("Отмена", HideOverlay, ButtonKind.Ghost, 15f);
            cancel.style.marginRight = 10f;
            buttons.Add(cancel);
            propose.clicked += () =>
            {
                HideOverlay();
                Act(() => _game.ProposeTrade(to, give, giveCount, get, getCount, giveInfluence, getInfluence));
            };
            buttons.Add(propose);
            box.Add(buttons);
            RefreshSummary();
            ShowOverlay(OverlayKind.TradeCompose, Centered(box), Theme.Shade);
        }

        /// <summary>Получатель сделки (человек) решает. Ему видно, сколько у него просимых карт.</summary>
        void ShowTradeResponse()
        {
            var offer = _game.PendingTrade;
            if (offer == null) return;
            var me = _game.Players[offer.To];
            var box = DialogBox(560f);
            box.style.alignItems = Align.Center;
            Ui.Border(box, 3f, Palette.PlayerColor(offer.From));
            box.Add(Ui.Text("Вам предлагают сделку", 14f, Theme.TextDim, true));
            var who = Ui.Text(_game.Players[offer.From].Name, 30f, Palette.PlayerColor(offer.From), true);
            who.style.marginTop = 4f;
            box.Add(who);
            string mine = offer.GetCount > 0 ? $"у вас {me.CardCount(offer.GetResource)} × {Palette.CardTitle(offer.GetResource)}" : "";
            if (offer.GetInfluence > 0) mine += (mine.Length > 0 ? ", " : "") + $"влияния {me.Influence}";
            var text = Ui.Text($"Вы получите {TradeOffer.Side(offer.GiveCount, offer.GiveResource, offer.GiveInfluence)}\n" +
                               $"и отдадите {TradeOffer.Side(offer.GetCount, offer.GetResource, offer.GetInfluence)} ({mine}).", 17f, Theme.Text);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 14f;
            text.style.marginBottom = 20f;
            box.Add(text);
            var buttons = Ui.Row();
            var no = Ui.Button("Отказаться", () => { HideOverlay(); Act(() => _game.RespondTrade(false)); }, ButtonKind.Secondary, 16f);
            no.style.marginRight = 12f;
            buttons.Add(no);
            buttons.Add(Ui.Button("Согласиться", () => { HideOverlay(); Act(() => _game.RespondTrade(true)); }, ButtonKind.Primary, 16f));
            box.Add(buttons);
            ShowOverlay(OverlayKind.TradeRespond, Centered(box), Theme.Shade);
        }

        /// <summary>Улучшение своей дороги до магистрали: способы оплаты.</summary>
        void ShowUpgrade(Road route, Vector2 mapPosition)
        {
            var options = _game.GetUpgradeOptions(route.Id);
            int cost = _game.UpgradeCost(route);
            var box = Ui.Card(14f);
            box.pickingMode = PickingMode.Position;
            box.style.position = Position.Absolute;
            box.style.width = 360f;
            box.style.backgroundColor = Theme.Panel;
            Ui.Border(box, 2f, new Color(1f, 0.60f, 0.20f));

            box.Add(Ui.Text($"{LocationName(route.A)} — {LocationName(route.B)}: улучшить до магистрали?", 15f, null, true));
            var info = Ui.Text($"Стоит {cost} карт(ы), занимает ход. +{_game.Config.UpgradePoints} очка, проезд для соперников ×{_game.Config.RushHourTollMultiplier}.", 12f, Theme.TextDim);
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.marginTop = 4f;
            box.Add(info);
            if (options.Count == 0)
            {
                var none = Ui.Text("Не хватает карт для улучшения.", 13f, Theme.Danger);
                none.style.marginTop = 8f;
                box.Add(none);
            }
            foreach (var option in options)
            {
                var chosen = option;
                var button = Ui.Button(PaymentLabel(cost, option), () =>
                {
                    HideOverlay();
                    Act(() => _game.UpgradeRoad(route.Id, chosen.Resource, chosen.Machinery));
                }, ButtonKind.Secondary, 14f);
                button.style.marginTop = 8f;
                box.Add(button);
            }
            var cancel = Ui.Button("Отмена", HideOverlay, ButtonKind.Ghost, 14f);
            cancel.style.marginTop = 8f;
            box.Add(cancel);

            float estimatedHeight = 110f + 46f * (options.Count + 1);
            var local = _overlay.WorldToLocal(_map.LocalToWorld(mapPosition));
            var mapBounds = _map.worldBound;
            var mapMax = _overlay.WorldToLocal(new Vector2(mapBounds.xMax, mapBounds.yMax));
            box.style.left = Mathf.Max(10f, Mathf.Min(local.x + 14f, mapMax.x - 370f));
            box.style.top = Mathf.Max(10f, Mathf.Min(local.y + 14f, mapMax.y - estimatedHeight));
            ShowOverlay(OverlayKind.Upgrade, box, new Color(0f, 0f, 0f, 0.15f));
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
            var text = Ui.Text("Остальные — отвернитесь: сейчас откроются карты и контракты этого игрока.", 15f, Theme.Text);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 14f;
            text.style.marginBottom = 16f;
            box.Add(text);

            // Пока вас не было: что сделали соперники с прошлого хода этого игрока (открытая информация).
            var unseen = UnseenLog(playerIndex);
            if (unseen.Count > 0 && _game.Phase != GamePhase.SetupContracts)
            {
                var report = Ui.Card(12f);
                report.style.alignSelf = Align.Stretch;
                report.style.backgroundColor = Theme.PanelLight;
                report.style.marginBottom = 18f;
                Ui.Radius(report, 10f);
                report.Add(Ui.Text("Пока вас не было", 13f, Theme.Accent, true));
                const int maxLines = 8;
                if (unseen.Count > maxLines)
                    report.Add(Ui.Text($"…и ещё {unseen.Count - maxLines} событий раньше", 12f, Theme.TextDim));
                foreach (var line in unseen.Skip(Math.Max(0, unseen.Count - maxLines)))
                {
                    var label = Ui.Text("• " + line, 12f, Theme.Text);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.marginTop = 3f;
                    report.Add(label);
                }
                box.Add(report);
            }
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
            var box = DialogBox(960f);

            var title = Ui.Text("Победа: " + string.Join(", ", result.Winners.Select(w => w.PlayerName)), 34f, Theme.Accent, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(title);
            var subtitle = Ui.Text("Дороги + контракты (★ — бонус контракта мэрии) ± плата за проезд + «Крупнейшая сеть» + цели мэрии ± удача + способность персонажа (Чиновник — за контракты, Мостовик — за мосты)", 13f, Theme.TextDim);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 20f;
            box.Add(subtitle);

            float[] widths = { 190f, 64f, 140f, 70f, 100f, 58f, 62f, 82f, 70f, 64f };
            box.Add(ResultRow(new[] { "Компания", "Дороги", "Контракты", "Проезд", "Сеть", "Цели", "Удача", "Персонаж", "Штрафы", "Итого" }, widths, Theme.TextDim, -1, false));

            int place = 1;
            foreach (var s in result.Ranking)
            {
                string contracts = $"{s.ContractPoints:+#;-#;0} ({s.CompletedContracts} из {s.CompletedContracts + s.FailedContracts})"
                                   + (s.MayorBonus > 0 ? $" ★+{s.MayorBonus}" : "");
                int tollBalance = s.TollsEarned - s.TollsPaid;
                string tolls = $"{tollBalance:+#;-#;0}";
                string network = s.NetworkDistricts + " р." + (s.NetworkBonus > 0 ? $" (+{s.NetworkBonus})" : "");
                string character = s.Character == CharacterKind.None ? "—" : s.CharacterPoints > 0 ? "+" + s.CharacterPoints : "0";
                var cells = new[] { $"{place}. {s.PlayerName}", s.RoadPoints.ToString(), contracts, tolls, network,
                    s.GoalPoints.ToString(), $"{s.LuckPoints:+#;-#;0}", character, s.Penalties == 0 ? "—" : s.Penalties.ToString(), s.Total.ToString() };
                box.Add(ResultRow(cells, widths, Theme.Text, s.PlayerIndex, place == 1));
                place++;
            }

            if (_profileCredits != null && _profileCredits.Count > 0)
            {
                var credit = Ui.Text("В кошелёк профиля: " + string.Join(" · ", _profileCredits.Select(c => $"{c.Profile} +{c.Amount}"))
                                     + " — тратится на оформление в «Профиле».", 14f, Theme.Accent, true);
                credit.style.whiteSpace = WhiteSpace.Normal;
                credit.style.unityTextAlign = TextAnchor.MiddleCenter;
                credit.style.marginTop = 10f;
                box.Add(credit);
            }

            if (_game.Config.InfluenceEnabled)
            {
                var influence = Ui.Text("Влияние на конец партии (в очки не входит, остаток людей уходит в профиль): "
                                        + string.Join(" · ", result.Ranking.Select(r => $"{r.PlayerName} — {r.Influence} (заработано {r.InfluenceEarned})")),
                    13f, Theme.TextDim);
                influence.style.whiteSpace = WhiteSpace.Normal;
                influence.style.unityTextAlign = TextAnchor.MiddleCenter;
                influence.style.marginTop = 10f;
                box.Add(influence);
            }

            box.Add(GameSummary(result));

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

        /// <summary>Сводка под таблицей итогов: кто взял цели мэрии и как кому везло.</summary>
        VisualElement GameSummary(GameResult result)
        {
            var row = Ui.Row();
            row.style.marginTop = 20f;
            row.style.alignItems = Align.FlexStart;

            VisualElement Column(string caption)
            {
                var column = Ui.Card(12f);
                column.style.flexGrow = 1f;
                column.style.flexBasis = 0f;
                column.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(column, 10f);
                column.Add(Ui.Text(caption, 14f, Theme.Accent, true));
                return column;
            }
            Label Line(string text, Color color, bool bold = false)
            {
                var label = Ui.Text(text, 13f, color, bold);
                label.style.marginTop = 5f;
                label.style.whiteSpace = WhiteSpace.Normal;
                return label;
            }

            // Цели мэрии.
            var goals = Column("Цели мэрии");
            goals.style.marginRight = 12f;
            if (_game.ActiveGoals.Count == 0) goals.Add(Line("В этой партии целей не было.", Theme.TextDim));
            foreach (var goal in _game.ActiveGoals)
            {
                var owner = _game.GoalOwner(goal.Id);
                goals.Add(owner.HasValue
                    ? Line($"«{goal.Name}» — {_game.Players[owner.Value].Name}, +{goal.Points}", Palette.PlayerColor(owner.Value))
                    : Line($"«{goal.Name}» — никто не выполнил", Theme.TextDim));
            }
            row.Add(goals);

            // Удача.
            var luck = Column("Удача");
            var players = _game.Players;
            int drawnTotal = players.Sum(p => p.LuckBuffs + p.LuckDebuffs);
            if (!_game.Config.LuckEnabled) luck.Add(Line("Колода удачи была выключена.", Theme.TextDim));
            else if (drawnTotal == 0) luck.Add(Line("Карту удачи никто не брал.", Theme.TextDim));
            else
            {
                foreach (var p in players.OrderByDescending(x => x.LuckPoints).ThenBy(x => x.Index))
                {
                    int drawn = p.LuckBuffs + p.LuckDebuffs;
                    string text = drawn == 0
                        ? $"{p.Name}: не рисковал"
                        : $"{p.Name}: {drawn} карт ({p.LuckBuffs} баф., {p.LuckDebuffs} дебаф.), {p.LuckPoints:+#;-#;0} очк.";
                    luck.Add(Line(text, Palette.PlayerColor(p.Index)));
                }
                var luckiest = players.Where(p => p.LuckBuffs + p.LuckDebuffs > 0).OrderByDescending(p => p.LuckPoints).First();
                var unluckiest = players.Where(p => p.LuckBuffs + p.LuckDebuffs > 0).OrderBy(p => p.LuckPoints).First();
                if (luckiest.LuckPoints > 0) luck.Add(Line($"Самый везучий: {luckiest.Name}", Theme.Success, true));
                if (unluckiest != luckiest && unluckiest.LuckPoints < 0) luck.Add(Line($"Не повезло: {unluckiest.Name}", Theme.Danger, true));
                luck.Add(Line("Очки удачи — только гранты, премии и штрафы; карты от поставок и кражи сюда не входят.", Theme.TextDim));
            }
            row.Add(luck);
            return row;
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
            var text = Ui.Text(!Online ? "Партия сохранена — её можно будет продолжить из главного меню."
                : _net.IsHost ? "Сетевая партия закончится у всех игроков." : "Вы выйдете из сетевой партии; за вас доиграет бот хоста.", 15f, Theme.TextDim);
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

        // ================================================================== Обучение

        sealed class TutorialStep
        {
            public Func<VisualElement> Target;
            public string Title;
            public string Text;
        }

        TutorialStep[] TutorialSteps() => new[]
        {
            new TutorialStep
            {
                Target = () => _map,
                Title = "Москва",
                Text = "Вы — дорожная компания. Стройте участки дорог между местами города: каждая дорога сразу приносит очки (дорога длиной 4 — 7 очков). Цветные пятна — районы."
            },
            new TutorialStep
            {
                Target = () => _contractsPage,
                Title = "Контракты мэрии",
                Text = "Контракт — соединить два района, подойдёт любое их место. Выполненный контракт прибавляет очки, невыполненный отнимает. Сложные дороже: транзит — один путь через третий район, узел — первый район связать с двумя другими. Наведите на контракт — районы и путь подсветятся."
            },
            new TutorialStep
            {
                Target = () => _faceUpRow,
                Title = "Ресурсы",
                Text = "За ход можно взять 2 карты: открытые или из колоды. Асфальт — улицы, бетон — проспекты, сталь — мосты, щебень — загородные дороги. Техника — джокер, но открытая техника заменяет обе карты."
            },
            new TutorialStep
            {
                Target = () => _handRow,
                Title = "Ваши карты",
                Text = "Чтобы построить дорогу длиной 3, нужно 3 карты её ресурса. Белые дороги строятся любым одним ресурсом. Мостов мало и стали мало — за них будет борьба."
            },
            new TutorialStep
            {
                Target = () => _map,
                Title = "Платный проезд",
                Text = "Чужая дорога — не тупик. Контракт засчитается, если на пути есть хотя бы одна ваша дорога и не больше двух чужих: за каждую чужую вы платите владельцу 2 очка. Самая большая сеть по районам даёт +10."
            },
            new TutorialStep
            {
                Target = () => _eventCard,
                Title = "Городские события",
                Text = "В начале круга может открыться событие (примерно в половине кругов). Час пик — дороги этого круга станут платными вдвое. Ремонт — пара участков закрыта. Субсидия — мосты дешевле. Новый район — на окраине появляются места и контракты."
            },
            new TutorialStep
            {
                Target = () => _playersBox,
                Title = "Персонажи",
                Text = "У каждой компании свой персонаж со способностью — она написана под именем игрока. Везунчик выбирает из двух карт удачи, Прораб раз за партию строит бесплатно, Сборщик пошлин берёт за проезд больше. Играйте от своей способности."
            },
            new TutorialStep
            {
                Target = () => _tabGoals,
                Title = "Цели мэрии",
                Text = "Три общие цели открыты для всех. Награду получает тот, кто выполнит цель первым, — следите за прогрессом соперников."
            },
            new TutorialStep
            {
                Target = () => _luckButton,
                Title = "Удача",
                Text = "Когда берёте карты, одну из двух можно заменить картой удачи — не больше одной за ход. Бафы: грант, поставка ресурсов, премия за контракты. Дебафы: штраф, кража карты, отзыв невыполненного контракта. Риск, а не бесплатный бонус."
            }
        };


        void ShowTutorialStep(int index)
        {
            if (_game == null) return;
            var steps = TutorialSteps();
            if (index >= steps.Length)
            {
                FinishTutorial();
                return;
            }
            var step = steps[index];

            var layer = Ui.Box();
            Ui.Fill(layer);

            // Рамка вокруг того, о чём рассказываем.
            var target = step.Target();
            if (IsLaidOut(target))
            {
                var bounds = target.worldBound;
                var topLeft = _overlay.WorldToLocal(bounds.position);
                var frame = Ui.Box();
                frame.style.position = Position.Absolute;
                frame.style.left = topLeft.x - 6f;
                frame.style.top = topLeft.y - 6f;
                frame.style.width = bounds.width + 12f;
                frame.style.height = bounds.height + 12f;
                Ui.Radius(frame, 12f);
                Ui.Border(frame, 3f, Theme.Accent);
                layer.Add(frame);
            }

            var card = Ui.Card(22f);
            card.pickingMode = PickingMode.Position;
            card.style.position = Position.Absolute;
            card.style.width = 560f;
            card.style.backgroundColor = Theme.Panel;
            Ui.Border(card, 2f, Theme.Accent);
            var mapBounds = _map.worldBound;
            var mapTopLeft = _overlay.WorldToLocal(mapBounds.position);
            card.style.left = mapTopLeft.x + mapBounds.width / 2f - 280f;
            // Когда рассказываем про карту, карточку ставим вниз, иначе — в середину карты.
            card.style.top = mapTopLeft.y + (step.Target() == _map ? mapBounds.height - 250f : mapBounds.height / 2f - 110f);

            card.Add(Ui.Text($"Обучение · {index + 1} из {steps.Length}", 12f, Theme.TextDim, true));
            var title = Ui.Text(step.Title, 24f, Theme.Accent, true);
            title.style.marginTop = 4f;
            card.Add(title);
            var text = Ui.Text(step.Text, 15f);
            text.style.marginTop = 8f;
            text.style.marginBottom = 16f;
            card.Add(text);

            var buttons = Ui.Row();
            bool last = index == steps.Length - 1;
            var next = Ui.Button(last ? "Начать игру" : "Далее", () => ShowTutorialStep(index + 1), ButtonKind.Primary, 15f);
            next.style.marginRight = 10f;
            buttons.Add(next);
            if (!last) buttons.Add(Ui.Button("Пропустить обучение", FinishTutorial, ButtonKind.Ghost, 14f));
            card.Add(buttons);
            layer.Add(card);

            ShowOverlay(OverlayKind.Tutorial, layer, new Color(0f, 0f, 0f, 0.35f));
        }

        void FinishTutorial()
        {
            PlayerPrefs.SetInt(TutorialPrefsKey, 1);
            PlayerPrefs.Save();
            HideOverlay();
            OnTurnStarted();
        }

        // ================================================================== Мелочи

        void RefreshEvent()
        {
            var cityEvent = _game.CurrentEvent;
            bool playing = _game.Phase != GamePhase.GameOver && _game.Phase != GamePhase.SetupContracts;
            Ui.Show(_eventCard, _game.Config.EventsEnabled && playing);
            if (cityEvent == null)
            {
                // Спокойный круг: событие не выпало.
                var calm = new Color(0.55f, 0.60f, 0.70f);
                _eventChip.style.backgroundColor = calm;
                _eventTitle.text = "Спокойный круг";
                _eventTitle.style.color = calm;
                _eventText.text = "В этом круге событий нет. Новое может открыться в начале следующего круга.";
                Ui.Border(_eventCard, 2f, new Color(calm.r, calm.g, calm.b, 0.35f));
                return;
            }
            var color = EventColor(cityEvent.Kind);
            _eventChip.style.backgroundColor = color;
            _eventTitle.text = cityEvent.Title;
            _eventTitle.style.color = color;
            _eventText.text = cityEvent.Describe(_game);
            Ui.Border(_eventCard, 2f, new Color(color.r, color.g, color.b, 0.6f));
        }

        /// <summary>Карточка события в панели мягко окрашивается цветом события и за пару секунд возвращается к обычной.</summary>
        void GlowEventCard(Color color)
        {
            int version = ++_eventGlowVersion;
            var start = new Color(color.r, color.g, color.b, 1f);
            _app.Tweener.Add(2.0f, k =>
            {
                if (version != _eventGlowVersion) return;
                float s = Mathf.Sin(Mathf.Min(k, 1f) * Mathf.PI);
                _eventCard.style.backgroundColor = Color.Lerp(Theme.PanelLight, Color.Lerp(Theme.PanelLight, start, 0.28f), s);
            }, () =>
            {
                if (version == _eventGlowVersion) _eventCard.style.backgroundColor = Theme.PanelLight;
            }, 0f, false);
        }

        int _eventGlowVersion;

        void SelectTab(SideTab tab)
        {
            _sideTab = tab;
            Ui.Show(_contractsPage, tab == SideTab.Contracts);
            Ui.Show(_goalsPage, tab == SideTab.Goals);
            Ui.Show(_logPage, tab == SideTab.Log);
            if (tab == SideTab.Goals && _game != null) _seenGoalOwners = _game.ActiveGoals.Count(g => _game.GoalOwner(g.Id).HasValue);
            if (_game != null)
            {
                RefreshTabs();
                if (tab == SideTab.Log) RefreshLog();
            }
        }

        /// <summary>Подписи вкладок: число контрактов, целей; «•» — новая взятая цель, которую ещё не видели.</summary>
        void RefreshTabs()
        {
            // Выбор контрактов всегда на вкладке «Контракты».
            if (TryGetContractChooser(out _) && _sideTab != SideTab.Contracts) SelectTab(SideTab.Contracts);
            var viewer = _game.Players[Viewer];
            int goals = _game.ActiveGoals.Count;
            int taken = _game.ActiveGoals.Count(g => _game.GoalOwner(g.Id).HasValue);
            _tabContracts.text = $"Контракты · {viewer.Contracts.Count}";
            _tabGoals.text = taken > _seenGoalOwners && _sideTab != SideTab.Goals ? $"Цели · {goals - taken} •" : $"Цели · {goals - taken}";
            _tabLog.text = "Журнал";
            Ui.Show(_tabGoals, goals > 0);
            foreach (var (button, tab) in new[] { (_tabContracts, SideTab.Contracts), (_tabGoals, SideTab.Goals), (_tabLog, SideTab.Log) })
            {
                bool active = tab == _sideTab;
                button.style.color = active ? Theme.Accent : Theme.TextDim;
                button.style.borderBottomWidth = active ? 2f : 0f;
                button.style.borderBottomColor = Theme.Accent;
            }
        }

        /// <summary>Цели мэрии: название, награда, условие и статус — свободна (с прогрессом зрителя) или чья.</summary>
        void RefreshGoals()
        {
            _goalsBox.Clear();
            var goals = _game.ActiveGoals;
            Ui.Show(_goalsBox, goals.Count > 0);
            if (goals.Count == 0) return;
            var goalsNote = Ui.Text("Награду получает тот, кто выполнит цель первым.", 11f, Theme.TextDim);
            goalsNote.style.marginBottom = 6f;
            _goalsBox.Add(goalsNote);
            var viewer = _game.Players[Viewer];
            foreach (var goal in goals)
            {
                var owner = _game.GoalOwner(goal.Id);
                var row = Ui.Row();
                row.style.backgroundColor = Theme.PanelLight;
                row.style.marginBottom = 4f;
                row.style.alignItems = Align.Center;
                Ui.Radius(row, 6f);
                Ui.Padding(row, 5f, 8f);
                if (owner.HasValue) Ui.Border(row, 2f, Palette.PlayerColor(owner.Value));

                var texts = Ui.Column();
                texts.style.flexGrow = 1f;
                texts.style.flexShrink = 1f;
                texts.Add(Ui.Text(goal.Name, 13f, owner.HasValue ? Theme.TextDim : Theme.Text, true));
                string status = owner.HasValue
                    ? "выполнил: " + _game.Players[owner.Value].Name
                    : $"{goal.Describe(_game.Map)} · у вас {Math.Min(goal.Count, _game.GoalProgress(viewer.Index, goal))}/{goal.Count}";
                var statusLabel = Ui.Text(status, 11f, owner.HasValue ? Palette.PlayerColor(owner.Value) : Theme.TextDim);
                statusLabel.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(statusLabel);
                row.Add(texts);

                row.Add(Ui.Text("+" + goal.Points, 14f, owner.HasValue ? Theme.TextDim : Theme.Accent, true));
                _goalsBox.Add(row);
            }
        }

        public static Color EventColor(CityEventKind kind)
        {
            switch (kind)
            {
                case CityEventKind.RushHour: return new Color(1f, 0.60f, 0.20f);
                case CityEventKind.Repair: return new Color(0.95f, 0.36f, 0.36f);
                case CityEventKind.Subsidy: return new Color(0.45f, 0.75f, 1f);
                case CityEventKind.Parade: return new Color(0.80f, 0.50f, 1f);
                case CityEventKind.Snow: return new Color(0.85f, 0.92f, 1f);
                case CityEventKind.Investor: return new Color(1f, 0.85f, 0.30f);
                default: return new Color(0.40f, 0.85f, 0.50f);
            }
        }

        string LocationName(string locationId) => _game.Map.LocationName(locationId);

        string DistrictName(string districtId) => _game.Map.DistrictName(districtId);

        /// <summary>Подпись способа оплаты; cost — сколько карт стоит действие (с учётом скидок и событий).</summary>
        static string PaymentLabel(int cost, PaymentOption option)
        {
            if (option.Resource == Resource.Machinery) return $"Только техника × {option.Machinery}";
            string main = $"{Palette.CardTitle(option.Resource)} × {cost - option.Machinery}";
            return option.Machinery == 0 ? main : $"{main} + техника × {option.Machinery}";
        }
    }
}
