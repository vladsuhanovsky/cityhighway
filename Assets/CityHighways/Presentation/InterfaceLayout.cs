using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Shared layout dimensions from InterfaceTheme, kept separate from
    /// gameplay-specific constants. Each property has a safe fallback so
    /// old resources and scenes remain usable.
    /// </summary>
    public static class InterfaceLayout
    {
        static InterfaceTheme Style => InterfaceTheme.Current;
        static float Value(float? value, float fallback, float min, float max) =>
            value.HasValue ? Mathf.Clamp(value.Value, min, max) : fallback;

        public static float CommonButtonHeight => Value(Style != null ? (float?)Style.commonButtonMinHeight : null, 38f, 24f, 64f);
        public static float CommonButtonRadius => Value(Style != null ? (float?)Style.commonButtonCornerRadius : null, 8f, 0f, 20f);
        public static float CardRadius => Value(Style != null ? (float?)Style.cardCornerRadius : null, 10f, 0f, 24f);
        public static float SectionGap => Value(Style != null ? (float?)Style.cardSectionSpacing : null, 8f, 0f, 10f);

        public static float NewGameHelpMaxWidth => Value(Style != null ? (float?)Style.newGameHelpMaxWidth : null, 640f, 480f, 900f);
        public static float OnlineNameFieldWidth => Value(Style != null ? (float?)Style.onlineNameFieldWidth : null, 320f, 200f, 540f);
        public static float RulesNavigationWidth => Value(Style != null ? (float?)Style.rulesNavigationWidth : null, 250f, 160f, 390f);
        public static float RulesContentHeight => Value(Style != null ? (float?)Style.rulesContentHeight : null, 570f, 350f, 850f);
        public static float SettingsNavigationWidth => Value(Style != null ? (float?)Style.settingsNavigationWidth : null, 210f, 150f, 340f);
        public static float StatsHistoryHeight => Value(Style != null ? (float?)Style.statsHistoryHeight : null, 600f, 350f, 850f);
        public static float ProfileContentHeight => Value(Style != null ? (float?)Style.profileContentHeight : null, 664f, 350f, 850f);

        public static float HudTopBarHeight => Value(Style != null ? (float?)Style.hudTopBarHeight : null, 60f, 42f, 110f);
        public static float HudBottomBarHeight => Value(Style != null ? (float?)Style.hudBottomBarHeight : null, 72f, 50f, 120f);
        public static float HudDrawerWidth => Value(Style != null ? (float?)Style.hudDrawerWidth : null, 380f, 250f, 560f);
    }
}
