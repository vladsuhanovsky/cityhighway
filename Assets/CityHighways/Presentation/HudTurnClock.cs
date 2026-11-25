using UnityEngine;

namespace CityHighways.Presentation
{
    public enum HudRefreshKind { None, ClockOnly, Full }

    /// <summary>Display policy only. Does not advance turns or decide whether a player may act.</summary>
    public static class HudTurnClock
    {
        // A game state, language, focus or notification change always takes
        // precedence over a pending timer-only repaint.
        public static HudRefreshKind SelectRefresh(bool full, bool clock) =>
            full ? HudRefreshKind.Full : clock ? HudRefreshKind.ClockOnly : HudRefreshKind.None;

        public static int Seconds(float remaining) => Mathf.Max(0, Mathf.CeilToInt(remaining));
        public static bool IsUrgent(bool visible, float remaining) => visible && remaining <= 10f;
        public static bool NeedsRefresh(float before, float after) =>
            Seconds(before) != Seconds(after) || IsUrgent(true, before) != IsUrgent(true, after);

        public static string Text(float remaining, bool english)
        {
            int seconds = Seconds(remaining);
            return english ? $"  ·  remaining {seconds / 60}:{seconds % 60:00}" :
                $"  ·  осталось {seconds / 60}:{seconds % 60:00}";
        }
    }
}
