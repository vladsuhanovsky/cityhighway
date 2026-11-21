using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure presentation timing for gameplay effects; no impact on game rules.</summary>
    public static class GameFxTuning
    {
        public const float ToastTop = 70f;
        public const float ToastTravel = 14f;

        // The notification holds its text for most of its lifetime, fading only at the ends.
        public static float ToastOpacity(float progress)
        {
            float k = Mathf.Clamp01(progress);
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.12f));
            float leave = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - k) / 0.2f));
            return Mathf.Min(enter, leave);
        }

        public static float ToastTopAt(float progress, bool reducedMotion)
        {
            if (reducedMotion) return ToastTop;
            float enter = Mathf.Clamp01(progress / 0.12f);
            return ToastTop - ToastTravel * (1f - Mathf.SmoothStep(0f, 1f, enter));
        }

        public static int FireworkCount(int normal, bool reducedMotion) =>
            reducedMotion ? 0 : Mathf.Max(0, normal);

        public static float EventGlowStrength(float progress, bool reducedMotion) =>
            reducedMotion ? 0f : Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
    }
}
