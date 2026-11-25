using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// A downloaded replay must describe the record the player clicked.
    /// This prevents stale/cross-map payloads from entering the replay or
    /// raid screen; it does not establish authoritative game-rule validity.
    /// </summary>
    public static class SocialReplayMatch
    {
        public static bool Matches(SocialCloud.RecordInfo record, ReplayFile replay)
        {
            if (record == null || record.id <= 0 || replay == null || replay.start == null)
                return false;

            if (!string.IsNullOrEmpty(record.mode) &&
                !string.Equals(record.mode, replay.mode, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(record.mapPath) &&
                !string.Equals(record.mapPath, replay.mapPath, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(record.mapName) &&
                !string.Equals(record.mapName, replay.map, StringComparison.Ordinal))
                return false;

            // Cloud record cards always carry the mode, score, outcome and
            // seat count. Compatibility callers supplying an ID only still
            // receive the replay, but cannot assert absent metadata.
            if (!string.IsNullOrEmpty(record.mode) &&
                (record.score != replay.authorScore ||
                 record.won != replay.won ||
                 (record.seats > 0 && replay.players?.Length != record.seats)))
                return false;
            return true;
        }
    }
}
