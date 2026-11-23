using System;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>Deterministic GitHub-flavored Markdown summary for a manual 3D benchmark review.</summary>
    public static class City3DPerformanceMarkdownReport
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            return value.Replace("\\", "\\\\").Replace("|", "\\|")
                .Replace("\r", " ").Replace("\n", " ")
                .Replace("<", "&lt;").Replace(">", "&gt;");
        }

        static string Time(float value) => value.ToString("F2", Invariant);
        static string Percent(float value) =>
            value.ToString("+0.00;-0.00;0.00", Invariant) + "%";

        public static string Render(City3DPerformanceReviewArtifact artifact)
        {
            if (artifact == null || artifact.version !=
                City3DPerformanceReviewArtifact.CurrentVersion ||
                artifact.scenarios == null || artifact.scenarios.Length == 0)
                throw new ArgumentException("Valid benchmark review data is required.",
                    nameof(artifact));

            var output = new StringBuilder();
            output.AppendLine("## City Highways 3D performance audit");
            output.AppendLine();
            output.AppendLine("**Verdict:** " + Cell(artifact.overallStatus) +
                "  |  **P95 regression allowance:** " +
                artifact.allowedP95RegressionPercent.ToString("F2", Invariant) + "%");
            output.AppendLine("**Baseline revision:** " + Cell(artifact.baselineRevision) +
                "  |  **Current revision:** " + Cell(artifact.currentRevision));
            output.AppendLine("**Generated UTC:** " + Cell(artifact.generatedUtc));
            output.AppendLine();
            output.AppendLine("**Coverage:** " +
                artifact.coveragePercent.ToString("F1", Invariant) + "%  |  " +
                "**Matched:** " + artifact.matched + "  |  **Passed:** " + artifact.passed +
                "  |  **Regressions:** " + artifact.regressions);
            output.AppendLine("**Missing:** " + artifact.missing + "  |  **Additional:** " +
                artifact.additional + "  |  **Invalid:** " + artifact.invalid +
                "  |  **Unverified legacy:** " + artifact.legacyMatches);
            output.AppendLine();
            output.AppendLine("| City | Quality | Scenario | Status | Reason | Baseline P95 (ms) | Current P95 (ms) | P95 change |");
            output.AppendLine("| --- | --- | --- | --- | --- | ---: | ---: | ---: |");
            foreach (var item in artifact.scenarios)
            {
                output.Append("| ").Append(Cell(item.city))
                    .Append(" | ").Append(item.quality == 0 ? "Low" :
                        item.quality == 1 ? "Medium" :
                        item.quality == 2 ? "High" : "Unknown")
                    .Append(" | ").Append(Cell(item.scenario))
                    .Append(" | ").Append(Cell(item.status))
                    .Append(" | ").Append(Cell(item.reason))
                    .Append(" | ").Append(item.hasBaseline ? Time(item.baselineP95Ms) : "—")
                    .Append(" | ").Append(item.hasCurrent ? Time(item.currentP95Ms) : "—")
                    .Append(" | ").Append(item.hasComparableDelta ?
                        Percent(item.p95DeltaPercent) : "—").AppendLine(" |");
            }
            output.AppendLine();
            output.AppendLine("> Advisory manual measurements from Unity frame intervals, not CPU/GPU profiler timings. " +
                "Scenario seeds, camera and workload are manually supplied; revision labels are not Git-verified.");
            return output.ToString();
        }
    }
}
