using System;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    public static class City3DPerformanceRepeatExport
    {
        static string Csv(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                values[i] = "\"" + (values[i] ?? "").Replace("\"", "\"\"") + "\"";
            return string.Join(",", values);
        }

        static string Md(string value) =>
            string.IsNullOrEmpty(value) ? "—" : value.Replace("\\", "\\\\")
                .Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ")
                .Replace("<", "&lt;").Replace(">", "&gt;");

        public static string ToMarkdown(City3DPerformanceRepeatSeries series)
        {
            if (series == null || !series.IsValid(out _))
                throw new ArgumentException("Valid repeat journal required.");
            var analysis = City3DPerformanceRepeatAnalysis.Evaluate(series);
            var culture = CultureInfo.InvariantCulture;
            var output = new StringBuilder();
            output.AppendLine("## City Highways — repeated 3D performance captures");
            output.AppendLine();
            output.AppendLine("**Scenario:** " + Md(series.city) + " [" + series.quality +
                "] " + Md(series.scenario));
            output.AppendLine("**Observed environment:** " + Md(series.environmentSignature));
            output.AppendLine("**Runs:** " + analysis.Count +
                " | **Median P95:** " + analysis.MedianP95Ms.ToString("F3", culture) +
                " ms | **Range:** " + analysis.RangePercent.ToString("F2", culture) +
                "% | **Median absolute deviation:** " +
                analysis.MedianAbsoluteDeviationMs.ToString("F3", culture) + " ms");
            output.AppendLine("**Advisory:** " + analysis.Explanation);
            output.AppendLine();
            output.AppendLine("| Run | Captured UTC | Frames | P95 (ms) |");
            output.AppendLine("| ---: | --- | ---: | ---: |");
            for (int i = 0; i < series.repeats.Count; i++)
            {
                var capture = series.repeats[i];
                output.AppendLine("| " + (i + 1) + " | " +
                    Md(capture.capturedUtc) + " | " + capture.snapshot.Frames +
                    " | " + capture.snapshot.P95Ms.ToString("F3", culture) + " |");
            }
            output.AppendLine();
            output.AppendLine("> Repeats share manually declared scenario labels and " +
                "recorded environment fields. They do not prove identical seeds, " +
                "camera, workload, hardware load, or GPU timing.");
            return output.ToString();
        }

        public static string ToCsv(City3DPerformanceRepeatSeries series)
        {
            if (series == null || !series.IsValid(out _))
                throw new ArgumentException("Valid repeat journal required.");
            var output = new StringBuilder();
            output.AppendLine(Csv("Run", "City", "Quality", "Scenario",
                "CapturedUtc", "Frames", "P95Ms", "Environment"));
            var culture = CultureInfo.InvariantCulture;
            for (int i = 0; i < series.repeats.Count; i++)
            {
                var capture = series.repeats[i];
                output.AppendLine(Csv((i + 1).ToString(culture), capture.city,
                    capture.quality.ToString(culture), capture.scenario,
                    capture.capturedUtc, capture.snapshot.Frames.ToString(culture),
                    capture.snapshot.P95Ms.ToString("F3", culture),
                    series.environmentSignature));
            }
            return output.ToString();
        }
    }
}
