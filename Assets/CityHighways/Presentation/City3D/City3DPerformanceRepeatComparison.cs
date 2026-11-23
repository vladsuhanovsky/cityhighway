using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Advisory comparison of two separate repeated-capture journals.
    /// Never presents an incompatible/undersampled/noisy series as a performance PASS.
    /// </summary>
    public static class City3DPerformanceRepeatComparison
    {
        public enum Verdict
        {
            Invalid,
            DifferentScenario,
            DifferentEnvironment,
            InsufficientRepeats,
            UnstableRuns,
            Regression,
            Improvement,
            WithinThreshold
        }

        public struct Report
        {
            public Verdict Status;
            public string Explanation;
            public int BaselineRuns;
            public int CurrentRuns;
            public float BaselineMedianP95Ms;
            public float CurrentMedianP95Ms;
            public float P95ChangePercent;
            public float BaselineRangePercent;
            public float CurrentRangePercent;
            public bool HasComparableDelta;
        }

        public static Report Compare(City3DPerformanceRepeatSeries baseline,
            City3DPerformanceRepeatSeries current, float allowedRegressionPercent = 10f)
        {
            var result = new Report();
            if (baseline == null || current == null ||
                !baseline.IsValid(out _) || !current.IsValid(out _) ||
                float.IsNaN(allowedRegressionPercent) ||
                float.IsInfinity(allowedRegressionPercent) ||
                allowedRegressionPercent < 0f)
                return Set(result, Verdict.Invalid,
                    "Two valid repeat journals and a finite nonnegative threshold are required.");

            var before = City3DPerformanceRepeatAnalysis.Evaluate(baseline);
            var after = City3DPerformanceRepeatAnalysis.Evaluate(current);
            result.BaselineRuns = before.Count;
            result.CurrentRuns = after.Count;
            result.BaselineMedianP95Ms = before.MedianP95Ms;
            result.CurrentMedianP95Ms = after.MedianP95Ms;
            result.BaselineRangePercent = before.RangePercent;
            result.CurrentRangePercent = after.RangePercent;

            if (!string.Equals(baseline.city, current.city, StringComparison.Ordinal) ||
                baseline.quality != current.quality ||
                !string.Equals(baseline.scenario, current.scenario, StringComparison.Ordinal))
                return Set(result, Verdict.DifferentScenario,
                    "City, quality or exact manually entered scenario differs.");
            if (!string.Equals(baseline.environmentSignature,
                current.environmentSignature, StringComparison.Ordinal))
                return Set(result, Verdict.DifferentEnvironment,
                    "Recorded Unity/platform/GPU/viewport/VSync/FPS-cap environment differs.");
            if (!before.HasEnoughRepeats || !after.HasEnoughRepeats)
                return Set(result, Verdict.InsufficientRepeats,
                    "At least three valid separate runs are needed in each journal.");
            if (before.HasVariabilityWarning || after.HasVariabilityWarning)
                return Set(result, Verdict.UnstableRuns,
                    "One or both journals have a P95 run range above 10% of the median. " +
                    "Repeat the measurements before comparing revisions.");

            result.HasComparableDelta = true;
            result.P95ChangePercent = (float)(100d *
                ((double)after.MedianP95Ms / before.MedianP95Ms - 1d));
            // Ignore sub-micropercentage rounding noise at the exact threshold.
            const float boundaryEpsilon = 0.0001f;
            if ((double)result.P95ChangePercent >
                (double)allowedRegressionPercent + boundaryEpsilon)
                return Set(result, Verdict.Regression,
                    "Median P95 increase exceeds the allowed regression threshold.");
            if ((double)result.P95ChangePercent <
                -(double)allowedRegressionPercent - boundaryEpsilon)
                return Set(result, Verdict.Improvement,
                    "Median P95 decreases by more than the selected threshold.");
            return Set(result, Verdict.WithinThreshold,
                "Median P95 difference is within the selected threshold.");
        }

        static Report Set(Report report, Verdict verdict, string explanation)
        {
            report.Status = verdict;
            report.Explanation = explanation;
            return report;
        }
    }
}
