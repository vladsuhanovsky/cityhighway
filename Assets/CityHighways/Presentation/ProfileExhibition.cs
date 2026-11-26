using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Presentation
{
    public sealed class Exhibit
    {
        public string Id { get; }
        public string Group { get; }
        public string Vehicle { get; }
        public string Achievement { get; }
        public string Map { get; }
        internal Exhibit(string id, string group, string vehicle = null, string achievement = null, string map = null)
        { Id = id; Group = group; Vehicle = vehicle; Achievement = achievement; Map = map; }

        public bool IsUnlocked(PlayerProfile profile) => profile != null &&
            (Achievement == null || profile.achievements?.Contains(Achievement) == true) &&
            (Map == null || profile.HasMark("map:" + Map));
    }

    /// <summary>Exhibition unlocks are a read-only projection of existing progress.
    /// No new achievements, loot rolls, payments or independently mutable ownership.</summary>
    public static class ProfileExhibition
    {
        public const int SlotCount = 3;
        static readonly Exhibit[] Catalog =
        {
            new Exhibit("paver", "vehicles", "paver"),
            new Exhibit("mixer", "vehicles", "mixer", "first_contract"),
            new Exhibit("grader", "vehicles", "grader", "upgrades3"),
            new Exhibit("crane", "vehicles", "crane", "bridges5"),
            new Exhibit("moscow", "cities", map: "Москва"),
            new Exhibit("amsterdam", "cities", map: "Амстердам"),
            new Exhibit("chongqing", "cities", map: "Чунцин"),
            new Exhibit("first_win", "medals", achievement: "first_win"),
            new Exhibit("mg_win", "medals", achievement: "mg_win"),
            new Exhibit("travel", "medals", achievement: "travel")
        };
        public static IReadOnlyList<Exhibit> All { get; } = Array.AsReadOnly(Catalog);
        public static Exhibit Find(string id) => Catalog.FirstOrDefault(e => e.Id == id);
        static CompanyIdentityRecord Record(ProfileBook book, PlayerProfile profile)
        {
            var key = CompanyIdentity.OwnerKey(profile);
            return string.IsNullOrEmpty(key) ? null : book?.companies?.FirstOrDefault(r => r != null && r.owner == key);
        }

        public static string[] Normalize(PlayerProfile profile, string[] raw)
        {
            var result = new string[SlotCount];
            var used = new HashSet<string>();
            for (int i = 0; i < result.Length; i++)
            {
                string id = raw != null && i < raw.Length ? raw[i] : "";
                var exhibit = Find(id);
                result[i] = exhibit != null && exhibit.IsUnlocked(profile) && used.Add(id) ? id : "";
            }
            return result;
        }

        public static string[] Read(ProfileBook book, PlayerProfile profile) =>
            Normalize(profile, Record(book, profile)?.exhibits);

        public static bool Store(ProfileBook book, PlayerProfile profile, int slot, string id)
        {
            if (book == null || string.IsNullOrEmpty(CompanyIdentity.OwnerKey(profile)) ||
                slot < 0 || slot >= SlotCount) return false;
            id = id ?? "";
            if (id.Length > 0 && Find(id)?.IsUnlocked(profile) != true) return false;
            var next = Read(book, profile);
            if (id.Length > 0)
                for (int i = 0; i < next.Length; i++)
                    if (next[i] == id) next[i] = "";
            next[slot] = id;
            CompanyIdentity.Store(book, profile, CompanyIdentity.Read(book, profile));
            Record(book, profile).exhibits = next;
            return true;
        }
    }
}
