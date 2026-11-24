using System;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Portable Markdown summary of manually labelled synthetic CPU captures.
    /// Does not claim to verify the named revisions or test environment.
    /// </summary>
    public static class City3DMeshAppendBenchmarkReview
    {
        public static string ToMarkdown(
            City3DMeshAppendBenchmarkComparison.Report report,
            string baselineRevision, string currentRevision, string environment,
            string baselineFilename, string currentFilename)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.Scenarios == null || report.Scenarios.Length == 0 ||
                string.IsNullOrWhiteSpace(report.UnityVersion) ||
                report.SourceQuads <= 0 || report.AppendsPerRun <= 0)
                throw new ArgumentException("Comparison report has missing fields.", nameof(report));
            Require(baselineRevision, nameof(baselineRevision));
            Require(currentRevision, nameof(currentRevision));
            Require(environment, nameof(environment));
            Require(baselineFilename, nameof(baselineFilename));
            Require(currentFilename, nameof(currentFilename));

            var markdown = new StringBuilder();
            markdown.AppendLine("# MeshBuilder.Append CPU benchmark comparison");
            markdown.AppendLine();
            markdown.AppendLine("Synthetic Unity Editor wall-clock benchmark (not FPS or GPU time).");
            markdown.AppendLine();
            markdown.AppendLine("## Experiment provenance (manually entered; not verified)");
            markdown.AppendLine();
            Field(markdown, "Baseline revision", baselineRevision);
            Field(markdown, "Current revision", currentRevision);
            Field(markdown, "Environment and settings", environment);
            Field(markdown, "Baseline CSV file", baselineFilename);
            Field(markdown, "Current CSV file", currentFilename);
            Field(markdown, "Unity version (from CSV)", report.UnityVersion);
            markdown.AppendLine("- Source quads: " +
                report.SourceQuads.ToString(CultureInfo.InvariantCulture));
            markdown.AppendLine("- Appends per run: " +
                report.AppendsPerRun.ToString(CultureInfo.InvariantCulture));
            markdown.AppendLine();
            markdown.AppendLine("## Measured scenario medians and ranges");
            markdown.AppendLine();
            markdown.AppendLine("| Scenario | Runs per capture | Baseline median (ms) | Current median (ms) | Elapsed-time change | Baseline range (ms) | Current range (ms) |");
            markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var scenario in report.Scenarios)
            {
                if (string.IsNullOrWhiteSpace(scenario.Name) || scenario.Runs <= 0 ||
                    !ValidRange(scenario.BaselineMinMs, scenario.BaselineMedianMs,
                        scenario.BaselineMaxMs) ||
                    !ValidRange(scenario.CurrentMinMs, scenario.CurrentMedianMs,
                        scenario.CurrentMaxMs) ||
                    double.IsNaN(scenario.ChangePercent) ||
                    double.IsInfinity(scenario.ChangePercent))
                    throw new ArgumentException("Comparison report has invalid timing samples.",
                        nameof(report));
                markdown.Append("| ").Append(Escape(scenario.Name)).Append(" | ")
                    .Append(scenario.Runs.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(Milliseconds(scenario.BaselineMedianMs)).Append(" | ")
                    .Append(Milliseconds(scenario.CurrentMedianMs)).Append(" | ")
                    .Append(scenario.ChangePercent > 0d ? "+" : "")
                    .Append(scenario.ChangePercent.ToString("F2", CultureInfo.InvariantCulture))
                    .Append("% | ")
                    .Append(Milliseconds(scenario.BaselineMinMs)).Append("–")
                    .Append(Milliseconds(scenario.BaselineMaxMs)).Append(" | ")
                    .Append(Milliseconds(scenario.CurrentMinMs)).Append("–")
                    .Append(Milliseconds(scenario.CurrentMaxMs)).AppendLine(" |");
            }
            markdown.AppendLine();
            markdown.AppendLine("A negative percentage means shorter measured CPU elapsed time. " +
                "Differences are advisory, not statistical confidence intervals. " +
                "The CSV workload metadata is checked for compatibility, " +
                "but hardware, revisions, and Editor conditions are not verified. " +
                "Repeat captures and confirm in the Unity Profiler on representative 3D scenes.");
            return markdown.ToString();
        }

        static bool ValidRange(double min, double median, double max)
        {
            return !double.IsNaN(min) && !double.IsNaN(median) && !double.IsNaN(max) &&
                !double.IsInfinity(min) && !double.IsInfinity(median) &&
                !double.IsInfinity(max) && min >= 0d && min <= median && median <= max;
        }

        static void Require(string input, string param)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("A provenance label is required.", param);
        }

        static string Milliseconds(double value) =>
            value.ToString("F6", CultureInfo.InvariantCulture);

        static void Field(StringBuilder output, string label, string value)
        {
            output.Append("- ").Append(label).Append(": ")
                .AppendLine(Escape(value));
        }

        static string Escape(string value)
        {
            return value.Trim().Replace("\\", "\\\\").Replace("|", "\\|")
                .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }
    }
}
