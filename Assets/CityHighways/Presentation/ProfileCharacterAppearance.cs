namespace CityHighways.Presentation
{
    /// <summary>Compatibility adapter for the initial Character 3D Lab API.
    /// All normalization and ownership filtering live in ProfileAppearance.</summary>
    public sealed class ProfileCharacterAppearance
    {
        public ProfileAppearance Snapshot { get; }
        public string Serialized => Snapshot.Serialized;
        public string Gender => Snapshot.Gender;
        public string Tone => Snapshot.Tone;
        public string Equipped(string slot) => Snapshot.EquippedId(slot);
        public string Visible(string slot) => Snapshot.VisibleId(slot);
        public bool HasHat => !string.IsNullOrEmpty(Visible("hat"));
        public const int MaxOutfitLength = ProfileAppearance.MaxSerializedLength;

        ProfileCharacterAppearance(ProfileAppearance snapshot) { Snapshot = snapshot; }

        public static ProfileCharacterAppearance FromLook(string serialized) =>
            new ProfileCharacterAppearance(ProfileAppearance.FromSerialized(serialized));

        public static ProfileCharacterAppearance FromProfile(PlayerProfile profile) =>
            new ProfileCharacterAppearance(ProfileAppearance.FromProfile(profile));
    }
}
