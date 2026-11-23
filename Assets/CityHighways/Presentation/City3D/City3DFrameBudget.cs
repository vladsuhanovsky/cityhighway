using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure advisory thresholds for rolling 3D frame-interval snapshots.</summary>
    public static class City3DFrameBudget
    {
        public enum Result { InsufficientSamples, Disabled, Pass, OverBudget }

        /// <summary>
        /// The P95 wall-clock frame duration should not exceed 1000 / target FPS.
        /// Minimum 60 observations protects against interpreting a tiny snapshot.
        /// </summary>
        public static Result Evaluate(City3DFrameMonitor.Snapshot snapshot, int targetFps,
            int minimumFrames = 60)
        {
            if (snapshot.Frames < minimumFrames || snapshot.Frames <= 0 ||
                float.IsNaN(snapshot.P95Ms) || float.IsInfinity(snapshot.P95Ms) ||
                snapshot.P95Ms <= 0f)
                return Result.InsufficientSamples;
            if (targetFps <= 0) return Result.Disabled;
            return snapshot.P95Ms <= 1000f / targetFps
                ? Result.Pass : Result.OverBudget;
        }

        public static float LimitMs(int targetFps) =>
            targetFps > 0 ? 1000f / targetFps : 0f;

        public static int TargetForQuality(int quality, int low, int medium, int high) =>
            quality <= 0 ? Mathf.Max(0, low) :
            quality == 1 ? Mathf.Max(0, medium) : Mathf.Max(0, high);
    }
}
