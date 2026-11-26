using System;
using System.Collections.Generic;

namespace CityHighways.Presentation
{
    /// <summary>Cosmetic-only snapshot shared by preview and future city avatars.
    /// Contains catalog IDs, never paths, purchased flags, profile references or game RNG.</summary>
    public sealed class ProfileAppearance
    {
        public const int MaxSerializedLength = 4096;
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
                if (item != null && outfit.HiddenBy(slot) == null)
                    visible.Add(slot, item.id);
            }
        }

        public static ProfileAppearance FromOutfit(Outfit outfit) =>
            new ProfileAppearance(outfit ?? Outfit.Default);

        public static ProfileAppearance FromSerialized(string outfit) =>
            new ProfileAppearance(Outfit.Parse(outfit != null && outfit.Length <= MaxSerializedLength ? outfit : ""));

        public static ProfileAppearance FromLook(PlayerLook look) => FromSerialized(look.Outfit);

        public string VisibleId(string slot) =>
            slot != null && visible.TryGetValue(slot, out var id) ? id : "";

        public Outfit ToOutfit() => Outfit.Parse(Serialized);
    }
}
