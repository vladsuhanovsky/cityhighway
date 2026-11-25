using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Central editable visual settings for the screens that use UI Toolkit.
    /// This does not change game rules, persistence or gameplay data.
    /// Edit through City Highways > UI > Interface Editor.
    /// </summary>
    [CreateAssetMenu(fileName = "InterfaceTheme",
        menuName = "City Highways/Interface Theme")]
    public sealed class InterfaceTheme : ScriptableObject
    {
        public const string ResourcePath = "CityHighways/UI/InterfaceTheme";
        static InterfaceTheme _cached;
        public static InterfaceTheme Current
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<InterfaceTheme>(ResourcePath);
                return _cached;
            }
        }

        [Header("Global palette (shared by menus, cards and HUD)")]
        public Color background = new Color(0.106f, 0.149f, 0.192f);
        public Color panel = new Color(0.173f, 0.243f, 0.314f);
        public Color panelLight = new Color(0.204f, 0.286f, 0.369f);
        public Color panelHover = new Color(0.243f, 0.345f, 0.443f);
        public Color text = new Color(0.925f, 0.941f, 0.945f);
        public Color textDim = new Color(0.667f, 0.718f, 0.722f);
        public Color accent = new Color(0.906f, 0.298f, 0.235f);
        public Color accentHover = new Color(0.925f, 0.439f, 0.388f);
        public Color gold = new Color(0.95f, 0.69f, 0.26f);
        public Color success = new Color(0.153f, 0.682f, 0.376f);
        [Range(0.7f, 1f)] public float panelGlassOpacity = 0.94f;

        [Header("Shared control styling")]
        [Range(24f, 64f)] public float commonButtonMinHeight = 38f;
        [Range(0f, 20f)] public float commonButtonCornerRadius = 8f;
        [Range(0f, 24f)] public float cardCornerRadius = 10f;
        [Range(0f, 10f)] public float cardSectionSpacing = 8f;

        [Header("New game / configuration")]
        [Range(480f, 900f)] public float newGameHelpMaxWidth = 640f;

        [Header("Online / network lobby")]
        [Range(200f, 540f)] public float onlineNameFieldWidth = 320f;

        [Header("Tutorial and rules")]
        [Range(160f, 390f)] public float rulesNavigationWidth = 250f;
        [Range(350f, 850f)] public float rulesContentHeight = 570f;

        [Header("Settings")]
        [Range(150f, 340f)] public float settingsNavigationWidth = 210f;

        [Header("Statistics")]
        [Range(350f, 850f)] public float statsHistoryHeight = 600f;

        [Header("Profile")]
        [Range(350f, 850f)] public float profileContentHeight = 580f;

        [Header("Game HUD (Classic)")]
        [Range(42f, 110f)] public float hudTopBarHeight = 60f;
        [Range(50f, 120f)] public float hudBottomBarHeight = 72f;
        [Range(250f, 560f)] public float hudDrawerWidth = 380f;
    }
}
