using System;
using System.Collections.Generic;

namespace CityHighways.Presentation
{
    /// <summary>Cosmetic-only snapshot shared by preview and future city avatars.
    /// Contains catalog IDs, never paths, purchased flags, profile references or game RNG.</summary>
    public sealed class ProfileAppearance
    {
        public const int MaxSerializedLength = 4096;
        readonly Dictionary<string, string> equipped = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> visible = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Serialized { get; }
        public string Gender { get; }
        public string Tone { get; }

        ProfileAppearance(Outfit outfit)
        {
            Serialized = outfit.Serialize();
            Gender = outfit.Gender;
            Tone = outfit.Tone;
            foreach (var slot in CharacterCatalog.Slots)
            {
                var item = CharacterCatalog.Item(outfit.Get(slot));
                if (item == null || item.slot != slot || !item.Fits(Gender)) continue;
                equipped.Add(slot, item.id);
                if (outfit.HiddenBy(slot) == null) visible.Add(slot, item.id);
            }
        }

        public static ProfileAppearance FromOutfit(Outfit outfit) =>
            FromSerialized((outfit ?? Outfit.Default).Serialize());

        public static ProfileAppearance FromSerialized(string outfit) =>
            new ProfileAppearance(Outfit.Parse(outfit != null && outfit.Length <= MaxSerializedLength ? outfit : ""));

        public static ProfileAppearance FromLook(PlayerLook look) => FromSerialized(look.Outfit);

        // Ownership is applied only for a committed local profile, not for try-on or bot looks.
        public static ProfileAppearance FromProfile(PlayerProfile profile)
        {
            if (profile == null) return FromSerialized(null);
            var normalized = FromSerialized(profile.outfit);
            var owned = new HashSet<string>(profile.owned ?? new List<string>(), StringComparer.Ordinal);
            bool Allowed(CharacterItem item) => item != null && (item.price <= 0 || owned.Contains(item.id));
            var body = CharacterCatalog.Item(normalized.EquippedId("body"));
            var parts = new List<string> { "body=" + (Allowed(body) ? body.id : CharacterCatalog.DefaultFor("body", normalized.Gender)) };
            foreach (var slot in CharacterCatalog.Slots)
            {
                if (slot == "body") continue;
                var item = CharacterCatalog.Item(normalized.EquippedId(slot));
                if (Allowed(item)) parts.Add(slot + "=" + item.id);
            }
            return FromSerialized(string.Join(";", parts));
        }

        public string EquippedId(string slot) =>
            slot != null && equipped.TryGetValue(slot, out var id) ? id : "";

        public string VisibleId(string slot) =>
            slot != null && visible.TryGetValue(slot, out var id) ? id : "";

        public Outfit ToOutfit() => Outfit.Parse(Serialized);
    }
}
