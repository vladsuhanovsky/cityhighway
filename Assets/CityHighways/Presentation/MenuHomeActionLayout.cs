namespace CityHighways.Presentation
{
    /// <summary>
    /// Shared dimensions of the three paired actions on the home screen.
    /// They are UI Toolkit logical pixels, independent of the device's
    /// rendering scale. The red New Game button defines the column width.
    /// </summary>
    public static class MenuHomeActionLayout
    {
        public const float Width = 480f;
        public const float PrimaryHeight = 58f;    // ~44 physical px at the current menu scale
        public const float SecondaryHeight = 46f;  // ~35 physical px
        public const float InsidePairGap = 4f;
        public const float BetweenGroupsGap = 12f;
        public const float SecondaryFontSize = 17f;

        public static float PairHeight => PrimaryHeight + InsidePairGap + SecondaryHeight;
        public static float ThreePairsHeight => PairHeight * 3f + BetweenGroupsGap * 2f;
    }
}
