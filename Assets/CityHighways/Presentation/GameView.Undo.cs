using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    public sealed partial class GameView
    {
        UnityEngine.UIElements.Button _undoButton;
        int _undoArmedHistory = -1;
        int _undoArmedPlayer = -1;
        float _undoArmedUntil;

        void ClearUndoConfirmation()
        {
            _undoArmedHistory = -1;
            _undoArmedPlayer = -1;
            _undoArmedUntil = 0f;
        }

        bool UndoConfirmationActive
        {
            get
            {
                if (_game == null || _game.History.Count == 0) return false;
                var last = _game.History[_game.History.Count - 1];
                return UndoPresentation.ConfirmationValid(_game.History.Count,
                    _undoArmedHistory, last.player, _undoArmedPlayer,
                    Time.unscaledTime, _undoArmedUntil);
            }
        }

        /// <summary>
        /// Only a shared, fully local hot-seat session without timers,
        /// hidden-hand handoff or bots can rewind reversible actions.
        /// Network/session commands and game-over rewards are never rewound.
        /// </summary>
        bool CanUndoAction =>
            _game != null &&
            GameUndo.CanUndo(_game, Online,
                _setup.players != null && _setup.players.Any(p => p.isBot),
                _setup.tutorial, _setup.turnSeconds > 0, _setup.hideCardsBetweenTurns) &&
            _overlayKind == OverlayKind.None && _modal == Modal.None &&
            !_drawerOpen && !_playersOpen &&
            _app.Tweener.BlockingCount == 0 &&
            !_map.IsGamepadTargeting;

        void RefreshUndoButton()
        {
            if (_undoButton == null) return;
            bool available = CanUndoAction;
            if (!available && _undoArmedHistory >= 0) ClearUndoConfirmation();
            bool confirm = available && UndoConfirmationActive;
            _undoButton.text = LocalizationService.Get(confirm
                ? "game.undo.confirm_button" : "game.undo.action");
            if (available && _game.History.Count > 0)
            {
                var command = _game.History[_game.History.Count - 1];
                var action = UndoPresentation.ActionLabel(command,
                    LocalizationService.Language == "en");
                _undoButton.tooltip = string.Format(
                    LocalizationService.Get("game.undo.tooltip"), action);
            }
            else
                _undoButton.tooltip = LocalizationService.Get("game.undo.disabled_hint");
            Ui.SetEnabled(_undoButton, available);
        }

        /// <summary>
        /// Do not intercept typing, Ctrl+Shift+Z, mouse-only play, dialogs
        /// or anything disallowed by the exact same HUD button policy.
        /// </summary>
        void TickUndoShortcut()
        {
            if (_undoArmedHistory >= 0 && !UndoConfirmationActive)
            {
                ClearUndoConfirmation();
                RefreshUndoButton();
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            bool modifier = keyboard.ctrlKey.isPressed ||
                keyboard[UnityEngine.InputSystem.Key.LeftMeta].isPressed ||
                keyboard[UnityEngine.InputSystem.Key.RightMeta].isPressed;
            if (UndoPresentation.WantsShortcut(
                    modifier, keyboard.shiftKey.isPressed,
                    keyboard.zKey.wasPressedThisFrame,
                    GamepadControlGuide.TextEntryFocused(Root), CanUndoAction))
                UndoLastAction();
        }

        void UndoLastAction()
        {
            if (!CanUndoAction)
            {
                ClearUndoConfirmation();
                return;
            }

            var last = _game.History[_game.History.Count - 1];
            if (UndoPresentation.RequiresConfirmation(last, _game.ActingPlayerIndex) &&
                !UndoConfirmationActive)
            {
                _undoArmedHistory = _game.History.Count;
                _undoArmedPlayer = last.player;
                _undoArmedUntil = Time.unscaledTime + 6f;
                RefreshUndoButton();
                string owner = last.player < _game.Players.Count
                    ? _game.Players[last.player].Name : "?";
                Toast(string.Format(LocalizationService.Get("game.undo.confirm_other"),
                    owner), Theme.Gold);
                return;
            }
            ClearUndoConfirmation();
            if (!GameUndo.TryUndo(_game, Online,
                    _setup.players != null && _setup.players.Any(p => p.isBot),
                    _setup.tutorial, _setup.turnSeconds > 0, _setup.hideCardsBetweenTurns,
                    out var restored, out List<string> journal))
            {
                Flash(LocalizationService.Get("game.undo.unavailable"));
                return;
            }

            // Rewind never fires the old game's end-of-game listeners. Swap
            // it only after replay succeeded, then let Begin rebuild all
            // card, road, HUD and 3D projections and the new autosave.
            _app.Tweener.Clear();
            _map.StopGamepadTargeting(false);
            _game.Log -= AddLog;
            _game.StateChanged -= OnStateChanged;
            Begin(restored, _playSeconds, LocalizationService.Get("game.undo.done"),
                skipTutorialPrompt: true);

            // Reconstruct the displayed journal from the *remaining*
            // commands. Never keep a line from the undone action.
            _log.Clear();
            _logTotal = 0;
            _logSeenTotal = 0;
            _seenLog = null;
            _logShownTotal = -1;
            foreach (string entry in journal.Skip(Math.Max(0, journal.Count - (LogHistory - 1))))
                AddLog(entry);
            AddLog(LocalizationService.Get("game.undo.done"));
            _app.Sound.Play(Sfx.Undo, 0.55f);
            _dirty = true;
        }
    }
}
