using System;
using System.Collections.Generic;

namespace CityHighways.Presentation
{
    /// <summary>Read-only per-scenario report. Missing and additional seeds are never called passes.</summary>
    public static class City3DPerformanceDiagnostics
    {
        public enum Kind { Pass, Regression, Invalid, Missing, Additional }

        public struct Row
        {
            public string City;
            public int Quality;
            public string Scenario;
            public Kind Status;
            public string Reason;
            public float BaselineP95Ms;
            public float CurrentP95Ms;
            public float DeltaPercent;
        }

        public struct Report
        {
            public Row[] Rows;
            public int Matched, Passed, Regressions, Invalid, Missing, Additional;
            public int LegacyMatches;
            public float CoveragePercent;
            public float WorstP95Percent;
            public bool HasRegressions;
            public bool CompleteCoverage;
        }

        static bool Same(City3DPerformanceBaseline a, City3DPerformanceBaseline b) =>
            string.Equals(a.city, b.city, StringComparison.Ordinal) &&
            a.quality == b.quality &&
            string.Equals(a.scenario, b.scenario, StringComparison.Ordinal);

        public static Report Build(City3DPerformanceSuite baseline,
            City3DPerformanceSuite current, float allowedRegression)
        {
            var report = new Report { Rows = new Row[0] };
            if (baseline == null || current == null ||
                !baseline.IsValid(out _) || !current.IsValid(out _)) return report;

            var rows = new List<Row>();
            foreach (var before in baseline.entries)
            {
                City3DPerformanceBaseline after = null;
                foreach (var candidate in current.entries)
                    if (Same(before, candidate)) { after = candidate; break; }
                var row = new Row
                {
                    City = before.city,
                    Quality = before.quality,
                    Scenario = before.scenario,
                    BaselineP95Ms = before.snapshot.P95Ms
                };
                if (after == null)
                {
                    row.Status = Kind.Missing;
                    row.Reason = "No current capture for this city/quality/scenario.";
                    report.Missing++;
                }
                else
                {
                    row.CurrentP95Ms = after.snapshot.P95Ms;
                    report.Matched++;
                    var environmentMatch = City3DPerformanceEnvironment.Compare(
                        before.environment, after.environment);
                    if (environmentMatch == City3DPerformanceEnvironment.Compatibility.Incomplete ||
                        environmentMatch == City3DPerformanceEnvironment.Compatibility.Different)
                    {
                        row.Status = Kind.Invalid;
                        row.Reason = environmentMatch ==
                            City3DPerformanceEnvironment.Compatibility.Incomplete
                            ? "Environment metadata absent on one side."
                            : EnvironmentDifference(before.environment, after.environment);
                        report.Invalid++;
                        rows.Add(row);
                        continue;
                    }
                    if (environmentMatch ==
                        City3DPerformanceEnvironment.Compatibility.BothLegacy)
                        report.LegacyMatches++;
                    var difference = City3DPerformanceRegression.Compare(
                        before.snapshot, before.city, before.quality, before.scenario,
                        after.snapshot, after.city, after.quality, after.scenario,
                        allowedRegression);
                    row.DeltaPercent = difference.P95DeltaPercent;
                    if (difference.Status == City3DPerformanceRegression.Verdict.Regression)
                    {
                        row.Status = Kind.Regression;
                        row.Reason = "P95 increase exceeds allowed threshold.";
                        report.Regressions++;
                        report.WorstP95Percent = Math.Max(report.WorstP95Percent,
                            difference.P95DeltaPercent);
                    }
                    else if (difference.Status == City3DPerformanceRegression.Verdict.Pass)
                    {
                        row.Status = Kind.Pass;
                        row.Reason = "P95 within allowed threshold.";
                        report.Passed++;
                    }
                    else
                    {
                        row.Status = Kind.Invalid;
                        row.Reason = "Frame comparison rejected: " + difference.Status;
                        report.Invalid++;
                    }
                }
                rows.Add(row);
            }
            foreach (var after in current.entries)
            {
                bool seen = false;
                foreach (var before in baseline.entries)
                    if (Same(before, after)) { seen = true; break; }
                if (seen) continue;
                rows.Add(new Row
                {
                    City = after.city,
                    Quality = after.quality,
                    Scenario = after.scenario,
                    CurrentP95Ms = after.snapshot.P95Ms,
                    Status = Kind.Additional,
                    Reason = "No baseline capture for this city/quality/scenario."
                });
                report.Additional++;
            }
            report.CoveragePercent = 100f * report.Matched / baseline.entries.Count;
            report.CompleteCoverage = report.Missing == 0 &&
                report.Invalid == 0 && report.Additional == 0;
            report.HasRegressions = report.Regressions > 0;
            rows.Sort((a, b) =>
            {
                int priority = Priority(a.Status).CompareTo(Priority(b.Status));
                if (priority != 0) return priority;
                int city = string.CompareOrdinal(a.City, b.City);
                if (city != 0) return city;
                int quality = a.Quality.CompareTo(b.Quality);
                return quality != 0 ? quality : string.CompareOrdinal(a.Scenario, b.Scenario);
            });
            report.Rows = rows.ToArray();
            return report;
        }

        static string EnvironmentDifference(City3DPerformanceEnvironment before,
            City3DPerformanceEnvironment after)
        {
            if (before.unityVersion != after.unityVersion) return "Unity version differs.";
            if (before.platform != after.platform) return "Runtime platform differs.";
            if (before.graphicsDevice != after.graphicsDevice) return "Graphics device differs.";
            if (before.width != after.width || before.height != after.height)
                return "Viewport resolution differs.";
            if (before.vSyncCount != after.vSyncCount) return "VSync setting differs.";
            if (before.targetFrameRate != after.targetFrameRate)
                return "Target frame rate differs.";
            return "Capture environments differ.";
        }

        static int Priority(Kind status)
        {
            switch (status)
            {
                case Kind.Regression: return 0;
                case Kind.Invalid: return 1;
                case Kind.Missing: return 2;
                case Kind.Additional: return 3;
                default: return 4;
            }
        }
    }
}
