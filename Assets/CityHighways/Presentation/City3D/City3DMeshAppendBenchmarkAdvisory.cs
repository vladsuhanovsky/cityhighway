using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Conservative, descriptive heuristics for noisy synthetic Editor CPU timings.
    /// A clean advisory is NOT a statistical significance test.
    /// </summary>
    public static class City3DMeshAppendBenchmarkAdvisory
    {
        public const int RecommendedRuns = 7;
        public const double RangeWarningPercent = 10d;

        public struct Assessment
        {
            public int Runs;
            public bool HasEnoughRuns;
            public bool RangesOverlap;
            public double BaselineRangePercent;
            public double CurrentRangePercent;
            public bool HasLargeWithinCaptureRange;
            public bool NeedsReview;
            public string Explanation;
        }

        public static Assessment Evaluate(
            City3DMeshAppendBenchmarkComparison.ScenarioResult scenario)
        {
            if (scenario.Runs <= 0 ||
                !ValidRange(scenario.BaselineMinMs, scenario.BaselineMedianMs,
                    scenario.BaselineMaxMs) ||
                !ValidRange(scenario.CurrentMinMs, scenario.CurrentMedianMs,
                    scenario.CurrentMaxMs))
                throw new ArgumentException("Scenario has invalid timing ranges.",
                    nameof(scenario));

            double beforeSpread = RangePercent(scenario.BaselineMinMs,
                scenario.BaselineMedianMs, scenario.BaselineMaxMs);
            double afterSpread = RangePercent(scenario.CurrentMinMs,
                scenario.CurrentMedianMs, scenario.CurrentMaxMs);
            bool enough = scenario.Runs >= RecommendedRuns;
            // Endpoints count as overlapping: a single tied sample is not
            // evidence of disjoint observed ranges.
            bool overlap = scenario.BaselineMinMs <= scenario.CurrentMaxMs &&
                scenario.CurrentMinMs <= scenario.BaselineMaxMs;
            bool wide = beforeSpread > RangeWarningPercent ||
                afterSpread > RangeWarningPercent;

            return new Assessment
            {
                Runs = scenario.Runs,
                HasEnoughRuns = enough,
                RangesOverlap = overlap,
                BaselineRangePercent = beforeSpread,
                CurrentRangePercent = afterSpread,
                HasLargeWithinCaptureRange = wide,
                NeedsReview = !enough || overlap || wide,
                Explanation = (enough
                    ? "Run count meets the recommended minimum. "
                    : "Fewer than seven measured runs per capture. ") +
                    (wide
                        ? "Within-capture min/max range exceeds 10% of the median. "
                        : "No large within-capture range under the 10% advisory threshold. ") +
                    (overlap
                        ? "Observed baseline/current ranges overlap. "
                        : "Observed baseline/current ranges do not overlap. ") +
                    "Neither non-overlapping ranges nor a clean advisory prove an " +
                    "end-to-end FPS gain or statistical significance."
            };
        }

        static bool ValidRange(double min, double median, double max)
        {
            return !double.IsNaN(min) && !double.IsNaN(median) &&
                !double.IsNaN(max) && !double.IsInfinity(min) &&
                !double.IsInfinity(median) && !double.IsInfinity(max) &&
                min >= 0d && min <= median && median <= max;
        }

        static double RangePercent(double min, double median, double max)
        {
            if (median == 0d)
                return min == max ? 0d : double.PositiveInfinity;
            return 100d * ((max - min) / median);
        }
    }
}
