using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Verifies successful social GET/mutation responses before allowing
    /// JsonUtility to fill an object with implicit defaults. Reads the same
    /// bounded JSON grammar as the durable report ACK validator.
    /// </summary>
    public static class SocialReadContracts
    {
        public const int MaxCharacters = 256 * 1024;

        static bool Parse(string json, out Dictionary<string, object> root) =>
            SocialReportAck.TryReadRoot(json, MaxCharacters, out root);

        static bool Fields(object value, out Dictionary<string, object> fields)
        {
            fields = value as Dictionary<string, object>;
            return fields != null;
        }

        static bool Array(Dictionary<string, object> fields, string name, int max, out List<object> array)
        {
            array = null;
            return fields.TryGetValue(name, out var value) &&
                (array = value as List<object>) != null && array.Count <= max;
        }

        static bool Text(Dictionary<string, object> fields, string name, int max,
            bool required = false, bool nullable = false)
        {
            if (!fields.TryGetValue(name, out var value)) return false;
            if (value == null) return nullable;
            return value is string text && text.Length <= max &&
                (!required || !string.IsNullOrWhiteSpace(text));
        }

        static bool Boolean(Dictionary<string, object> fields, string name) =>
            fields.TryGetValue(name, out var value) && value is bool;

        static bool Integer(Dictionary<string, object> fields, string name, int min, int max) =>
            fields.TryGetValue(name, out var value) && SocialReportAck.ReadInt(value, min, max, out _);

        static bool Record(object value)
        {
            if (!Fields(value, out var r)) return false;
            return r.TryGetValue("id", out var id) &&
                SocialReportAck.ReadLong(id, 1, long.MaxValue, out _) &&
                Text(r, "playerId", 20, required: true) &&
                Text(r, "name", 128) &&
                Text(r, "mode", 16, required: true) &&
                Text(r, "mapPath", 200, required: true) &&
                Text(r, "mapName", 128) &&
                Integer(r, "score", -100000, 100000) &&
                Boolean(r, "won") &&
                Integer(r, "seats", 1, 8) &&
                Text(r, "date", 64) &&
                SocialReportOutbox.ValidOwner((string)r["playerId"]) &&
                (string.Equals((string)r["mode"], "classic", StringComparison.Ordinal) ||
                 string.Equals((string)r["mode"], "coop", StringComparison.Ordinal));
        }

        static bool Records(Dictionary<string, object> root, string key, int max)
        {
            if (!Array(root, key, max, out var records)) return false;
            foreach (object record in records)
                if (!Record(record)) return false;
            return true;
        }

        public static bool ValidateFriends(string json) =>
            Parse(json, out var root) && Records(root, "records", 200);

        static bool ClubInfo(object value)
        {
            if (!Fields(value, out var club)) return false;
            return Text(club, "id", 64, required: true) &&
                Text(club, "name", 64, required: true) &&
                Text(club, "tag", 8, required: true) &&
                Text(club, "code", 32, required: true) &&
                Boolean(club, "open") && Text(club, "logo", 128, nullable: true) &&
                Boolean(club, "owner");
        }

        static bool Members(Dictionary<string, object> root)
        {
            if (!Array(root, "members", 30, out var list)) return false;
            foreach (object value in list)
            {
                if (!Fields(value, out var member) ||
                    !Text(member, "name", 128) ||
                    !Text(member, "role", 16, required: true) ||
                    !Boolean(member, "me") ||
                    !Integer(member, "amount", 0, 10000000) ||
                    !Integer(member, "games", 0, 1000000))
                    return false;
            }
            return true;
        }

        static bool Week(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("week", out var value) || !Fields(value, out var week))
                return false;
            return Text(week, "id", 64, required: true) &&
                Text(week, "endsAt", 64, required: true) &&
                Text(week, "kind", 32, required: true) &&
                Integer(week, "target", 0, 10000000) &&
                Integer(week, "progress", 0, 10000000) &&
                Boolean(week, "completed");
        }

        static bool Logos(Dictionary<string, object> root)
        {
            if (!Array(root, "logos", 256, out var logos)) return false;
            foreach (object value in logos)
            {
                if (!Fields(value, out var logo) ||
                    !Text(logo, "logo", 128, required: true) ||
                    !Text(logo, "week", 64))
                    return false;
            }
            return true;
        }

        public static bool TryParseClub(string json, out SocialCloud.ClubState state)
        {
            state = null;
            if (!Parse(json, out var root) || !root.TryGetValue("club", out var club))
                return false;

            // An account with no membership has exactly { "club": null }.
            // JsonUtility sometimes materializes class fields from JSON null;
            // override that value explicitly before publishing it to the UI.
            if (club != null &&
                (!ClubInfo(club) || !Members(root) || !Week(root) ||
                 !Integer(root, "weeksDone", 0, 1000000) || !Logos(root) ||
                 !Records(root, "records", 40)))
                return false;

            try
            {
                state = JsonUtility.FromJson<SocialCloud.ClubState>(json);
                if (state == null) return false;
                if (club == null) state.club = null;
                return true;
            }
            catch (Exception)
            {
                state = null;
                return false;
            }
        }

        public static bool ValidateClubList(string json)
        {
            if (!Parse(json, out var root) || !Array(root, "clubs", 30, out var clubs))
                return false;
            foreach (object value in clubs)
            {
                if (!Fields(value, out var club) ||
                    !Text(club, "id", 64, required: true) ||
                    !Text(club, "name", 64, required: true) ||
                    !Text(club, "tag", 8, required: true) ||
                    !Text(club, "logo", 128, nullable: true) ||
                    !Integer(club, "members", 0, 30))
                    return false;
            }
            return true;
        }
    }
}
