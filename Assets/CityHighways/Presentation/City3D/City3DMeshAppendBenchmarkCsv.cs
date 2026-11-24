using System;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>Portable, invariant-culture rows for manually measured append timings.</summary>
    public static class City3DMeshAppendBenchmarkCsv
    {
        static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        public static string Export(DateTime capturedUtc, string unityVersion,
            int sourceQuads, int appendsPerRun, string[] labels, double[][] runs)
        {
            if (sourceQuads <= 0) throw new ArgumentOutOfRangeException(nameof(sourceQuads));
            if (appendsPerRun <= 0) throw new ArgumentOutOfRangeException(nameof(appendsPerRun));
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (runs == null) throw new ArgumentNullException(nameof(runs));
            if (labels.Length == 0 || labels.Length != runs.Length)
                throw new ArgumentException("A nonempty run series is required per label.");
            var result = new StringBuilder();
            result.AppendLine("captured_utc,unity_version,scenario,run,elapsed_ms,source_quads,appends_per_run");
            for (int i = 0; i < labels.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(labels[i]) || runs[i] == null ||
                    runs[i].Length == 0)
                    throw new ArgumentException("Scenario labels and samples must be nonempty.");
                for (int j = 0; j < runs[i].Length; j++)
                {
                    double elapsed = runs[i][j];
                    if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0d)
                        throw new ArgumentException("Timing samples must be finite and nonnegative.");
                    result.Append(Quote(capturedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))).Append(',')
                        .Append(Quote(unityVersion)).Append(',')
                        .Append(Quote(labels[i])).Append(',')
                        .Append((j + 1).ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(elapsed.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(sourceQuads.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(appendsPerRun.ToString(CultureInfo.InvariantCulture)).AppendLine();
                }
            }
            return result.ToString();
        }
    }
}
