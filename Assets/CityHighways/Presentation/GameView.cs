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
    public sealed partial class GameView
    {
        const float TopBarHeight = 60f;
        const float BottomBarHeight = 72f;
        const float DrawerWidth = 380f;
        const float ContractCardWidth = 200f;
        const float ContractsSheetWidth = 760f;
        const float PlayersPanelWidth = 420f;
        const float BotDelay = 0.55f;
        /// <summary>Сколько последних записей хранит и показывает журнал (старые уходят).</summary>
        const int LogHistory = 200;
        const int ResourceCount = ResourceInfo.Count;
        static bool EnglishUi => LocalizationService.Language == "en";
        static string LuckCardTitle(LuckCardKind kind)
        {
            if (!EnglishUi) return LuckInfo.Title(kind);
            switch (kind)
            {
                case LuckCardKind.Grant: return "City Hall Grant";
                case LuckCardKind.Supply: return "Unexpected Supply";
                case LuckCardKind.Premium: return "Completion Bonus";
                case LuckCardKind.Fine: return "Inspection";
                case LuckCardKind.Theft: return "Warehouse Theft";
                default: return "Contract Recall";
            }
        }

        static string LuckCardDescription(LuckCardKind kind, GameConfig config)
        {
            if (!EnglishUi) return LuckInfo.Describe(kind, config);
            switch (kind)
            {
                case LuckCardKind.Grant: return config.InfluenceEnabled ? $"+{config.LuckGrantInfluence} influence." : $"+{config.LuckGrant} pts.";
                case LuckCardKind.Supply: return $"+{config.LuckSupply} resource cards from the deck.";
                case LuckCardKind.Premium: return $"+{config.LuckPremiumPerContract} pts per completed contract (maximum {config.LuckPremiumMax}).";
                case LuckCardKind.Fine: return $"−{config.LuckFine} pts.";
                case LuckCardKind.Theft: return "Discard a random card from your hand.";
                default: return $"Return your lowest-value unfinished contract to the bottom of the deck without a penalty. If none, lose {config.LuckFine} pts.";
            }
        }

        static string DisplayPlace(string name) => MagnateLocationText.Name(name);

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
            public Label Ability;
            public ProfileBadge Badge;
            public Label Title;
            public ProgressBar Pieces;
            public Label Score;
        }

        /// <summary>Игрок в нижней полосе: кружок его цвета с буквой и видимые очки.</summary>
        sealed class PlayerChip
        {
            public VisualElement Root;
            public VisualElement Dot;
            public Label Score;
        }

        /// <summary>Оформление игроков из профилей (эмблема, титул, рамка): за одним компьютером — по имени, в сети — от хоста.</summary>
        PlayerLook[] _looks = new PlayerLook[0];
        /// <summary>Сколько влияния ушло в профили после партии — показывается в итогах.</summary>
        List<Profiles.Credit> _profileCredits;

        PlayerLook LookOf(int index) => index >= 0 && index < _looks.Length ? _looks[index] : PlayerLook.None;

        /// <summary>Наряд персонажа игрока: из профиля; у ботов — случайный, но постоянный для имени.</summary>
        Outfit OutfitOf(int index)
        {
            bool bot;
            if (Online) bot = index < _net.Seats.Count && _net.Seats[index].isBot && string.IsNullOrEmpty(_net.Seats[index].peer);
            else bot = index < _setup.players.Count && _setup.players[index].isBot;
            return bot ? Outfit.ForBot(_game.Players[index].Name) : Outfit.Parse(LookOf(index).Outfit);
        }

        void BuildLooks()
        {
            _looks = new PlayerLook[_game.Players.Count];
            for (int i = 0; i < _looks.Length; i++)
            {
                if (Online)
                {
                    var seat = i < _net.Seats.Count ? _net.Seats[i] : null;
                    _looks[i] = seat == null ? PlayerLook.None
                        : new PlayerLook(seat.emblem, seat.title, seat.frame, seat.isBot && string.IsNullOrEmpty(seat.peer) ? "" : seat.outfit);
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
            /// <summary>Сколько каждому заплатили за проезд и какие дороги в пробке.</summary>
            public int[] Earned;
            /// <summary>Видимые очки без проезда (дороги, цели, удача, штрафы) — для всплывающих «+N».</summary>
            public int[] Points;
            public readonly HashSet<string> Jammed = new HashSet<string>();
            public readonly HashSet<string> Highways = new HashSet<string>();
        }

        /// <summary>Строка о финале под статусом: последний круг (ходов до конца, заказ мэрии) или «скоро финал».</summary>
        string FinalText()
        {
            if (_game.IsFinalRound)
            {
                int left = _game.FinalTurnsLeft;
                string text = EnglishUi ? (left <= 1 ? "Final round: this is the last turn!" : $"Final round: {left} turns remaining")
                    : (left <= 1 ? "Последний круг: это последний ход партии!" : $"Последний круг: до конца {left} {Plural(left, "ход", "хода", "ходов")}");
                var order = _game.FinalOrderRoad;
                if (order != null)
                    text += _game.FinalOrderWinner >= 0
                        ? (EnglishUi ? $"\nFinal city order completed by {_game.Players[_game.FinalOrderWinner].Name}" : $"\nФинальный заказ выполнил {_game.Players[_game.FinalOrderWinner].Name}")
                        : (EnglishUi ? $"\nFinal city order: {DisplayPlace(LocationName(order.A))} — {DisplayPlace(LocationName(order.B))} (+{_game.Config.FinalOrderBonus})" : $"\nФинальный заказ: {LocationName(order.A)} — {LocationName(order.B)} (+{_game.Config.FinalOrderBonus})");
                return text;
            }
            if (_game.FinalWarned)
            {
                int toFinal = _game.FewestPieces - _game.Config.FinalRoundPieces;
                return toFinal > 0 ? (EnglishUi ? $"Final round approaching: the building leader has {toFinal} road pieces above the final-round threshold" : $"Скоро финал: до последнего круга {toFinal} {Plural(toFinal, "участок", "участка", "участков")} у лидера стройки") : "";
            }
            return "";
        }

        static string Plural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return one;
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
            return many;
        }

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        public VisualElement Root { get; }
        bool _gamepadFocusActive;
        readonly GamepadFocusReturn _gamepadFocusReturn = new GamepadFocusReturn();
        readonly Label _gamepadGuide;

        Game _game;
        IBot[] _bots;
        readonly MapView _map;

        // Поле и плашка статуса
        readonly VisualElement _mapArea;
        readonly MiniMapView _minimap;
        readonly VisualElement _layersPanel, _layersList;
        int _layers;

        // ================================================================== Слои карты

        [Flags]
        public enum MapLayer { StationNames = 1, DistrictNames = 2, FreeRoads = 4, Cars = 8, People = 16, Focus = 32 }

        static (MapLayer layer, string title, string hint)[] LayerItems => new[]
        {
            (MapLayer.StationNames, LocalizationService.Get("game.ui.001"), LocalizationService.Get("game.ui.002")),
            (MapLayer.DistrictNames, LocalizationService.Get("game.ui.003"), LocalizationService.Get("game.ui.004")),
            (MapLayer.FreeRoads, LocalizationService.Get("game.ui.005"), LocalizationService.Get("game.ui.006")),
            (MapLayer.Cars, LocalizationService.Get("game.ui.007"), LocalizationService.Get("game.ui.008")),
            (MapLayer.People, LocalizationService.Get("game.ui.009"), LocalizationService.Get("game.ui.010")),
            (MapLayer.Focus, LocalizationService.Get("game.ui.011"), LocalizationService.Get("game.ui.012")),
        };

        bool HasLayer(MapLayer layer) => (_layers & (int)layer) != 0;

        void BuildLayersList()
        {
            _layersList.Clear();
            var caption = Ui.Text(LocalizationService.Get("game.ui.013"), 12f, Theme.TextDim, true);
            caption.style.marginBottom = 6f;
            _layersList.Add(caption);
            foreach (var item in LayerItems)
            {
                bool on = HasLayer(item.layer);
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
                row.name = "gamepad-layer-classic-" + (int)layer;
                void ToggleLayer()
                {
                    bool restore = _gamepadFocusActive && row.panel != null &&
                        row.panel.focusController.focusedElement == row;
                    _layers ^= (int)layer;
                    // Слои — личная настройка игрока: в сохранённые настройки, даже в тренировке и сетевой партии.
                    _setup.classicLayers = _layers;
                    var stored = GameSetup.Load();
                    stored.classicLayers = _layers;
                    stored.Save();
                    ApplyLayers();
                    BuildLayersList();
                    if (restore) UiGamepadFocus.RestoreNamedFocus(_layersList, row.name);
                }
                row.RegisterCallback<ClickEvent>(_ => ToggleLayer());
                GamepadSelectable.Attach(row, ToggleLayer);
                _layersList.Add(row);
            }

            // Optional analysis mode, separate from persistent map-layer preferences.
            bool networks = _map.OwnershipView;
            var networkButton = Ui.Button(EnglishUi
                ? (networks ? "✓ Networks: by owner" : "Networks: off")
                : (networks ? "✓ Сети: по владельцам" : "Сети: выключены"), () =>
            {
                _map.OwnershipView = !_map.OwnershipView;
                _map.Repaint();
                BuildLayersList();
            }, networks ? ButtonKind.Primary : ButtonKind.Secondary, 13f);
            networkButton.style.marginTop = 8f;
            _layersList.Add(networkButton);
            if (networks && _game != null)
            {
                for (int i = 0; i < _game.Players.Count; i++)
                {
                    var line = Ui.Row();
                    line.style.alignItems = Align.Center;
                    var marker = Ui.Box();
                    marker.style.width = 9f;
                    marker.style.height = 9f;
                    marker.style.marginRight = 6f;
                    marker.style.backgroundColor = Palette.PlayerColor(i);
                    Ui.Radius(marker, 5f);
                    line.Add(marker);
                    line.Add(Ui.Text(_game.Players[i].Name, 12f, Theme.Text));
                    _layersList.Add(line);
                }
            }
        }

        /// <summary>
        /// Способности на карте: мосты Мостовика подсвечены его цветом и подписаны «+N» (столько очков он за них
        /// получит), дороги Сборщика пошлин — мягким ореолом его цвета (проезд по ним дороже). Соперники видят,
        /// чья это выгода.
        /// </summary>
        void SetupAbilityMarks()
        {
            var game = _game;
            CharacterKind? OwnerCharacter(Road road)
            {
                var owner = game.GetRoadOwner(road.Id);
                if (!owner.HasValue || owner.Value < 0 || owner.Value >= game.Players.Count) return null;
                return game.Players[owner.Value].Character;
            }
            bool Applies(Road road)
            {
                var kind = OwnerCharacter(road);
                return kind == CharacterKind.Bridger && road.IsBridge || kind == CharacterKind.Tollman;
            }
            _map.AbilityGlow = road => Applies(road) ? Palette.PlayerColor(game.GetRoadOwner(road.Id).Value) : (Color?)null;
            _map.RoadBadge = road => OwnerCharacter(road) == CharacterKind.Bridger && road.IsBridge && game.Config.BridgerPointsPerBridge > 0
                ? $"+{game.Config.BridgerPointsPerBridge}" : null;
            _map.RoadBadgeColor = road => game.GetRoadOwner(road.Id) is int owner ? Palette.PlayerColor(owner) : (Color?)null;
        }

        void ApplyLayers()
        {
            if (_game == null) return;
            _map.SetLabelLayers(HasLayer(MapLayer.StationNames), HasLayer(MapLayer.DistrictNames));
            _map.FreeRoadAlpha = HasLayer(MapLayer.FreeRoads) ? 1f : 0f;
            _map.FocusRadius = HasLayer(MapLayer.Focus) ? 230f : 0f;
            if (_city3D != null)
            {
                _city3D.ShowTraffic = HasLayer(MapLayer.Cars);
                _city3D.ShowPeople = HasLayer(MapLayer.People);
            }
        }
        readonly VisualElement _statusBanner;
        readonly VisualElement _statusChip;
        readonly Label _statusName;
        readonly Label _statusText;
        readonly Label _finalRound;
        readonly Label _flashLabel;
        readonly UnityEngine.UIElements.Button _passButton;

        // Верхняя панель: логотип, ход, событие, ресурсы, меню и настройки
        readonly VisualElement _topBar;
        readonly Label _logo;
        readonly Label _turnLabel;
        readonly VisualElement _eventPill;
        readonly VisualElement _eventPillDot;
        readonly Label _eventPillText;
        readonly Label _resourcesCaption;
        readonly VisualElement _handRow;
        readonly Dictionary<Resource, ResourceChip> _hand = new Dictionary<Resource, ResourceChip>();
        readonly Label _piecesLabel;
        readonly ProgressBar _piecesBar;
        readonly Label _handTitle;
        readonly IconButton _menuButton;
        readonly VisualElement _menuDot;

        // Нижняя полоса: вкладки
        readonly UnityEngine.UIElements.Button _actionsTab, _contractsTab, _playersTab, _logTab;
        readonly VisualElement _playerChipsRow;
        PlayerChip[] _playerChips = new PlayerChip[0];
        readonly Label _viewHint;

        // Выезжающие панели: действия, контракты, журнал
        enum SheetKind { None, Actions, Contracts, Log }
        SheetKind _sheet = SheetKind.None;
        readonly VisualElement _sheetHost;
        readonly VisualElement _actionsSheet, _contractsSheet, _logSheet;
        readonly VisualElement _faceUpRow;
        readonly VisualElement _actionsLayoutRow, _actionButtons;
        CardView[] _faceUp = new CardView[0];
        readonly UnityEngine.UIElements.Button _deckButton;
        readonly UnityEngine.UIElements.Button _luckButton;
        readonly UnityEngine.UIElements.Button _tradeButton;
        readonly UnityEngine.UIElements.Button _influenceButton;
        readonly UnityEngine.UIElements.Button _contractsButton;
        readonly Label _luckOdds;
        readonly VisualElement _contractsBox;
        readonly CardCarousel _contractCarousel, _choiceCarousel;
        readonly ScrollView _logPage;
        readonly VisualElement _logBox;
        /// <summary>Сколько записей журнала было, когда его открывали последний раз (для «•» на вкладке).</summary>
        int _logSeenTotal;
        /// <summary>Ключ «ход:игрок:фаза», для которого панель уже открывалась сама (повторно не навязываем).</summary>
        string _autoSheetKey;

        // Боковое меню
        readonly VisualElement _drawer;
        bool _drawerOpen;
        readonly VisualElement _eventCard;
        readonly VisualElement _eventChip;
        readonly Label _eventTitle;
        readonly Label _eventText;
        readonly Label _goalsTitle;
        readonly ScrollView _goalsPage;
        readonly VisualElement _goalsBox;
        readonly Label _drawerInfo;
        int _seenGoalOwners;   // сколько целей было взято, когда меню открывали последний раз

        // Окна: настройки, правила. «Игроки» — не окно, а панель справа на игровом поле.
        enum Modal { None, Settings, Rules }
        Modal _modal = Modal.None;
        readonly VisualElement _modalLayer;
        readonly VisualElement _settingsModal, _rulesModal;
        readonly VisualElement _playersPanel;
        bool _playersOpen;
        readonly VisualElement _settingsBody, _rulesBody;
        readonly VisualElement _playersBox;
        PlayerRow[] _playerRows = new PlayerRow[0];

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
        string _flashPrefixKey;
        float _flashUntil;
        string _renderedLanguage;
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
        /// <summary>_logTotal на момент последней отрисовки журнала; −1 — журнал надо собрать заново.</summary>
        int _logShownTotal = -1;
        /// <summary>Журнал прокручен до конца: новые записи держат его внизу. Прокрутил вверх — журнал стоит на месте.</summary>
        bool _logStick = true;
        UnityEngine.UIElements.Button _logNewButton;
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
            Root.style.backgroundColor = Theme.Background;
            Root.RegisterCallback<PointerDownEvent>(_ => _gamepadFocusActive = false);
            // Capture UI Submit before it can activate a focused HUD control
            // while the player is intentionally choosing a map target.
            Root.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                if (_map == null || !_map.IsGamepadTargeting || evt.target == _map) return;
                evt.PreventDefault();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);

            // Макет: карта на весь экран; сверху — ход, событие, журнал и меню, снизу — вкладки, игроки и ресурсы.
            // Всё остальное — выезжающие панели (действия, контракты, журнал), боковое меню и окна.

            // ---------- Игровое поле
            _mapArea = Ui.Box();
            _mapArea.style.position = Position.Absolute;
            _mapArea.style.left = 0f;
            _mapArea.style.right = 0f;
            _mapArea.style.top = TopBarHeight;
            _mapArea.style.bottom = BottomBarHeight;
            Root.Add(_mapArea);

            _map = new MapView();
            _map.CanGamepadTarget = () => GamepadMapTargetPolicy.AllowsInput(
                _game != null && HumanToAct,
                _overlayKind != OverlayKind.None,
                _modal != Modal.None || _drawerOpen || _playersOpen,
                NeedsHandoff, false);
            // Дороги лежат на земле 3D-города: камеру можно крутить, и они поворачиваются вместе с ней.
            _map.GroundRoads = true;
            Ui.Fill(_map);
            _map.Hovered += OnMapHover;
            _map.LocationHovered += OnLocationHover;
            _map.RoadClicked += OnRoadClicked;
            _mapArea.Add(_map);

            // Мини-карта в левом нижнем углу поля.
            _minimap = new MiniMapView(_map, 236f, 150f);
            _minimap.style.position = Position.Absolute;
            _minimap.style.left = 14f;
            _minimap.style.bottom = 14f;
            _mapArea.Add(_minimap);

            // Кооператив: заказы города и кризис — в правом верхнем углу поля.
            _coopPanel = new CoopPanel(contract =>
            {
                if (contract == null) _map.ClearContractHighlight();
                else _map.SetContractHighlight(contract.DistrictA, contract.DistrictB, contract.IsComplex ? contract.DistrictC : null);
            });
            Ui.Show(_coopPanel, false);
            _coopPanel.style.top = 58f; // под кнопкой «Слои карты»
            _mapArea.Add(_coopPanel);

            // Статус хода — плашка сверху по центру поля.
            _statusBanner = Ui.Row();
            _statusBanner.style.position = Position.Absolute;
            _statusBanner.style.top = 12f;
            _statusBanner.style.left = Length.Percent(50f);
            _statusBanner.style.translate = new Translate(Length.Percent(-50f), new Length(0f));
            _statusBanner.style.maxWidth = 760f;
            _statusBanner.style.alignItems = Align.Center;
            _statusBanner.style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(_statusBanner, 12f);
            Ui.Border(_statusBanner, 1f, Theme.Line);
            Ui.Padding(_statusBanner, 10f, 16f);
            Ui.Shadow(_statusBanner, 12f);
            _mapArea.Add(_statusBanner);

            // Слои карты: кнопка в правом верхнем углу поля, по клику — список переключателей (как в магнате).
            _layers = GameSetup.Load().classicLayers;
            _layersPanel = new VisualElement();
            _layersPanel.style.position = Position.Absolute;
            _layersPanel.style.right = 14f;
            _layersPanel.style.top = 14f;
            _layersPanel.style.alignItems = Align.FlexEnd;
            var layersButton = Ui.Button(LocalizationService.Get("game.ui.014"), () =>
            {
                bool open = _layersList.style.display == DisplayStyle.None;
                _app.Sound.Play(open ? Sfx.Open : Sfx.Close, 0.4f);
                Ui.Show(_layersList, open);
            }, ButtonKind.Secondary, 13f);
            layersButton.style.minHeight = 34f;
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

            _statusChip = Ui.Box();
            _statusChip.style.width = 14f;
            _statusChip.style.height = 14f;
            _statusChip.style.marginRight = 12f;
            _statusChip.style.flexShrink = 0f;
            Ui.Radius(_statusChip, 7f);
            Ui.Transitions(_statusChip, "background-color");
            _statusBanner.Add(_statusChip);
            var status = Ui.Column();
            status.style.flexShrink = 1f;
            _statusBanner.Add(status);
            _statusName = Ui.Text("", 18f, null, true);
            status.Add(_statusName);
            _statusText = Ui.Text("", 14f, Theme.Text);
            _statusText.style.marginTop = 2f;
            status.Add(_statusText);
            _finalRound = Ui.Text("", 14f, Theme.Gold, true);
            _finalRound.style.whiteSpace = WhiteSpace.Normal;
            _finalRound.style.marginTop = 4f;
            status.Add(_finalRound);
            _flashLabel = Ui.Text("", 14f, Theme.Danger);
            _flashLabel.style.marginTop = 4f;
            status.Add(_flashLabel);
            _passButton = Ui.Button(LocalizationService.Get("game.ui.015"), () => Act(() => _game.Pass()), ButtonKind.Primary, 14f);
            _passButton.style.marginLeft = 14f;
            _passButton.style.flexShrink = 0f;
            _statusBanner.Add(_passButton);

            // ---------- Выезжающие снизу панели (над нижней полосой)
            _sheetHost = Ui.Box();
            _sheetHost.style.position = Position.Absolute;
            _sheetHost.style.left = 0f;
            _sheetHost.style.right = 0f;
            _sheetHost.style.bottom = BottomBarHeight;
            _sheetHost.style.top = TopBarHeight;
            Root.Add(_sheetHost);

            VisualElement Sheet(string title, float width, Action onClose, out VisualElement body)
            {
                var sheet = Ui.Column();
                sheet.pickingMode = PickingMode.Position;
                sheet.style.position = Position.Absolute;
                sheet.style.bottom = 0f;
                sheet.style.left = Length.Percent(50f);
                sheet.style.width = width;
                sheet.style.marginLeft = -width / 2f;
                sheet.style.backgroundColor = Theme.PanelGlass;
                sheet.style.borderTopLeftRadius = 14f;
                sheet.style.borderTopRightRadius = 14f;
                sheet.style.borderTopWidth = 1f;
                sheet.style.borderLeftWidth = 1f;
                sheet.style.borderRightWidth = 1f;
                sheet.style.borderTopColor = Theme.Line;
                sheet.style.borderLeftColor = Theme.Line;
                sheet.style.borderRightColor = Theme.Line;
                Ui.Padding(sheet, 12f, 18f);
                Ui.Shadow(sheet, 14f);
                var header = Ui.Row();
                header.style.marginBottom = 8f;
                header.Add(Ui.Text(title, 20f, Theme.Text, true));
                var spacer = Ui.Box();
                spacer.style.flexGrow = 1f;
                header.Add(spacer);
                header.Add(new IconButton(IconButton.Glyph.Close, "", onClose, 12f));
                sheet.Add(header);
                body = Ui.Column();
                body.style.flexGrow = 1f;
                body.style.flexShrink = 1f;
                body.style.minHeight = 0f;
                sheet.Add(body);
                sheet.style.display = DisplayStyle.None;
                sheet.style.opacity = 0f;
                _sheetHost.Add(sheet);
                return sheet;
            }

            // Действия: открытый ряд карт и кнопки хода.
            _actionsSheet = Sheet("Действия", 860f, CloseSheet, out var actionsBody);
            _actionsSheet.style.maxHeight = Length.Percent(100f);
            var actionsRow = Ui.Row();
            _actionsLayoutRow = actionsRow;
            actionsRow.style.alignItems = Align.FlexStart;
            var actionsScroll = new ScrollView(ScrollViewMode.Vertical);
            actionsScroll.style.flexGrow = 1f;
            actionsScroll.style.flexShrink = 1f;
            actionsScroll.style.minHeight = 0f;
            Ui.StyleScroller(actionsScroll);
            actionsBody.Add(actionsScroll);
            actionsScroll.Add(actionsRow);
            _faceUpRow = Ui.Row();
            _faceUpRow.style.marginRight = 18f;
            actionsRow.Add(_faceUpRow);
            var actionButtons = Ui.Column();
            _actionButtons = actionButtons;
            actionButtons.style.flexGrow = 1f;
            actionsRow.Add(actionButtons);
            var firstRow = Ui.Row();
            var secondRow = Ui.Row();
            secondRow.style.marginTop = 8f;
            actionButtons.Add(firstRow);
            actionButtons.Add(secondRow);
            UnityEngine.UIElements.Button ActionButton(VisualElement row, string text, Action onClick, ButtonKind kind, bool last = false)
            {
                var button = Ui.Button(text, onClick, kind, 14f);
                button.style.flexGrow = 1f;
                button.style.flexBasis = 0f;
                button.style.paddingLeft = 6f;
                button.style.paddingRight = 6f;
                if (!last) button.style.marginRight = 8f;
                row.Add(button);
                return button;
            }
            _deckButton = ActionButton(firstRow, LocalizationService.Get("game.ui.016"), () => Act(DrawFromDeck), ButtonKind.Primary);
            _luckButton = ActionButton(firstRow, LocalizationService.Get("game.ui.017"), () => Act(() => _game.DrawLuckCard()), ButtonKind.Secondary, true);
            _contractsButton = ActionButton(secondRow, LocalizationService.Get("game.ui.018"), () => Act(() => _game.DrawContracts()), ButtonKind.Secondary);
            _influenceButton = ActionButton(secondRow, LocalizationService.Get("game.ui.019"), ShowInfluenceMenu, ButtonKind.Secondary);
            _tradeButton = ActionButton(secondRow, LocalizationService.Get("game.ui.020"), ShowTradeComposer, ButtonKind.Secondary, true);
            _luckOdds = Ui.Text("", 13f, Theme.TextDim);
            _luckOdds.style.marginTop = 10f;
            actionButtons.Add(_luckOdds);

            // Контракты: карточки с swipe-навигацией (и выбор новых контрактов).
            _contractsSheet = Sheet(LocalizationService.Get("game.ui.018"), ContractsSheetWidth, CloseSheet, out var contractsBody);
            // Не выше поля: если выбор персонажа и контрактов не помещается — прокрутка.
            _contractsSheet.style.maxHeight = Length.Percent(100f);
            var contractsScroll = new ScrollView(ScrollViewMode.Vertical);
            contractsScroll.style.flexShrink = 1f;
            contractsScroll.style.minHeight = 0f;
            Ui.StyleScroller(contractsScroll);
            contractsBody.Add(contractsScroll);
            _contractsBox = Ui.Column();
            contractsScroll.Add(_contractsBox);
            _contractCarousel = new CardCarousel(ContractCardWidth, 12f);
            _contractCarousel.Snapped += () => _app.Sound.Play(Sfx.Tick, 0.5f);
            _choiceCarousel = new CardCarousel(ContractCardWidth, 12f);
            _choiceCarousel.Snapped += () => _app.Sound.Play(Sfx.Tick, 0.5f);

            // Журнал.
            // Кнопка журнала наверху, поэтому и панель выезжает сверху справа, под верхней полосой.
            _logSheet = Sheet(LocalizationService.Get("game.ui.021"), 640f, CloseSheet, out var logBody);
            _logSheet.style.height = 360f;
            _logSheet.style.bottom = StyleKeyword.Auto;
            _logSheet.style.top = 0f;
            _logSheet.style.left = StyleKeyword.Auto;
            _logSheet.style.right = 12f;
            _logSheet.style.marginLeft = 0f;
            _logSheet.style.borderTopLeftRadius = 0f;
            _logSheet.style.borderTopRightRadius = 0f;
            _logSheet.style.borderBottomLeftRadius = 14f;
            _logSheet.style.borderBottomRightRadius = 14f;
            _logSheet.style.borderTopWidth = 0f;
            _logSheet.style.borderBottomWidth = 1f;
            _logSheet.style.borderBottomColor = Theme.Line;
            var logHost = Ui.Column();
            logHost.style.flexGrow = 1f;
            logHost.style.flexShrink = 1f;
            logHost.style.minHeight = 0f;
            logHost.style.overflow = Overflow.Hidden;
            logBody.Add(logHost);
            _logBox = Ui.Column();
            _logPage = new ScrollView(ScrollViewMode.Vertical);
            _logPage.style.flexGrow = 1f;
            _logPage.style.flexShrink = 1f;
            _logPage.style.minHeight = 0f;
            Ui.StyleScroller(_logPage);
            _logPage.Add(_logBox);
            logHost.Add(_logPage);
            // Журнал держится внизу, пока игрок сам не прокрутит его вверх.
            _logPage.verticalScroller.valueChanged += value =>
            {
                _logStick = value >= _logPage.verticalScroller.highValue - 4f;
                if (_logStick && _logNewButton != null) Ui.Show(_logNewButton, false);
            };
            _logPage.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => KeepLogAtEnd());
            _logPage.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => KeepLogAtEnd());
            _logNewButton = Ui.Button(LocalizationService.Get("game.ui.022"), () =>
            {
                _logStick = true;
                Ui.Show(_logNewButton, false);
                ScrollLogToEnd();
            }, ButtonKind.Secondary, 12f);
            _logNewButton.style.position = Position.Absolute;
            _logNewButton.style.bottom = 8f;
            _logNewButton.style.alignSelf = Align.Center;
            Ui.Show(_logNewButton, false);
            logHost.Add(_logNewButton);

            // ---------- Верхняя панель (поверх поля — чтобы тень ложилась на карту)
            _topBar = Ui.Row();
            _topBar.pickingMode = PickingMode.Position;
            _topBar.style.position = Position.Absolute;
            _topBar.style.left = 0f;
            _topBar.style.right = 0f;
            _topBar.style.top = 0f;
            _topBar.style.height = TopBarHeight;
            _topBar.style.backgroundColor = Theme.Panel;
            _topBar.style.borderBottomWidth = 1f;
            _topBar.style.borderBottomColor = Theme.Line;
            Ui.Padding(_topBar, 0f, 16f);
            Ui.Shadow(_topBar, 0f, 12f, 0.5f);
            Root.Add(_topBar);

            _logo = Ui.Text("City Highways", 20f, Theme.Text, true);
            Fonts.Apply(_logo, Fonts.Weight.Logo);
            _logo.style.whiteSpace = WhiteSpace.NoWrap;
            _logo.style.marginRight = 14f;
            _topBar.Add(_logo);
            var divider = Ui.Box();
            divider.style.width = 1f;
            divider.style.height = 26f;
            divider.style.backgroundColor = Theme.Line;
            divider.style.marginRight = 12f;
            _topBar.Add(divider);
            _turnLabel = Ui.Text("", 16f, null, true);
            _turnLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _turnLabel.style.marginRight = 12f;
            _topBar.Add(_turnLabel);

            // Событие круга — цветная «пилюля»; подробности — в подсказке и в меню.
            _eventPill = Ui.Row();
            _eventPill.pickingMode = PickingMode.Position;
            _eventPill.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(_eventPill, 14f);
            Ui.Padding(_eventPill, 4f, 12f);
            Ui.Border(_eventPill, 1f, Theme.Line);
            Ui.Transitions(_eventPill, "background-color", "border-top-color", "border-bottom-color", "border-left-color", "border-right-color");
            _eventPillDot = Ui.Box();
            _eventPillDot.style.width = 8f;
            _eventPillDot.style.height = 8f;
            _eventPillDot.style.marginRight = 8f;
            Ui.Radius(_eventPillDot, 4f);
            _eventPill.Add(_eventPillDot);
            _eventPillText = Ui.Text("", 13f, null, true);
            _eventPillText.style.whiteSpace = WhiteSpace.NoWrap;
            _eventPill.Add(_eventPillText);
            _eventPill.RegisterCallback<ClickEvent>(_ => ToggleDrawer());
            AttachTooltip(_eventPill, () => _game == null ? "" : _eventText.text);
            _topBar.Add(_eventPill);

            var spaceLeft = Ui.Box();
            spaceLeft.style.flexGrow = 1f;
            _topBar.Add(spaceLeft);

            var spaceRight = Ui.Box();
            spaceRight.style.flexGrow = 1f;
            _topBar.Add(spaceRight);

            // Undo is visible but disabled unless the offline replay policy
            // permits reversing the latest action; keyboard/controller focus
            // uses the standard UI Toolkit button path.
            _undoButton = Ui.Button(LocalizationService.Get("game.undo.action"),
                UndoLastAction, ButtonKind.Ghost, 13f);
            _undoButton.style.marginRight = 6f;
            _undoButton.style.minHeight = 40f;
            _topBar.Add(_undoButton);
            _redoButton = Ui.Button(LocalizationService.Get("game.redo.action"),
                RedoLastAction, ButtonKind.Ghost, 13f);
            _redoButton.style.marginRight = 6f;
            _redoButton.style.minHeight = 40f;
            _topBar.Add(_redoButton);

            // Журнал — наверху, рядом с меню; панель журнала выезжает сверху справа.
            _logTab = Ui.Button(LocalizationService.Get("game.ui.021"), () => ToggleSheet(SheetKind.Log), ButtonKind.Ghost, 14f);
            _logTab.style.marginRight = 8f;
            _logTab.style.minHeight = 40f;
            Ui.Border(_logTab, 0f, Color.clear);
            _logTab.style.borderBottomWidth = 3f;
            Ui.Transitions(_logTab, "background-color", "color", "border-bottom-color");
            _topBar.Add(_logTab);

            _menuButton = new IconButton(IconButton.Glyph.Menu, LocalizationService.Get("game.hud.menu"), ToggleDrawer);
            _menuButton.style.marginRight = 8f;
            _menuDot = Ui.Box();
            _menuDot.style.position = Position.Absolute;
            _menuDot.style.top = 4f;
            _menuDot.style.right = 4f;
            _menuDot.style.width = 8f;
            _menuDot.style.height = 8f;
            _menuDot.style.backgroundColor = Theme.Accent;
            Ui.Radius(_menuDot, 4f);
            Ui.Show(_menuDot, false);
            _menuButton.Add(_menuDot);
            _topBar.Add(_menuButton);
            _topBar.Add(new IconButton(IconButton.Glyph.Gear, LocalizationService.Get("game.hud.settings"), () => ToggleModal(Modal.Settings)));

            // ---------- Нижняя полоса: вкладки
            var bottomBar = Ui.Row();
            bottomBar.pickingMode = PickingMode.Position;
            bottomBar.style.position = Position.Absolute;
            bottomBar.style.left = 0f;
            bottomBar.style.right = 0f;
            bottomBar.style.bottom = 0f;
            bottomBar.style.height = BottomBarHeight;
            bottomBar.style.backgroundColor = Theme.Panel;
            bottomBar.style.borderTopWidth = 1f;
            bottomBar.style.borderTopColor = Theme.Line;
            Ui.Padding(bottomBar, 0f, 14f);
            Ui.Shadow(bottomBar, 0f, 12f, 0.5f);
            Root.Add(bottomBar);

            // Три части: слева вкладки, по центру «Игроки», справа от них — ресурсы.
            // Левая и правая части одинаково тянутся (основа 0), поэтому «Игроки» стоят ровно по центру.
            var bottomLeft = Ui.Row();
            bottomLeft.style.flexGrow = 1f;
            bottomLeft.style.flexBasis = 0f;
            bottomLeft.style.minWidth = 0f;
            bottomLeft.style.height = Length.Percent(100f);
            bottomBar.Add(bottomLeft);
            var bottomCenter = Ui.Row();
            bottomCenter.style.flexShrink = 0f;
            bottomCenter.style.height = Length.Percent(100f);
            bottomBar.Add(bottomCenter);
            var resourcesRow = Ui.Row();
            resourcesRow.style.flexGrow = 1f;
            resourcesRow.style.flexBasis = 0f;
            resourcesRow.style.minWidth = 0f;
            resourcesRow.style.height = Length.Percent(100f);
            resourcesRow.style.paddingLeft = 10f;
            bottomBar.Add(resourcesRow);

            UnityEngine.UIElements.Button Tab(VisualElement parent, string text, Action onClick)
            {
                var button = Ui.Button(text, onClick, ButtonKind.Ghost, 15f);
                button.style.marginRight = 8f;
                button.style.minHeight = 52f;
                Ui.Border(button, 0f, Color.clear);
                button.style.borderBottomWidth = 3f;
                Ui.Transitions(button, "background-color", "color", "border-bottom-color", "translate");
                parent.Add(button);
                return button;
            }
            _actionsTab = Tab(bottomLeft, LocalizationService.Get("game.hud.actions"), () => ToggleSheet(SheetKind.Actions));
            _contractsTab = Tab(bottomLeft, LocalizationService.Get("game.hud.contracts"), () => ToggleSheet(SheetKind.Contracts));
            _viewHint = Ui.Text(LocalizationService.Get("game.ui.023"), 12f, Theme.TextDim);
            _viewHint.style.whiteSpace = WhiteSpace.NoWrap;
            _viewHint.style.marginLeft = 10f;
            _viewHint.style.flexShrink = 1f;
            _viewHint.style.overflow = Overflow.Hidden;
            bottomLeft.Add(_viewHint);
            // У кнопки «Игроки» внутри — подпись и портреты игроков из профилей (текст кнопки пустой, иначе наложится).
            _playersTab = Tab(bottomCenter, "", TogglePlayers);
            _playersTab.style.marginRight = 0f;
            _playersTab.style.flexDirection = FlexDirection.Row;
            _playersTab.style.alignItems = Align.Center;
            var playersCaption = Ui.Text(LocalizationService.Get("game.hud.players"), 15f, null, true);
            _playersTab.Add(playersCaption);
            _playerChipsRow = Ui.Row();
            _playerChipsRow.style.marginLeft = 6f;
            _playersTab.Add(_playerChipsRow);

            // Ресурсы: карты в руке (деления + число) и оставшиеся участки дороги.
            _resourcesCaption = Ui.Text(LocalizationService.Get("game.hud.resources"), 11f, Theme.TextDim, true);
            _resourcesCaption.style.letterSpacing = 1.5f;
            _resourcesCaption.style.marginRight = 10f;
            resourcesRow.Add(_resourcesCaption);
            _handRow = Ui.Row();
            resourcesRow.Add(_handRow);
            foreach (var color in ResourceInfo.Building.Concat(new[] { Resource.Machinery }))
            {
                var chip = new ResourceChip(color);
                var resource = color;
                AttachTooltip(chip, () => ResourceTooltip(resource));
                _hand[color] = chip;
                _handRow.Add(chip);
            }
            var pieces = Ui.Column();
            pieces.pickingMode = PickingMode.Position;
            pieces.style.marginLeft = 6f;
            var piecesTop = Ui.Row();
            piecesTop.Add(Ui.Text(LocalizationService.Get("game.ui.024"), 11f, Theme.TextDim, true));
            _piecesLabel = Ui.Number("", 14f, Theme.Text, true);
            _piecesLabel.style.marginLeft = 6f;
            piecesTop.Add(_piecesLabel);
            pieces.Add(piecesTop);
            _piecesBar = new ProgressBar(86f, 6f, Theme.Success);
            _piecesBar.style.marginTop = 4f;
            pieces.Add(_piecesBar);
            AttachTooltip(pieces, () => _game == null ? "" : PiecesTooltip());
            resourcesRow.Add(pieces);
            _handTitle = Ui.Text("", 12f, Theme.TextDim, true);
            _handTitle.style.marginLeft = 10f;
            resourcesRow.Add(_handTitle);


            // ---------- Боковое меню (☰): событие, цели мэрии, правила, выход
            _drawer = Ui.Column();
            _drawer.pickingMode = PickingMode.Position;
            _drawer.style.position = Position.Absolute;
            _drawer.style.right = 0f;
            _drawer.style.top = TopBarHeight;
            _drawer.style.bottom = BottomBarHeight;
            _drawer.style.width = DrawerWidth;
            _drawer.style.backgroundColor = Theme.PanelGlass;
            _drawer.style.borderLeftWidth = 1f;
            _drawer.style.borderLeftColor = Theme.Line;
            Ui.Padding(_drawer, 16f, 18f);
            Ui.Shadow(_drawer, 0f, 16f, 0.55f);
            _drawer.style.display = DisplayStyle.None;
            _drawer.style.opacity = 0f;
            Root.Add(_drawer);

            var drawerHeader = Ui.Row();
            drawerHeader.style.marginBottom = 12f;
            drawerHeader.Add(Ui.Text(LocalizationService.Get("game.ui.025"), 22f, Theme.Text, true));
            var drawerSpace = Ui.Box();
            drawerSpace.style.flexGrow = 1f;
            drawerHeader.Add(drawerSpace);
            drawerHeader.Add(new IconButton(IconButton.Glyph.Close, "", CloseDrawer, 12f));
            _drawer.Add(drawerHeader);

            _eventCard = Ui.Column();
            _eventCard.style.marginBottom = 14f;
            Ui.Padding(_eventCard, 10f, 12f);
            Ui.Radius(_eventCard, 10f);
            _eventCard.style.backgroundColor = Theme.PanelLight;
            var eventHeader = Ui.Row();
            eventHeader.style.alignItems = Align.Center;
            _eventChip = Ui.Box();
            _eventChip.style.width = 10f;
            _eventChip.style.height = 10f;
            _eventChip.style.marginRight = 8f;
            Ui.Radius(_eventChip, 5f);
            eventHeader.Add(_eventChip);
            _eventTitle = Ui.Text("", 16f, null, true);
            eventHeader.Add(_eventTitle);
            _eventCard.Add(eventHeader);
            _eventText = Ui.Text("", 14f, Theme.TextDim);
            _eventText.style.marginTop = 4f;
            _eventText.style.whiteSpace = WhiteSpace.Normal;
            _eventCard.Add(_eventText);
            Ui.Show(_eventCard, false);
            _drawer.Add(_eventCard);

            _goalsTitle = Ui.SectionTitle(LocalizationService.Get("game.ui.026"));
            _drawer.Add(_goalsTitle);
            _goalsBox = Ui.Column();
            _goalsPage = new ScrollView(ScrollViewMode.Vertical);
            _goalsPage.style.flexGrow = 1f;
            _goalsPage.style.flexShrink = 1f;
            _goalsPage.style.minHeight = 0f;
            Ui.StyleScroller(_goalsPage);
            _goalsPage.Add(_goalsBox);
            _drawer.Add(_goalsPage);

            _drawerInfo = Ui.Text("", 13f, Theme.TextDim);
            _drawerInfo.style.marginTop = 10f;
            _drawerInfo.style.marginBottom = 12f;
            _drawer.Add(_drawerInfo);
            var drawerButtons = Ui.Row();
            var rulesButton = Ui.Button(LocalizationService.Get("game.ui.027"), () => ToggleModal(Modal.Rules), ButtonKind.Secondary, 14f);
            rulesButton.style.flexGrow = 1f;
            rulesButton.style.marginRight = 8f;
            drawerButtons.Add(rulesButton);
            var exitButton = Ui.Button(LocalizationService.Get("game.ui.028"), ShowConfirmExit, ButtonKind.Ghost, 14f);
            exitButton.style.flexGrow = 1f;
            drawerButtons.Add(exitButton);
            _drawer.Add(drawerButtons);

            // ---------- Окна: игроки, настройки, правила (затемнение #00000080)
            _modalLayer = Ui.Column();
            Ui.Fill(_modalLayer);
            _modalLayer.pickingMode = PickingMode.Position;
            _modalLayer.style.backgroundColor = Theme.Shade;
            _modalLayer.style.alignItems = Align.Center;
            _modalLayer.style.justifyContent = Justify.Center;
            _modalLayer.style.display = DisplayStyle.None;
            _modalLayer.style.opacity = 0f;
            _modalLayer.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == _modalLayer) CloseModal();
            });
            Root.Add(_modalLayer);

            // ---------- Панель «Игроки»: справа на игровом поле, поле за ней не затемняется.
            var playersDock = Ui.Column();
            playersDock.style.position = Position.Absolute;
            playersDock.style.top = 12f;
            playersDock.style.bottom = 12f;
            playersDock.style.right = 12f;
            playersDock.style.width = PlayersPanelWidth;
            _sheetHost.Add(playersDock);
            _playersPanel = Ui.Column();
            _playersPanel.pickingMode = PickingMode.Position;
            _playersPanel.style.flexShrink = 1f;
            _playersPanel.style.minHeight = 0f;
            _playersPanel.style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(_playersPanel, 14f);
            Ui.Border(_playersPanel, 1f, Theme.Line);
            Ui.Padding(_playersPanel, 12f, 14f);
            Ui.Shadow(_playersPanel, 14f);
            var playersHeader = Ui.Row();
            playersHeader.style.marginBottom = 8f;
            playersHeader.Add(Ui.Text(LocalizationService.Get("game.hud.players"), 20f, Theme.Text, true));
            var playersHeaderSpace = Ui.Box();
            playersHeaderSpace.style.flexGrow = 1f;
            playersHeader.Add(playersHeaderSpace);
            playersHeader.Add(new IconButton(IconButton.Glyph.Close, "", ClosePlayers, 12f));
            _playersPanel.Add(playersHeader);
            var playersScroll = new ScrollView(ScrollViewMode.Vertical);
            playersScroll.style.flexShrink = 1f;
            playersScroll.style.minHeight = 0f;
            Ui.StyleScroller(playersScroll);
            _playersBox = Ui.Column();
            playersScroll.Add(_playersBox);
            _playersPanel.Add(playersScroll);
            _playersPanel.style.display = DisplayStyle.None;
            _playersPanel.style.opacity = 0f;
            playersDock.Add(_playersPanel);
            _settingsModal = ModalCard(LocalizationService.Get("game.hud.settings"), 480f, out _settingsBody);
            _rulesModal = ModalCard(LocalizationService.Get("game.hud.rules"), 1200f, out _rulesBody);

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
            _tooltip.pickingMode = PickingMode.Ignore;
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
            _toastHolder.style.right = 0f;
            _toastHolder.pickingMode = PickingMode.Ignore;
            _toastHolder.style.top = TopBarHeight + 120f;
            _toastHolder.style.justifyContent = Justify.Center;
            _toast = Ui.Text("", 20f, Theme.Gold, true);
            _toast.pickingMode = PickingMode.Ignore;
            _toast.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
            _toast.style.whiteSpace = WhiteSpace.Normal;
            _toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            Ui.Radius(_toast, 22f);
            Ui.Padding(_toast, 10f, 24f);
            _toastHolder.Add(_toast);
            Ui.Show(_toastHolder, false);
            _fx.Add(_toastHolder);

            _gamepadGuide = GamepadControlGuide.Create();
            _gamepadGuide.style.bottom = BottomBarHeight + 8f;
            // Bottom-right help is purely informative and cannot intercept
            // UI clicks. Hide it while the map cursor or a modal is active.
            Root.Add(_gamepadGuide);
            Root.RegisterCallback<GeometryChangedEvent>(_ => AdaptLayout());
        }

        // ================================================================== Партия

        public void StartNewGame()
        {
            Game game;
            try
            {
                var map = CityHighwaysApp.LoadMap(_setup.mapResourcePath);
                int seed = UnityEngine.Random.Range(0, int.MaxValue);
                if (_setup.tutorial)
                {
                    // Обучение: одна и та же раздача, без персонажей и лишних правил.
                    seed = 2026;
                    game = new Game(map, _setup.PlayerNames(), GameSetup.TutorialConfig(), seed,
                        new[] { CharacterKind.None, CharacterKind.None });
                }
                else
                {
                    var characters = _setup.ResolveCharacters(new System.Random(seed ^ 0x5bd1e995));
                    game = new Game(map, _setup.PlayerNames(), _setup.CreateConfig(), seed, characters);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError(e.Message);
                return;
            }
            Begin(game, 0f, _setup.tutorial ? LocalizationService.Get("game.ui.029") : EnglishUi ? $"New game on {DisplayPlace(game.Map.Name)}." : $"Новая партия на карте «{game.Map.Name}».");
        }

        /// <summary>Сетевая партия: Game уже собран сессией (у хоста — новый, у клиента — по данным хоста).</summary>
        public void StartOnline()
        {
            _net.SeatsChanged += () => _dirty = true;
            _net.Notice += text => Toast(text, Theme.Gold);
            _net.Resynced += () =>
            {
                if (_game != null)
                {
                    _game.Log -= AddLog;
                    _game.StateChanged -= OnStateChanged;
                }
                Begin(_net.Game, _playSeconds, EnglishUi ? "Game synchronized with the host." : "Партия синхронизирована с хостом.");
            };
            _net.Closed += text =>
            {
                if (_game != null && _game.Phase == GamePhase.GameOver) return;
                ShowConnectionLost(text);
            };
            string role = _net.IsHost ? LocalizationService.Get("game.ui.032") : LocalizationService.Get("game.ui.033") + _net.Seats[0].name;
            Begin(_net.Game, 0f, EnglishUi ? $"Online game ({_net.Transport.Kind}, {role}) on {DisplayPlace(_net.Game.Map.Name)}." : $"Сетевая партия ({_net.Transport.Kind}, {role}) на карте «{_net.Game.Map.Name}».");
        }

        void ShowConnectionLost(string text)
        {
            var box = DialogBox(560f);
            box.Add(Ui.Text(LocalizationService.Get("game.ui.030"), 22f, Theme.Danger, true));
            var label = Ui.Text(text, 15f, Theme.Text);
            label.style.marginTop = 10f;
            label.style.marginBottom = 20f;
            box.Add(label);
            box.Add(Ui.Button(LocalizationService.Get("game.ui.031"), _app.ShowMenu, ButtonKind.Primary, 16f));
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
                ShowError((EnglishUi ? "The save was damaged or did not match the map and was deleted.\n" : "Сохранение повреждено или не подходит к карте, поэтому удалено.\n") + e.Message);
                return;
            }
            Begin(game, file.playSeconds, EnglishUi ? $"Game resumed (saved {file.savedAt})." : $"Партия продолжена (сохранена {file.savedAt}).");
        }

        void Begin(Game game, float playSeconds, string firstLogLine,
            bool skipTutorialPrompt = false, bool preserveRedoHistory = false)
        {
            ClearUndoConfirmation();
            if (!preserveRedoHistory) ClearRedoHistory();
            _game = game;
            _playSeconds = playSeconds;
            _game.Log += AddLog;
            _lastTradeShown = _game.TradeNumber;
            _lastAuctionShown = _game.AuctionNumber;
            _lastWhisperShown = _game.WhisperNumber;
            _lastAccusationShown = _game.AccusationNumber;
            _lastFinalWarningShown = _game.FinalWarningNumber;
            _lastCoopNewsShown = _game.CoopNewsNumber;
            _lastFinalOrderShown = _game.FinalOrderNumber;
            _game.StateChanged += OnStateChanged;

            _bots = new IBot[_game.Players.Count];
            for (int i = 0; i < _bots.Length; i++)
                if (IsRealBot(i)) _bots[i] = _setup.players[i].CreateBot(UnityEngine.Random.Range(0, int.MaxValue));

            _map.TiltEnabled = _app.Display == null || _app.Display.tiltedBoard;
            _map.SetGame(_game);
            // The player legend must use the current game, not names from the last party.
            BuildLayersList();
            // Под экраном загрузки 3D-город строится отдельным шагом (Build3D), чтобы полоса загрузки двигалась.
            bool deferred = DeferStart;
            DeferStart = false;
            if (!deferred) Setup3D();
            ApplyLayers();
            SetupAbilityMarks();
            _minimap.SetGame(_game);
            bool minimap = _app.Display == null || _app.Display.showMinimap;
            _minimap.style.display = minimap ? DisplayStyle.Flex : DisplayStyle.None;
            _minimap.style.opacity = minimap ? 1f : 0f;
            BuildFaceUpRow();
            BuildPlayerRows();
            // Новая партия — панели закрыты, откроются сами, когда понадобятся.
            if (_sheet != SheetKind.None) CloseSheet();
            CloseDrawer();
            CloseModal();
            ClosePlayers();
            _autoSheetKey = null;
            _autoChoiceOpen = false;
            _logSeenTotal = 0;
            _seenGoalOwners = 0;

            _log.Clear();
            _logShownTotal = -1;
            _logStick = true;
            AddLog(firstLogLine);
            _revealedFor = -1;
            _selectionKey = null;
            _selectedContracts.Clear();
            _pinnedContracts.Clear();
            _flash = null;
            _flashPrefixKey = null;
            HideOverlay();
            _snapshot = TakeSnapshot();
            _dirty = true;
            if (!Online && !_setup.tutorial) SaveSystem.Save(_setup, _game, _playSeconds);

            if (_coach != null) _coach.Panel.RemoveFromHierarchy();
            _coach = _setup.tutorial ? new TutorialCoach(_game, _map) : null;
            if (_coach != null) _fx.Add(_coach.Panel);
            bool showTutorial = !skipTutorialPrompt && !Online && !_setup.tutorial &&
                HumanCount > 0 && PlayerPrefs.GetInt(TutorialPrefsKey, 0) == 0;
            Action start = () =>
            {
                if (showTutorial) _app.Tweener.After(0.5f, () => ShowTutorialStep(0));
                else _app.Tweener.After(0.4f, OnTurnStarted);
            };
            // Под экраном загрузки первый ход ждёт, пока экран не уберут (ReleaseStart): боты не ходят, таймер не идёт.
            if (deferred) _pendingStart = start;
            else start();
        }

        /// <summary>
        /// Партия запускается под экраном загрузки: Begin не строит 3D и не начинает первый ход —
        /// это делают Build3D и ReleaseStart отдельными шагами. Сбрасывается в первом же Begin.
        /// </summary>
        public bool DeferStart { get; set; }
        Action _pendingStart;

        /// <summary>Шаг загрузки: построить 3D-город для уже начатой партии.</summary>
        public void Build3D()
        {
            if (_game == null) return;
            Setup3D();
            ApplyLayers();
        }

        /// <summary>Экран загрузки убран — начать первый ход (или первое знакомство с правилами).</summary>
        public void ReleaseStart()
        {
            var start = _pendingStart;
            _pendingStart = null;
            start?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (_game == null) return;

            // Rebuild localized text on a live language change, including the journal.
            if (_renderedLanguage != LocalizationService.Language)
            {
                _renderedLanguage = LocalizationService.Language;
                _logShownTotal = -1;
                // Re-create map layer captions and network button in the active language.
                BuildLayersList();
                _dirty = true;
            }

            _map.Tick(deltaTime);
            TickUndoShortcut();
            if ((_undoButton != null && _undoButton.enabledSelf != CanUndoAction) ||
                (_redoButton != null && _redoButton.enabledSelf != CanRedoAction))
                RefreshUndoButton();
            Sync3D(deltaTime);
            if (_game.Phase != GamePhase.GameOver) _playSeconds += deltaTime;

            if (_flash != null && Time.time > _flashUntil)
            {
                _flash = null;
                _flashPrefixKey = null;
                _dirty = true;
            }

            if (_dirty)
            {
                string selectedCard = _gamepadFocusActive && Root.panel != null
                    ? (Root.panel.focusController.focusedElement as VisualElement)?.name
                    : null;
                _dirty = false;
                Refresh();
                if (selectedCard != null &&
                    selectedCard.StartsWith("gamepad-contract-", StringComparison.Ordinal))
                    UiGamepadFocus.RestoreNamedFocus(Root, selectedCard);
            }

            // A controller can navigate the existing HUD and dialog buttons
            // without invoking or bypassing any game-rule command.
            if (_map.GamepadExitedTargetingThisFrame) _gamepadFocusActive = true;
            VisualElement controllerScope = _overlayKind != OverlayKind.None ? _overlay :
                _modal != Modal.None ? _modalLayer :
                _drawerOpen ? _drawer :
                _playersOpen ? _playersPanel : Root;
            _gamepadFocusReturn.Update(Root, controllerScope, _gamepadFocusActive);
            UiGamepadFocus.MaintainGameplayFocus(Root, ref _gamepadFocusActive,
                _map.IsGamepadTargeting || _map.GamepadExitedTargetingThisFrame,
                controllerScope);
            GamepadControlGuide.Update(_gamepadGuide,
                GamepadControlGuide.Context.Game,
                UnityEngine.InputSystem.Gamepad.current != null, _gamepadFocusActive,
                _map.IsGamepadTargeting || controllerScope != Root,
                LocalizationService.Language == "en",
                GamepadControlGuide.TextEntryFocused(Root));
            TickTurnTimer(deltaTime);
            TickBots(deltaTime);
        }

        // ---------------------------------------------------------------- 3D-город под картой

        City3DScene _city3D;

        /// <summary>
        /// Построить 3D-город для карты партии (если включён в настройках и рендер его тянет).
        /// Не вышло — остаётся прежняя плоская карта, игра от этого не зависит.
        /// </summary>
        void Setup3D()
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

        /// <summary>Убрать 3D-город (выход в меню, новая партия, выключили в настройках).</summary>
        public void Release3D()
        {
            foreach (var stage in _heroStages.Values) stage.Release();
            _heroStages.Clear();
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
            var game = _game;
            _city3D.Sync(_map.Layout, panelSize, _map.worldBound.position, _map.Light, id => game.IsDistrictOpen(id), deltaTime);
            _city3D.SyncRoads(game);
            // Этап 2 полноценного 3D: бордюры цвета компании, разметка и машины — в сцене.
            _city3D.SyncRoadDetails(game, Palette.PlayerColor, game.Players.Count);
            _city3D.SyncTraffic(game, _map.PulseTime);
            _city3D.SyncPeople(game, _map.PulseTime);
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
            Toast(string.Format(LocalizationService.Get("game.notice.turn_timeout"), name), Theme.Danger);
            _dirty = true;
        }

        string TimerText()
        {
            if (!TimerEnabled || !HumanToAct || _timerExpired) return "";
            int seconds = Mathf.Max(0, Mathf.CeilToInt(_turnTimeLeft));
            return EnglishUi ? $"  ·  remaining {seconds / 60}:{seconds % 60:00}" : $"  ·  осталось {seconds / 60}:{seconds % 60:00}";
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
            if (!result.Success) Flash(result.Error, "game.ui.034");
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
            if (Online && !IsRealBot(index)) return index == _net.LocalSeat ? LocalizationService.Get("game.ui.035") : LocalizationService.Get("game.ui.036");
            if (!IsRealBot(index)) return "";
            return index < _setup.players.Count && _setup.players[index].botLevel == BotLevel.Random ? LocalizationService.Get("game.ui.037") : LocalizationService.Get("game.ui.038");
        }

        string WhoActsText(int acting)
        {
            if (AwaitingHost) return LocalizationService.Get("game.ui.039");
            if (IsRealBot(acting)) return LocalizationService.Get("game.ui.040");
            if (Online) return LocalizationService.Get("game.ui.041") + _game.Players[acting].Name + ".";
            return LocalizationService.Get("game.ui.042");
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
                    Flash(LocalizationService.Get("game.ui.043"));
                    return;
                case GamePhase.DrawingSecondCard:
                    Flash(LocalizationService.Get("game.ui.044"));
                    return;
                case GamePhase.ChoosingContracts:
                    Flash(LocalizationService.Get("game.ui.045"));
                    return;
                case GamePhase.ChoosingLuck:
                    Flash(LocalizationService.Get("game.ui.046"));
                    return;
                case GamePhase.RespondingTrade:
                    Flash(LocalizationService.Get("game.ui.047"));
                    return;
                case GamePhase.Bidding:
                    Flash(LocalizationService.Get("game.ui.048"));
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
                _map.SelectedRoadId = route.Id;
                ShowUpgrade(route, position);
                return;
            }

            var options = _game.GetPaymentOptions(route.Id);
            if (_game.CanBuildRoadFree(route.Id))
            {
                // Прорабу всегда предлагаем выбор: бесплатно или картами.
                _map.SelectedRoadId = route.Id;
                ShowPayment(route, options, position, true);
                return;
            }
            if (options.Count == 0)
            {
                Flash(_game.GetBuildBlocker(route.Id) ?? LocalizationService.Get("game.ui.049"));
                return;
            }
            if (options.Count == 1)
            {
                var only = options[0];
                Act(() => _game.BuildRoad(route.Id, only.Resource, only.Machinery));
                return;
            }
            _map.SelectedRoadId = route.Id;
            ShowPayment(route, options, position);
        }

        void OnMapHover(Road route, Vector2 position)
        {
            if (route == null || _overlayKind != OverlayKind.None || _game == null)
            {
                _map.SetConstructionPreview(null, false, false);
                Ui.Show(_tooltip, false);
                return;
            }

            bool canPreview = HumanToAct && _game.Phase == GamePhase.AwaitingAction &&
                              !_game.GetRoadOwner(route.Id).HasValue;
            bool free = canPreview && _game.CanBuildRoadFree(route.Id);
            bool payable = canPreview && _game.GetPaymentOptions(route.Id).Count > 0;
            _map.SetConstructionPreview(canPreview ? route.Id : null, free || payable, free);
            ShowMapTooltip(RoadTooltip(route), position);
        }

        /// <summary>Подсказка рядом с курсором над картой, не вылезая за край поля.</summary>
        void ShowMapTooltip(string text, Vector2 position)
        {
            _tooltipText.text = text;
            var local = _fx.WorldToLocal(_map.LocalToWorld(position));
            var mapBounds = _map.worldBound;
            var mapMax = _fx.WorldToLocal(new Vector2(mapBounds.xMax, mapBounds.yMax));
            // Leave a minimum margin on compact windows; avoid a tooltip wider
            // than the available map viewport.
            float tooltipWidth = GameFxTuning.TooltipWidth(mapMax.x);
            _tooltip.style.width = tooltipWidth;
            var tooltipPosition = GameFxTuning.TooltipPosition(local, mapMax, tooltipWidth, 130f);
            _tooltip.style.left = tooltipPosition.x;
            _tooltip.style.top = tooltipPosition.y;
            Ui.Show(_tooltip, true);
        }

        static string RoadKindLabel(Resource? resource)
        {
            if (!EnglishUi) return ResourceInfo.RoadKind(resource);
            if (!resource.HasValue) return "Street (any resource)";
            switch (resource.Value)
            {
                case Resource.Asphalt: return "Street";
                case Resource.Concrete: return "Avenue";
                case Resource.Steel: return "Bridge";
                case Resource.Gravel: return "Country Road";
                default: return "Road";
            }
        }

        string RoadTooltip(Road road)
        {
            string kind = RoadKindLabel(road.Resource);
            string resource = road.Resource.HasValue ? Palette.CardTitle(road.Resource.Value) : LocalizationService.Get("game.ui.050");
            string text = EnglishUi ? $"<b>{DisplayPlace(LocationName(road.A))} — {DisplayPlace(LocationName(road.B))}</b>\n{kind} · length {road.Length} · {resource} · {_game.Config.PointsForLength(road.Length)} pts" : $"<b>{LocationName(road.A)} — {LocationName(road.B)}</b>\n{char.ToUpper(kind[0]) + kind.Substring(1)} · длина {road.Length} · {resource} · {_game.Config.PointsForLength(road.Length)} очк.";

            int? owner = _game.GetRoadOwner(road.Id);
            if (!owner.HasValue && road.Id == _game.FinalOrderRoadId)
                text += EnglishUi ? $"\n<color=#FFCC40>Final City Hall order: +{_game.Config.FinalOrderBonus} pts to whoever builds it in the last round</color>" : $"\n<color=#FFCC40>Финальный заказ мэрии: +{_game.Config.FinalOrderBonus} очков тому, кто построит её в последнем круге</color>";
            if (owner.HasValue)
            {
                text += (EnglishUi ? "\nBuilt by: " : "\nПостроил: ") + _game.Players[owner.Value].Name;
                if (_game.IsRushHourRoad(road.Id))
                    text += EnglishUi ? $"\n<color=#FF9A33>Busy highway (rush hour): toll ×{_game.Config.RushHourTollMultiplier}</color>" : $"\n<color=#FF9A33>Загруженная магистраль (час пик): проезд ×{_game.Config.RushHourTollMultiplier}</color>";
                int traffic = _game.TrafficOn(road.Id);
                if (traffic > 0) text += EnglishUi ? $"\nTraffic: {traffic} rival routes" : $"\nЗагрузка: {traffic} {Plural(traffic, "маршрут", "маршрута", "маршрутов")} соперников";
                if (_game.IsJammed(road.Id))
                    text += EnglishUi ? $"\n<color=#FF6B5B>Traffic jam: +{_game.Config.JamFee} pts paid to the city. The owner can upgrade the road to a highway.</color>" : $"\n<color=#FF6B5B>Пробка: проезд ещё +{_game.Config.JamFee} очк. городу. Владелец может улучшить дорогу до магистрали.</color>";
                else if (_game.Config.JamThreshold > 0 && !_game.IsRushHourRoad(road.Id) && traffic == _game.Config.JamThreshold - 1)
                    text += EnglishUi ? "\n<color=#FFB35B>One more route will cause a traffic jam</color>" : "\n<color=#FFB35B>Ещё один маршрут — и здесь пробка</color>";
                int viewer = Viewer;
                if (owner.Value != viewer && !IsBot(viewer))
                {
                    if (_game.HasPass(viewer, owner.Value)) text += EnglishUi ? "\nYour road pass makes this toll free" : "\nУ вас пропуск: проезд владельцу бесплатно";
                    else text += EnglishUi ? $"\nYour toll: −{_game.TollFor(road.Id, viewer)} pts to the owner" : $"\nПроезд для вас: −{_game.TollFor(road.Id, viewer)} очк. владельцу";
                    if (_game.IsJammed(road.Id)) text += EnglishUi ? $" and −{_game.JamFeeFor(road.Id)} to the city" : $" и −{_game.JamFeeFor(road.Id)} городу";
                }
                return text;
            }
            if (_game.IsScheduledRepair(road.Id)) text += EnglishUi ? "\n<color=#FF6B6B>Repair Year: closed this round (City Hall permit allows construction)</color>" : "\n<color=#FF6B6B>Год ремонта: закрыт в этом круге (разрешение мэрии пускает строить)</color>";
            else if (_game.IsUnderRepair(road.Id)) text += EnglishUi ? "\n<color=#FF6B6B>Roadworks: closed until end of round</color>" : "\n<color=#FF6B6B>Ремонт: закрыт до конца круга</color>";
            if (!owner.HasValue && _game.IsNextRepair(road.Id)) text += EnglishUi ? "\n<color=#FF9A33>Repair Year: road closes next round — build it now</color>" : "\n<color=#FF9A33>Год ремонта: закроют в следующем круге — успейте построить</color>";
            if (_game.IsParadeClosed(road.Id)) text += EnglishUi ? "\n<color=#CC80FF>Parade: closed until end of round</color>" : "\n<color=#CC80FF>Парад: закрыт до конца круга</color>";
            if (road.Resource == Resource.Gravel && _game.CurrentEvent?.Kind == CityEventKind.Snow)
                text += EnglishUi ? $"\n<color=#D9EBFF>Snowfall: +{_game.Config.SnowExtraCost} cards to build cost</color>" : $"\n<color=#D9EBFF>Снегопад: +{_game.Config.SnowExtraCost} карта к цене</color>";
            if (_game.CurrentEvent?.Kind == CityEventKind.Investor)
                text += EnglishUi ? $"\n<color=#FFD94D>Investor: +{_game.Config.InvestorBonus} pts for construction</color>" : $"\n<color=#FFD94D>Инвестор: +{_game.Config.InvestorBonus} очка за постройку</color>";
            int cost = _game.BuildCost(road);
            if (cost < road.Length) text += EnglishUi ? $"\n<color=#7FC8FF>City Hall subsidy: now costs {cost} cards</color>" : $"\n<color=#7FC8FF>Субсидия мэрии: сейчас стоит {cost} карт(ы)</color>";
            if (_game.CurrentEvent != null && _game.CurrentEvent.Kind == CityEventKind.RushHour)
                text += EnglishUi ? "\n<color=#FF9A33>Rush hour: the road built now will charge double tolls</color>" : "\n<color=#FF9A33>Час пик: построенная сейчас дорога станет платной ×2</color>";
            if (HumanToAct && _game.Phase == GamePhase.AwaitingAction)
            {
                bool free = _game.CanBuildRoadFree(road.Id);
                var ways = _game.GetPaymentOptions(road.Id);
                if (free) text += EnglishUi ? "\n<color=#66E7FF>Free construction available</color>" : "\n<color=#66E7FF>Доступно бесплатное строительство</color>";
                if (ways.Count > 0)
                    text += EnglishUi ? $"\n<color=#7FD88A>Preview: {_game.BuildCost(road)} cards · {ways.Count} payment options</color>"
                        : $"\n<color=#7FD88A>Предпросмотр: {_game.BuildCost(road)} карт · способов оплаты: {ways.Count}</color>";
                string blocker = _game.GetBuildBlocker(road.Id);
                return text + "\n" + (blocker != null && !free && ways.Count == 0 ? LocalizationService.TranslateGameError(blocker)
                    : EnglishUi ? "<color=#7FD88A>Click to build</color>" : "<color=#7FD88A>Кликните, чтобы построить</color>");
            }
            return text;
        }

        // ================================================================== Анимации по изменениям

        void OnStateChanged()
        {
            // Any new real game action forks the undo timeline. A stale
            // "Redo" must never replay an older action into this branch.
            ClearRedoHistory();
            var now = TakeSnapshot();
            bool finished = now.Phase == GamePhase.GameOver && (_snapshot == null || _snapshot.Phase != GamePhase.GameOver);
            if (_snapshot != null) AnimateChanges(_snapshot, now);
            _snapshot = now;
            _dirty = true;

            // Автосохранение после каждого хода; по окончании партии — запись статистики.
            if (finished)
            {
                if (!Online && !_setup.tutorial) SaveSystem.Delete();
                // Сначала профили (они знают, какие достижения открыла партия), потом запись в статистику.
                _profileCredits = Profiles.CreditAfterGame(_game, _setup, Online ? _net.LocalSeat : (int?)null);
                if (!_setup.tutorial)
                {
                    var achievements = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var credit in _profileCredits)
                        if (credit.Unlocked != null && credit.Unlocked.Count > 0)
                            achievements[Online && _net.LocalSeat >= 0 && _net.LocalSeat < _game.Players.Count ? _game.Players[_net.LocalSeat].Name : credit.Profile] =
                                credit.Unlocked.Select(a => a.Title).ToList();
                    // Повтор партии — для «Счётчика партий», рекордов друзей и клуба.
                    int localSeat = Online ? _net.LocalSeat : -1;
                    var replay = Replays.Capture(_game, _setup, _playSeconds, Online, localSeat);
                    string replayId = replay != null && Replays.Store(replay) ? replay.id : null;
                    StatsLog.Append(_game, _setup, _playSeconds, Online, achievements, replayId, _ghost?.Name, _ghost?.Score ?? 0);
                    SocialCloud.ReportGame(_game, _setup, replay, _ghost);
                    RaidFinished(replay);
                }
                else TutorialCoach.MarkDone();
            }
            else if (now.Phase != GamePhase.GameOver && !Online && !_setup.tutorial)
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
                LuckNumber = _game.LuckNumber,
                Earned = _game.Players.Select(p => p.TollsEarned).ToArray(),
                Points = _game.Players.Select(p => p.RoadPoints + p.GoalPoints + p.LuckPoints + p.PenaltyPoints).ToArray()
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
                if (owner.HasValue && _game.IsJammed(route.Id)) s.Jammed.Add(route.Id);
                if (owner.HasValue && _game.IsRushHourRoad(route.Id)) s.Highways.Add(route.Id);
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

            foreach (var roadId in now.Highways)
                if (before.Owners.ContainsKey(roadId) && !before.Highways.Contains(roadId))
                    StartRoadSweep(roadId, true);

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
                    Toast(string.Format(LocalizationService.Get("game.notice.city_event"), CityEventTitle(cityEvent.Kind)), color);
                    _app.Sound.Play(cityEvent.Kind == CityEventKind.NewDistrict ? Sfx.Fanfare : Sfx.Swish, 0.3f);
                    _map.Flash(color);
                    _map.Repaint();
                    GlowEventCard(color);
                });
            }

            // Кооператив: заказ города выполнен или просрочен.
            if (_game.CoopNewsNumber > _lastCoopNewsShown)
            {
                _lastCoopNewsShown = _game.CoopNewsNumber;
                bool good = _game.LastCoopNewsGood;
                Toast(EnglishUi ? CoopJournalText(_game.LastCoopNews) ?? _game.LastCoopNews : _game.LastCoopNews, good ? Theme.Success : Theme.Danger);
                _app.Sound.Play(good ? Sfx.Fanfare : Sfx.Swish, good ? 0.5f : 0.4f);
                _map.Flash(good ? Theme.Success : Theme.Danger);
            }

            // Финал: «скоро финал», финальный заказ мэрии (открыт / выполнен).
            if (_game.FinalWarningNumber > _lastFinalWarningShown)
            {
                _lastFinalWarningShown = _game.FinalWarningNumber;
                Toast(string.Format(LocalizationService.Get("game.notice.final_soon"), _game.FewestPieces, _game.Config.FinalRoundPieces), Theme.Gold);
            }
            if (_game.FinalOrderNumber > _lastFinalOrderShown)
            {
                _lastFinalOrderShown = _game.FinalOrderNumber;
                var order = _game.FinalOrderRoad;
                if (order != null)
                {
                    string where = $"{LocationName(order.A)} — {LocationName(order.B)}";
                    Toast(_game.FinalOrderWinner >= 0
                        ? string.Format(LocalizationService.Get("game.notice.final_order_completed"), _game.Players[_game.FinalOrderWinner].Name, _game.Config.FinalOrderBonus)
                        : string.Format(LocalizationService.Get("game.notice.final_order_open"), where, _game.Config.FinalOrderBonus), Theme.Gold);
                }
            }

            // Взятки: в начале круга — сколько шептались в прошлом (без имён), и итог обвинения — видно всем.
            if (_game.WhisperNumber > _lastWhisperShown)
            {
                _lastWhisperShown = _game.WhisperNumber;
                Toast(string.Format(LocalizationService.Get("game.notice.whispers"), _game.LastWhisperCount), Theme.TextDim);
            }
            if (_game.AccusationNumber > _lastAccusationShown && _game.LastAccusation != null)
            {
                _lastAccusationShown = _game.AccusationNumber;
                var a = _game.LastAccusation;
                string accuser = _game.Players[a.Accuser].Name, accused = _game.Players[a.Accused].Name;
                Toast(a.Correct
                        ? string.Format(LocalizationService.Get("game.notice.bribe_exposed"), accuser, accused, _game.Config.BribeFineFor(_game.Players.Count) * a.Exposed)
                        : string.Format(LocalizationService.Get("game.notice.false_accusation"), accuser, accused, _game.Config.FalseAccusationFine),
                    a.Correct ? Theme.Danger : Theme.TextDim);
                _app.Sound.Play(a.Correct ? Sfx.Horn : Sfx.Swish, 0.35f);
            }

            // Аукцион мэрии: начало и итог.
            if (_game.AuctionNumber > _lastAuctionShown)
            {
                _lastAuctionShown = _game.AuctionNumber;
                if (_game.Phase == GamePhase.Bidding && _game.AuctionContract != null)
                {
                    Toast(string.Format(LocalizationService.Get("game.notice.auction_open"), ContractTitle(_game.AuctionContract)), Theme.Gold);
                    _app.Sound.Play(Sfx.Fanfare, 0.3f);
                }
                else if (_game.LastAuction != null)
                {
                    var auction = _game.LastAuction;
                    Toast(auction.Winner >= 0
                        ? string.Format(LocalizationService.Get("game.notice.auction_won"), _game.Players[auction.Winner].Name, auction.Price)
                        : LocalizationService.Get("game.notice.auction_empty"), auction.Winner >= 0 ? Theme.Success : Theme.TextDim);
                }
            }

            // Итог сделки.
            if (_game.TradeNumber > _lastTradeShown && _game.LastTrade != null)
            {
                _lastTradeShown = _game.TradeNumber;
                var trade = _game.LastTrade;
                string answer = LocalizationService.Get(_game.LastTradeAccepted ? "game.notice.trade_accepted" : "game.notice.trade_declined");
                Toast(string.Format(LocalizationService.Get("game.notice.trade_result"), _game.Players[trade.To].Name, answer), _game.LastTradeAccepted ? Theme.Success : Theme.TextDim);
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
                    Toast(string.Format(LocalizationService.Get("game.notice.goal_claimed"), MayorGoalName(goal), who, goal.Points), Theme.Gold);
                    // Салют над серединой карты — цветом компании, взявшей цель.
                    int goalBurst = GameFxTuning.FireworkCount(48, Ui.ReducedMotion);
                    if (goalBurst > 0)
                    {
                        _map.Fireworks(_map.contentRect.center + new Vector2(-80f, -40f), Palette.PlayerColor(pair.Value), goalBurst);
                        _map.Fireworks(_map.contentRect.center + new Vector2(90f, 10f), Theme.Gold, GameFxTuning.FireworkCount(40, false));
                    }
                    _app.Sound.Play(Sfx.Fanfare, 0.7f);
                });
            }

            // Проезд по дорогам того, кто сидит за экраном, и новые пробки.
            int me = Viewer;
            if (before.Earned != null && now.Earned != null && me >= 0 && me < now.Earned.Length && me < before.Earned.Length
                && !IsBot(me) && now.Earned[me] > before.Earned[me])
            {
                int gain = now.Earned[me] - before.Earned[me];
                _app.Tweener.After(claimDuration + 0.3f, () =>
                    Toast(string.Format(LocalizationService.Get("game.notice.toll_earned"), gain), Theme.Gold));
            }
            int newJamIndex = 0;
            foreach (var roadId in now.Jammed)
            {
                if (before.Jammed.Contains(roadId)) continue;
                var jammedRoad = _game.Map.GetRoad(roadId);
                if (jammedRoad == null) continue;
                bool mine = now.Owners.TryGetValue(roadId, out var jamOwner) && jamOwner == me && !IsBot(me);
                int jamIndex = newJamIndex++;
                // Avoid simultaneous error toasts: the journal retains every jam,
                // while at most two receive prominent animated alerts.
                if (jamIndex < 2)
                {
                    string text = string.Format(LocalizationService.Get("game.notice.traffic_jam"), LocationName(jammedRoad.A), LocationName(jammedRoad.B)) +
                                  (mine ? LocalizationService.Get("game.notice.jam_upgrade") : string.Format(LocalizationService.Get("game.notice.jam_fee"), _game.Config.JamFee));
                    string alertRoad = roadId;
                    _app.Tweener.After(claimDuration + 0.5f + jamIndex * 1.8f, () =>
                    {
                        Toast(text, Theme.Danger);
                        int sparks = GameFxTuning.JamSparks(jamIndex, Ui.ReducedMotion);
                        if (sparks > 0 && IsLaidOut(_map))
                            _map.Fireworks(_map.WorldToLocal(_map.RoadWorldCenter(alertRoad)), Theme.Danger, sparks);
                    });
                }
            }

            // Очки по ходу партии: «+N» / «−N» у строки игрока (дороги, цели, удача, штрафы; проезд — монетками ниже).
            if (before.Points != null && now.Points != null)
                for (int i = 0; i < now.Points.Length && i < before.Points.Length; i++)
                {
                    int diff = now.Points[i] - before.Points[i];
                    if (diff == 0 || i >= _playerRows.Length) continue;
                    var row = PlayerAnchor(i);
                    _app.Tweener.After(claimDuration * 0.8f, () => FloatText(row, diff > 0 ? $"+{diff}" : $"{diff}", diff > 0 ? Theme.Success : Theme.Danger));
                }

            // Оплаченные маршруты: свет по пути и монетки владельцам дорог. Свой маршрут виден всегда, чужой —
            // только если за проезд платили (тогда он и так открыт всем).
            int coinWave = 0;
            foreach (var key in now.Completed)
            {
                if (before.Completed.Contains(key)) continue;
                int separator = key.IndexOf(':');
                int payer = int.Parse(key.Substring(0, separator));
                var paidContract = _game.Players[payer].Contracts.FirstOrDefault(c => c.Id == key.Substring(separator + 1));
                var paid = paidContract == null ? null : _game.PaidRouteOf(payer, paidContract);
                if (paid == null) continue;
                bool mine = payer == Viewer && !IsBot(payer);
                if (!mine && paid.Passages.Count == 0) continue;
                float delay = claimDuration + 0.2f + coinWave * 0.9f;
                GlowRoute(paid.Path, Palette.PlayerColor(payer), delay);
                if (paid.Passages.Count > 0) FlyCoins(paid, payer, delay + 0.5f);
                coinWave++;
            }

            // Stagger local contract rewards so concurrent completions do not
            // immediately replace each other in the toast slot.
            int shownContracts = 0;
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
                var paidCheck = _game.CheckContract(playerIndex, contract);
                int contractToll = paidCheck.Toll + paidCheck.JamFee;
                float noticeDelay = claimDuration + shownContracts++ * 2.0f;
                _app.Tweener.After(noticeDelay, () =>
                {
                    string toll = contractToll > 0 ? string.Format(LocalizationService.Get("game.notice.contract_toll"), contractToll, paidCheck.JamFee > 0 ? LocalizationService.Get("game.notice.contract_toll_jam") : "") : "";
                    Toast(string.Format(LocalizationService.Get("game.notice.contract_completed"), ContractTitle(contract), contract.Points, toll), Theme.Success);
                    // Салют над районами контракта.
                    int contractBurst = GameFxTuning.ContractSparksPerDistrict(contract.Districts.Count(), Ui.ReducedMotion);
                    if (contractBurst > 0)
                        foreach (var district in contract.Districts)
                            _map.Fireworks(_map.DistrictScreenCenter(district), Palette.PlayerColor(playerIndex), contractBurst);
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
            if (_game.Phase == GamePhase.SetupContracts)
                Toast(_game.ActingPlayerIndex >= 0 && _game.Players[_game.ActingPlayerIndex].CharacterOffer.Count > 0
                    ? string.Format(LocalizationService.Get("game.notice.choose_company"), name)
                    : string.Format(LocalizationService.Get("game.notice.choose_contracts"), name), Theme.Gold);
            else Toast(HumanCount > 1 ? string.Format(LocalizationService.Get("game.notice.player_turn"), name) : LocalizationService.Get("game.notice.your_turn"), Theme.Gold);
        }

        static float ClaimDuration(Road road) => road == null ? 0f : Ui.ReducedMotion ? 0.04f : 0.12f * road.Length + 0.1f;

        void AnimateClaim(Road route)
        {
            if (route == null) return;
            _map.BuildProgress[route.Id] = Ui.ReducedMotion ? 1f : 0f;
            int segments = route.Length;
            int played = 0;

            _app.Tweener.Add(ClaimDuration(route), k =>
            {
                _map.BuildProgress[route.Id] = Ui.ReducedMotion ? 1f : k;
                int shown = Mathf.FloorToInt(k * segments + 0.001f);
                if (!Ui.ReducedMotion && shown > played)
                {
                    played = shown;
                    _app.Sound.Play(Sfx.Clack, 0.8f);
                }
            }, () =>
            {
                _map.BuildProgress.Remove(route.Id);
                _app.Sound.Play(Sfx.Horn, 0.6f);
                StartRoadSweep(route.Id, false);
                // Small player-colored completion sparkle. Keep major fireworks for
                // contracts and mayor goals; this should not overwhelm rapid bot turns.
                int sparks = GameFxTuning.FireworkCount(12, Ui.ReducedMotion);
                if (sparks > 0 && IsLaidOut(_map))
                    _map.Fireworks(_map.WorldToLocal(_map.RoadWorldCenter(route.Id)),
                        Palette.PlayerColor(_game.GetRoadOwner(route.Id) ?? 0), sparks);
            });
        }

        void StartRoadSweep(string roadId, bool upgrade)
        {
            if (Ui.ReducedMotion) return;
            // A delayed tween from a previous party must not paint on the next one.
            Game sourceGame = _game;
            _map.RoadSweeps[roadId] = 0f;
            if (upgrade) _map.UpgradeSweeps.Add(roadId);
            else _map.UpgradeSweeps.Remove(roadId);
            _app.Tweener.Add(0.65f, k =>
            {
                if (_game != sourceGame || Ui.ReducedMotion) return;
                _map.RoadSweeps[roadId] = k;
                _map.Repaint();
            }, () =>
            {
                if (_game != sourceGame) return;
                _map.RoadSweeps.Remove(roadId);
                _map.UpgradeSweeps.Remove(roadId);
                _map.Repaint();
            }, 0f, false);
        }

        void FlyCard(int playerIndex, Resource color, int faceUpSlot, float delay)
        {
            VisualElement from = faceUpSlot >= 0 ? FaceUpAnchor(faceUpSlot) : DeckAnchor;
            bool toHand = playerIndex == Viewer && !IsBot(playerIndex) && !NeedsHandoff;
            VisualElement to = toHand ? (VisualElement)_hand[color] : PlayerAnchor(playerIndex);
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
            bool reducedMotion = Ui.ReducedMotion;
            _app.Tweener.Add(GameFxTuning.CardFlightDuration(reducedMotion), k =>
            {
                float eased = Ui.EaseInOutCubic(k);
                var position = Vector2.Lerp(start, end, eased);
                position.y -= Mathf.Sin(k * Mathf.PI) * GameFxTuning.CardArcHeight(reducedMotion);
                card.style.left = position.x;
                card.style.top = position.y;
                card.style.opacity = Mathf.Clamp01(k * 8f) * (k > 0.85f ? (1f - k) / 0.15f : 1f);
            }, () =>
            {
                card.RemoveFromHierarchy();
                if (to is ResourceChip chip) chip.Pulse();
            }, delay);
        }

        /// <summary>Свет по маршруту контракта на карте (волна от начала к концу, затем гаснет).</summary>
        void GlowRoute(IReadOnlyList<string> path, Color color, float delay)
        {
            if (path == null || path.Count == 0) return;
            var ids = path.ToList();
            _app.Tweener.Add(1.8f, k =>
            {
                _map.RoutePath = ids;
                _map.RouteProgress = k;
                _map.RouteColor = color;
            }, () =>
            {
                if (_map.RoutePath == ids) _map.RoutePath = null;
            }, delay, false);
        }

        /// <summary>
        /// Плата за проезд: с каждой чужой дороги маршрута монетка «+N» летит к строке владельца,
        /// у плательщика всплывает «−N»; сбор за пробку — красное «−N» над дорогой (сгорает).
        /// </summary>
        void FlyCoins(PaidRoute route, int payer, float delay)
        {
            if (!IsLaidOut(_map) || !IsLaidOut(_fx)) return;
            int paidRoads = route.Passages.Count(p => p.Toll > 0 && p.Owner >= 0 && p.Owner < _playerRows.Length);
            if (!GameFxTuning.AnimateIndividualCoins(paidRoads, Ui.ReducedMotion))
            {
                // Clear, compact feedback when the settlement is dense or motion is reduced.
                foreach (var owner in route.Passages.Where(p => p.Toll > 0 && p.Owner >= 0 && p.Owner < _playerRows.Length)
                    .GroupBy(p => p.Owner))
                {
                    var target = PlayerAnchor(owner.Key);
                    int earned = owner.Sum(p => p.Toll);
                    _app.Tweener.After(delay, () => FloatText(target, $"+{earned}", Theme.Gold));
                }
                if (payer >= 0 && payer < _playerRows.Length)
                {
                    var payerRow = PlayerAnchor(payer);
                    int total = route.Toll + route.JamFee;
                    _app.Tweener.After(delay, () => FloatText(payerRow, $"−{total}", Theme.Danger));
                }
                return;
            }

            int index = 0;
            foreach (var passage in route.Passages)
            {
                float start = delay + index++ * 0.22f;
                var from = _fx.WorldToLocal(_map.RoadWorldCenter(passage.RoadId));
                if (passage.JamFee > 0)
                {
                    var burnt = from;
                    _app.Tweener.After(start, () => FloatAt(burnt, $"−{passage.JamFee}", Theme.Danger));
                }
                if (passage.Toll <= 0 || passage.Owner < 0 || passage.Owner >= _playerRows.Length) continue;
                var target = PlayerAnchor(passage.Owner);
                if (!IsLaidOut(target)) continue;
                var to = _fx.WorldToLocal(target.worldBound.center);

                const float size = 30f;
                var coin = Ui.Box();
                coin.pickingMode = PickingMode.Ignore;
                coin.style.position = Position.Absolute;
                coin.style.width = size;
                coin.style.height = size;
                coin.style.backgroundColor = new Color(1f, 0.80f, 0.25f);
                Ui.Radius(coin, size / 2f);
                Ui.Border(coin, 2f, new Color(0.72f, 0.50f, 0.08f));
                coin.style.justifyContent = Justify.Center;
                coin.style.alignItems = Align.Center;
                var label = Ui.Text($"+{passage.Toll}", 12f, new Color(0.30f, 0.20f, 0.02f), true);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                coin.Add(label);
                coin.style.opacity = 0f;
                _fx.Add(coin);

                _app.Tweener.After(start, () => _app.Sound.Play(Sfx.Coin, 0.7f));
                _app.Tweener.Add(1.0f, k =>
                {
                    // Сначала монетка выпрыгивает над дорогой, потом по дуге летит к владельцу.
                    float pop = Mathf.Clamp01(k / 0.25f);
                    float fly = Ui.EaseInOutCubic(Mathf.Clamp01((k - 0.25f) / 0.75f));
                    var position = Vector2.Lerp(from + new Vector2(0f, -26f * Ui.EaseOutCubic(pop)), to, fly);
                    position.y -= Mathf.Sin(fly * Mathf.PI) * 60f;
                    coin.style.left = position.x - size / 2f;
                    coin.style.top = position.y - size / 2f;
                    float scale = GameFxTuning.CoinScale(k);
                    coin.style.scale = new Scale(new Vector3(scale, scale, 1f));
                    coin.style.opacity = k > 0.9f ? (1f - k) / 0.1f : 1f;
                }, () =>
                {
                    coin.RemoveFromHierarchy();
                    FloatText(target, $"+{passage.Toll}", Theme.Gold);
                }, start, false);
            }
            if (payer >= 0 && payer < _playerRows.Length)
            {
                int total = route.Toll + route.JamFee;
                var payerRow = PlayerAnchor(payer);
                _app.Tweener.After(delay, () => FloatText(payerRow, $"−{total}", Theme.Danger));
            }
        }

        /// <summary>Всплывающая надпись у правого края элемента (строки игрока): поднимается и гаснет.</summary>
        void FloatText(VisualElement anchor, string text, Color color)
        {
            if (!IsLaidOut(anchor) || !IsLaidOut(_fx)) return;
            var bounds = anchor.worldBound;
            FloatAt(_fx.WorldToLocal(new Vector2(bounds.xMax - 34f, bounds.center.y)), text, color);
        }

        /// <summary>Всплывающая надпись в точке слоя эффектов.</summary>
        void FloatAt(Vector2 at, string text, Color color)
        {
            var label = Ui.Text(text, 20f, color, true);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.8f);
            label.style.unityTextOutlineWidth = 0.25f;
            label.style.left = at.x - 14f;
            label.style.top = at.y - 12f;
            _fx.Add(label);
            _app.Tweener.Add(1.3f, k =>
            {
                label.style.top = at.y - 12f - 34f * Ui.EaseOutCubic(k);
                label.style.opacity = k < 0.1f ? k / 0.1f : k > 0.7f ? (1f - k) / 0.3f : 1f;
            }, label.RemoveFromHierarchy, 0f, false);
        }

        static bool IsLaidOut(VisualElement e) =>
            e != null && e.panel != null && !float.IsNaN(e.worldBound.width) && e.worldBound.width > 1f;

        void Toast(string text, Color color)
        {
            _toast.text = text;
            _toast.style.color = color;
            int version = ++_toastVersion;
            bool reducedMotion = Ui.ReducedMotion;
            _toastHolder.style.opacity = 0f;
            _toastHolder.style.top = GameFxTuning.ToastTopAt(0f, reducedMotion)
                + GameFxTuning.ToastVerticalOffset(Root.layout.width);
            Ui.Show(_toastHolder, true);

            _app.Tweener.Add(2.4f, k =>
            {
                if (version != _toastVersion) return;
                _toastHolder.style.opacity = GameFxTuning.ToastOpacity(k);
                _toastHolder.style.top = GameFxTuning.ToastTopAt(k, reducedMotion)
                    + GameFxTuning.ToastVerticalOffset(Root.layout.width);
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
            back.Add(Ui.Text("?", 96f, Theme.Gold, true));
            back.Add(Ui.Text(LocalizationService.Get("game.ui.051"), 20f, Theme.Gold, true));
            card.Add(back);

            // Лицо.
            var face = Ui.Column();
            face.style.alignItems = Align.Center;
            face.Add(Ui.Text(buff ? LocalizationService.Get("game.ui.052") : LocalizationService.Get("game.ui.053"), 14f, color, true));
            var title = Ui.Text(LuckCardTitle(kind), 24f, Theme.Text, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginTop = 10f;
            face.Add(title);
            var effect = Ui.Text(LuckCardDescription(kind, _game.Config), 13f, Theme.TextDim);
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
                Ui.Border(card, 3f, showFace ? color : Theme.Gold);
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

        void Flash(string message, string prefixKey = null)
        {
            // Keep the core error and optional prefix untranslated until rendering.
            // A language change must not leave a previously shown error in the old language.
            _flash = message;
            _flashPrefixKey = prefixKey;
            _flashUntil = Time.time + 3.5f;
            _app.Sound.Play(Sfx.Error, 0.7f);
            _dirty = true;
        }

        void AddLog(string message)
        {
            _logTotal++;
            _log.Add(message);
            if (_log.Count > LogHistory) _log.RemoveAt(0);
            _dirty = true;
        }

        // ================================================================== Обновление экрана

        void Refresh()
        {
            if (_game == null) return;
            _turnLabel.text = (_game.Phase == GamePhase.SetupContracts ? LocalizationService.Get("game.ui.054") : LocalizationService.Get("game.ui.055") + _game.TurnNumber)
                              + (_game.Scenario != ScenarioKind.Standard ? " · " + Scenarios.Title(_game.Scenario) : "")
                              + (_game.IsCooperative ? LocalizationService.Get("game.ui.056") : "");
            RefreshEvent();
            RefreshGoals();
            RefreshGhost();
            _coopPanel?.Set(_game);
            RefreshTabs();
            _map.RefreshVisibility();
            RefreshFaceUp();
            RefreshPlayers();
            RefreshContracts();
            RefreshLog();
            RefreshStatus();
            RefreshHand();
            _coach?.Update();
            RefreshOverlays();
            RefreshMap();
            RefreshDrawerInfo();
            AutoPanels();
            RefreshTabs();
            RefreshUndoButton();
            _minimap.Refresh();
        }

        void RefreshDrawerInfo()
        {
            _drawerInfo.text = EnglishUi
                ? $"Deck: {_game.DeckCount} · discard: {_game.DiscardCount} · contract deck: {_game.ContractDeckCount}" + (_game.Scenario != ScenarioKind.Standard ? $"\nScenario: {ScenarioMenuLocalization.Title(_game.Scenario)}" : "")
                : $"Колода: {_game.DeckCount} · сброс {_game.DiscardCount} · контрактов в колоде {_game.ContractDeckCount}" + (_game.Scenario != ScenarioKind.Standard ? $"\nСценарий: {Scenarios.Title(_game.Scenario)}" : "");
        }

        void BuildFaceUpRow()
        {
            _faceUpRow.Clear();
            _faceUp = new CardView[_game.FaceUpCards.Count];
            for (int i = 0; i < _faceUp.Length; i++)
            {
                int slot = i;
                var card = new CardView(64f, 92f, false);
                void DrawCard() => Act(() => _game.DrawFaceUpCard(slot));
                card.RegisterCallback<ClickEvent>(_ => DrawCard());
                GamepadSelectable.Attach(card, DrawCard,
                    () =>
                    {
                        bool drawing = _game.Phase == GamePhase.AwaitingAction ||
                                       _game.Phase == GamePhase.DrawingSecondCard;
                        bool forbidden = _game.Phase == GamePhase.DrawingSecondCard &&
                                         _game.FaceUpCards[slot] == Resource.Machinery;
                        bool available = GameFxTuning.CanHighlightFaceUp(HumanToAct, drawing,
                            _game.FaceUpCards[slot].HasValue, forbidden);
                        card.style.top = GameFxTuning.FaceUpHoverOffset(available, Ui.ReducedMotion);
                    },
                    () => card.style.top = 0f);
                Ui.Transitions(card, 0.14f, "top");
                card.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    bool drawing = _game.Phase == GamePhase.AwaitingAction || _game.Phase == GamePhase.DrawingSecondCard;
                    bool forbidden = _game.Phase == GamePhase.DrawingSecondCard &&
                                     _game.FaceUpCards[slot] == Resource.Machinery;
                    bool available = GameFxTuning.CanHighlightFaceUp(HumanToAct, drawing,
                        _game.FaceUpCards[slot].HasValue, forbidden);
                    card.style.top = GameFxTuning.FaceUpHoverOffset(available, Ui.ReducedMotion);
                });
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
                bool available = GameFxTuning.CanHighlightFaceUp(
                    HumanToAct, canDraw, card.HasValue, forbidden);
                // Keep mouse click validation in Act(), but exclude unavailable
                // cards from controller/tab focus navigation.
                _faceUp[i].focusable = available;
                if (!available) _faceUp[i].style.top = 0f;
            }
            _faceUpRow.style.opacity = canDraw ? 1f : 0.7f;

            bool firstCard = _game.Phase == GamePhase.AwaitingAction;
            _deckButton.text = (firstCard ? LocalizationService.Get("game.ui.057") : LocalizationService.Get("game.ui.058"));
            Ui.SetEnabled(_deckButton, canDraw && _game.DeckCount + _game.DiscardCount > 0);
            Ui.Show(_luckButton, _game.Config.LuckEnabled);
            // На кнопке — сколько карт осталось до перемешивания; под кнопками — шансы.
            _luckButton.text = LocalizationService.Get("game.ui.017");
            Ui.Show(_luckOdds, _game.Config.LuckEnabled);
            int buffs = _game.LuckBuffsLeft, debuffs = _game.LuckDebuffsLeft;
            if (buffs + debuffs == 0) _luckOdds.text = LocalizationService.Get("game.ui.059");
            else
            {
                int chance = (int)Math.Round(100.0 * buffs / (buffs + debuffs));
                int luckLeft = _game.LuckWillReshuffle ? _game.LuckDiscardCount : _game.LuckDeckCount;
                _luckOdds.text = (EnglishUi ? $"Deck: {_game.DeckCount} · contracts: {_game.ContractDeckCount} · luck cards: {luckLeft} · buff chance: {chance}%" : $"В колоде {_game.DeckCount} · контрактов {_game.ContractDeckCount} · удача {luckLeft}: шанс бафа {chance}%")
                                 + (_game.LuckWillReshuffle ? LocalizationService.Get("game.ui.060") : "")
                                 + (_game.TradeUsedThisTurn ? LocalizationService.Get("game.ui.061") : "")
                                 + (_game.InfluenceUsedThisTurn ? LocalizationService.Get("game.ui.062") : "")
                                 + (HumanToAct && _game.BribeDiscountActive ? LocalizationService.Get("game.ui.063") : "")
                                 + (_game.AnnouncedBribes > 0 ? (EnglishUi ? $" · unreported bribes last round: {_game.AnnouncedBribes}" : $" · нераскрытых взяток прошлого круга: {_game.AnnouncedBribes}") : "");
            }
            Ui.SetEnabled(_luckButton, HumanToAct && _game.CanDrawLuckCard());
            _contractsButton.text = LocalizationService.Get("game.ui.064");
            Ui.SetEnabled(_contractsButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction && _game.ContractDeckCount > 0);
            Ui.Show(_tradeButton, _game.Config.TradesEnabled);
            _tradeButton.text = LocalizationService.Get("game.ui.020");
            Ui.SetEnabled(_tradeButton, HumanToAct && _game.CanProposeTrade());
            Ui.Show(_influenceButton, _game.Config.InfluenceEnabled);
            _influenceButton.text = EnglishUi ? $"Influence {_game.Players[Viewer].Influence}" : $"Влияние {_game.Players[Viewer].Influence}";
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

        static string InfluenceTitle(InfluenceAction action)
        {
            if (!EnglishUi) return InfluenceInfo.Title(action);
            switch (action)
            {
                case InfluenceAction.RefreshMarket: return "Refresh Market";
                case InfluenceAction.ExtraContracts: return "Choose Contracts";
                case InfluenceAction.Permit: return "City Hall Permit";
                default: return "Cancel Event";
            }
        }

        string InfluenceDescription(InfluenceAction action)
        {
            if (!EnglishUi) return InfluenceInfo.Describe(action, _game.Config);
            switch (action)
            {
                case InfluenceAction.RefreshMarket:
                    return "Discard all face-up cards and deal a new market. Your turn continues.";
                case InfluenceAction.ExtraContracts:
                    return $"Draw {_game.Config.ContractsDrawn + 1} contracts instead of {_game.Config.ContractsDrawn}, then choose which to keep. This uses your turn.";
                case InfluenceAction.Permit:
                    return "Ignore parade closures and snow construction penalties this turn." +
                           (_game.Config.ScheduledRepairs > 0 ? " You may also build on roads scheduled for repairs." : "");
                default:
                    return "End the current event immediately for all players (except New District).";
            }
        }

        static string BribeTitle(BribeKind kind) => !EnglishUi ? BribeInfo.Title(kind)
            : kind == BribeKind.Discount ? "Expedite Approval" : "Extra Delivery";

        static string BribeDescription(BribeKind kind) => !EnglishUi ? BribeInfo.Describe(kind)
            : kind == BribeKind.Discount ? "Roads cost one fewer card this turn (minimum one)." : "Draw one card immediately; your turn continues normally.";

        /// <summary>Траты влияния, взятки и разоблачения: у каждого — цена, описание и причина, если сейчас нельзя.</summary>
        void ShowInfluenceMenu()
        {
            if (!HumanToAct || _game.Phase != GamePhase.AwaitingAction) return;
            var me = _game.CurrentPlayer;
            var box = DialogBox(600f);
            Ui.Border(box, 2f, Theme.Gold);
            box.Add(Ui.Text(EnglishUi ? $"Influence: {me.Influence}" : $"Влияние: {me.Influence}", 26f, Theme.Gold, true));
            var note = Ui.Text(EnglishUi ? "Spend influence once per turn before your main action. Spending it does not use your turn except when choosing contracts. Influence comes from completed contracts, new districts, City Hall goals, grants and trailing the leader. Influence is not added to your score." : "Раз в ход, до основного действия; ход не тратится (кроме «Контрактов с выбором» — это и есть ход). " +
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
                    int cost = _game.InfluenceCost(action);
                    var blocker = _game.GetInfluenceBlocker(action);
                    var row = Ui.Row();
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 8f;
                    var texts = Ui.Column();
                    texts.style.flexGrow = 1f;
                    texts.style.flexShrink = 1f;
                    texts.Add(Ui.Text(InfluenceTitle(action), 15f, Theme.Text, true));
                    var description = Ui.Text(blocker != null ? LocalizationService.TranslateLegacy(blocker) : InfluenceDescription(action), 12f, blocker != null ? Theme.Danger : Theme.TextDim);
                    description.style.whiteSpace = WhiteSpace.Normal;
                    texts.Add(description);
                    row.Add(texts);
                    var button = Ui.Button(EnglishUi ? $"{cost} inf." : $"{cost} вл.", () =>
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
            Group(LocalizationService.Get("game.ui.065"), true);
            Group(LocalizationService.Get("game.ui.066"), false);

            if (_game.BribesAllowed)
            {
                int fine = _game.Config.BribeFineFor(_game.Players.Count);
                var bribeHeader = Ui.Text(LocalizationService.Get("game.ui.067"), 15f, Theme.Text, true);
                bribeHeader.style.marginTop = 6f;
                box.Add(bribeHeader);
                var bribeNote = Ui.Text(EnglishUi ? $"Bribes stay secret until next round, when only their number is announced. You can be accused before that round ends: a correct accusation costs you −{fine} per bribe and rewards the accuser +{_game.Config.BribeWhistleReward} influence. Clues include drawing three or more extra cards or building a road below its usual cost." : $"Сейчас никто ничего не увидит. В начале следующего круга объявят только, сколько было взяток, — без имён. " +
                                        $"До конца того круга вас могут обвинить: угадал — штраф −{fine} за каждую взятку, ему +{_game.Config.BribeWhistleReward} влияния. " +
                                        "Улики против вас — рука выросла на 3+ карты или дорога дешевле своей длины.", 12f, Theme.TextDim);
                bribeNote.style.whiteSpace = WhiteSpace.Normal;
                bribeNote.style.marginBottom = 6f;
                box.Add(bribeNote);
                foreach (var kind in BribeInfo.All)
                {
                    var chosen = kind;
                    var blocker = _game.GetBribeBlocker(kind);
                    box.Add(ActionLine(BribeTitle(kind), blocker != null ? LocalizationService.TranslateLegacy(blocker) : BribeDescription(kind), blocker != null,
                        EnglishUi ? $"{BribeInfo.Cost(kind, _game.Config)} inf." : $"{BribeInfo.Cost(kind, _game.Config)} вл.", () =>
                        {
                            HideOverlay();
                            Act(() =>
                            {
                                var result = _game.Bribe(chosen);
                                if (result.Success) Flash(EnglishUi ? "Bribe paid. It will be announced next round without revealing your identity." : "Взятка дана. О ней узнают в начале следующего круга — но не кто дал.");
                                return result;
                            });
                        }));
                }

                var accuseHeader = Ui.Text(LocalizationService.Get("game.ui.068"), 15f, Theme.Text, true);
                accuseHeader.style.marginTop = 6f;
                box.Add(accuseHeader);
                var accuseNote = Ui.Text(EnglishUi
                    ? (_game.AnnouncedBribes > 0 ? $"Unresolved bribes last round: {_game.AnnouncedBribes}. You can also guess this round’s bribes. Wrong accusation: −{_game.Config.FalseAccusationFine} points." : $"No bribes were announced last round, but you can still accuse someone this round. Wrong accusation: −{_game.Config.FalseAccusationFine} points.")
                    : (_game.AnnouncedBribes > 0 ? $"Нераскрытых взяток прошлого круга: {_game.AnnouncedBribes}. Взятку этого круга тоже можно угадать. Ошибка — −{_game.Config.FalseAccusationFine} вам." : $"О взятках прошлого круга не объявляли, но взятку этого круга угадать можно. Ошибка — −{_game.Config.FalseAccusationFine} вам."), 12f, Theme.TextDim);
                accuseNote.style.whiteSpace = WhiteSpace.Normal;
                accuseNote.style.marginBottom = 6f;
                box.Add(accuseNote);
                var accuseRow = Ui.Row();
                accuseRow.style.flexWrap = Wrap.Wrap;
                foreach (var other in _game.Players.Where(p => p.Index != me.Index))
                {
                    int target = other.Index;
                    var cell = Ui.Column();
                    cell.style.marginRight = 8f;
                    cell.style.marginBottom = 6f;
                    var button = Ui.Button(LocalizationService.Get("game.ui.069") + other.Name, () =>
                    {
                        HideOverlay();
                        Act(() => _game.Accuse(target));
                    }, ButtonKind.Secondary, 13f);
                    Ui.SetEnabled(button, _game.GetAccuseBlocker(target) == null);
                    cell.Add(button);
                    string evidence = _game.SuspicionText(target);
                    var hint = Ui.Text(evidence.Length > 0 ? LocalizationService.Get("game.ui.070") + evidence : LocalizationService.Get("game.ui.071"), 11f,
                        evidence.Length > 0 ? Theme.Gold : Theme.TextDim);
                    hint.style.marginTop = 2f;
                    cell.Add(hint);
                    accuseRow.Add(cell);
                }
                box.Add(accuseRow);
            }

            var close = Ui.Button(LocalizationService.Get("game.ui.072"), HideOverlay, ButtonKind.Ghost, 15f);
            close.style.alignSelf = Align.FlexEnd;
            close.style.marginTop = 6f;
            box.Add(close);
            ShowOverlay(OverlayKind.Influence, Centered(box), Theme.Shade);
        }

        /// <summary>
        /// Игроки: подробные строки — в панели «Игроки» справа на поле (портрет из профиля, способность, очки и участки полосками),
        /// компактные портреты — во вкладке внизу по центру, всегда на виду (туда же летят карты и монетки).
        /// </summary>
        void BuildPlayerRows()
        {
            BuildLooks();
            _playersBox.Clear();
            _playerChipsRow.Clear();
            _playerRows = new PlayerRow[_game.Players.Count];
            _playerChips = new PlayerChip[_game.Players.Count];
            foreach (var player in _game.Players)
            {
                var color = Palette.PlayerColor(player.Index);
                var row = Ui.Column();
                row.style.marginBottom = 8f;
                Ui.Padding(row, 10f, 12f);
                Ui.Radius(row, 10f);
                Ui.Transitions(row, "background-color", "border-top-color", "border-bottom-color", "border-left-color", "border-right-color");

                var line = Ui.Row();
                line.style.alignItems = Align.Center;
                // Только картинка из профиля — без кружка персонажа (персонаж назван в строке имени).
                var portrait = new CharacterView(56f, true);
                portrait.Set(OutfitOf(player.Index));
                portrait.style.marginRight = 10f;
                portrait.style.backgroundColor = Theme.Panel;
                Ui.Radius(portrait, 12f);
                Ui.Border(portrait, 2f, color);
                line.Add(portrait);
                var badge = new ProfileBadge(16f);
                badge.style.marginRight = 6f;
                line.Add(badge);
                var names = Ui.Column();
                names.style.flexGrow = 1f;
                names.style.flexShrink = 1f;
                var name = Ui.Text("", 16f, null, true);
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                name.style.whiteSpace = WhiteSpace.NoWrap;
                names.Add(name);
                var title = Ui.Text("", 11f, Theme.Gold, true);
                names.Add(title);
                var stats = Ui.Number("", 13f, Theme.TextDim);
                stats.style.marginTop = 2f;
                names.Add(stats);
                line.Add(names);
                var score = Ui.Number("0", 24f, Theme.Text, true);
                score.style.marginLeft = 12f;
                line.Add(score);
                row.Add(line);

                var piecesLine = Ui.Row();
                piecesLine.style.marginTop = 8f;
                var piecesCaption = Ui.Text(LocalizationService.Get("game.ui.024"), 12f, Theme.TextDim);
                piecesCaption.style.width = 70f;
                piecesLine.Add(piecesCaption);
                var pieces = new ProgressBar(PlayersPanelWidth - 140f, 6f, color);
                piecesLine.Add(pieces);
                row.Add(piecesLine);

                var ability = Ui.Text("", 13f, Theme.TextDim);
                ability.style.whiteSpace = WhiteSpace.Normal;
                ability.style.marginTop = 6f;
                row.Add(ability);

                _playersBox.Add(row);
                _playerRows[player.Index] = new PlayerRow
                {
                    Root = row, Name = name, Stats = stats, Ability = ability, Badge = badge, Title = title,
                    Pieces = pieces, Score = score
                };

                // Портрет во вкладке «Игроки»: персонаж из профиля в кольце цвета игрока.
                var chip = Ui.Row();
                chip.pickingMode = PickingMode.Position;
                chip.style.marginLeft = 8f;
                var dot = Ui.Box();
                dot.style.width = 40f;
                dot.style.height = 40f;
                dot.style.backgroundColor = color;
                dot.style.alignItems = Align.Center;
                dot.style.justifyContent = Justify.Center;
                Ui.Radius(dot, 20f);
                Ui.Border(dot, 2f, new Color(0f, 0f, 0f, 0f));
                Ui.Transitions(dot, "scale", "border-top-color", "border-bottom-color", "border-left-color", "border-right-color");
                var face = new CharacterView(32f, true);
                face.Set(OutfitOf(player.Index));
                face.style.backgroundColor = Theme.Panel;
                Ui.Radius(face, 16f);
                dot.Add(face);
                chip.Add(dot);
                var chipScore = Ui.Number("0", 15f, Theme.Text, true);
                chipScore.style.marginLeft = 5f;
                chip.Add(chipScore);
                int index = player.Index;
                AttachTooltip(chip, () => PlayerTooltip(index));
                _playerChipsRow.Add(chip);
                _playerChips[player.Index] = new PlayerChip { Root = chip, Dot = dot, Score = chipScore };
            }
        }

        string PlayerTooltip(int index)
        {
            var player = _game.Players[index];
            string character = player.Character != CharacterKind.None ? " · " + CharacterLocalization.Title(player.Character) : "";
            return EnglishUi ? $"{player.Name}{character}{BotTag(index)}\n{_game.VisibleScore(player)} pts · {player.RoadPieces} road pieces · {player.HandSize} cards" + (_game.Config.InfluenceEnabled ? $" · {player.Influence} influence" : "") + "\nMore details in the Players panel." : $"{player.Name}{character}{BotTag(index)}\n{_game.VisibleScore(player)} очков · {player.RoadPieces} участков · {player.HandSize} карт" + (_game.Config.InfluenceEnabled ? $" · {player.Influence} влияния" : "") + "\nПодробнее — в панели «Игроки».";
        }

        void RefreshPlayers()
        {
            int acting = _game.ActingPlayerIndex;
            int start = Math.Max(1, _game.Config.RoadPiecesFor(_game.Players.Count));
            foreach (var player in _game.Players)
            {
                var row = _playerRows[player.Index];
                bool active = player.Index == acting;
                string character = player.Character != CharacterKind.None ? " · " + Characters.Title(player.Character) : "";
                row.Name.text = player.Name + character + BotTag(player.Index);
                row.Ability.text = player.Character == CharacterKind.None ? "" : CharacterLocalization.Ability(player.Character, _game.Config)
                    + (player.Character == CharacterKind.Foreman ? (EnglishUi ? $" Remaining: {player.FreeRoadsLeft}." : $" Осталось: {player.FreeRoadsLeft}.") : "");
                Ui.Show(row.Ability, player.Character != CharacterKind.None);
                // Видимые очки: дороги, цели, удача, штрафы и проезд (он платится по ходу партии). Контракты секретны.
                int points = _game.VisibleScore(player);
                row.Score.text = points.ToString();
                row.Stats.text = EnglishUi ? $"{player.RoadPieces} roads · {player.HandSize} cards" + (_game.Config.InfluenceEnabled ? $" · {player.Influence} inf." : "") : $"{player.RoadPieces} уч · {player.HandSize} карт" + (_game.Config.InfluenceEnabled ? $" · {player.Influence} вл" : "");
                row.Pieces.Set(player.RoadPieces / (float)start);
                row.Root.style.backgroundColor = active ? Theme.PanelHover : Theme.PanelLight;
                var look = LookOf(player.Index);
                var frame = Profiles.FrameColor(look.Frame);
                bool framed = frame.a > 0f;
                Ui.Border(row.Root, active || framed ? 2f : 1f, active ? Palette.PlayerColor(player.Index) : framed ? frame : Theme.Line);
                row.Badge.Set(look.Emblem, framed ? frame : new Color(0.98f, 0.78f, 0.25f));
                row.Title.text = Profiles.TitleText(look.Title);
                Ui.Show(row.Title, row.Title.text.Length > 0);

                var chip = _playerChips[player.Index];
                chip.Score.text = points.ToString();
                Ui.Border(chip.Dot, 2f, active ? Color.white : new Color(0f, 0f, 0f, 0f));
                chip.Dot.style.scale = new Scale(active ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one);
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
                _statusChip.style.backgroundColor = Theme.Gold;
                _statusName.text = LocalizationService.Get("game.ui.091");
            }

            _statusText.text = Instruction(acting) + TimerText();
            _statusText.style.color = TimerEnabled && HumanToAct && !_timerExpired && _turnTimeLeft <= 10f ? Theme.Danger : Theme.Text;
            MarkLogSeen();
            _finalRound.text = FinalText();
            _finalRound.style.color = _game.IsFinalRound ? Theme.Gold : Theme.TextDim;
            Ui.Show(_finalRound, _finalRound.text.Length > 0 && _game.Phase != GamePhase.GameOver);
            _flashLabel.text = _flash == null ? "" :
                (_flashPrefixKey == null ? "" : LocalizationService.Get(_flashPrefixKey)) +
                LocalizationService.TranslateGameError(_flash);
            Ui.Show(_flashLabel, _flash != null);
            Ui.Show(_passButton, HumanToAct && _game.Phase == GamePhase.AwaitingAction && !_game.HasAnyLegalAction());
        }

        string Instruction(int acting)
        {
            if (acting < 0) return LocalizationService.Get("game.ui.092");
            if (AwaitingHost) return LocalizationService.Get("game.ui.039");
            if (IsRealBot(acting)) return LocalizationService.Get("game.ui.093");
            if (IsBot(acting)) return EnglishUi ? $"{_game.Players[acting].Name} is playing online…" : $"Ходит {_game.Players[acting].Name} (по сети)…";
            if (NeedsHandoff) return LocalizationService.Get("game.ui.094");

            switch (_game.Phase)
            {
                case GamePhase.SetupContracts:
                    return _game.ActingPlayerIndex >= 0 && _game.Players[_game.ActingPlayerIndex].CharacterOffer.Count > 0
                        ? LocalizationService.Get("game.ui.095")
                        : LocalizationService.Get("game.ui.096");
                case GamePhase.AwaitingAction:
                    return LocalizationService.Get("game.ui.097");
                case GamePhase.DrawingSecondCard:
                    return LocalizationService.Get("game.ui.098");
                case GamePhase.ChoosingContracts:
                    return LocalizationService.Get("game.ui.099");
                case GamePhase.ChoosingLuck:
                    return LocalizationService.Get("game.ui.100");
                case GamePhase.RespondingTrade:
                    return LocalizationService.Get("game.ui.101");
                case GamePhase.Bidding:
                    return LocalizationService.Get("game.ui.102");
                default:
                    return "";
            }
        }

        /// <summary>Ресурсы в верхней панели: деления по картам в руке и полоска оставшихся участков.</summary>
        void RefreshHand()
        {
            var viewer = _game.Players[Viewer];
            bool hidden = NeedsHandoff;
            _handTitle.text = hidden ? LocalizationService.Get("game.ui.103") : HumanCount > 1 ? viewer.Name : "";
            foreach (var pair in _hand) pair.Value.Set(viewer.CardCount(pair.Key), hidden);

            int start = Math.Max(1, _game.Config.RoadPiecesFor(_game.Players.Count));
            _piecesLabel.text = viewer.RoadPieces.ToString();
            bool low = viewer.RoadPieces <= _game.Config.FinalWarningPieces;
            _piecesBar.Set(viewer.RoadPieces / (float)start, low ? Theme.Danger : Theme.Success);
        }

        void RefreshMap()
        {
            // A preview must never survive the end of a construction action
            // or remain underneath a modal dialog.
            if (!HumanToAct || _game.Phase != GamePhase.AwaitingAction ||
                _overlayKind != OverlayKind.None)
                _map.SetConstructionPreview(null, false, false);
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
            _map.SetConstructionPreview(null, false, false);
            _overlayKind = kind;
            _map.Interactive = false;
            _map.Affordable.Clear();
            if (kind != OverlayKind.Payment && kind != OverlayKind.Upgrade) _map.SelectedRoadId = null;
        }

        void HideOverlay()
        {
            _overlay.Clear();
            Ui.Show(_overlay, false);
            _overlayKind = OverlayKind.None;
            _overlayPlayer = -1;
            _map.Interactive = true;
            _map.SelectedRoadId = null;
            _map.SetConstructionPreview(null, false, false);
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
            Ui.Border(box, 2f, Theme.Gold);

            box.Add(Ui.Text(EnglishUi ? $"{DisplayPlace(LocationName(route.A))} — {DisplayPlace(LocationName(route.B))}: how will you pay?" : $"{LocationName(route.A)} — {LocationName(route.B)}: чем платить?", 15f, null, true));
            if (canBuildFree)
            {
                var free = Ui.Button(EnglishUi ? $"Free — Foreman ({_game.CurrentPlayer.FreeRoadsLeft} left)" : $"Бесплатно — Прораб (осталось {_game.CurrentPlayer.FreeRoadsLeft})", () =>
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
            var cancel = Ui.Button(LocalizationService.Get("game.ui.104"), HideOverlay, ButtonKind.Ghost, 14f);
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
            Ui.Border(box, 3f, Theme.Gold);
            box.Add(Ui.Text(LocalizationService.Get("game.ui.105"), 24f, Theme.Gold, true));
            var note = Ui.Text(LocalizationService.Get("game.ui.106"), 13f, Theme.TextDim);
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
                card.Add(Ui.Text(buff ? LocalizationService.Get("game.ui.052") : LocalizationService.Get("game.ui.053"), 13f, color, true));
                var title = Ui.Text(LuckCardTitle(kind), 22f, Theme.Text, true);
                title.style.marginTop = 8f;
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.whiteSpace = WhiteSpace.Normal;
                card.Add(title);
                bool insured = !buff && _game.CurrentPlayer.Character == CharacterKind.Insurer;
                var text = Ui.Text(insured ? (EnglishUi ? $"Insurance: only −{_game.Config.InsurerFine} pt." : $"Страховка: только −{_game.Config.InsurerFine} очко.") : LuckCardDescription(kind, _game.Config), 13f, Theme.TextDim);
                text.style.marginTop = 10f;
                text.style.unityTextAlign = TextAnchor.MiddleCenter;
                text.style.whiteSpace = WhiteSpace.Normal;
                card.Add(text);
                var take = Ui.Button(LocalizationService.Get("game.ui.107"), () =>
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
        /// <summary>Наставник обучающей партии (null — обычная партия).</summary>
        TutorialCoach _coach;
        int _lastFinalWarningShown;
        int _lastCoopNewsShown;
        CoopPanel _coopPanel;
        int _lastFinalOrderShown;
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
            Ui.Border(box, 3f, Theme.Gold);
            box.Add(Ui.Text(LocalizationService.Get("game.ui.108"), 13f, Theme.TextDim, true));
            var title = Ui.Text(ContractTitle(contract), 26f, Theme.Gold, true);
            title.style.marginTop = 6f;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            box.Add(title);
            var value = Ui.Text(EnglishUi ? $"{contract.Points} points. Complete it by the end to earn another +{_game.Config.AuctionBonus}; fail and lose {contract.Points} points, just like a normal contract." : $"{contract.Points} очков, а выполнен к концу партии — ещё +{_game.Config.AuctionBonus}. Не выполнен — −{contract.Points}, как обычный контракт.", 15f, Theme.Text);
            value.style.whiteSpace = WhiteSpace.Normal;
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            value.style.marginTop = 8f;
            box.Add(value);
            var rules = Ui.Text(LocalizationService.Get("game.ui.109"), 12f, Theme.TextDim);
            rules.style.whiteSpace = WhiteSpace.Normal;
            rules.style.unityTextAlign = TextAnchor.MiddleCenter;
            rules.style.marginTop = 8f;
            box.Add(rules);

            var done = _game.Players.Where(p => p.Index != bidder && _game.HasBid(p.Index)).Select(p => p.Name).ToList();
            var status = Ui.Text(done.Count > 0 ? LocalizationService.Get("game.ui.110") + string.Join(", ", done) : LocalizationService.Get("game.ui.111"), 12f, Theme.TextDim);
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
                amount.text = bid + (EnglishUi ? " inf." : " вл.");
                place.text = bid > 0 ? (EnglishUi ? $"Bid {bid}" : $"Поставить {bid}") : LocalizationService.Get("game.ui.112");
            }
            row.Add(Ui.Button("–", () => { bid = Math.Max(0, bid - 1); Refresh(); }, ButtonKind.Secondary, 20f));
            row.Add(amount);
            row.Add(Ui.Button("+", () => { bid = Math.Min(me.Influence, bid + 1); Refresh(); }, ButtonKind.Secondary, 20f));
            box.Add(row);
            box.Add(Ui.Text(EnglishUi ? $"Your influence: {me.Influence}" : $"Ваше влияние: {me.Influence}", 13f, Theme.TextDim));

            place = Ui.Button("", () => { HideOverlay(); Act(() => _game.Bid(bid)); }, ButtonKind.Primary, 17f);
            place.style.marginTop = 16f;
            box.Add(place);
            Refresh();
            ShowOverlay(OverlayKind.Auction, Centered(box), Theme.Shade);
            _overlayPlayer = bidder;
        }

        static string TradeSide(int count, Resource resource, int influence, bool pass = false)
        {
            if (!EnglishUi) return TradeOffer.Side(count, resource, influence, pass);
            var parts = new List<string>();
            if (count > 0) parts.Add($"{count} × {Palette.CardTitle(resource)}");
            if (influence > 0) parts.Add($"{influence} influence");
            if (pass) parts.Add("road pass");
            return parts.Count > 0 ? string.Join(" and ", parts) : "nothing";
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
            bool passesOn = _game.Config.PassesEnabled;
            bool givePass = false, getPass = false;
            int minCards = influenceOn || passesOn ? 0 : 1; // с влиянием или пропуском одна из сторон может быть без карт

            var box = DialogBox(560f);
            Ui.Border(box, 2f, Theme.Gold);
            box.Add(Ui.Text(LocalizationService.Get("game.ui.020"), 26f, Theme.Gold, true));
            var note = Ui.Text(LocalizationService.Get("game.ui.113")
                               + (influenceOn ? LocalizationService.Get("game.ui.114") : "")
                               + (passesOn ? LocalizationService.Get("game.ui.115") : ""),
                13f, Theme.TextDim);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginBottom = 14f;
            box.Add(note);

            var summary = Ui.Text("", 15f, Theme.Text, true);
            summary.style.whiteSpace = WhiteSpace.Normal;
            var propose = Ui.Button(LocalizationService.Get("game.ui.116"), () => { }, ButtonKind.Primary, 16f);

            void RefreshSummary()
            {
                string passProblem = givePass && _game.HasPass(to, me.Index)
                    ? (EnglishUi ? $" {_game.Players[to].Name} already has your road pass." : $" У {_game.Players[to].Name} уже есть ваш пропуск.")
                    : getPass && _game.HasPass(me.Index, to)
                        ? (EnglishUi ? $" You already have {_game.Players[to].Name}'s road pass." : $" У вас уже есть пропуск {_game.Players[to].Name}.") : "";
                summary.text = EnglishUi
                    ? $"{_game.Players[to].Name} receives {TradeSide(giveCount, give, giveInfluence, givePass)}; you receive {TradeSide(getCount, get, getInfluence, getPass)}. Your {Palette.CardTitle(give)}: {me.CardCount(give)}" + (influenceOn ? $", influence: {me.Influence}." : ".") + passProblem
                    : $"{_game.Players[to].Name} получит {TradeSide(giveCount, give, giveInfluence, givePass)}, " +
                      $"а вы — {TradeSide(getCount, get, getInfluence, getPass)}. У вас {Palette.CardTitle(give)}: {me.CardCount(give)}" +
                      (influenceOn ? $", влияния: {me.Influence}." : ".") + passProblem;
                bool valid = (giveCount + giveInfluence > 0 || givePass) && (getCount + getInfluence > 0 || getPass)
                             && !(giveCount > 0 && getCount > 0 && give == get)
                             && me.CardCount(give) >= giveCount && me.Influence >= giveInfluence
                             && passProblem.Length == 0;
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
            box.Add(Line(LocalizationService.Get("game.ui.117"), Cycle(() => _game.Players[to].Name, () =>
            {
                othersIndex = (othersIndex + 1) % others.Count;
                to = others[othersIndex].Index;
            })));
            var giveRow = Ui.Row();
            giveRow.Add(Cycle(() => Palette.CardTitle(give), () => give = NextResource(give)));
            giveRow.Add(Counter(() => giveCount, v => giveCount = v, minCards, max));
            box.Add(Line(LocalizationService.Get("game.ui.118"), giveRow));
            if (influenceOn) box.Add(Line(LocalizationService.Get("game.ui.119"), Counter(() => giveInfluence, v => giveInfluence = v, 0, maxInfluence)));
            if (passesOn) box.Add(Line(LocalizationService.Get("game.ui.120"), Cycle(() => givePass ? LocalizationService.Get("game.ui.121") : LocalizationService.Get("game.ui.122"), () => givePass = !givePass)));
            var getRow = Ui.Row();
            getRow.Add(Cycle(() => Palette.CardTitle(get), () => get = NextResource(get)));
            getRow.Add(Counter(() => getCount, v => getCount = v, minCards, max));
            box.Add(Line(LocalizationService.Get("game.ui.123"), getRow));
            if (influenceOn) box.Add(Line(LocalizationService.Get("game.ui.119"), Counter(() => getInfluence, v => getInfluence = v, 0, maxInfluence)));
            if (passesOn) box.Add(Line(LocalizationService.Get("game.ui.120"), Cycle(() => getPass ? LocalizationService.Get("game.ui.124") : LocalizationService.Get("game.ui.125"), () => getPass = !getPass)));

            summary.style.marginTop = 6f;
            summary.style.marginBottom = 16f;
            box.Add(summary);

            var buttons = Ui.Row();
            buttons.style.justifyContent = Justify.FlexEnd;
            var cancel = Ui.Button(LocalizationService.Get("game.ui.104"), HideOverlay, ButtonKind.Ghost, 15f);
            cancel.style.marginRight = 10f;
            buttons.Add(cancel);
            propose.clicked += () =>
            {
                HideOverlay();
                Act(() => _game.ProposeTrade(to, give, giveCount, get, getCount, giveInfluence, getInfluence, givePass, getPass));
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
            box.Add(Ui.Text(LocalizationService.Get("game.ui.126"), 14f, Theme.TextDim, true));
            var who = Ui.Text(_game.Players[offer.From].Name, 30f, Palette.PlayerColor(offer.From), true);
            who.style.marginTop = 4f;
            box.Add(who);
            string mine = offer.GetCount > 0 ? (EnglishUi ? $"you have {me.CardCount(offer.GetResource)} × {Palette.CardTitle(offer.GetResource)}" : $"у вас {me.CardCount(offer.GetResource)} × {Palette.CardTitle(offer.GetResource)}") : "";
            if (offer.GetInfluence > 0) mine += (mine.Length > 0 ? ", " : "") + (EnglishUi ? $"influence {me.Influence}" : $"влияния {me.Influence}");
            string passNote = offer.GivePass || offer.GetPass
                ? (EnglishUi ? "\n<size=13>A road pass allows free travel on all roads owned by the seller until the game ends.</size>" : "\n<size=13>Пропуск — бесплатный проезд по всем дорогам продавца до конца партии.</size>") : "";
            var text = Ui.Text((EnglishUi ? $"You receive {TradeSide(offer.GiveCount, offer.GiveResource, offer.GiveInfluence, offer.GivePass)}\n" + $"and give {TradeSide(offer.GetCount, offer.GetResource, offer.GetInfluence, offer.GetPass)}" : $"Вы получите {TradeSide(offer.GiveCount, offer.GiveResource, offer.GiveInfluence, offer.GivePass)}\n" + $"и отдадите {TradeSide(offer.GetCount, offer.GetResource, offer.GetInfluence, offer.GetPass)}") +
                               (mine.Length > 0 ? $" ({mine})" : "") + "." + passNote, 17f, Theme.Text);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 14f;
            text.style.marginBottom = 20f;
            box.Add(text);
            var buttons = Ui.Row();
            var no = Ui.Button(LocalizationService.Get("game.ui.127"), () => { HideOverlay(); Act(() => _game.RespondTrade(false)); }, ButtonKind.Secondary, 16f);
            no.style.marginRight = 12f;
            buttons.Add(no);
            buttons.Add(Ui.Button(LocalizationService.Get("game.ui.128"), () => { HideOverlay(); Act(() => _game.RespondTrade(true)); }, ButtonKind.Primary, 16f));
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

            box.Add(Ui.Text(EnglishUi ? $"{DisplayPlace(LocationName(route.A))} — {DisplayPlace(LocationName(route.B))}: upgrade to highway?" : $"{LocationName(route.A)} — {LocationName(route.B)}: улучшить до магистрали?", 15f, null, true));
            var info = Ui.Text(EnglishUi ? $"Costs {cost} cards and uses your turn. +{_game.Config.UpgradePoints} pts. Rivals pay ×{_game.Config.RushHourTollMultiplier} tolls afterward" + (_game.IsJammed(route.Id) ? "; this clears the traffic jam." : ". Highways do not suffer traffic jams.") : $"Стоит {cost} карт(ы), занимает ход. +{_game.Config.UpgradePoints} очка, дальше проезд для соперников ×{_game.Config.RushHourTollMultiplier}" +
                               (_game.IsJammed(route.Id) ? ", и пробка на ней исчезнет." : ". Магистраль не встаёт в пробку."), 12f, Theme.TextDim);
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.marginTop = 4f;
            box.Add(info);
            if (options.Count == 0)
            {
                var none = Ui.Text(LocalizationService.Get("game.ui.129"), 13f, Theme.Danger);
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
            var cancel = Ui.Button(LocalizationService.Get("game.ui.104"), HideOverlay, ButtonKind.Ghost, 14f);
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

            box.Add(Ui.Text(LocalizationService.Get("game.ui.130"), 14f, Theme.TextDim, true));
            var title = Ui.Text(player.Name, 36f, Palette.PlayerColor(playerIndex), true);
            title.style.marginTop = 6f;
            box.Add(title);
            var text = Ui.Text(LocalizationService.Get("game.ui.131"), 15f, Theme.Text);
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
                report.Add(Ui.Text(LocalizationService.Get("game.ui.132"), 13f, Theme.Gold, true));
                const int maxLines = 8;
                if (unseen.Count > maxLines)
                    report.Add(Ui.Text(EnglishUi ? $"…and {unseen.Count - maxLines} earlier events" : $"…и ещё {unseen.Count - maxLines} событий раньше", 12f, Theme.TextDim));
                foreach (var line in unseen.Skip(Math.Max(0, unseen.Count - maxLines)))
                {
                    var label = Ui.Text("• " + JournalDisplayText(line), 12f, Theme.Text);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.marginTop = 3f;
                    report.Add(label);
                }
                box.Add(report);
            }
            box.Add(Ui.Button(LocalizationService.Get("game.ui.133"), () =>
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

            // Победители в полный рост рядом с заголовком.
            var hero = Ui.Row();
            hero.style.justifyContent = Justify.Center;
            hero.style.alignItems = Align.FlexEnd;
            foreach (var winner in result.Winners.Take(3))
            {
                var figure = new CharacterView(result.Winners.Count > 1 ? 80f : 100f);
                figure.Set(OutfitOf(winner.PlayerIndex));
                figure.style.marginLeft = 6f;
                figure.style.marginRight = 6f;
                hero.Add(figure);
            }
            if (result.Cooperative && result.Winners.Count == 0)
            {
                // Поражение команды: вместо победителей — город.
                var city = new MenuIcon(MenuIcon.Glyph.Skyline, 90f, Theme.Danger);
                city.style.marginRight = 6f;
                hero.Add(city);
            }
            var title = Ui.Text(result.Cooperative
                    ? (result.TeamWon ? LocalizationService.Get("game.ui.134") : LocalizationService.Get("game.ui.135"))
                    : (EnglishUi ? "Victory: " : "Победа: ") + string.Join(", ", result.Winners.Select(w => w.PlayerName)),
                34f, result.Cooperative && !result.TeamWon ? Theme.Danger : Theme.Gold, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginLeft = 16f;
            title.style.marginBottom = 20f;
            hero.Add(title);
            box.Add(hero);
            if (result.Cooperative)
            {
                var outcome = Ui.Text((EnglishUi ? (result.TeamWon ? "The team saved the city." : "The city defeated the team.") : result.CoopOutcome) + (EnglishUi ? $" Team score: {result.TeamTotal}." : $" Очки команды: {result.TeamTotal}."), 16f, Theme.Text, true);
                outcome.style.whiteSpace = WhiteSpace.Normal;
                outcome.style.unityTextAlign = TextAnchor.MiddleCenter;
                outcome.style.marginBottom = 6f;
                box.Add(outcome);
            }
            var subtitle = Ui.Text(EnglishUi ? "Roads + contracts (★ City Hall bonus) ± tolls + Largest Network + City Hall goals ± luck + character abilities (Official: contracts, Bridge Builder: bridges)" : "Дороги + контракты (★ — бонус контракта мэрии) ± плата за проезд + «Крупнейшая сеть» + цели мэрии ± удача + способность персонажа (Чиновник — за контракты, Мостовик — за мосты)", 13f, Theme.TextDim);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 20f;
            box.Add(subtitle);

            float[] widths = { 190f, 64f, 140f, 70f, 100f, 58f, 62f, 82f, 70f, 64f };
            box.Add(ResultRow(new[] { LocalizationService.Get("game.ui.136"), LocalizationService.Get("game.ui.137"), LocalizationService.Get("game.ui.018"), LocalizationService.Get("game.ui.138"), LocalizationService.Get("game.ui.139"), LocalizationService.Get("game.ui.140"), LocalizationService.Get("game.ui.017"), LocalizationService.Get("game.ui.141"), LocalizationService.Get("game.ui.142"), LocalizationService.Get("game.ui.143") }, widths, Theme.TextDim, -1, false));

            int place = 1;
            foreach (var s in result.Ranking)
            {
                string contracts = $"{s.ContractPoints:+#;-#;0} ({s.CompletedContracts} {(EnglishUi ? "of" : "из")} {s.CompletedContracts + s.FailedContracts})"
                                   + (s.MayorBonus > 0 ? $" ★+{s.MayorBonus}" : "");
                int tollBalance = s.TollsEarned - s.TollsPaid - s.JamFees;
                string tolls = $"{tollBalance:+#;-#;0}" + (s.JamFees > 0 ? (EnglishUi ? $" (jams −{s.JamFees})" : $" (пробки −{s.JamFees})") : "");
                string network = s.NetworkDistricts + (EnglishUi ? " dist." : " р.") + (s.NetworkBonus > 0 ? $" (+{s.NetworkBonus})" : "");
                string character = s.Character == CharacterKind.None ? "—" : s.CharacterPoints > 0 ? "+" + s.CharacterPoints : "0";
                var cells = new[] { $"{place}. {s.PlayerName}", s.RoadPoints.ToString(), contracts, tolls, network,
                    s.GoalPoints.ToString(), $"{s.LuckPoints:+#;-#;0}", character, s.Penalties == 0 ? "—" : s.Penalties.ToString(), s.Total.ToString() };
                box.Add(ResultRow(cells, widths, Theme.Text, s.PlayerIndex, place == 1));
                place++;
            }

            var friends = _game.Result.Ranking.Where(r => r.FriendBonus > 0).ToList();
            if (friends.Count > 0)
            {
                var friend = Ui.Text((EnglishUi ? "Mayor’s Friend — most influence spent: " : "«Друг мэра» — больше всех потратил влияния: ") + string.Join(", ", friends.Select(f => $"{f.PlayerName} +{f.FriendBonus}")) + (EnglishUi ? " (final score)." : " (в итоге)."),
                    13f, Theme.Gold);
                friend.style.whiteSpace = WhiteSpace.Normal;
                friend.style.unityTextAlign = TextAnchor.MiddleCenter;
                friend.style.marginTop = 8f;
                box.Add(friend);
            }

            if (_game.FinalAudit.Count > 0)
            {
                var found = _game.FinalAudit.Where(a => a.Exposed > 0).ToList();
                var audit = Ui.Text(found.Count > 0
                        ? (EnglishUi ? "Final audit uncovered bribes: " : "Проверка перед итогами нашла взятки: ") + string.Join(" · ", found.Select(a => $"{_game.Players[a.Player].Name} −{a.Fine}")) + (EnglishUi ? " (see Penalties)." : " (в столбце «Штрафы»).")
                        : LocalizationService.Get("game.ui.144"), 13f, found.Count > 0 ? Theme.Danger : Theme.TextDim);
                audit.style.whiteSpace = WhiteSpace.Normal;
                audit.style.unityTextAlign = TextAnchor.MiddleCenter;
                audit.style.marginTop = 8f;
                box.Add(audit);
            }

            if (_profileCredits != null && _profileCredits.Count > 0)
            {
                var credit = Ui.Text(LocalizationService.Get("game.ui.145") + string.Join(" · ", _profileCredits.Select(c => $"{c.Profile} +{c.Amount + c.Bonus}"))
                                     + (EnglishUi ? " — spend it on profile cosmetics." : " — тратится на оформление в «Профиле»."), 14f, Theme.Gold, true);
                credit.style.whiteSpace = WhiteSpace.Normal;
                credit.style.unityTextAlign = TextAnchor.MiddleCenter;
                credit.style.marginTop = 10f;
                box.Add(credit);
                // Новые достижения и задание дня — их награда уже в сумме выше.
                foreach (var c in _profileCredits.Where(x => (x.Unlocked != null && x.Unlocked.Count > 0) || x.DailyDone))
                {
                    var parts = (c.Unlocked ?? new List<Achievement>()).Select(a => $"«{a.Title}» +{a.Reward}").ToList();
                    if (c.DailyDone) parts.Add(EnglishUi ? $"daily challenge +{Achievements.DailyReward}" : $"задание дня +{Achievements.DailyReward}");
                    var line = Ui.Text($"★ {c.Profile}: " + string.Join(", ", parts), 13f, Theme.Gold);
                    line.style.whiteSpace = WhiteSpace.Normal;
                    line.style.unityTextAlign = TextAnchor.MiddleCenter;
                    line.style.marginTop = 4f;
                    box.Add(line);
                }
            }

            if (_game.Config.InfluenceEnabled)
            {
                var influence = Ui.Text((EnglishUi ? "Influence at game end (not part of the score; remaining human-player influence goes to profiles): " : "Влияние на конец партии (в очки не входит, остаток людей уходит в профиль): ")
                                        + string.Join(" · ", result.Ranking.Select(r => EnglishUi ? $"{r.PlayerName} — {r.Influence} (earned {r.InfluenceEarned})" : $"{r.PlayerName} — {r.Influence} (заработано {r.InfluenceEarned})")),
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
            if (_setup.tutorial)
            {
                var done = Ui.Text(EnglishUi ? "Tutorial complete! Full games add city events, City Hall goals, luck cards, characters, trades and passes, road upgrades, City Hall auctions and bribes. See Rules and first-game tips for details." : "Обучение пройдено! В обычной партии есть ещё: городские события, цели мэрии, карты удачи, " +
                                   "персонажи, сделки и пропуска, улучшение дорог, аукцион мэрии и взятки. Всё это — в «Правилах», " +
                                   "а коротко — в подсказках первой партии.", 14f, Theme.Gold);
                done.style.whiteSpace = WhiteSpace.Normal;
                done.style.unityTextAlign = TextAnchor.MiddleCenter;
                done.style.marginTop = 14f;
                box.Add(done);
            }
            var again = _setup.tutorial
                ? Ui.Button(LocalizationService.Get("game.ui.146"), _app.StartTutorial, ButtonKind.Primary, 17f)
                : Ui.Button(LocalizationService.Get("game.ui.147"), () => _app.StartGame(_setup), ButtonKind.Primary, 17f);
            again.style.marginRight = 12f;
            buttons.Add(again);
            buttons.Add(Ui.Button(LocalizationService.Get("game.ui.031"), _app.ShowMenu, ButtonKind.Secondary, 17f));
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
                column.Add(Ui.Text(caption, 14f, Theme.Gold, true));
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
            var goals = Column(LocalizationService.Get("game.ui.026"));
            goals.style.marginRight = 12f;
            if (_game.ActiveGoals.Count == 0) goals.Add(Line(LocalizationService.Get("game.ui.148"), Theme.TextDim));
            foreach (var goal in _game.ActiveGoals)
            {
                var owner = _game.GoalOwner(goal.Id);
                goals.Add(owner.HasValue
                    ? Line($"«{MayorGoalName(goal)}» — {_game.Players[owner.Value].Name}, +{goal.Points}", Palette.PlayerColor(owner.Value))
                    : Line(EnglishUi ? $"“{MayorGoalName(goal)}” — no winner" : $"«{goal.Name}» — никто не выполнил", Theme.TextDim));
            }
            row.Add(goals);

            // Удача.
            var luck = Column(LocalizationService.Get("game.ui.017"));
            var players = _game.Players;
            int drawnTotal = players.Sum(p => p.LuckBuffs + p.LuckDebuffs);
            if (!_game.Config.LuckEnabled) luck.Add(Line(LocalizationService.Get("game.ui.149"), Theme.TextDim));
            else if (drawnTotal == 0) luck.Add(Line(LocalizationService.Get("game.ui.150"), Theme.TextDim));
            else
            {
                foreach (var p in players.OrderByDescending(x => x.LuckPoints).ThenBy(x => x.Index))
                {
                    int drawn = p.LuckBuffs + p.LuckDebuffs;
                    string text = drawn == 0
                        ? (EnglishUi ? $"{p.Name}: did not take risks" : $"{p.Name}: не рисковал")
                        : (EnglishUi ? $"{p.Name}: {drawn} cards ({p.LuckBuffs} buffs, {p.LuckDebuffs} debuffs), {p.LuckPoints:+#;-#;0} pts" : $"{p.Name}: {drawn} карт ({p.LuckBuffs} баф., {p.LuckDebuffs} дебаф.), {p.LuckPoints:+#;-#;0} очк.");
                    luck.Add(Line(text, Palette.PlayerColor(p.Index)));
                }
                var luckiest = players.Where(p => p.LuckBuffs + p.LuckDebuffs > 0).OrderByDescending(p => p.LuckPoints).First();
                var unluckiest = players.Where(p => p.LuckBuffs + p.LuckDebuffs > 0).OrderBy(p => p.LuckPoints).First();
                if (luckiest.LuckPoints > 0) luck.Add(Line(EnglishUi ? $"Luckiest: {luckiest.Name}" : $"Самый везучий: {luckiest.Name}", Theme.Success, true));
                if (unluckiest != luckiest && unluckiest.LuckPoints < 0) luck.Add(Line(EnglishUi ? $"Unluckiest: {unluckiest.Name}" : $"Не повезло: {unluckiest.Name}", Theme.Danger, true));
                luck.Add(Line(LocalizationService.Get("game.ui.151"), Theme.TextDim));
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
                var cell = Ui.Text(cells[i], total ? 20f : 15f, total ? Theme.Gold : color, total || i == 0);
                cell.style.width = widths[i];
                cell.style.whiteSpace = WhiteSpace.NoWrap;
                row.Add(cell);
            }
            return row;
        }

        void ShowConfirmExit()
        {
            var box = DialogBox(480f);
            box.Add(Ui.Text(LocalizationService.Get("game.ui.152"), 22f, null, true));
            var text = Ui.Text(!Online ? LocalizationService.Get("game.ui.153")
                : _net.IsHost ? LocalizationService.Get("game.ui.154") : LocalizationService.Get("game.ui.155"), 15f, Theme.TextDim);
            text.style.marginTop = 8f;
            text.style.marginBottom = 20f;
            box.Add(text);

            var buttons = Ui.Row();
            var exit = Ui.Button(LocalizationService.Get("game.ui.156"), _app.ShowMenu, ButtonKind.Primary, 16f);
            exit.style.marginRight = 10f;
            buttons.Add(exit);
            buttons.Add(Ui.Button(LocalizationService.Get("game.ui.157"), HideOverlay, ButtonKind.Secondary, 16f));
            box.Add(buttons);

            ShowOverlay(OverlayKind.ConfirmExit, Centered(box), Theme.Shade);
        }

        void ShowError(string message)
        {
            var box = DialogBox(640f);
            box.Add(Ui.Text(LocalizationService.Get("game.ui.158"), 22f, Theme.Danger, true));
            var text = Ui.Text(message, 14f, Theme.Text);
            text.style.marginTop = 10f;
            text.style.marginBottom = 20f;
            box.Add(text);
            box.Add(Ui.Button(LocalizationService.Get("game.ui.031"), _app.ShowMenu, ButtonKind.Primary, 16f));
            ShowOverlay(OverlayKind.ConfirmExit, Centered(box), Theme.ShadeOpaque);
        }

        // ================================================================== Обучение

        sealed class TutorialStep
        {
            public Func<VisualElement> Target;
            public string Title;
            public string Text;
        }

        static readonly string[] EnglishFirstGameTutorialTitles =
        {
            "The Map", "City Hall Contracts", "Resources", "Your Cards",
            "Tolls", "City Events", "Characters", "City Hall Goals", "Luck Cards"
        };

        static readonly string[] EnglishFirstGameTutorialTexts =
        {
            "You run a road company. Build roads between city locations to earn points immediately (a length-four road earns seven points). Colored areas represent districts.",
            "Your contracts are in the Contracts tab at the bottom. Swipe through the cards. Connect any locations in the two required districts. Completed contracts award points; unfinished ones deduct points. Transit contracts pass through a third district, and hub contracts connect one district to two others. Hover to highlight the route.",
            "Face-up cards and the deck are in Actions. Draw two cards per turn, from the market or the deck. Asphalt builds streets, concrete builds avenues, steel builds bridges, and gravel builds country roads. Machinery is a wild card; taking face-up machinery uses both draws.",
            "Your hand is below the map beside Players. Each segment represents one card. A length-three road costs three matching resource cards. White roads can use any one resource type. Steel and bridges are scarce.",
            "Rival roads are not dead ends. A contract route must use at least one of your roads and may use up to two rival roads. Pay their owners two points per rival road. The largest district network earns a bonus of ten points.",
            "A colored marker at the top announces city events. Hover or open Menu for details. Events can appear at the start of a round: rush hour doubles tolls on affected roads, repairs close road segments, subsidies make bridges cheaper, and a new district adds locations and contracts.",
            "Each company has a character ability shown in Players. Lucky chooses between two luck cards, Foreman can build a free road, and Toll Collector earns more in tolls. Make use of your character's strengths.",
            "Open Menu in the top-right corner for City Hall goals. Three shared goals are available to all players; the first player to complete each goal claims the reward. Watch your rivals' progress.",
            "Luck is in Actions. While drawing cards, replace one of your two draws with a luck card, at most once per turn. Benefits include grants, resource deliveries and contract bonuses. Risks include fines, theft and contract recalls."
        };

        TutorialStep[] TutorialSteps() => new[]
        {
            new TutorialStep
            {
                Target = () => _map,
                Title = _game?.Map.Name ?? LocalizationService.Get("game.ui.159"),
                Text = "Вы — дорожная компания. Стройте участки дорог между местами города: каждая дорога сразу приносит очки (дорога длиной 4 — 7 очков). Цветные пятна — районы."
            },
            new TutorialStep
            {
                Target = () => _contractsTab,
                Title = "Контракты мэрии",
                Text = "Ваши контракты — во вкладке «Контракты» внизу, карточки листаются свайпом. Контракт — соединить два района, подойдёт любое их место. Выполненный контракт прибавляет очки, невыполненный отнимает. Сложные дороже: транзит — один путь через третий район, узел — первый район связать с двумя другими. Наведите на контракт — районы и путь подсветятся."
            },
            new TutorialStep
            {
                Target = () => _actionsTab,
                Title = "Ресурсы",
                Text = "Открытые карты и колода — в панели «Действия» внизу. За ход можно взять 2 карты: открытые или из колоды. Асфальт — улицы, бетон — проспекты, сталь — мосты, щебень — загородные дороги. Техника — джокер, но открытая техника заменяет обе карты."
            },
            new TutorialStep
            {
                Target = () => _handRow,
                Title = "Ваши карты",
                Text = "Ваши карты — внизу, справа от «Игроков»: одно деление — одна карта. Чтобы построить дорогу длиной 3, нужно 3 карты её ресурса. Белые дороги строятся любым одним ресурсом. Мостов мало и стали мало — за них будет борьба."
            },
            new TutorialStep
            {
                Target = () => _map,
                Title = "Платный проезд",
                Text = "Чужая дорога — не тупик. Контракт засчитается, если на пути есть хотя бы одна ваша дорога и не больше двух чужих: за каждую чужую вы платите владельцу 2 очка. Самая большая сеть по районам даёт +10."
            },
            new TutorialStep
            {
                Target = () => _eventPill,
                Title = "Городские события",
                Text = "Событие круга — цветная метка вверху, подробности — при наведении и в «Меню». В начале круга может открыться событие (примерно в половине кругов). Час пик — дороги этого круга станут платными вдвое. Ремонт — пара участков закрыта. Субсидия — мосты дешевле. Новый район — на окраине появляются места и контракты."
            },
            new TutorialStep
            {
                Target = () => _playersTab,
                Title = "Персонажи",
                Text = "У каждой компании свой персонаж со способностью — она видна в панели «Игроки» (кнопка внизу по центру). Везунчик выбирает из двух карт удачи, Прораб раз за партию строит бесплатно, Сборщик пошлин берёт за проезд больше. Играйте от своей способности."
            },
            new TutorialStep
            {
                Target = () => _menuButton,
                Title = LocalizationService.Get("game.ui.026"),
                Text = "Цели — в «Меню» справа вверху. Три общие цели открыты для всех. Награду получает тот, кто выполнит цель первым, — следите за прогрессом соперников."
            },
            new TutorialStep
            {
                Target = () => _actionsTab,
                Title = LocalizationService.Get("game.ui.017"),
                Text = "Кнопка «Удача» — в панели «Действия». Когда берёте карты, одну из двух можно заменить картой удачи — не больше одной за ход. Бафы: грант, поставка ресурсов, премия за контракты. Дебафы: штраф, кража карты, отзыв невыполненного контракта. Риск, а не бесплатный бонус."
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
                Ui.Border(frame, 3f, Theme.Gold);
                layer.Add(frame);
            }

            var card = Ui.Card(22f);
            card.pickingMode = PickingMode.Position;
            card.style.position = Position.Absolute;
            card.style.width = 560f;
            card.style.backgroundColor = Theme.Panel;
            Ui.Border(card, 2f, Theme.Gold);
            var mapBounds = _map.worldBound;
            var mapTopLeft = _overlay.WorldToLocal(mapBounds.position);
            card.style.left = mapTopLeft.x + mapBounds.width / 2f - 280f;
            // Когда рассказываем про карту, карточку ставим вниз, иначе — в середину карты.
            card.style.top = mapTopLeft.y + (step.Target() == _map ? mapBounds.height - 250f : mapBounds.height / 2f - 110f);

            card.Add(Ui.Text(EnglishUi ? $"Tutorial · {index + 1} of {steps.Length}" : $"Обучение · {index + 1} из {steps.Length}", 12f, Theme.TextDim, true));
            var title = Ui.Text(EnglishUi ? EnglishFirstGameTutorialTitles[index] : step.Title, 24f, Theme.Gold, true);
            title.style.marginTop = 4f;
            card.Add(title);
            var text = Ui.Text(EnglishUi ? EnglishFirstGameTutorialTexts[index] : step.Text, 15f);
            text.style.marginTop = 8f;
            text.style.marginBottom = 16f;
            card.Add(text);

            var buttons = Ui.Row();
            bool last = index == steps.Length - 1;
            var next = Ui.Button(last ? LocalizationService.Get("game.ui.160") : LocalizationService.Get("game.ui.161"), () => ShowTutorialStep(index + 1), ButtonKind.Primary, 15f);
            next.style.marginRight = 10f;
            buttons.Add(next);
            if (!last) buttons.Add(Ui.Button(LocalizationService.Get("game.ui.162"), FinishTutorial, ButtonKind.Ghost, 14f));
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
            bool shown = _game.Config.EventsEnabled && playing;
            Ui.Show(_eventCard, shown);
            Ui.Show(_eventPill, shown);
            if (cityEvent == null)
            {
                // Спокойный круг: событие не выпало.
                var calm = new Color(0.55f, 0.60f, 0.70f);
                _eventChip.style.backgroundColor = calm;
                _eventTitle.text = LocalizationService.Get("game.ui.163");
                _eventTitle.style.color = calm;
                _eventText.text = LocalizationService.Get("game.ui.164");
                Ui.Border(_eventCard, 2f, new Color(calm.r, calm.g, calm.b, 0.35f));
                SetEventPill(calm, LocalizationService.Get("game.ui.163"));
                return;
            }
            var color = EventColor(cityEvent.Kind);
            _eventChip.style.backgroundColor = color;
            _eventTitle.text = CityEventTitle(cityEvent.Kind);
            _eventTitle.style.color = color;
            _eventText.text = CityEventDescription(cityEvent);
            Ui.Border(_eventCard, 2f, new Color(color.r, color.g, color.b, 0.6f));
            SetEventPill(color, CityEventTitle(cityEvent.Kind));
        }

        void SetEventPill(Color color, string title)
        {
            _eventPillDot.style.backgroundColor = color;
            _eventPillText.text = title;
            _eventPillText.style.color = color;
            Ui.Border(_eventPill, 1f, new Color(color.r, color.g, color.b, 0.55f));
        }

        /// <summary>Карточка события в панели мягко окрашивается цветом события и за пару секунд возвращается к обычной.</summary>
        void GlowEventCard(Color color)
        {
            int version = ++_eventGlowVersion;
            var start = new Color(color.r, color.g, color.b, 1f);
            _app.Tweener.Add(2.0f, k =>
            {
                if (version != _eventGlowVersion) return;
                float s = GameFxTuning.EventGlowStrength(k, Ui.ReducedMotion);
                var glow = Color.Lerp(Theme.PanelLight, Color.Lerp(Theme.PanelLight, start, 0.28f), s);
                _eventCard.style.backgroundColor = glow;
                _eventPill.style.backgroundColor = Color.Lerp(Theme.PanelLight, Color.Lerp(Theme.PanelLight, start, 0.45f), s);
            }, () =>
            {
                if (version != _eventGlowVersion) return;
                _eventCard.style.backgroundColor = Theme.PanelLight;
                _eventPill.style.backgroundColor = Theme.PanelLight;
            }, 0f, false);
        }

        int _eventGlowVersion;

        /// <summary>Подписи вкладок, активная вкладка, точки «есть новое» на журнале и меню.</summary>
        void RefreshTabs()
        {
            var viewer = _game.Players[Viewer];
            bool choosing = TryGetContractChooser(out _);
            _contractsTab.text = choosing ? (EnglishUi ? "Contracts •" : "Контракты •") : NeedsHandoff ? LocalizationService.Get("game.ui.018") : (EnglishUi ? $"Contracts · {viewer.Contracts.Count}" : $"Контракты · {viewer.Contracts.Count}");
            _logTab.text = _logTotal > _logSeenTotal && _sheet != SheetKind.Log ? (EnglishUi ? "Journal •" : "Журнал •") : LocalizationService.Get("game.ui.021");
            StyleTab(_actionsTab, _sheet == SheetKind.Actions);
            StyleTab(_contractsTab, _sheet == SheetKind.Contracts);
            StyleTab(_logTab, _sheet == SheetKind.Log);
            StyleTab(_playersTab, _playersOpen);
            // Красная точка на «Меню» — кто-то выполнил цель мэрии, а вы ещё не смотрели.
            int taken = _game.ActiveGoals.Count(g => _game.GoalOwner(g.Id).HasValue);
            Ui.Show(_menuDot, taken > _seenGoalOwners && !_drawerOpen);
        }

        static void StyleTab(UnityEngine.UIElements.Button tab, bool active)
        {
            tab.style.color = active ? Theme.Text : Theme.TextDim;
            tab.style.borderBottomColor = active ? Theme.Accent : new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>Куда летят карты и монетки игрока: строка в панели «Игроки», если она открыта, иначе его портрет внизу.</summary>
        VisualElement PlayerAnchor(int index)
        {
            if (_playersOpen && index >= 0 && index < _playerRows.Length) return _playerRows[index].Root;
            return index >= 0 && index < _playerChips.Length ? _playerChips[index].Root : _playersTab;
        }

        /// <summary>Откуда летит открытая карта: из ряда, если панель действий открыта, иначе от вкладки «Действия».</summary>
        VisualElement FaceUpAnchor(int slot) =>
            _sheet == SheetKind.Actions && slot >= 0 && slot < _faceUp.Length ? (VisualElement)_faceUp[slot] : _actionsTab;

        VisualElement DeckAnchor => _sheet == SheetKind.Actions ? (VisualElement)_deckButton : _actionsTab;

        /// <summary>Подсказка при наведении на элемент интерфейса (у UI Toolkit в игре своих подсказок нет).</summary>
        void AttachTooltip(VisualElement element, Func<string> text)
        {
            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_game == null || _overlayKind != OverlayKind.None) return;
                var value = text();
                if (string.IsNullOrEmpty(value)) return;
                _tooltipText.text = value;
                var bounds = element.worldBound;
                var below = _fx.WorldToLocal(new Vector2(bounds.xMin, bounds.yMax + 8f));
                var above = _fx.WorldToLocal(new Vector2(bounds.xMin, bounds.yMin - 130f));
                float availableWidth = _fx.layout.width;
                float availableHeight = _fx.layout.height;
                float tooltipWidth = GameFxTuning.TooltipWidth(availableWidth);
                _tooltip.style.width = tooltipWidth;
                bool fromBottom = bounds.center.y > _fx.worldBound.center.y;
                var ideal = fromBottom ? above : below;
                var position = GameFxTuning.TooltipPosition(
                    ideal - new Vector2(18f, 18f),
                    new Vector2(availableWidth, availableHeight), tooltipWidth, 130f);
                _tooltip.style.left = position.x;
                _tooltip.style.top = position.y;
                Ui.Show(_tooltip, true);
            });
            element.RegisterCallback<PointerLeaveEvent>(_ => Ui.Show(_tooltip, false));
        }

        string ResourceTooltip(Resource resource)
        {
            if (NeedsHandoff) return LocalizationService.Get("game.tooltip.hand_hidden");
            int count = _game.Players[Viewer].CardCount(resource);
            string name = EnglishUi ? Palette.CardTitle(resource) : ResourceInfo.RussianName(resource);
            string use = resource == Resource.Machinery ? LocalizationService.Get("game.ui.165") : LocalizationService.Get("game.ui.166") + RoadKindLabel(resource);
            return EnglishUi ? $"{name}: {count} cards\n{use}. Each segment represents one card." : $"{char.ToUpper(name[0]) + name.Substring(1)}: {count} шт.\n{use}. Одно деление — одна карта.";
        }

        string PiecesTooltip()
        {
            var viewer = _game.Players[Viewer];
            int start = _game.Config.RoadPiecesFor(_game.Players.Count);
            return string.Format(LocalizationService.Get("game.tooltip.remaining_roads"), viewer.RoadPieces, start, _game.Config.FinalRoundPieces);
        }

        /// <summary>Адаптивность: на узком окне прячем второстепенное, панели не шире окна.</summary>
        void AdaptLayout()
        {
            float width = Root.layout.width;
            if (float.IsNaN(width) || width < 100f) return;
            Ui.Show(_logo, width >= 1280f);
            Ui.Show(_resourcesCaption, width >= 1500f);
            Ui.Show(_handTitle, width >= 1700f);
            Ui.Show(_viewHint, width >= 1800f);
            void Fit(VisualElement sheet, float preferred)
            {
                float w = GameFxTuning.SheetWidth(width, preferred);
                sheet.style.width = w;
                sheet.style.marginLeft = -w / 2f;
            }
            Fit(_actionsSheet, 860f);
            Fit(_contractsSheet, ContractsSheetWidth);
            _contractsSheet.style.maxHeight = Length.Percent(100f);
            bool compact = GameFxTuning.CompactActions(width);
            _actionsLayoutRow.style.flexDirection = compact ? FlexDirection.Column : FlexDirection.Row;
            if (compact)
            {
                _actionsLayoutRow.style.width = Length.Percent(100f);
                _faceUpRow.style.width = Length.Percent(100f);
            }
            else
            {
                _actionsLayoutRow.style.width = StyleKeyword.Auto;
                _faceUpRow.style.width = StyleKeyword.Auto;
            }
            _faceUpRow.style.flexWrap = compact ? Wrap.Wrap : Wrap.NoWrap;
            _faceUpRow.style.marginRight = compact ? 0f : 18f;
            _faceUpRow.style.marginBottom = compact ? 10f : 0f;
            if (compact) _actionButtons.style.width = Length.Percent(100f);
            else _actionButtons.style.width = StyleKeyword.Auto;
            _logSheet.style.width = GameFxTuning.SheetWidth(width, 640f);
            _logSheet.style.maxHeight = Mathf.Max(120f, Root.layout.height - TopBarHeight - BottomBarHeight - 12f);
            _statusBanner.style.maxWidth = GameFxTuning.SheetWidth(width, 760f);
            _statusBanner.style.top = width < 900f ? 58f : 12f;
            _toast.style.maxWidth = GameFxTuning.ToastMaxWidth(width);
            _drawer.style.width = Mathf.Min(DrawerWidth, width * 0.9f);
        }

        /// <summary>Подсказка над местом карты: район, дороги, ваши контракты с этим районом.</summary>
        void OnLocationHover(Location location, Vector2 position)
        {
            if (location == null || _overlayKind != OverlayKind.None || _game == null) return;
            ShowMapTooltip(LocationTooltip(location), position);
        }

        string LocationTooltip(Location location)
        {
            var lines = new List<string> { $"{DisplayPlace(location.Name)} · {DisplayPlace(DistrictName(location.DistrictId))}" };
            var roads = _game.Map.Roads.Where(r => (r.A == location.Id || r.B == location.Id) && _game.IsRoadOpen(r.Id)).ToList();
            int free = roads.Count(r => !_game.GetRoadOwner(r.Id).HasValue);
            lines.Add(string.Format(LocalizationService.Get("game.tooltip.location_roads"), roads.Count, free));
            var owners = roads.Select(r => _game.GetRoadOwner(r.Id)).Where(o => o.HasValue).GroupBy(o => o.Value)
                .Select(g => $"{_game.Players[g.Key].Name} — {g.Count()}").ToList();
            if (owners.Count > 0) lines.Add(LocalizationService.Get("game.tooltip.built_by") + string.Join(", ", owners));
            if (!NeedsHandoff)
            {
                var viewer = _game.Players[Viewer];
                foreach (var contract in viewer.Contracts.Where(c => c.Districts.Contains(location.DistrictId)))
                {
                    bool done = _game.CheckContract(viewer.Index, contract).Completed;
                    lines.Add(string.Format(LocalizationService.Get("game.tooltip.your_contract"), ContractTitle(contract), contract.Points, done ? LocalizationService.Get("game.tooltip.completed") : ""));
                }
            }
            return string.Join("\n", lines);
        }

        static string MayorGoalName(MayorGoal goal)
        {
            if (!EnglishUi) return goal.Name;
            switch (goal.Kind)
            {
                case GoalKind.Locations: return "District Connections";
                case GoalKind.Bridges: return "Bridge Builder";
                case GoalKind.Network: return "Connected Network";
                case GoalKind.ResourcePieces: return "Material Roads";
                case GoalKind.LongRoads: return "Long Roads";
                default: return goal.Name;
            }
        }

        static string MayorGoalDescription(MayorGoal goal, CityMap map)
        {
            if (!EnglishUi) return goal.Describe(map);
            switch (goal.Kind)
            {
                case GoalKind.Locations:
                    return $"Build your roads at {goal.Count} of {map.LocationsIn(goal.District).Count} locations in {DisplayPlace(map.DistrictName(goal.District))}";
                case GoalKind.Bridges:
                    return $"Build {goal.Count} bridges across the river";
                case GoalKind.Network:
                    return $"Connect {goal.Count} districts in one network";
                case GoalKind.ResourcePieces:
                    return $"Build {goal.Count} road segments using {Palette.CardTitle(goal.Resource ?? Resource.Asphalt)}";
                case GoalKind.LongRoads:
                    return $"Build {goal.Count} roads of length {goal.Length} or more";
                default: return goal.Describe(map);
            }
        }

        /// <summary>Цели мэрии: название, награда, условие и статус — свободна (с прогрессом зрителя) или чья.</summary>
        void RefreshGoals()
        {
            _goalsBox.Clear();
            var goals = _game.ActiveGoals;
            Ui.Show(_goalsBox, goals.Count > 0);
            Ui.Show(_goalsTitle, goals.Count > 0);
            if (goals.Count == 0) return;
            var goalsNote = Ui.Text(LocalizationService.Get("game.tooltip.goal_reward"), 11f, Theme.TextDim);
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
                texts.Add(Ui.Text(MayorGoalName(goal), 13f, owner.HasValue ? Theme.TextDim : Theme.Text, true));
                string status = owner.HasValue
                    ? LocalizationService.Get("game.ui.167") + _game.Players[owner.Value].Name
                    : (EnglishUi ? $"{MayorGoalDescription(goal, _game.Map)} · yours: {Math.Min(goal.Count, _game.GoalProgress(viewer.Index, goal))}/{goal.Count}" : $"{goal.Describe(_game.Map)} · у вас {Math.Min(goal.Count, _game.GoalProgress(viewer.Index, goal))}/{goal.Count}");
                var statusLabel = Ui.Text(status, 11f, owner.HasValue ? Palette.PlayerColor(owner.Value) : Theme.TextDim);
                statusLabel.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(statusLabel);
                row.Add(texts);

                row.Add(Ui.Text("+" + goal.Points, 14f, owner.HasValue ? Theme.TextDim : Theme.Gold, true));
                _goalsBox.Add(row);
            }
        }

        static string CityEventTitle(CityEventKind kind)
        {
            if (!EnglishUi)
            {
                switch (kind)
                {
                    case CityEventKind.RushHour: return "Час пик";
                    case CityEventKind.Repair: return "Ремонт";
                    case CityEventKind.Subsidy: return "Субсидия мэрии";
                    case CityEventKind.Parade: return "Парад";
                    case CityEventKind.Snow: return "Снегопад";
                    case CityEventKind.Investor: return "Инвестор";
                    default: return "Новый район";
                }
            }
            switch (kind)
            {
                case CityEventKind.RushHour: return "Rush Hour";
                case CityEventKind.Repair: return "Roadworks";
                case CityEventKind.Subsidy: return "City Hall Subsidy";
                case CityEventKind.Parade: return "Parade";
                case CityEventKind.Snow: return "Snowfall";
                case CityEventKind.Investor: return "Investor";
                default: return "New District";
            }
        }

        string CityEventDescription(CityEvent cityEvent)
        {
            if (!EnglishUi) return cityEvent.Describe(_game);
            var config = _game.Config;
            switch (cityEvent.Kind)
            {
                case CityEventKind.RushHour:
                    return $"Roads built this round become busy highways: tolls cost {config.TollPerRoad * config.RushHourTollMultiplier} pts instead of {config.TollPerRoad}.";
                case CityEventKind.Repair:
                {
                    var roads = new List<string>();
                    foreach (var id in cityEvent.ClosedRoads)
                    {
                        var road = _game.Map.GetRoad(id);
                        if (road != null)
                            roads.Add(DisplayPlace(_game.Map.LocationName(road.A)) + " — " + DisplayPlace(_game.Map.LocationName(road.B)));
                    }
                    return roads.Count == 0 ? "No available roads were found for repairs."
                        : "Construction blocked for this round: " + string.Join("; ", roads) + ".";
                }
                case CityEventKind.Subsidy:
                    return $"Bridges cost {config.BridgeSubsidy} fewer steel cards this round (minimum one).";
                case CityEventKind.Parade:
                    return $"District {DisplayPlace(_game.Map.DistrictName(config.ParadeDistrict))} is closed: roads touching it cannot be built this round.";
                case CityEventKind.Snow:
                    return $"Gravel roads cost {config.SnowExtraCost} extra cards this round.";
                case CityEventKind.Investor:
                    return $"Every road built this round earns an extra +{config.InvestorBonus} pts.";
                default:
                    return $"New district {DisplayPlace(_game.Map.DistrictName(cityEvent.DistrictId))} opened on the outskirts, bringing locations, roads and contracts.";
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

        string DistrictName(string districtId) => DisplayPlace(_game.Map.DistrictName(districtId));

        /// <summary>Подпись способа оплаты; cost — сколько карт стоит действие (с учётом скидок и событий).</summary>
        static string PaymentLabel(int cost, PaymentOption option)
        {
            if (option.Resource == Resource.Machinery) return EnglishUi ? $"Machinery only × {option.Machinery}" : $"Только техника × {option.Machinery}";
            string main = $"{Palette.CardTitle(option.Resource)} × {cost - option.Machinery}";
            return option.Machinery == 0 ? main : EnglishUi ? $"{main} + machinery × {option.Machinery}" : $"{main} + техника × {option.Machinery}";
        }
    }
}
