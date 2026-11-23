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
            public string reason;
            public float baselineP95Ms;
            public float currentP95Ms;
            public float p95DeltaPercent;
            public bool hasBaseline;
            public bool hasCurrent;
            public bool hasComparableDelta;
            // Optional provenance in schema v1. Older review exports omit both fields.
            public bool hasCurrentEnvironment;
            public string currentEnvironmentSignature;
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
                City3DPerformanceBaseline nowCapture = null;
                if (hasCurrent)
                    foreach (var candidate in current.entries)
                        if (candidate.city == row.City && candidate.quality == row.Quality &&
                            candidate.scenario == row.Scenario)
                        {
                            nowCapture = candidate;
                            break;
                        }
                bool recorded = nowCapture != null &&
                    nowCapture.environmentRecorded &&
                    nowCapture.environment != null &&
                    nowCapture.environment.IsValid();
                artifact.scenarios[i] = new Scenario
                {
                    city = row.City,
                    quality = row.Quality,
                    scenario = row.Scenario,
                    status = row.Status.ToString(),
                    reason = row.Reason,
                    baselineP95Ms = row.BaselineP95Ms,
                    currentP95Ms = row.CurrentP95Ms,
                    p95DeltaPercent = row.DeltaPercent,
                    hasBaseline = hasBaseline,
                    hasCurrent = hasCurrent,
                    hasComparableDelta = hasBaseline && hasCurrent &&
                        row.Status != City3DPerformanceDiagnostics.Kind.Invalid,
                    hasCurrentEnvironment = recorded,
                    currentEnvironmentSignature = recorded
                        ? nowCapture.environment.Describe() : null
                };
            }
            return artifact;
        }

        [Serializable]
        sealed class VersionHeader { public int version; }

        /// <summary>
        /// Validate saved review contents rather than trusting a displayed PASS.
        /// This does not prove the original benchmark captures or user-entered revisions.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (version != CurrentVersion || scenarios == null || scenarios.Length == 0)
            {
                reason = "Missing or unsupported review artifact version/scenarios.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(generatedUtc) ||
                string.IsNullOrWhiteSpace(overallStatus) ||
                float.IsNaN(allowedP95RegressionPercent) ||
                float.IsInfinity(allowedP95RegressionPercent) ||
                allowedP95RegressionPercent < 0f ||
                float.IsNaN(coveragePercent) || float.IsInfinity(coveragePercent) ||
                coveragePercent < 0f || coveragePercent > 100f ||
                matched < 0 || passed < 0 || regressions < 0 || invalid < 0 ||
                missing < 0 || additional < 0 || legacyMatches < 0)
            {
                reason = "Review metadata or counts are invalid.";
                return false;
            }
            int countedPass = 0, countedRegression = 0, countedInvalid = 0;
            int countedMissing = 0, countedAdditional = 0;
            var unique = new System.Collections.Generic.HashSet<string>(
                StringComparer.Ordinal);
            foreach (var item in scenarios)
            {
                if (string.IsNullOrWhiteSpace(item.city) ||
                    string.IsNullOrWhiteSpace(item.scenario) ||
                    item.quality < 0 || item.quality > 2)
                {
                    reason = "Review contains a scenario with invalid identity.";
                    return false;
                }
                string key = item.city + "\u001f" + item.quality + "\u001f" + item.scenario;
                if (!unique.Add(key))
                {
                    reason = "Review contains duplicate scenarios.";
                    return false;
                }
                if (item.hasBaseline && (!FinitePositive(item.baselineP95Ms)))
                {
                    reason = "Review contains invalid baseline frame time.";
                    return false;
                }
                if (item.hasCurrent && (!FinitePositive(item.currentP95Ms)))
                {
                    reason = "Review contains invalid current frame time.";
                    return false;
                }
                if (item.hasCurrentEnvironment &&
                    (!item.hasCurrent ||
                     string.IsNullOrWhiteSpace(item.currentEnvironmentSignature)))
                {
                    reason = "Review current environment provenance is inconsistent.";
                    return false;
                }
                if (!item.hasCurrentEnvironment &&
                    !string.IsNullOrEmpty(item.currentEnvironmentSignature))
                {
                    reason = "Review environment signature lacks its presence flag.";
                    return false;
                }
                switch (item.status)
                {
                    case "Pass":
                    case "Regression":
                        if (!item.hasBaseline || !item.hasCurrent ||
                            !item.hasComparableDelta ||
                            float.IsNaN(item.p95DeltaPercent) ||
                            float.IsInfinity(item.p95DeltaPercent) ||
                            Math.Abs(item.p95DeltaPercent -
                                (item.currentP95Ms / item.baselineP95Ms - 1f) *
                                100f) > 0.05f)
                        {
                            reason = "Review contains an inconsistent comparable result.";
                            return false;
                        }
                        if (item.status == "Pass") countedPass++;
                        else countedRegression++;
                        break;
                    case "Invalid":
                        if (!item.hasBaseline || !item.hasCurrent ||
                            item.hasComparableDelta)
                        {
                            reason = "Invalid review case must not claim a comparable delta.";
                            return false;
                        }
                        countedInvalid++;
                        break;
                    case "Missing":
                        if (!item.hasBaseline || item.hasCurrent || item.hasComparableDelta)
                        {
                            reason = "Missing review case has invalid presence flags.";
                            return false;
                        }
                        countedMissing++;
                        break;
                    case "Additional":
                        if (item.hasBaseline || !item.hasCurrent || item.hasComparableDelta)
                        {
                            reason = "Additional review case has invalid presence flags.";
                            return false;
                        }
                        countedAdditional++;
                        break;
                    default:
                        reason = "Unknown review scenario status.";
                        return false;
                }
            }
            int actualMatched = countedPass + countedRegression + countedInvalid;
            int baselineTotal = actualMatched + countedMissing;
            if (actualMatched != matched || countedPass != passed ||
                countedRegression != regressions || countedInvalid != invalid ||
                countedMissing != missing || countedAdditional != additional ||
                baselineTotal <= 0 ||
                Math.Abs(coveragePercent - 100f * actualMatched / baselineTotal) >
                    0.11f || legacyMatches > countedPass + countedRegression)
            {
                reason = "Review totals or coverage do not match its scenario rows.";
                return false;
            }
            string expectedStatus = matched == 0 || missing > 0 ||
                additional > 0 || invalid > 0 ? "Incomplete" :
                legacyMatches > 0 ? "UnverifiedLegacy" : "Pass";
            if (regressions > 0) expectedStatus = "Regression";
            if (!string.Equals(overallStatus, expectedStatus, StringComparison.Ordinal))
            {
                reason = "Overall review status does not agree with scenario rows.";
                return false;
            }
            reason = null;
            return true;
        }

        static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        public static bool TryFromJson(string json,
            out City3DPerformanceReviewArtifact artifact, out string reason)
        {
            artifact = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "Review JSON is empty.";
                return false;
            }
            try
            {
                // Use an independent header so missing version cannot be defaulted.
                var header = JsonUtility.FromJson<VersionHeader>(json);
                if (header == null || header.version != CurrentVersion)
                {
                    reason = "Missing or unsupported review format version.";
                    return false;
                }
                var parsed = JsonUtility.FromJson<City3DPerformanceReviewArtifact>(json);
                if (parsed == null)
                {
                    reason = "Review JSON has no object.";
                    return false;
                }
                if (!parsed.IsValid(out reason)) return false;
                artifact = parsed;
                return true;
            }
            catch (Exception e)
            {
                reason = "Invalid review JSON: " + e.Message;
                return false;
            }
        }

        public string ToJson()
        {
            if (!IsValid(out var reason))
                throw new InvalidOperationException(reason);
            return JsonUtility.ToJson(this, true);
        }
    }
}
