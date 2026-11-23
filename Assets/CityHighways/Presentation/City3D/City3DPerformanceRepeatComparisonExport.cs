using System;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>GitHub-ready review text for two repeat journals.</summary>
    public static class City3DPerformanceRepeatComparisonExport
    {
        static string Cell(string text) =>
            string.IsNullOrEmpty(text) ? "—" :
            text.Replace("\\", "\\\\").Replace("|", "\\|")
                .Replace("\r", " ").Replace("\n", " ")
                .Replace("<", "&lt;").Replace(">", "&gt;");

        public static string ToMarkdown(City3DPerformanceRepeatSeries baseline,
            City3DPerformanceRepeatSeries current, float threshold)
        {
            var report = City3DPerformanceRepeatComparison.Compare(
                baseline, current, threshold);
            if (report.Status == City3DPerformanceRepeatComparison.Verdict.Invalid)
                throw new ArgumentException("Valid repeat journals and threshold are required.");
            var culture = CultureInfo.InvariantCulture;
            var output = new StringBuilder();
            output.AppendLine("## City Highways — repeat series comparison");
            output.AppendLine();
            output.AppendLine("**Baseline scenario:** " + Cell(baseline.city) + " [" +
                baseline.quality + "] " + Cell(baseline.scenario));
            output.AppendLine("**Current scenario:** " + Cell(current.city) + " [" +
                current.quality + "] " + Cell(current.scenario));
            output.AppendLine("**Baseline environment:** " +
                Cell(baseline.environmentSignature));
            output.AppendLine("**Current environment:** " +
                Cell(current.environmentSignature));
            output.AppendLine("**Allowed regression:** " +
                threshold.ToString("F2", culture) + "%");
            output.AppendLine("**Verdict:** " + report.Status);
            output.AppendLine("**Reason:** " + Cell(report.Explanation));
            output.AppendLine();
            output.AppendLine("| Metric | Baseline runs | Current runs |");
            output.AppendLine("| --- | ---: | ---: |");
            output.AppendLine("| Repeat count | " + report.BaselineRuns +
                " | " + report.CurrentRuns + " |");
            output.AppendLine("| Median P95 (ms) | " +
                report.BaselineMedianP95Ms.ToString("F3", culture) + " | " +
                report.CurrentMedianP95Ms.ToString("F3", culture) + " |");
            output.AppendLine("| P95 range / median (%) | " +
                report.BaselineRangePercent.ToString("F2", culture) + " | " +
                report.CurrentRangePercent.ToString("F2", culture) + " |");
            output.AppendLine();
            output.AppendLine("**Median P95 change:** " +
                (report.HasComparableDelta
                    ? report.P95ChangePercent.ToString("+0.00;-0.00;0.00",
                        culture) + "%"
                    : "Not comparable"));
            output.AppendLine();
            output.AppendLine("> This is an advisory comparison of observed Unity Editor " +
                "frame intervals, not an authenticated hardware benchmark. " +
                "Matching scenario labels do not prove equal random seeds, camera " +
                "positions or scene workload.");
            return output.ToString();
        }

        public static string ToCsv(City3DPerformanceRepeatSeries baseline,
            City3DPerformanceRepeatSeries current, float threshold)
        {
            var report = City3DPerformanceRepeatComparison.Compare(
                baseline, current, threshold);
            if (report.Status == City3DPerformanceRepeatComparison.Verdict.Invalid)
                throw new ArgumentException("Valid repeat journals and threshold are required.");
            var c = CultureInfo.InvariantCulture;
            var output = new StringBuilder();
            output.AppendLine("Side,City,Quality,Scenario,Environment,Runs,MedianP95Ms,RangePercent,Verdict,MedianP95ChangePercent");
            Write(output, "Baseline", baseline, report.BaselineRuns,
                report.BaselineMedianP95Ms, report.BaselineRangePercent,
                report.Status, report.HasComparableDelta ? report.P95ChangePercent : (float?)null, c);
            Write(output, "Current", current, report.CurrentRuns,
                report.CurrentMedianP95Ms, report.CurrentRangePercent,
                report.Status, report.HasComparableDelta ? report.P95ChangePercent : (float?)null, c);
            return output.ToString();
        }

        static string Quote(string value) =>
            "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        static void Write(StringBuilder output, string side,
            City3DPerformanceRepeatSeries series, int runs, float median,
            float range, City3DPerformanceRepeatComparison.Verdict status,
            float? delta, CultureInfo c)
        {
            output.AppendLine(string.Join(",", new[]
            {
                Quote(side), Quote(series.city), Quote(series.quality.ToString(c)),
                Quote(series.scenario), Quote(series.environmentSignature),
                Quote(runs.ToString(c)), Quote(median.ToString("F3", c)),
                Quote(range.ToString("F2", c)), Quote(status.ToString()),
                Quote(delta.HasValue ? delta.Value.ToString("F2", c) : "")
            }));
        }
    }
}
