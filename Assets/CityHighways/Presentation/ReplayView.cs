using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Просмотр повтора партии (своей, друга или клубного товарища). Партия заново проигрывается по записанным
    /// действиям на том же поле, что и в игре:
    /// - «▶ / ⏸», шаг вперёд и назад, в начало и в конец, скорость ×1 / ×2 / ×4, ползунок по всей партии;
    /// - справа — счёт по ходу партии, комментарий к каждому действию («Аня строит Арбат — Кремль за бетон»);
    /// - «Показать руки» открывает карты всех игроков — видно, ради чего копились ресурсы (обучающий режим).
    /// Шаг назад пересобирает партию с начала до нужного действия: это быстро, партия детерминирована.
    /// </summary>
    public sealed class ReplayView
    {
        const float BaseInterval = 0.7f;

        readonly CityHighwaysApp _app;
        readonly ReplayFile _file;
        readonly Action _back;
        readonly CityMap _cityMap;
        Game _game;
        int _position;
        bool _playing;
        float _speed = 1f;
        float _timer;
        bool _showHands;
        string _error;

        readonly MapView _map;
        readonly VisualElement _scoreList, _commentList;
        readonly Label _turnLabel, _progressLabel, _errorLabel, _titleLabel, _factsLabel, _commentTitle;
        readonly VisualElement _side, _bottom, _transportRow, _timelineRow;
        string _errorKey, _errorDetail;
        int _errorAction;
        readonly Button _playButton, _speedButton, _handsButton;
        readonly Button _firstButton, _previousButton, _nextButton, _lastButton, _backButton;
        readonly Label _controllerGuide;
        bool _controllerActive;
        string _renderedLanguage;
        readonly Slider _slider;
        bool _sliderSync;
        readonly List<(Func<string> render, bool important)> _comments = new List<(Func<string>, bool)>();

        public VisualElement Root { get; }

        public ReplayView(CityHighwaysApp app, ReplayFile file, Action back)
        {
            _app = app;
            _file = file;
            _back = back;
            Root = Ui.Column();
            Ui.Fill(Root);
            Root.style.backgroundColor = Theme.Background;

            try
            {
                _cityMap = CityHighwaysApp.LoadMap(file.mapPath);
            }
            catch (Exception e)
            {
                SetError("map", e.Message);
            }

            // Верхняя строка: назад, заголовок, факты.
            var top = Ui.Row();
            top.style.alignItems = Align.Center;
            top.style.height = 60f;
            Ui.Padding(top, 8f, 18f);
            top.style.backgroundColor = Theme.PanelGlass;
            top.style.borderBottomWidth = 1f;
            top.style.borderBottomColor = Theme.Line;
            _backButton = Ui.Button("← Назад", () => _back?.Invoke(), ButtonKind.Ghost, 15f);
            _backButton.name = "gamepad-replay-back";
            _backButton.style.marginRight = 14f;
            top.Add(_backButton);
            var icon = new MenuIcon(MenuIcon.Glyph.Play, 22f, Theme.Gold);
            icon.style.marginRight = 10f;
            top.Add(icon);
            var titles = Ui.Column();
            titles.style.flexGrow = 1f;
            titles.style.minWidth = 0f;
            _titleLabel = Ui.Text("", 20f, Theme.Gold, true);
            _titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _titleLabel.style.overflow = Overflow.Hidden;
            _titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            _factsLabel = Ui.Text("", 13f, Theme.TextDim);
            _factsLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _factsLabel.style.overflow = Overflow.Hidden;
            _factsLabel.style.textOverflow = TextOverflow.Ellipsis;
            titles.Add(_titleLabel);
            titles.Add(_factsLabel);
            top.Add(titles);
            _handsButton = Ui.Button("Показать руки", ToggleHands, ButtonKind.Secondary, 13f);
            _handsButton.name = "gamepad-replay-hands";
            _handsButton.style.width = 150f;
            top.Add(_handsButton);
            Root.Add(top);

            // Середина: поле и панель справа.
            var middle = Ui.Row();
            middle.style.flexGrow = 1f;
            middle.style.minHeight = 0f;
            _map = new MapView { Interactive = false };
            _map.TiltEnabled = app.Display != null && app.Display.tiltedBoard;
            _map.style.flexGrow = 1f;
            middle.Add(_map);

            _side = Ui.Column();
            var side = _side;
            side.style.width = 360f;
            side.style.flexShrink = 0f;
            side.style.backgroundColor = Theme.Panel;
            side.style.borderLeftWidth = 1f;
            side.style.borderLeftColor = Theme.Line;
            Ui.Padding(side, 14f, 14f);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            Ui.StyleScroller(scroll);
            _turnLabel = Ui.Text("", 16f, Theme.Text, true);
            _turnLabel.style.whiteSpace = WhiteSpace.Normal;
            _turnLabel.style.marginBottom = 8f;
            scroll.Add(_turnLabel);
            _scoreList = Ui.Column();
            scroll.Add(_scoreList);
            _commentTitle = Ui.SectionTitle("Что происходит");
            _commentTitle.style.marginTop = 12f;
            scroll.Add(_commentTitle);
            _commentList = Ui.Column();
            scroll.Add(_commentList);
            side.Add(scroll);
            middle.Add(side);
            Root.Add(middle);

            // Низ: управление.
            _bottom = Ui.Row();
            var bottom = _bottom;
            bottom.style.alignItems = Align.Center;
            bottom.style.height = 68f;
            Ui.Padding(bottom, 10f, 18f);
            bottom.style.backgroundColor = Theme.PanelGlass;
            bottom.style.borderTopWidth = 1f;
            bottom.style.borderTopColor = Theme.Line;
            _transportRow = Ui.Row();
            _transportRow.style.alignItems = Align.Center;
            _transportRow.style.flexShrink = 0f;
            _timelineRow = Ui.Row();
            _timelineRow.style.alignItems = Align.Center;
            _timelineRow.style.flexGrow = 1f;
            _timelineRow.style.minWidth = 0f;
            bottom.Add(_transportRow);
            bottom.Add(_timelineRow);
            Button Control(string text, Action action, float width)
            {
                var button = Ui.Button(text, action, ButtonKind.Secondary, 15f);
                button.style.width = width;
                button.style.marginRight = 6f;
                button.style.flexShrink = 0f;
                _transportRow.Add(button);
                return button;
            }
            _firstButton = Control("⏮", () => SeekTo(0), 48f);
            _previousButton = Control("◀", StepBack, 48f);
            _playButton = Control("▶ Смотреть", TogglePlay, 140f);
            _nextButton = Control("▶|", () => { _playing = false; _timer = 0f; StepForward(true); }, 48f);
            _lastButton = Control("⏭", () => SeekTo(_file.Actions), 48f);
            _speedButton = Control("×1", CycleSpeed, 56f);
            _firstButton.name = "gamepad-replay-first";
            _previousButton.name = "gamepad-replay-previous";
            _playButton.name = "gamepad-replay-play";
            _nextButton.name = "gamepad-replay-next";
            _lastButton.name = "gamepad-replay-last";
            _speedButton.name = "gamepad-replay-speed";
            _slider = new Slider(0f, Mathf.Max(1, _file.Actions));
            _slider.name = "gamepad-replay-seek";
            _slider.style.flexGrow = 1f;
            _slider.style.marginLeft = 12f;
            _slider.style.marginRight = 12f;
            _slider.RegisterValueChangedCallback(e =>
            {
                if (_sliderSync) return;
                _playing = false;
                SeekTo(Mathf.RoundToInt(e.newValue));
            });
            _slider.style.minWidth = 0f;
            _timelineRow.Add(_slider);
            _progressLabel = Ui.Number("", 13f, Theme.TextDim);
            _progressLabel.style.width = 150f;
            _progressLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _timelineRow.Add(_progressLabel);
            Root.Add(bottom);

            // A dedicated layout row cannot cover playback buttons or intercept mouse input.
            _controllerGuide = GamepadControlGuide.Create();
            _controllerGuide.style.position = Position.Relative;
            _controllerGuide.style.right = StyleKeyword.Auto;
            _controllerGuide.style.bottom = StyleKeyword.Auto;
            _controllerGuide.style.alignSelf = Align.FlexEnd;
            _controllerGuide.style.marginRight = 18f;
            _controllerGuide.style.marginBottom = 4f;
            Root.Add(_controllerGuide);

            _errorLabel = Ui.Text("", 15f, Theme.Danger, true);
            _errorLabel.style.position = Position.Absolute;
            _errorLabel.style.left = 24f;
            _errorLabel.style.top = 76f;
            _errorLabel.style.whiteSpace = WhiteSpace.Normal;
            _errorLabel.style.maxWidth = 700f;
            Root.Add(_errorLabel);
            Root.RegisterCallback<GeometryChangedEvent>(e => AdaptLayout(e.newRect.width));
            AdaptLayout(1280f);

            if (_error == null) Rebuild(0);
            if (_game != null) _map.SetGame(_game);
            RefreshPanel();
        }

        // ================================================================== Воспроизведение

        Game NewGame()
        {
            var start = _file.start;
            var characters = (start.characters ?? new int[start.players.Length]).Select(c => (CharacterKind)c).ToList();
            var game = new Game(_cityMap, start.players, start.config, start.seed, characters);
            game.Log += line => _comments.Add((() => GameView.FormatJournalMessage(game, line), true));
            return game;
        }

        /// <summary>Партия с начала до действия target (комментарии — только для последних шагов).</summary>
        void Rebuild(int target)
        {
            if (_cityMap == null || _file.start == null) return;
            try
            {
                _comments.Clear();
                _game = NewGame();
                _position = 0;
                target = Mathf.Clamp(target, 0, _file.Actions);
                while (_position < target)
                {
                    if (!Apply(_position >= target - 12)) break;
                }
                if (_comments.Count > 60) _comments.RemoveRange(0, _comments.Count - 60);
            }
            catch (Exception e)
            {
                SetError("open", e.Message);
                _game = null;
            }
        }

        /// <summary>Выполнить следующее действие; false — повтор разошёлся с правилами этой версии.</summary>
        bool Apply(bool comment)
        {
            var command = _file.history[_position];
            Func<string> text = comment ? ReplayText.Describe(_game, command) : null;
            int before = _comments.Count;
            var result = _game.Execute(command);
            if (!result.Success)
            {
                SetError("command", result.Error, _position + 1);
                _playing = false;
                return false;
            }
            if (!comment) _comments.RemoveRange(before, _comments.Count - before);
            else if (text != null) _comments.Insert(before, (text, false));
            _position++;
            if (command.kind == GameCommand.Build || command.kind == GameCommand.BuildFree || command.kind == GameCommand.Upgrade)
                _map.SelectedRoadId = command.road;
            return true;
        }

        void StepForward(bool userStep)
        {
            if (_game == null || _error != null || _position >= _file.Actions) return;
            if (Apply(true))
            {
                if (_comments.Count > 80) _comments.RemoveRange(0, _comments.Count - 80);
                if (userStep) _app.Sound.Play(Sfx.Tick, 0.4f);
            }
            _map.RefreshVisibility();
            _map.Repaint();
            RefreshPanel();
        }

        void StepBack()
        {
            _playing = false;
            SeekTo(_position - 1);
        }

        void SeekTo(int target)
        {
            if (_cityMap == null) return;
            _playing = false;
            _timer = 0f;
            target = Mathf.Clamp(target, 0, _file.Actions);
            if (target == _position && _game != null)
            {
                RefreshPanel();
                return;
            }
            if (_error != null && target > _position)
            {
                RefreshPanel();
                return;
            }
            if (target > _position && _game != null)
            {
                // Вперёд — просто доигрываем, без пересборки.
                while (_position < target)
                    if (!Apply(_position >= target - 12)) break;
                if (_comments.Count > 80) _comments.RemoveRange(0, _comments.Count - 80);
            }
            else
            {
                _error = _errorKey = _errorDetail = null;
                Rebuild(target);
                if (_game != null) _map.SwapGame(_game);
            }
            _map.RefreshVisibility();
            _map.Repaint();
            RefreshPanel();
        }

        void TogglePlay()
        {
            if (_game == null || _error != null || _file.Actions <= 0) return;
            if (_position >= _file.Actions) SeekTo(0);
            if (_game == null || _error != null) return;
            _playing = !_playing;
            _timer = 0f;
            RefreshPanel();
        }

        void CycleSpeed()
        {
            _speed = _speed >= 4f ? 1f : _speed * 2f;
            _speedButton.text = $"×{_speed:0}";
        }

        void ToggleHands()
        {
            _showHands = !_showHands;
            RefreshPanel();
        }

        public void Tick(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Ui.GoBack(Root);
                return;
            }
            UiGamepadFocus.MaintainGameplayFocus(Root, ref _controllerActive);
            // Back may synchronously remove this view from the panel.
            if (Root.panel == null) return;
            GamepadControlGuide.Update(_controllerGuide, GamepadControlGuide.Context.Replay,
                Gamepad.current != null, _controllerActive, false, LocalizationService.Language == "en", false);
            if (_renderedLanguage != LocalizationService.Language) RefreshPanel();

            _map.Tick(deltaTime);
            if (!_playing || _game == null || _error != null) return;
            _timer += deltaTime * _speed;
            // Набор карт — быстро, стройка и события — с паузой, чтобы успеть рассмотреть.
            float interval = BaseInterval;
            if (_position < _file.Actions)
            {
                string kind = _file.history[_position].kind;
                if (kind == GameCommand.DrawDeck || kind == GameCommand.DrawFaceUp) interval *= 0.55f;
                if (kind == GameCommand.Build || kind == GameCommand.Upgrade) interval *= 1.5f;
            }
            if (_timer < interval) return;
            _timer = 0f;
            StepForward(false);
            if (_position >= _file.Actions)
            {
                _playing = false;
                RefreshPanel();
            }
        }

        // ================================================================== Панель

        void RefreshPanel()
        {
            _errorLabel.text = _error == null ? "" : ReplayText.Error(_errorKey, _errorDetail, _errorAction);
            Ui.Show(_errorLabel, !string.IsNullOrEmpty(_error));
            _renderedLanguage = LocalizationService.Language;
            bool english = _renderedLanguage == "en";
            _titleLabel.text = ReplayText.Title(_file);
            _titleLabel.tooltip = _titleLabel.text;
            _factsLabel.text = ReplayText.Facts(_file, ShortDate(_file.date));
            _factsLabel.tooltip = _factsLabel.text;
            _commentTitle.text = english ? "What is happening" : "Что происходит";
            _backButton.text = english ? "← Back" : "← Назад";
            _handsButton.text = ReplayTransportState.HandsLabel(_showHands, english);
            _playButton.text = ReplayTransportState.PlayLabel(_playing, _position, _file.Actions, english);
            var transport = ReplayTransportState.Read(_game != null, _error != null, _position, _file.Actions);
            _firstButton.SetEnabled(transport.CanGoBack);
            _previousButton.SetEnabled(transport.CanGoBack);
            _nextButton.SetEnabled(transport.CanGoForward);
            _lastButton.SetEnabled(transport.CanGoForward);
            _playButton.SetEnabled(transport.CanPlay);
            _speedButton.SetEnabled(transport.CanPlay);
            _slider.SetEnabled(transport.CanSeek);
            _handsButton.SetEnabled(_game != null);
            _progressLabel.text = $"{_position} / {_file.Actions}";
            _sliderSync = true;
            _slider.SetValueWithoutNotify(_position);
            _sliderSync = false;

            _scoreList.Clear();
            _commentList.Clear();
            if (_game == null) return;

            _turnLabel.text = ReplayText.Turn(_game);

            foreach (var player in _game.Players)
            {
                var row = Ui.Column();
                row.style.marginBottom = 6f;
                row.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(row, 8f);
                Ui.Padding(row, 6f, 10f);
                row.style.borderLeftWidth = 5f;
                row.style.borderLeftColor = Palette.PlayerColor(player.Index);
                bool acting = _game.Phase != GamePhase.GameOver && _game.CurrentPlayerIndex == player.Index;
                if (acting)
                {
                    row.style.borderRightWidth = 2f;
                    row.style.borderRightColor = Theme.Gold;
                }
                var head = Ui.Row();
                head.style.alignItems = Align.Center;
                var name = Ui.Text(player.Name + (player.Index == _file.authorSeat ? " ★" : ""), 14f, Theme.Text, true);
                name.style.flexGrow = 1f;
                head.Add(name);
                int score = _game.Result != null
                    ? _game.Result.Ranking.FirstOrDefault(s => s.PlayerIndex == player.Index)?.Total ?? 0
                    : player.RoadPoints + player.GoalPoints + player.LuckPoints;
                head.Add(Ui.Number(ReplayText.Points(score), 14f, Theme.Gold, true));
                row.Add(head);
                string details = ReplayText.PlayerDetails(player.HandSize, player.RoadPieces, player.Roads.Count);
                if (player.Character != CharacterKind.None) details = Characters.Title(player.Character) + " · " + details;
                var info = Ui.Text(details, 11f, Theme.TextDim);
                info.style.whiteSpace = WhiteSpace.Normal;
                row.Add(info);
                if (_showHands && player.HandSize > 0)
                {
                    var hand = new List<string>();
                    for (int r = 0; r < ResourceInfo.Count; r++)
                    {
                        int count = player.CardCount((Resource)r);
                        if (count > 0) hand.Add($"{Palette.CardTitle((Resource)r)} ×{count}");
                    }
                    var handLabel = Ui.Text((english ? "Hand: " : "Рука: ") + string.Join(", ", hand), 11f, new Color(0.62f, 0.85f, 1f));
                    handLabel.style.whiteSpace = WhiteSpace.Normal;
                    row.Add(handLabel);
                    if (player.Contracts.Count > 0)
                    {
                        var contracts = Ui.Text((english ? "Contracts: " : "Контракты: ") + string.Join(", ", player.Contracts.Select(c =>
                            $"{MagnateLocationText.Name(_game.Map.DistrictName(c.DistrictA))} — {MagnateLocationText.Name(_game.Map.DistrictName(c.DistrictB))} ({c.Points})")), 11f, Theme.TextDim);
                        contracts.style.whiteSpace = WhiteSpace.Normal;
                        row.Add(contracts);
                    }
                }
                _scoreList.Add(row);
            }

            if (_game.IsCooperative)
            {
                var coop = Ui.Text(ReplayText.Coop(_game.OrdersDone, _game.OrdersTarget, _game.Crisis, _game.CrisisMax), 13f,
                    _game.Crisis >= _game.CrisisMax - 2 ? Theme.Danger : Theme.Text, true);
                coop.style.marginTop = 2f;
                _scoreList.Add(coop);
            }

            // Свежие комментарии — сверху.
            for (int i = _comments.Count - 1; i >= 0 && i >= _comments.Count - 40; i--)
            {
                var (render, important) = _comments[i];
                var line = Ui.Text(render(), 12f, important ? Theme.Text : Theme.TextDim);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.marginBottom = 4f;
                if (important)
                {
                    line.style.borderLeftWidth = 2f;
                    line.style.borderLeftColor = Theme.Gold;
                    line.style.paddingLeft = 6f;
                }
                _commentList.Add(line);
            }
        }

        /// <summary>Комментарий к действию по состоянию до него.</summary>
        void SetError(string key, string detail, int action = 0)
        {
            _error = _errorKey = key;
            _errorDetail = detail;
            _errorAction = action;
        }

        void AdaptLayout(float width)
        {
            if (float.IsNaN(width) || width <= 0f) return;
            bool compact = ReplayLayout.Compact(width);
            _side.style.width = ReplayLayout.SideWidth(width);
            _bottom.style.flexDirection = compact ? FlexDirection.Column : FlexDirection.Row;
            _bottom.style.height = compact ? 104f : 68f;
            _bottom.style.alignItems = compact ? Align.Stretch : Align.Center;
            _transportRow.style.justifyContent = compact ? Justify.Center : Justify.FlexStart;
            _timelineRow.style.flexGrow = 1f;
            _progressLabel.style.width = compact ? 100f : 150f;
            _controllerGuide.style.maxWidth = Mathf.Max(120f, Mathf.Min(580f, width - 32f));
            _errorLabel.style.maxWidth = Mathf.Max(100f, Mathf.Min(700f, width - 48f));
        }

        static string ShortDate(string date) =>
            DateTime.TryParse(date, out var d) ? d.ToString("dd.MM.yyyy HH:mm") : date ?? "";
    }
}
