using System;

namespace CityHighways.Presentation
{
    /// <summary>Inter-run P95 spread; does not pool individual frame samples.</summary>
    public static class City3DPerformanceRepeatAnalysis
    {
        public const int RecommendedRepeats = 3;
        public struct Report
        {
            public int Count;
            public bool HasEnoughRepeats;
            public bool HasVariabilityWarning;
            public float MedianP95Ms;
            public float MinP95Ms;
            public float MaxP95Ms;
            public float RangePercent;
            public float MedianAbsoluteDeviationMs;
            public string Explanation;
        }

        public static Report Evaluate(City3DPerformanceRepeatSeries series)
        {
            if (series == null || !series.IsValid(out _))
                return new Report { Explanation = "Add at least one valid repeat." };
            int count = series.repeats.Count;
            var values = new float[count];
            for (int i = 0; i < count; i++)
                values[i] = series.repeats[i].snapshot.P95Ms;
            Array.Sort(values);
            float median = Median(values);
            var deviations = new float[count];
            for (int i = 0; i < count; i++)
                deviations[i] = Math.Abs(values[i] - median);
            Array.Sort(deviations);
            float spread = (float)(100d * ((double)values[count - 1] - values[0]) /
                median);
            var result = new Report
            {
                Count = count,
                HasEnoughRepeats = count >= RecommendedRepeats,
                MedianP95Ms = median,
                MinP95Ms = values[0],
                MaxP95Ms = values[count - 1],
                RangePercent = spread,
                MedianAbsoluteDeviationMs = Median(deviations)
            };
            // Advisory, configurable workflow guidance, not a statistical CI.
            result.HasVariabilityWarning = result.HasEnoughRepeats && spread > 10f;
            result.Explanation = !result.HasEnoughRepeats
                ? "At least three separate captures are recommended before " +
                  "assessing run-to-run reproducibility."
                : result.HasVariabilityWarning
                    ? "Run-to-run P95 range exceeds 10% of its median. " +
                      "Check workload and repeat; this is not an automatic rejection."
                    : "Run-to-run P95 range is within the advisory 10% threshold.";
            return result;
        }

        static float Median(float[] ordered)
        {
            int middle = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (float)(((double)ordered[middle - 1] + ordered[middle]) / 2d)
                : ordered[middle];
        }
    }
}
