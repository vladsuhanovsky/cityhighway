using System;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Управление игровыми панелями, выдвижным меню и окнами настроек.</summary>
    public sealed partial class GameView
    {
        // ================================================================== Панели, меню и окна

        VisualElement SheetElement(SheetKind kind) =>
            kind == SheetKind.Actions ? _actionsSheet : kind == SheetKind.Contracts ? _contractsSheet : kind == SheetKind.Log ? _logSheet : null;

        void ToggleSheet(SheetKind kind)
        {
            if (_sheet == kind) CloseSheet();
            else OpenSheet(kind);
        }

        /// <summary>Открыть одну из нижних панелей (другая, если была, уезжает).</summary>
        void OpenSheet(SheetKind kind)
        {
            if (kind == SheetKind.None || _sheet == kind) return;
            var previous = SheetElement(_sheet);
            if (previous != null) Ui.Slide(previous, false, 0f, SheetHiddenY(_sheet));
            if (kind == SheetKind.Log) ClosePlayers();
            _sheet = kind;
            Ui.Slide(SheetElement(kind), true, 0f, SheetHiddenY(kind));
            _app.Sound.Play(Sfx.Open, 0.5f);
            if (kind == SheetKind.Log)
            {
                _logSeenTotal = _logTotal;
                RefreshLog(rebuild: true);
            }
            else Ui.Show(_logNewButton, false);
            if (_game != null) RefreshTabs();
        }

        /// <summary>Журнал прячется вверх (его кнопка наверху), остальные панели — вниз.</summary>
        static float SheetHiddenY(SheetKind kind) => kind == SheetKind.Log ? -110f : 110f;

        void CloseSheet()
        {
            var current = SheetElement(_sheet);
            if (current == null) return;
            Ui.Slide(current, false, 0f, SheetHiddenY(_sheet));
            _sheet = SheetKind.None;
            _app.Sound.Play(Sfx.Close, 0.45f);
            if (_game != null) RefreshTabs();
        }

        bool _autoChoiceOpen;

        /// <summary>
        /// Панели открываются сами там, где без них не обойтись: выбор контрактов — панель контрактов,
        /// начало своего хода — панель действий. Ходят соперники — панель действий уезжает, поле открыто.
        /// Закрыл панель сам — в этот же ход она больше не выскакивает.
        /// </summary>
        void AutoPanels()
        {
            if (_game == null || _game.Phase == GamePhase.GameOver) return;
            if (TryGetContractChooser(out var chooser))
            {
                string key = $"choose:{chooser.Index}:{_game.Phase}:{chooser.CharacterOffer.Count}:" + string.Join(",", chooser.PendingContracts.Select(c => c.Id));
                if (key != _autoSheetKey && _overlayKind == OverlayKind.None)
                {
                    _autoSheetKey = key;
                    _autoChoiceOpen = true;
                    OpenSheet(SheetKind.Contracts);
                }
                return;
            }
            if (_autoChoiceOpen)
            {
                _autoChoiceOpen = false;
                if (_sheet == SheetKind.Contracts) CloseSheet();
            }

            bool myMove = HumanToAct && !NeedsHandoff && _overlayKind == OverlayKind.None &&
                          (_game.Phase == GamePhase.AwaitingAction || _game.Phase == GamePhase.DrawingSecondCard);
            if (myMove)
            {
                string key = $"turn:{_game.TurnNumber}:{_game.ActingPlayerIndex}";
                if (key != _autoSheetKey)
                {
                    _autoSheetKey = key;
                    if (_sheet == SheetKind.None) OpenSheet(SheetKind.Actions);
                }
            }
            else if (!HumanToAct && _sheet == SheetKind.Actions) CloseSheet();
        }

        void ToggleDrawer()
        {
            if (_drawerOpen) CloseDrawer();
            else OpenDrawer();
        }

        void OpenDrawer()
        {
            if (_drawerOpen) return;
            ClosePlayers();
            _drawerOpen = true;
            Ui.Slide(_drawer, true, 105f, 0f);
            _app.Sound.Play(Sfx.Open, 0.5f);
            if (_game != null)
            {
                _seenGoalOwners = _game.ActiveGoals.Count(g => _game.GoalOwner(g.Id).HasValue);
                RefreshTabs();
            }
        }

        void CloseDrawer()
        {
            if (!_drawerOpen) return;
            _drawerOpen = false;
            Ui.Slide(_drawer, false, 105f, 0f);
            _app.Sound.Play(Sfx.Close, 0.45f);
        }

        VisualElement ModalElement(Modal modal) =>
            modal == Modal.Settings ? _settingsModal : modal == Modal.Rules ? _rulesModal : null;

        /// <summary>Карточка окна: заголовок с крестиком (или без, если title == null) и тело.</summary>
        VisualElement ModalCard(string title, float width, out VisualElement body)
        {
            var card = Ui.Column();
            card.pickingMode = PickingMode.Position;
            card.style.width = width;
            card.style.maxWidth = Length.Percent(94f);
            card.style.maxHeight = Length.Percent(92f);
            card.style.backgroundColor = Theme.Panel;
            Ui.Radius(card, 16f);
            Ui.Border(card, 1f, Theme.Line);
            Ui.Padding(card, 18f, 22f);
            Ui.Shadow(card, 16f, 22f, 0.6f);
            var header = Ui.Row();
            header.style.marginBottom = 12f;
            header.Add(Ui.Text(title ?? "", 22f, Theme.Text, true));
            var spacer = Ui.Box();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);
            header.Add(new IconButton(IconButton.Glyph.Close, "", CloseModal, 12f));
            card.Add(header);
            if (title == null) Ui.Show(header, false);
            body = Ui.Column();
            body.style.flexShrink = 1f;
            body.style.minHeight = 0f;
            card.Add(body);
            card.style.display = DisplayStyle.None;
            card.style.opacity = 0f;
            _modalLayer.Add(card);
            return card;
        }

        void TogglePlayers()
        {
            if (_playersOpen) ClosePlayers();
            else OpenPlayers();
        }

        /// <summary>Панель «Игроки» выезжает справа на поле. Справа же меню и журнал — их убираем, чтобы не наслаивались.</summary>
        void OpenPlayers()
        {
            if (_playersOpen) return;
            CloseDrawer();
            if (_sheet == SheetKind.Log) CloseSheet();
            _playersOpen = true;
            if (_game != null) RefreshPlayers();
            Ui.Slide(_playersPanel, true, 110f, 0f);
            _app.Sound.Play(Sfx.Open, 0.5f);
            if (_game != null) RefreshTabs();
        }

        void ClosePlayers()
        {
            if (!_playersOpen) return;
            _playersOpen = false;
            Ui.Slide(_playersPanel, false, 110f, 0f);
            _app.Sound.Play(Sfx.Close, 0.45f);
            if (_game != null) RefreshTabs();
        }

        void ToggleModal(Modal modal)
        {
            if (_modal == modal) CloseModal();
            else OpenModal(modal);
        }

        void OpenModal(Modal modal)
        {
            if (modal == Modal.None) return;
            var previous = ModalElement(_modal);
            if (previous != null) previous.style.display = DisplayStyle.None;
            _modal = modal;
            if (modal == Modal.Settings) BuildSettings();
            if (modal == Modal.Rules)
            {
                _rulesBody.Clear();
                _rulesBody.Add(MenuRulesPage.Build(_game?.Config ?? new GameConfig(), CloseModal));
            }
            Ui.Slide(_modalLayer, true, 0f, 0f);
            Ui.Slide(ModalElement(modal), true, 0f, 4f);
            _app.Sound.Play(Sfx.Open, 0.5f);
        }

        void CloseModal()
        {
            if (_modal == Modal.None) return;
            Ui.Slide(ModalElement(_modal), false, 0f, 4f);
            Ui.Slide(_modalLayer, false, 0f, 0f);
            _modal = Modal.None;
            _app.Sound.Play(Sfx.Close, 0.45f);
        }

        /// <summary>Окно настроек прямо в партии: звук, громкость, наклон поля, мини-карта.</summary>
        void BuildSettings()
        {
            _settingsBody.Clear();
            var display = _app.Display;

            VisualElement Row(string caption)
            {
                var row = Ui.Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 12f;
                row.Add(Ui.Text(caption, 16f, Theme.Text));
                _settingsBody.Add(row);
                return row;
            }

            void Toggle(string caption, Func<bool> get, Action<bool> set)
            {
                var row = Row(caption);
                var button = Ui.Button(LocalizationService.Get(get() ? "common.enabled" : "common.disabled"), () => { }, ButtonKind.Secondary, 14f);
                button.style.width = 170f;
                button.clicked += () =>
                {
                    set(!get());
                    button.text = LocalizationService.Get(get() ? "common.enabled" : "common.disabled");
                };
                row.Add(button);
            }

            Toggle(LocalizationService.Get("game.settings.sound"), () => _app.Sound.Enabled, on =>
            {
                _app.Sound.Enabled = on;
                _setup.sound = on;
                if (_setup.practice || _setup.tutorial)
                {
                    // Тренировка и обучение — свой набор настроек: переносим в сохранённые только звук.
                    var stored = GameSetup.Load();
                    stored.sound = on;
                    stored.Save();
                }
                else _setup.Save();
            });

            if (display != null)
            {
                var volumeRow = Row(LocalizationService.Get("game.settings.volume"));
                var controls = Ui.Row();
                var value = Ui.Number($"{Mathf.RoundToInt(display.volume * 100f)}%", 16f, Theme.Text, true);
                value.style.width = 70f;
                value.style.unityTextAlign = TextAnchor.MiddleCenter;
                void Change(float delta)
                {
                    display.volume = Mathf.Clamp01(Mathf.Round((display.volume + delta) * 10f) / 10f);
                    display.Save();
                    display.Apply();
                    value.text = $"{Mathf.RoundToInt(display.volume * 100f)}%";
                    _app.Sound.Play(Sfx.Bell, 0.5f);
                }
                var quieter = Ui.Button("–", () => Change(-0.1f), ButtonKind.Secondary, 16f);
                quieter.style.width = 44f;
                var louder = Ui.Button("+", () => Change(0.1f), ButtonKind.Secondary, 16f);
                louder.style.width = 44f;
                controls.Add(quieter);
                controls.Add(value);
                controls.Add(louder);
                volumeRow.Add(controls);

                Toggle(LocalizationService.Get("game.settings.board_tilt"), () => display.tiltedBoard, on =>
                {
                    display.tiltedBoard = on;
                    display.Save();
                    _map.TiltEnabled = on;
                });
                Toggle(LocalizationService.Get("game.settings.city_3d"), () => display.city3D, on =>
                {
                    display.city3D = on;
                    display.Save();
                    if (on) Setup3D();
                    else Release3D();
                });

                // Угол камеры 3D-города: 30° — почти сверху, 45° — панорама.
                var angleRow = Row(LocalizationService.Get("game.settings.camera_angle"));
                var angles = Ui.Row();
                var angleButtons = new List<Button>();
                foreach (var degrees in DisplaySettings.TiltOptions)
                {
                    float angle = degrees;
                    var button = Ui.Button($"{angle:0}°", () => { }, ButtonKind.Secondary, 14f);
                    button.style.width = 56f;
                    button.style.marginLeft = 4f;
                    button.clicked += () =>
                    {
                        display.tiltDegrees = angle;
                        display.Save();
                        _map.CameraTiltDegrees = angle;
                        _map.RefreshLayout();
                        foreach (var other in angleButtons)
                            other.style.borderBottomColor = other == button ? Theme.Accent : Color.clear;
                    };
                    button.style.borderBottomWidth = 3f;
                    button.style.borderBottomColor = Mathf.Approximately(display.tiltDegrees, angle) ? Theme.Accent : Color.clear;
                    angleButtons.Add(button);
                    angles.Add(button);
                }
                angleRow.Add(angles);
                Toggle(LocalizationService.Get("game.settings.minimap"), () => display.showMinimap, on =>
                {
                    display.showMinimap = on;
                    display.Save();
                    Ui.Slide(_minimap, on, -120f, 0f);
                });
            }

            var hint = Ui.Text(LocalizationService.Get("game.settings.display_hint"), 13f, Theme.TextDim);
            hint.style.marginTop = 4f;
            _settingsBody.Add(hint);
        }

    }
}
