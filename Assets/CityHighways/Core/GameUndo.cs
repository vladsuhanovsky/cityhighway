using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Guarded local-only undo. Instead of attempting a reverse payment,
    /// replays the deterministic command history without its last command.
    /// Never mutates the original Game on a denied/failed reconstruction.
    /// </summary>
    public static class GameUndo
    {
        public static bool Reversible(GameCommand command)
        {
            if (command == null) return false;
            switch (command.kind)
            {
                case GameCommand.DrawFaceUp:
                case GameCommand.DrawDeck:
                case GameCommand.Build:
                case GameCommand.BuildFree:
                case GameCommand.Upgrade:
                    return true;
                default:
                    return false;
            }
        }

        public static bool CanUndo(Game game, bool online, bool hasBots,
            bool tutorial, bool timerEnabled, bool hiddenHands)
        {
            if (game == null || online || hasBots || tutorial ||
                timerEnabled || hiddenHands || game.Phase == GamePhase.GameOver ||
                !game.CanReplay || game.History == null || game.History.Count == 0)
                return false;
            return Reversible(game.History[game.History.Count - 1]);
        }

        /// <summary>
        /// Create a separate, fully replayed state and optionally a replayed
        /// journal. Returns false without changing the current game on any
        /// invalid history or policy restriction.
        /// </summary>
        public static bool TryUndo(Game game, bool online, bool hasBots,
            bool tutorial, bool timerEnabled, bool hiddenHands,
            out Game restored, out List<string> journal)
        {
            restored = null;
            journal = new List<string>();
            if (!CanUndo(game, online, hasBots, tutorial, timerEnabled, hiddenHands))
                return false;
            // A loaded legacy/broken-history snapshot can still expose
            // StartInfo and History. Never replay a shortened history into
            // a new authoritative state unless the full history first
            // reconstructs the actual current game.
            if (!game.HasVerifiedUndoHistory()) return false;

            try
            {
                var commands = game.History.Take(game.History.Count - 1).ToArray();
                var rebuilt = Game.Replay(game.Map, game.StartInfo, commands, journal.Add);
                if (rebuilt.History.Count != commands.Length)
                {
                    journal.Clear();
                    return false;
                }
                restored = rebuilt;
                return true;
            }
            catch (Exception)
            {
                // The original live Game remains authoritative. No snapshot,
                // autosave or UI state is replaced after a failed replay.
                journal.Clear();
                return false;
            }
        }
    }
}
