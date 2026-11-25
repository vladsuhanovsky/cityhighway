using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Fixed-time departure choreography for the main-menu highway.
    /// Independent of screen resolution and the game's Unity update loop.
    /// </summary>
    public static class MenuRoadDeparture
    {
        public const float DurationSeconds = 0.85f;

        public static float Progress(float elapsed) =>
            Mathf.Clamp01(elapsed / DurationSeconds);

        /// <summary>Travel along the near lane, then turn onto the visible exit ramp.</summary>
        public static Vector2 Position(float progress, float width, float height)
        {
            float t = Mathf.Clamp01(progress);
            float roadTop = height * 0.82f;
            float lane = roadTop + (height - roadTop) * 0.72f;
            if (t <= 0.55f)
                return new Vector2(Mathf.Lerp(width * 0.61f, width * 0.74f, t / 0.55f), lane);
            float turn = (t - 0.55f) / 0.45f;
            return new Vector2(Mathf.Lerp(width * 0.74f, width * 0.82f, turn),
                Mathf.Lerp(lane, roadTop - 55f, turn));
        }
    }
}
