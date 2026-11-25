using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Runtime facade for MainMenuStyle.asset. A missing asset falls back to
    /// the proven defaults from PR #112, so editor and player builds work.
    /// Inspector changes are applied the next time the menu is constructed.
    /// </summary>
    public static class MenuHomeActionLayout
    {
        public const float DefaultWidth = 480f;
        public const float DefaultPrimaryHeight = 58f;
        public const float DefaultSecondaryHeight = 46f;
        public const float DefaultInsidePairGap = 4f;
        public const float DefaultBetweenGroupsGap = 12f;
        public const float DefaultSecondaryFontSize = 17f;

        public static float WidthFor(MainMenuStyle style) =>
            style != null ? Mathf.Clamp(style.buttonWidth, 320f, 640f) : DefaultWidth;
        public static float PrimaryHeightFor(MainMenuStyle style) =>
            style != null ? Mathf.Clamp(style.primaryHeight, 40f, 85f) : DefaultPrimaryHeight;
        public static float SecondaryHeightFor(MainMenuStyle style) =>
            style != null ? Mathf.Clamp(style.secondaryHeight, 30f, 70f) : DefaultSecondaryHeight;
        public static float InsidePairGapFor(MainMenuStyle style) =>
            style != null ? Mathf.Clamp(style.insidePairGap, 0f, 20f) : DefaultInsidePairGap;
        public static float BetweenGroupsGapFor(MainMenuStyle style) =>
            style != null ? Mathf.Clamp(style.betweenGroupsGap, 0f, 30f) : DefaultBetweenGroupsGap;
        public static float SecondaryFontSizeFor(MainMenuStyle style) =>
            style != null ? Mathf.Clamp(style.secondaryFontSize, 12f, 24f) : DefaultSecondaryFontSize;

        public static float Width => WidthFor(MainMenuStyle.Current);
        public static float PrimaryHeight => PrimaryHeightFor(MainMenuStyle.Current);
        public static float SecondaryHeight => SecondaryHeightFor(MainMenuStyle.Current);
        public static float InsidePairGap => InsidePairGapFor(MainMenuStyle.Current);
        public static float BetweenGroupsGap => BetweenGroupsGapFor(MainMenuStyle.Current);
        public static float SecondaryFontSize => SecondaryFontSizeFor(MainMenuStyle.Current);

        public static float PrimaryFontSize => MainMenuStyle.Current != null
            ? Mathf.Clamp(MainMenuStyle.Current.primaryFontSize, 12f, 26f) : 20f;
        public static float LogoFontSize => MainMenuStyle.Current != null
            ? Mathf.Clamp(MainMenuStyle.Current.logoFontSize, 42f, 90f) : 72f;
        public static float SubtitleFontSize => MainMenuStyle.Current != null
            ? Mathf.Clamp(MainMenuStyle.Current.subtitleFontSize, 12f, 25f) : 17f;
        public static float NewsWidth => MainMenuStyle.Current != null
            ? Mathf.Clamp(MainMenuStyle.Current.newsWidth, 450f, 1000f) : 780f;
        public static float NewsMinHeight => MainMenuStyle.Current != null
            ? Mathf.Clamp(MainMenuStyle.Current.newsMinHeight, 36f, 90f) : 48f;
        public static float NewsBottomGap => MainMenuStyle.Current != null
            ? Mathf.Clamp(MainMenuStyle.Current.newsBottomGap, 0f, 32f) : 18f;

        public static float PairHeight => PrimaryHeight + InsidePairGap + SecondaryHeight;
        public static float ThreePairsHeight => PairHeight * 3f + BetweenGroupsGap * 2f;
    }
}
