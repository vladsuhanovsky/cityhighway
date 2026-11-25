using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Editable main-menu theme. Select the asset through
    /// City Highways > UI > Edit Main Menu Style in Unity's toolbar.
    /// Values are loaded from Resources at the next menu build.
    /// </summary>
    [CreateAssetMenu(fileName = "MainMenuStyle",
        menuName = "City Highways/Main Menu Style")]
    public sealed class MainMenuStyle : ScriptableObject
    {
        public const string ResourcePath = "CityHighways/UI/MainMenuStyle";

        public static MainMenuStyle Current => Resources.Load<MainMenuStyle>(ResourcePath);

        [Header("1. Main buttons — same width for all six")]
        [Range(320f, 640f)] public float buttonWidth = 480f;
        [Range(40f, 85f)] public float primaryHeight = 58f;
        [Range(30f, 70f)] public float secondaryHeight = 46f;
        [Range(0f, 20f)] public float insidePairGap = 4f;
        [Range(0f, 30f)] public float betweenGroupsGap = 12f;
        [Range(12f, 26f)] public float primaryFontSize = 20f;
        [Range(12f, 24f)] public float secondaryFontSize = 17f;

        [Header("2. Header and news")]
        [Range(42f, 90f)] public float logoFontSize = 72f;
        [Range(12f, 25f)] public float subtitleFontSize = 17f;
        [Range(450f, 1000f)] public float newsWidth = 780f;
        [Range(36f, 90f)] public float newsMinHeight = 48f;
        [Range(0f, 32f)] public float newsBottomGap = 18f;

        [Header("3. Buttons colors")]
        public Color newGameGradientStart = new Color(0.75f, 0.22f, 0.17f);
        public Color newGameGradientEnd = new Color(0.91f, 0.30f, 0.24f);
        public Color secondaryNormal = new Color(0.16f, 0.23f, 0.31f, 0.98f);
        public Color secondaryHover = new Color(0.25f, 0.35f, 0.45f);

        [Header("4. City and road")]
        [Range(0f, 0.8f)] public float centerDarkening = 0.5f;
        [Range(0f, 2f)] public float trafficSpeed = 1f;
    }
}
