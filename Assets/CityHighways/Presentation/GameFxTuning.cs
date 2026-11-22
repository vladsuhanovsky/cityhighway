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

        // Resource cards respond only when the player can actually draw that card.
        public static bool CanHighlightFaceUp(bool humanTurn, bool drawingPhase,
            bool hasCard, bool secondDrawMachinery) =>
            humanTurn && drawingPhase && hasCard && !secondDrawMachinery;

        public static float FaceUpHoverOffset(bool canDraw, bool reducedMotion) =>
            canDraw && !reducedMotion ? -5f : 0f;

        public static float CardFlightDuration(bool reducedMotion) =>
            reducedMotion ? 0.04f : 0.5f;

        public static float CardArcHeight(bool reducedMotion) =>
            reducedMotion ? 0f : 50f;

        public static float ButtonHoverLift(bool reducedMotion) =>
            reducedMotion ? 0f : -1.5f;

        public static float ButtonPressScale(bool reducedMotion) =>
            reducedMotion ? 1f : 0.97f;

        public const int MaxAnimatedCoinsPerRoute = 6;

        // Large toll settlements use a single total per owner instead of a screenful
        // of moving coins. Reduced-motion players always get the readable totals.
        public static bool AnimateIndividualCoins(int paidRoadCount, bool reducedMotion) =>
            !reducedMotion && paidRoadCount > 0 && paidRoadCount <= MaxAnimatedCoinsPerRoute;

        public static float CoinScale(float progress)
        {
            float k = Mathf.Clamp01(progress);
            float pop = Mathf.Clamp01(k / 0.25f);
            float fly = Ui.EaseInOutCubic(Mathf.Clamp01((k - 0.25f) / 0.75f));
            return Mathf.Max(0.25f, 0.4f + 0.6f * Ui.EaseOutCubic(pop) - 0.3f * fly);
        }

        // Keep a multi-district contract celebration inside one predictable budget.
        public static int ContractSparksPerDistrict(int districtCount, bool reducedMotion) =>
            reducedMotion ? 0 : Mathf.Clamp(60 / Mathf.Max(1, districtCount), 8, 24);

        // Only the first two jams in a single update receive particles.
        // All jams still enter the journal and remain visible on the map.
        public static int JamSparks(int index, bool reducedMotion) =>
            reducedMotion || index < 0 || index >= 2 ? 0 : 8;

        public static int FireworkCount(int normal, bool reducedMotion) =>
            reducedMotion ? 0 : Mathf.Max(0, normal);

        // Persistent hazard markers are subtle, motion-free when accessibility asks for it.
        public static float SweepOpacity(float position, float progress, bool reducedMotion)
        {
            float distance = progress - position;
            return reducedMotion || distance < 0f || distance > 0.4f ? 0f : Mathf.Clamp01(1f - distance / 0.4f);
        }

        // An overcrowded map prioritizes actual jams and closures over weather.
        public static int RoadWarningPriority(bool jammed, bool repair, bool parade, bool snow) =>
            jammed ? 4 : repair ? 3 : parade ? 2 : snow ? 1 : 0;

        public static float RoadWarningOpacity(float time, bool reducedMotion) =>
            reducedMotion ? 0.82f : 0.72f + 0.18f * Mathf.Sin(time * 2.3f);

        public static float EventGlowStrength(float progress, bool reducedMotion) =>
            reducedMotion ? 0f : Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
    }
}
