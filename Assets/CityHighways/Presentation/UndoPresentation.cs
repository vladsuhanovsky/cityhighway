using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Pure labels and input policies for the local undo UX; none of these
    /// helpers changes game rules, a command history or a saved game.
    /// </summary>
    public static class UndoPresentation
    {
        public static string ActionLabel(GameCommand command, bool english)
        {
            switch (command?.kind)
            {
                case GameCommand.DrawDeck:
                    return english ? "deck card" : "карту из колоды";
                case GameCommand.DrawFaceUp:
                    return english ? "face-up card" : "открытую карту";
                case GameCommand.Build:
                    return english ? "road construction" : "строительство дороги";
                case GameCommand.BuildFree:
                    return english ? "free road construction" : "бесплатное строительство";
                case GameCommand.Upgrade:
                    return english ? "road upgrade" : "улучшение дороги";
                default:
                    return english ? "last action" : "последнее действие";
            }
        }

        /// <summary>
        /// End-of-turn card draws may move the turn to the next human.
        /// Require an extra confirmation before undoing that player's action.
        /// </summary>
        public static bool RequiresConfirmation(GameCommand command, int actingPlayer) =>
            command != null && command.player >= 0 && actingPlayer >= 0 &&
            command.player != actingPlayer;

        public static bool ConfirmationValid(int historyLength, int armedHistoryLength,
            int lastPlayer, int armedPlayer, float now, float expiresAt) =>
            historyLength > 0 && historyLength == armedHistoryLength &&
            lastPlayer == armedPlayer && now <= expiresAt;

        /// <summary>Windows/Steam Deck must not treat the system Win key as Cmd.</summary>
        public static bool UndoModifier(bool ctrlPressed, bool metaPressed, bool onMac) =>
            ctrlPressed || (onMac && metaPressed);

        /// <summary>Ctrl/Cmd+Z is reserved for undo outside text fields.</summary>
        public static bool WantsShortcut(bool modifier, bool shift, bool zPressed,
            bool textEntryFocused, bool canUndo) =>
            modifier && !shift && zPressed && !textEntryFocused && canUndo;

        /// <summary>Platform-specific hint, while the binding itself stays universal.</summary>
        public static string RedoShortcutLabel(bool onMac) =>
            onMac ? "Cmd+Shift+Z" : "Ctrl+Y / Ctrl+Shift+Z";

        /// <summary>
        /// Redo is Ctrl+Y or Ctrl+Shift+Z on Windows/Linux, and
        /// Command+Shift+Z on macOS. Never consume text-field editing.
        /// </summary>
        public static bool WantsRedoShortcut(bool modifier, bool shift,
            bool yPressed, bool zPressed, bool textEntryFocused,
            bool canRedo, bool onMac) =>
            modifier && !textEntryFocused && canRedo &&
            ((shift && zPressed) || (!onMac && !shift && yPressed));
    }
}
