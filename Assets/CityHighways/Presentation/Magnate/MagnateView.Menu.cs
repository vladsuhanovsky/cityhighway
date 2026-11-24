using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Меню партии и окна поверх поля, как в соревновании.
    /// - «Меню» (кнопка с тремя полосками) открывает под верхней строкой окно «Партия»: правила коротко и кнопки
    ///   «Обучение», «Правила», «Выйти в меню».
    /// - «Настройки» (шестерёнка в правом углу) — окно по центру: звук, громкость, наклон поля, 3D-город, угол
    ///   камеры, мини-карта, вращение левой кнопкой.
    /// - «Правила» из меню — полные правила режима (та же страница, что в главном меню).
    /// Окна закрываются крестиком, кликом по затемнению или Esc.
    /// </summary>
    public sealed partial class MagnateView
    {
        VisualElement _modalLayer, _settingsCard, _settingsBody, _rulesCard, _rulesBody;
        string _modal;

        void BuildModals()
        {
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
            _modalLayer.focusable = true;
            _modalLayer.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                CloseModal();
                e.StopPropagation();
            });
            Root.Add(_modalLayer);

            _settingsCard = ModalCard(LocalizationService.Get("magnate.menu.001"), 520f, out _settingsBody);
            _rulesCard = ModalCard(null, 1200f, out _rulesBody);
            _rulesCard.style.height = Length.Percent(92f);
        }

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
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            card.Add(body);
            card.style.display = DisplayStyle.None;
            card.style.opacity = 0f;
            _modalLayer.Add(card);
            return card;
        }

        VisualElement ModalElement(string modal) =>
            modal == "settings" ? _settingsCard : modal == "rules" ? _rulesCard : null;

        void ToggleModal(string modal)
        {
            if (_modal == modal) CloseModal();
            else OpenModal(modal);
        }

        void OpenModal(string modal)
        {
            var card = ModalElement(modal);
            if (card == null) return;
            ClosePopup(false);
            var previous = ModalElement(_modal);
            if (previous != null) previous.style.display = DisplayStyle.None;
            _modal = modal;
            if (modal == "settings") BuildSettings();
            if (modal == "rules")
            {
                _rulesBody.Clear();
                var page = MenuRulesPage.Build(new GameConfig(), CloseModal);
                page.style.flexGrow = 1f;
                _rulesBody.Add(page);
            }
            _modalLayer.BringToFront();
            Ui.Slide(_modalLayer, true, 0f, 0f);
            Ui.Slide(card, true, 0f, 4f);
            if (_gamepadFocusActive) UiGamepadFocus.FocusFirst(card);
            else _modalLayer.Focus();
            _app.Sound.Play(Sfx.Open, 0.5f);
        }

        void CloseModal()
        {
            if (_modal == null) return;
            Ui.Slide(ModalElement(_modal), false, 0f, 4f);
            Ui.Slide(_modalLayer, false, 0f, 0f);
            _modal = null;
            _app.Sound.Play(Sfx.Close, 0.45f);
        }

        /// <summary>Окно «Партия» под кнопкой «Меню»: правила коротко и кнопки обучения, полных правил и выхода.</summary>
        void FillMenu()
        {
            var actions = Ui.Row();
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginBottom = 10f;
            Button AddAction(string caption, ButtonKind kind, Action onClick)
            {
                var button = Ui.Button(caption, onClick, kind, 13f);
                button.style.marginRight = 8f;
                button.style.marginBottom = 4f;
                button.style.minWidth = 110f;
                actions.Add(button);
                return button;
            }
            AddAction(LocalizationService.Get("magnate.menu.002"), ButtonKind.Secondary, () =>
            {
                ClosePopup(false);
                StartCoach();
            });
            AddAction(LocalizationService.Get("magnate.menu.003"), ButtonKind.Secondary, () => OpenModal("rules"));
            AddAction(LocalizationService.Get("magnate.menu.004"), ButtonKind.Ghost, () =>
            {
                ClosePopup(false);
                _app.ShowMenu();
            });
            _popupBody.Add(Pad(actions));

            var subtitle = Ui.Text(LocalizationService.Get("magnate.menu.005"), 13f, Theme.TextDim, true);
            subtitle.style.marginBottom = 6f;
            _popupBody.Add(Pad(subtitle));
            FillRules();
            _popupTitle.text = LocalizationService.Get("magnate.menu.006");
        }

        void RefreshJournalButton()
        {
            if (_journalButton == null) return;
            bool open = _popupKind == "log";
            if (open) _logSeen = _log.Count;
            _journalButton.text = _log.Count > _logSeen && !open ? LocalizationService.Get("magnate.menu.007") : LocalizationService.Get("magnate.menu.008");
        }

        /// <summary>Настройки прямо в партии магната. Режим экрана и разрешение — в главном меню.</summary>
        void BuildSettings()
        {
            _settingsBody.Clear();
            var display = _app.Display;

            VisualElement Row(string caption)
            {
                var row = Ui.Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 12f;
                var label = Ui.Text(caption, 16f, Theme.Text);
                label.style.flexShrink = 1f;
                label.style.whiteSpace = WhiteSpace.Normal;
                row.Add(label);
                _settingsBody.Add(row);
                return row;
            }

            void Toggle(string caption, Func<bool> get, Action<bool> set)
            {
                var row = Row(caption);
                var button = Ui.Button(get() ? LocalizationService.Get("magnate.menu.009") : LocalizationService.Get("magnate.menu.010"), () => { }, ButtonKind.Secondary, 14f);
                button.style.width = 170f;
                button.style.flexShrink = 0f;
                button.clicked += () =>
                {
                    set(!get());
                    button.text = get() ? LocalizationService.Get("magnate.menu.009") : LocalizationService.Get("magnate.menu.010");
                };
                row.Add(button);
            }

            Toggle(LocalizationService.Get("magnate.menu.011"), () => _app.Sound.Enabled, on =>
            {
                _app.Sound.Enabled = on;
                _setup.sound = on;
                if (_setup.practice)
                {
                    // У тренировки свой набор настроек: переносим в сохранённые только звук.
                    var stored = GameSetup.Load();
                    stored.sound = on;
                    stored.Save();
                }
                else _setup.Save();
            });

            if (display != null)
            {
                var volumeRow = Row(LocalizationService.Get("magnate.menu.012"));
                var controls = Ui.Row();
                controls.style.flexShrink = 0f;
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

                Toggle(LocalizationService.Get("magnate.menu.013"), () => display.tiltedBoard, on =>
                {
                    display.tiltedBoard = on;
                    display.Save();
                    _map.TiltEnabled = on;
                });
                Toggle(LocalizationService.Get("magnate.menu.014"), () => display.city3D, on =>
                {
                    display.city3D = on;
                    display.Save();
                    if (on) Setup3D();
                    else Release3D();
                });

                // Угол камеры 3D-города: 30° — почти сверху, 45° — панорама.
                var angleRow = Row(LocalizationService.Get("magnate.menu.015"));
                var angles = Ui.Row();
                angles.style.flexShrink = 0f;
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

                Toggle(LocalizationService.Get("magnate.menu.016"), () => display.showMinimap, on =>
                {
                    display.showMinimap = on;
                    display.Save();
                    if (_minimap != null) Ui.Show(_minimap, on);
                });
                Toggle(LocalizationService.Get("magnate.menu.017"), () => display.leftDragRotates, on =>
                {
                    display.leftDragRotates = on;
                    display.Save();
                });
            }

            var hint = Ui.Text(LocalizationService.Get("magnate.menu.018"), 13f, Theme.TextDim);
            hint.style.marginTop = 4f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            _settingsBody.Add(hint);
        }
    }
}
