using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Advisory comparison of frame-time captures under explicitly matched conditions.
    /// Results cannot substitute for a hardware-controlled GPU/CPU benchmark.
    /// </summary>
    public static class City3DPerformanceRegression
    {
        public enum Verdict
        {
            InsufficientSamples,
            DifferentCity,
            DifferentQuality,
            DifferentScenario,
            InvalidBaseline,
            Pass,
            Regression
        }

        public struct Result
        {
            public Verdict Status;
            public float AverageDeltaPercent;
            public float P95DeltaPercent;
            public float P99DeltaPercent;
        }

        public static Result Compare(City3DFrameMonitor.Snapshot baseline,
            string baselineCity, int baselineQuality, string baselineScenario,
            City3DFrameMonitor.Snapshot current,
            string currentCity, int currentQuality, string currentScenario,
            float allowedP95RegressionPercent, int minimumSamples = 60)
        {
            var result = new Result();
            if (baseline.Frames < minimumSamples || current.Frames < minimumSamples)
            {
                result.Status = Verdict.InsufficientSamples;
                return result;
            }
            if (!string.Equals(baselineCity, currentCity, StringComparison.Ordinal))
            {
                result.Status = Verdict.DifferentCity;
                return result;
            }
            if (baselineQuality != currentQuality)
            {
                result.Status = Verdict.DifferentQuality;
                return result;
            }
            if (string.IsNullOrWhiteSpace(baselineScenario) ||
                !string.Equals(baselineScenario.Trim(), currentScenario?.Trim(),
                    StringComparison.Ordinal))
            {
                result.Status = Verdict.DifferentScenario;
                return result;
            }
            if (!PositiveFinite(baseline.AverageMs) || !PositiveFinite(baseline.P95Ms) ||
                !PositiveFinite(baseline.P99Ms) ||
                !PositiveFinite(current.AverageMs) || !PositiveFinite(current.P95Ms) ||
                !PositiveFinite(current.P99Ms))
            {
                result.Status = Verdict.InvalidBaseline;
                return result;
            }
            result.AverageDeltaPercent = Delta(baseline.AverageMs, current.AverageMs);
            result.P95DeltaPercent = Delta(baseline.P95Ms, current.P95Ms);
            result.P99DeltaPercent = Delta(baseline.P99Ms, current.P99Ms);
            // Avoid a false regression at an exact boundary due to float rounding.
            float threshold = float.IsNaN(allowedP95RegressionPercent) ||
                float.IsInfinity(allowedP95RegressionPercent)
                ? 0f : Math.Max(0f, allowedP95RegressionPercent);
            result.Status = result.P95DeltaPercent > threshold + 0.0001f
                ? Verdict.Regression : Verdict.Pass;
            return result;
        }

        static bool PositiveFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        static float Delta(float before, float after) => (after / before - 1f) * 100f;
    }
}
