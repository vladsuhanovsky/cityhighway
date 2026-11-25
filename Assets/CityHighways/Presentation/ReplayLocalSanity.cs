using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Minimal shape required to *open* a local saved replay. Unlike cloud
    /// result verification, unfinished local games may have an empty history
    /// and no identified author seat. No simulation is performed here.
    /// </summary>
    public static class ReplayLocalSanity
    {
        public static bool IsReadable(ReplayFile replay)
        {
            if (replay == null || replay.version != ReplayFile.CurrentVersion ||
                !ReplayStorageIO.IsSafeId(replay.id) ||
                (replay.mode != "classic" && replay.mode != "coop") ||
                string.IsNullOrWhiteSpace(replay.mapPath) ||
                replay.start == null || replay.start.config == null ||
                replay.start.players == null ||
                replay.start.players.Length < 2 || replay.start.players.Length > 8 ||
                replay.players == null ||
                replay.players.Length != replay.start.players.Length ||
                replay.scores == null ||
                replay.scores.Length != replay.players.Length ||
                (replay.humans != null && replay.humans.Length != replay.players.Length) ||
                (replay.start.characters != null &&
                 replay.start.characters.Length != replay.players.Length) ||
                replay.authorSeat < -1 || replay.authorSeat >= replay.players.Length ||
                replay.history == null || replay.history.Length > Replays.MaxCloudReplayActions ||
                replay.turns < 0)
                return false;

            foreach (var name in replay.start.players)
                if (string.IsNullOrWhiteSpace(name)) return false;
            foreach (var name in replay.players)
                if (string.IsNullOrWhiteSpace(name)) return false;
            foreach (var command in replay.history)
                if (command == null || string.IsNullOrEmpty(command.kind) ||
                    command.player < -1 || command.player >= replay.players.Length)
                    return false;
            return true;
        }
    }
}
