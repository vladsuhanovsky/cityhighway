using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Stable visual company identity. Seat numbers never depend on rank or turn order.</summary>
    public static class PlayerIdentity
    {
        const int Seats = 5;
        static readonly Vector2[][] Pips =
        {
            new[] { Vector2.zero },
            new[] { new Vector2(-3f, -3f), new Vector2(3f, 3f) },
            new[] { new Vector2(-3f, -3f), Vector2.zero, new Vector2(3f, 3f) },
            new[] { new Vector2(-3f, -3f), new Vector2(3f, -3f), new Vector2(-3f, 3f), new Vector2(3f, 3f) },
            new[] { new Vector2(-3f, -3f), new Vector2(3f, -3f), Vector2.zero, new Vector2(-3f, 3f), new Vector2(3f, 3f) }
        };

        public static bool MarkersEnabled => DisplaySettings.Active?.showOwnerMarkers == true;
        public static int PipCount(int index) => index >= 0 && index < Seats ? index + 1 : 0;
        public static bool HasRoadMarker(int owner, int playerCount) =>
            owner < playerCount && PipCount(owner) > 0;
        public static Vector2 PipOffset(int owner, int pip) =>
            pip >= 0 && pip < PipCount(owner) ? Pips[owner][pip] : Vector2.zero;
        public static string SeatLabel(int index) => PipCount(index) > 0 ? "[" + (index + 1) + "]" : "";
        public static string Label(int index, string name) => Label(index, name, MarkersEnabled);
        public static string Label(int index, string name, bool enabled) =>
            enabled && PipCount(index) > 0 ? SeatLabel(index) + " " + name : name;
    }
}
