using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Local-only redo of the most recently undone card/road command.
    /// The original pre-undo Game is a witness, never restored directly:
    /// the new live Game is rebuilt from deterministic commands.
    /// </summary>
    public static class GameRedo
    {
        public static bool CanRedo(Game current, Game beforeUndo, bool online,
            bool hasBots, bool tutorial, bool timerEnabled, bool hiddenHands)
        {
            if (current == null || beforeUndo == null ||
                online || hasBots || tutorial || timerEnabled || hiddenHands ||
                current.Phase == GamePhase.GameOver ||
                beforeUndo.Phase == GamePhase.GameOver ||
                !current.CanReplay || !beforeUndo.CanReplay ||
                !ReferenceEquals(current.Map, beforeUndo.Map) ||
                current.Seed != beforeUndo.Seed ||
                beforeUndo.History.Count != current.History.Count + 1)
                return false;

            var command = beforeUndo.History[beforeUndo.History.Count - 1];
            return GameUndo.Reversible(command);
        }

        /// <summary>
        /// Reject divergent timelines even when a different command happened
        /// to leave an apparently identical hand, map or score.
        /// </summary>
        public static bool HasExactHistoryPrefix(
            IReadOnlyList<GameCommand> current, IReadOnlyList<GameCommand> beforeUndo)
        {
            if (current == null || beforeUndo == null ||
                current.Count + 1 != beforeUndo.Count) return false;
            for (int i = 0; i < current.Count; i++)
                if (!SameCommand(current[i], beforeUndo[i])) return false;
            return true;
        }

        public static bool SameCommand(GameCommand a, GameCommand b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return string.Equals(a.kind, b.kind, StringComparison.Ordinal) &&
                a.player == b.player && a.slot == b.slot &&
                string.Equals(a.road, b.road, StringComparison.Ordinal) &&
                a.resource == b.resource && a.machinery == b.machinery &&
                SameContracts(a.contracts, b.contracts) &&
                a.index == b.index && a.to == b.to &&
                a.giveResource == b.giveResource && a.giveCount == b.giveCount &&
                a.getResource == b.getResource && a.getCount == b.getCount &&
                a.accept == b.accept && a.giveInfluence == b.giveInfluence &&
                a.getInfluence == b.getInfluence &&
                a.givePass == b.givePass && a.getPass == b.getPass &&
                a.option == b.option && a.bid == b.bid;
        }

        static bool SameContracts(string[] a, string[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        }

        /// <summary>
        /// Complete the removed command only if both histories remain
        /// replayable, the current history is an exact prefix, and the
        /// reconstructed result matches the saved original state.
        /// No mutation happens to either supplied Game on failure.
        /// </summary>
        public static bool TryRedo(Game current, Game beforeUndo, bool online,
            bool hasBots, bool tutorial, bool timerEnabled, bool hiddenHands,
            out Game restored, out List<string> journal)
        {
            restored = null;
            journal = new List<string>();
            if (!CanRedo(current, beforeUndo, online, hasBots, tutorial,
                    timerEnabled, hiddenHands) ||
                !HasExactHistoryPrefix(current.History, beforeUndo.History))
                return false;

            if (!current.HasVerifiedUndoHistory() ||
                !beforeUndo.HasVerifiedUndoHistory())
                return false;

            try
            {
                var command = beforeUndo.History[beforeUndo.History.Count - 1];
                var history = current.History.Concat(new[] { command }).ToArray();
                var rebuilt = Game.Replay(current.Map, current.StartInfo, history, journal.Add);
                if (!rebuilt.SameGameState(beforeUndo) ||
                    !HasExactHistoryPrefix(current.History, rebuilt.History))
                {
                    journal.Clear();
                    return false;
                }
                restored = rebuilt;
                return true;
            }
            catch (Exception)
            {
                journal.Clear();
                return false;
            }
        }
    }
}
