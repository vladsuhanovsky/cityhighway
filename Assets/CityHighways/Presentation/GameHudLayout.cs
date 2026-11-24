using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Widths are measured in UI Toolkit panel units, not physical pixels:
    /// Unity PanelSettings.scale can reduce the effective 1280px Deck width.
    /// Pure layout policy shared by Classic and Logistics Magnate.
    /// </summary>
    public static class GameHudLayout
    {
        public enum Tier { Narrow, Deck, Compact, Wide }

        public static Tier ForWidth(float width)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
                return Tier.Narrow;
            if (width < 760f) return Tier.Narrow;
            if (width < 1120f) return Tier.Deck;
            if (width < 1540f) return Tier.Compact;
            return Tier.Wide;
        }

        public static bool ShowClassicLogo(Tier tier) => tier == Tier.Wide;

        /// <summary>
        /// Compact Deck mode preserves the event icon and its tooltip but
        /// hides the long title in the permanently crowded top bar.
        /// </summary>
        public static bool ShowClassicEventText(Tier tier) =>
            tier == Tier.Wide || tier == Tier.Compact;

        public static bool ShowMagnateTitle(Tier tier) =>
            tier == Tier.Wide || tier == Tier.Compact;

        public static float HorizontalPadding(Tier tier)
        {
            switch (tier)
            {
                case Tier.Wide: return 16f;
                case Tier.Compact: return 12f;
                case Tier.Deck: return 10f;
                default: return 6f;
            }
        }

        public static float ActionGap(Tier tier)
        {
            switch (tier)
            {
                case Tier.Wide: return 6f;
                case Tier.Compact: return 5f;
                case Tier.Deck: return 4f;
                default: return 2f;
            }
        }

        public static float ClassicTurnMaxWidth(Tier tier)
        {
            switch (tier)
            {
                case Tier.Wide: return 370f;
                case Tier.Compact: return 300f;
                case Tier.Deck: return 210f;
                default: return 110f;
            }
        }

        public static float MagnateRoundMaxWidth(Tier tier)
        {
            switch (tier)
            {
                case Tier.Wide: return 340f;
                case Tier.Compact: return 260f;
                case Tier.Deck: return 160f;
                default: return 120f;
            }
        }

        public static float MagnateEventMaxWidth(Tier tier)
        {
            switch (tier)
            {
                case Tier.Wide: return 300f;
                case Tier.Compact: return 170f;
                case Tier.Deck: return 110f;
                default: return 80f;
            }
        }

        public static float GuideMaxWidth(float panelWidth)
        {
            if (float.IsNaN(panelWidth) || float.IsInfinity(panelWidth) ||
                panelWidth <= 0f) return 580f;
            return Math.Min(580f, Math.Max(120f, panelWidth - 32f));
        }
    }
}
