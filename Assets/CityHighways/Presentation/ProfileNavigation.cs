namespace CityHighways.Presentation
{
    public enum ProfileSection { Overview, Wardrobe, Collection, Achievements, Mastery, Wallet, Style }

    public static class ProfileNavigation
    {
        public static ProfileSection Primary(ProfileSection section)
        {
            switch (section)
            {
                case ProfileSection.Wallet:
                case ProfileSection.Style:
                    return ProfileSection.Overview;
                default:
                    return section;
            }
        }
    }
}
