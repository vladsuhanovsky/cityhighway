using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Compare archived audit outcomes without pretending report deltas are raw
    /// frame benchmarks. P95 movement requires the same declared baseline revision.
    /// </summary>
    public static class City3DArchivedReviewComparison
    {
        public enum Change { Unchanged, StatusChanged, Removed, Added }

        public struct Row
        {
            public string City;
            public int Quality;
            public string Scenario;
            public string PreviousStatus;
            public string LatestStatus;
            public Change Kind;
            public bool HasP95Movement;
            public float PreviousP95Ms;
            public float LatestP95Ms;
            public float P95MovementPercent;
            public string P95ComparisonReason;
        }

        public struct Report
        {
            public Row[] Rows;
            public int Matched;
            public int Changed;
            public int Added;
            public int Removed;
            public bool ComparableP95;
            public int ComparableRows;
            public int BlockedRows;
            public string Warning;
        }

        static string Key(string city, int quality, string scenario) =>
            city + "\u001f" + quality + "\u001f" + scenario;

        public static Report Compare(City3DPerformanceReviewArtifact previous,
            City3DPerformanceReviewArtifact latest)
        {
            if (previous == null || latest == null ||
                !previous.IsValid(out _) || !latest.IsValid(out _))
                throw new ArgumentException("Two valid archived reviews are required.");
            bool sameBaseline = !string.IsNullOrWhiteSpace(previous.baselineRevision) &&
                string.Equals(previous.baselineRevision, latest.baselineRevision,
                    StringComparison.Ordinal);
            bool sameAllowance = Math.Abs(previous.allowedP95RegressionPercent -
                latest.allowedP95RegressionPercent) < 0.0001f;
            var report = new Report
            {
                ComparableP95 = sameBaseline && sameAllowance,
                Warning = sameBaseline && sameAllowance
                    ? "P95 movement requires matching recorded current environments for each " +
                      "scenario; baseline labels and thresholds are user-entered, not verified."
                    : "P95 movement hidden: archived reports have different or unknown " +
                      "baseline revision labels or different regression thresholds."
            };
            var before = new Dictionary<string, City3DPerformanceReviewArtifact.Scenario>(
                StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in previous.scenarios)
                before.Add(Key(item.city, item.quality, item.scenario), item);

            var rows = new List<Row>();
            foreach (var now in latest.scenarios)
            {
                string key = Key(now.city, now.quality, now.scenario);
                seen.Add(key);
                var row = new Row
                {
                    City = now.city, Quality = now.quality, Scenario = now.scenario,
                    LatestStatus = now.status
                };
                if (!before.TryGetValue(key, out var old))
                {
                    row.Kind = Change.Added;
                    report.Added++;
                }
                else
                {
                    report.Matched++;
                    row.PreviousStatus = old.status;
                    row.Kind = string.Equals(old.status, now.status, StringComparison.Ordinal)
                        ? Change.Unchanged : Change.StatusChanged;
                    if (row.Kind == Change.StatusChanged) report.Changed++;
                    if (!report.ComparableP95)
                        row.P95ComparisonReason = "Baseline labels or thresholds differ.";
                    else if (!old.hasComparableDelta || !now.hasComparableDelta)
                        row.P95ComparisonReason = "At least one report lacks a comparable P95.";
                    else if (!old.hasCurrentEnvironment || !now.hasCurrentEnvironment ||
                        string.IsNullOrWhiteSpace(old.currentEnvironmentSignature) ||
                        string.IsNullOrWhiteSpace(now.currentEnvironmentSignature))
                        row.P95ComparisonReason = "Archived capture environment is unknown.";
                    else if (!string.Equals(old.currentEnvironmentSignature,
                        now.currentEnvironmentSignature, StringComparison.Ordinal))
                        row.P95ComparisonReason = "Archived capture environments differ.";
                    else
                    {
                        row.HasP95Movement = true;
                        row.PreviousP95Ms = old.currentP95Ms;
                        row.LatestP95Ms = now.currentP95Ms;
                        row.P95MovementPercent =
                            (now.currentP95Ms / old.currentP95Ms - 1f) * 100f;
                        row.P95ComparisonReason = "Recorded capture environments match.";
                    }
                    if (row.HasP95Movement) report.ComparableRows++;
                    else report.BlockedRows++;
                }
                rows.Add(row);
            }
            foreach (var old in previous.scenarios)
            {
                if (seen.Contains(Key(old.city, old.quality, old.scenario))) continue;
                rows.Add(new Row
                {
                    City = old.city, Quality = old.quality, Scenario = old.scenario,
                    PreviousStatus = old.status, Kind = Change.Removed
                });
                report.Removed++;
            }
            rows.Sort((a, b) =>
            {
                int priority = Priority(a.Kind).CompareTo(Priority(b.Kind));
                if (priority != 0) return priority;
                int city = string.CompareOrdinal(a.City, b.City);
                if (city != 0) return city;
                int quality = a.Quality.CompareTo(b.Quality);
                return quality != 0 ? quality :
                    string.CompareOrdinal(a.Scenario, b.Scenario);
            });
            report.Rows = rows.ToArray();
            return report;
        }

        static int Priority(Change kind)
        {
            switch (kind)
            {
                case Change.StatusChanged: return 0;
                case Change.Added: return 1;
                case Change.Removed: return 2;
                default: return 3;
            }
        }

        static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            return value.Replace("\\", "\\\\").Replace("|", "\\|")
                .Replace("\r", " ").Replace("\n", " ")
                .Replace("<", "&lt;").Replace(">", "&gt;");
        }

        public static string ToMarkdown(City3DPerformanceReviewArtifact previous,
            City3DPerformanceReviewArtifact latest)
        {
            var report = Compare(previous, latest);
            var text = new StringBuilder();
            text.AppendLine("## City Highways — archived 3D audit comparison");
            text.AppendLine();
            text.AppendLine("**Previous:** " + Cell(previous.currentRevision) +
                "  |  **Latest:** " + Cell(latest.currentRevision));
            text.AppendLine("**Baseline labels:** " + Cell(previous.baselineRevision) +
                " → " + Cell(latest.baselineRevision));
            text.AppendLine("**Thresholds:** " +
                previous.allowedP95RegressionPercent.ToString("F2", CultureInfo.InvariantCulture) +
                "% → " + latest.allowedP95RegressionPercent.ToString("F2",
                    CultureInfo.InvariantCulture) + "%");
            text.AppendLine("**Audit verdicts:** " + Cell(previous.overallStatus) +
                " → " + Cell(latest.overallStatus));
            text.AppendLine("**Matched:** " + report.Matched +
                "  |  **Status changed:** " + report.Changed +
                "  |  **Added:** " + report.Added +
                "  |  **Removed:** " + report.Removed);
            text.AppendLine("**P95 comparable rows:** " + report.ComparableRows +
                "  |  **P95 blocked rows:** " + report.BlockedRows);
            text.AppendLine();
            text.AppendLine("| City | Quality | Scenario | Change | Previous | Latest | Current P95 movement | Reason |");
            text.AppendLine("| --- | --- | --- | --- | --- | ---: | --- |");
            foreach (var row in report.Rows)
                text.AppendLine("| " + Cell(row.City) + " | " + row.Quality +
                    " | " + Cell(row.Scenario) + " | " + row.Kind +
                    " | " + Cell(row.PreviousStatus) + " | " +
                    Cell(row.LatestStatus) + " | " +
                    (row.HasP95Movement ? row.P95MovementPercent.ToString(
                        "+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%" : "—") +
                    " | " + Cell(row.P95ComparisonReason) + " |");
            text.AppendLine();
            text.AppendLine("> " + report.Warning);
            text.AppendLine("> Reports contain manual revision and seed labels. " +
                "This is an audit comparison, not a new hardware performance measurement.");
            return text.ToString();
        }
    }
}
