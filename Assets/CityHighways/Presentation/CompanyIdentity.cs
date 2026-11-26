using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CityHighways.Presentation
{
    // Stored in the local ProfileBook, never in the cloud-synchronized PlayerProfile.
    [Serializable]
    public sealed class CompanyIdentityRecord
    {
        public string owner, name, emblem, primary, secondary;
        public FleetPaintRecord[] fleet;
        public string[] exhibits;
    }

    /// <summary>Bounded cosmetic identity. Colors are fleet paint only, never ownership markers.</summary>
    public sealed class CompanyIdentity
    {
        public const int MaxNameLength = 32;
        public string Name { get; }
        public string Emblem { get; }
        public string Primary { get; }
        public string Secondary { get; }

        static readonly string[] Palette = { "navy", "orange", "steel", "white", "green", "red", "blue", "graphite" };
        public static string[] ColorIds => (string[])Palette.Clone();

        CompanyIdentity(string name, string emblem, string primary, string secondary)
        { Name = name; Emblem = emblem; Primary = primary; Secondary = secondary; }

        public static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var text = new StringBuilder(MaxNameLength);
            // Bound work even for malformed local JSON. Do not split UTF-16 surrogate pairs.
            int limit = Math.Min(value.Length, 256);
            for (int i = 0; i < limit && text.Length < MaxNameLength; i++)
            {
                char c = value[i];
                if (char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < limit && char.IsLowSurrogate(value[i + 1]) && text.Length + 2 <= MaxNameLength)
                    { text.Append(c); text.Append(value[++i]); }
                    else if (i + 1 < limit && char.IsLowSurrogate(value[i + 1])) break;
                }
                else if (!char.IsLowSurrogate(c)) text.Append(c);
            }
            return text.ToString().Trim();
        }

        public static CompanyIdentity Create(PlayerProfile profile, string name, string emblem, string primary, string secondary)
        {
            var item = Profiles.Item(emblem);
            bool allowed = item != null && item.Kind == ShopKind.Emblem &&
                (item.Price <= 0 || (profile?.owned != null && profile.owned.Contains(item.Id)));
            return new CompanyIdentity(NormalizeName(name), allowed ? item.Id : "",
                Palette.Contains(primary) ? primary : "navy", Palette.Contains(secondary) ? secondary : "orange");
        }

        public static Color Paint(string id)
        {
            switch (id)
            {
                case "orange": return new Color(.95f,.48f,.12f);
                case "steel": return new Color(.66f,.73f,.80f);
                case "white": return new Color(.94f,.94f,.91f);
                case "green": return new Color(.26f,.58f,.39f);
                case "red": return new Color(.77f,.25f,.23f);
                case "blue": return new Color(.23f,.47f,.78f);
                case "graphite": return new Color(.22f,.24f,.27f);
                default: return new Color(.13f,.23f,.36f);
            }
        }

        public static string OwnerKey(PlayerProfile profile)
        {
            if (profile == null) return "";
            if (profile.IsSteam) return "steam:" + profile.steamId;
            var name = (profile.name ?? "").Trim().ToLowerInvariant();
            return name.Length == 0 ? "" : "local:" + name;
        }

        public static CompanyIdentity Read(ProfileBook book, PlayerProfile profile)
        {
            string key = OwnerKey(profile);
            var record = string.IsNullOrEmpty(key) ? null : book?.companies?.FirstOrDefault(r => r != null && r.owner == key);
            return Create(profile, record?.name, record?.emblem, record?.primary, record?.secondary);
        }

        public static void Store(ProfileBook book, PlayerProfile profile, CompanyIdentity value)
        {
            string key = OwnerKey(profile);
            if (book == null || string.IsNullOrEmpty(key) || value == null) return;
            value = Create(profile, value.Name, value.Emblem, value.Primary, value.Secondary);
            if (book.companies == null) book.companies = new List<CompanyIdentityRecord>();
            var prior = book.companies.FirstOrDefault(r => r != null && r.owner == key);
            var fleet = prior?.fleet;
            book.companies.RemoveAll(r => r != null && r.owner == key);
            book.companies.Add(new CompanyIdentityRecord { owner = key, name = value.Name,
                emblem = value.Emblem, primary = value.Primary, secondary = value.Secondary, fleet = fleet, exhibits = prior?.exhibits });
        }

        public static void Move(ProfileBook book, string oldKey, string newKey)
        {
            if (book?.companies == null || string.IsNullOrEmpty(oldKey) || string.IsNullOrEmpty(newKey) || oldKey == newKey) return;
            var record = book.companies.FirstOrDefault(r => r != null && r.owner == oldKey);
            Remove(book, oldKey);
            if (record == null || book.companies.Any(r => r != null && r.owner == newKey)) return;
            // Preserve old snapshots used by local cosmetic transactions.
            book.companies.Add(new CompanyIdentityRecord { owner = newKey, name = record.name,
                emblem = record.emblem, primary = record.primary, secondary = record.secondary,
                fleet = record.fleet, exhibits = record.exhibits });
        }

        public static void Remove(ProfileBook book, string key)
        {
            if (!string.IsNullOrEmpty(key)) book?.companies?.RemoveAll(r => r != null && r.owner == key);
        }
    }
}
