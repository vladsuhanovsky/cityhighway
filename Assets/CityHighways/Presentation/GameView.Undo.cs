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
            _undoButton.text = LocalizationService.Get("game.undo.action");
            Ui.SetEnabled(_undoButton, CanUndoAction);
        }

        void UndoLastAction()
        {
            if (!CanUndoAction) return;
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
            _dirty = true;
        }
    }
}
