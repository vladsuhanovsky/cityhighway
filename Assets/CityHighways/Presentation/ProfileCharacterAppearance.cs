using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Presentation
{
    /// <summary>Immutable, catalog-normalized cosmetic snapshot. No profile storage, assets or game RNG.</summary>
    public sealed class ProfileCharacterAppearance
    {
        readonly Outfit outfit;
        public string Serialized { get; }
        public string Gender => outfit.Gender;
        public string Tone => outfit.Tone;
        public string Equipped(string slot) => outfit.Get(slot);
        public string Visible(string slot) => outfit.HiddenBy(slot) == null ? outfit.Get(slot) : "";
        public bool HasHat => !string.IsNullOrEmpty(Visible("hat"));
        public const int MaxOutfitLength = 4096;

        ProfileCharacterAppearance(Outfit normalized)
        {
            outfit = normalized;
            Serialized = normalized.Serialize();
        }

        // A preview or bot look may contain items that the local profile does not own.
        // These are cosmetic only, never authorization to purchase or equip an item.
        public static ProfileCharacterAppearance FromLook(string serialized)
        {
            return new ProfileCharacterAppearance(Outfit.Parse(
                serialized != null && serialized.Length <= MaxOutfitLength ? serialized : ""));
        }

        public static ProfileCharacterAppearance FromProfile(PlayerProfile profile)
        {
            if (profile == null) return FromLook(null);
            var normalized = FromLook(profile.outfit);
            var owned = new HashSet<string>(profile.owned ?? new List<string>(), StringComparer.Ordinal);
            var body = CharacterCatalog.Item(normalized.Equipped("body"));
            bool Allowed(CharacterItem item) => item != null && (item.price <= 0 || owned.Contains(item.id));
            var parts = new List<string>();
            // Preserve the existing body's gender even when an unowned tone needs replacement.
            string bodyId = Allowed(body) ? body.id : CharacterCatalog.DefaultFor("body", normalized.Gender);
            parts.Add("body=" + bodyId);
            foreach (var slot in CharacterCatalog.Slots.Where(s => s != "body"))
            {
                var item = CharacterCatalog.Item(normalized.Equipped(slot));
                if (Allowed(item)) parts.Add(slot + "=" + item.id);
            }
            return FromLook(string.Join(";", parts));
        }
    }
}
