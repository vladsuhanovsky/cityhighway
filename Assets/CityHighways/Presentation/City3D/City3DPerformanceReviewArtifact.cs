using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Portable, read-only review artifact. Source captures remain in their suite JSON files.
    /// Revision labels are entered by the tester and are not verified automatically.
    /// </summary>
    [Serializable]
    public sealed class City3DPerformanceReviewArtifact
    {
        [Serializable]
        public struct Scenario
        {
            public string city;
            public int quality;
            public string scenario;
            public string status;
            public float baselineP95Ms;
            public float currentP95Ms;
            public float p95DeltaPercent;
            public bool hasBaseline;
            public bool hasCurrent;
            public bool hasComparableDelta;
        }

        public const int CurrentVersion = 1;
        public int version;
        public string generatedUtc;
        public string baselineRevision;
        public string currentRevision;
        public string overallStatus;
        public float allowedP95RegressionPercent;
        public int matched;
        public int passed;
        public int regressions;
        public int invalid;
        public int missing;
        public int additional;
        public int legacyMatches;
        public float coveragePercent;
        public Scenario[] scenarios;

        public static City3DPerformanceReviewArtifact Build(
            City3DPerformanceSuite baseline, City3DPerformanceSuite current,
            float allowedRegression, string baselineLabel, string currentLabel,
            string utc = null)
        {
            if (baseline == null || current == null ||
                !baseline.IsValid(out _) || !current.IsValid(out _))
                throw new InvalidOperationException(
                    "Two valid performance scenario suites are required.");
            if (float.IsNaN(allowedRegression) || float.IsInfinity(allowedRegression) ||
                allowedRegression < 0f)
                throw new ArgumentOutOfRangeException(nameof(allowedRegression));
            var report = City3DPerformanceDiagnostics.Build(
                baseline, current, allowedRegression);
            var status = City3DPerformanceSuiteVerdict.Evaluate(true, true, report);
            var artifact = new City3DPerformanceReviewArtifact
            {
                version = CurrentVersion,
                generatedUtc = utc ?? DateTime.UtcNow.ToString("o",
                    System.Globalization.CultureInfo.InvariantCulture),
                baselineRevision = baselineLabel ?? "",
                currentRevision = currentLabel ?? "",
                overallStatus = status.ToString(),
                allowedP95RegressionPercent = allowedRegression,
                matched = report.Matched,
                passed = report.Passed,
                regressions = report.Regressions,
                invalid = report.Invalid,
                missing = report.Missing,
                additional = report.Additional,
                legacyMatches = report.LegacyMatches,
                coveragePercent = report.CoveragePercent,
                scenarios = new Scenario[report.Rows.Length]
            };
            for (int i = 0; i < report.Rows.Length; i++)
            {
                var row = report.Rows[i];
                bool hasBaseline = row.Status != City3DPerformanceDiagnostics.Kind.Additional;
                bool hasCurrent = row.Status != City3DPerformanceDiagnostics.Kind.Missing;
                artifact.scenarios[i] = new Scenario
                {
                    city = row.City,
                    quality = row.Quality,
                    scenario = row.Scenario,
                    status = row.Status.ToString(),
                    baselineP95Ms = row.BaselineP95Ms,
                    currentP95Ms = row.CurrentP95Ms,
                    p95DeltaPercent = row.DeltaPercent,
                    hasBaseline = hasBaseline,
                    hasCurrent = hasCurrent,
                    hasComparableDelta = hasBaseline && hasCurrent &&
                        row.Status != City3DPerformanceDiagnostics.Kind.Invalid
                };
            }
            return artifact;
        }

        public string ToJson()
        {
            if (version != CurrentVersion || scenarios == null || scenarios.Length == 0)
                throw new InvalidOperationException("Review artifact is incomplete.");
            return JsonUtility.ToJson(this, true);
        }
    }
}
