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
