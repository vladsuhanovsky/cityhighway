using System;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>Portable, deterministic checklist exports; no Editor dependency.</summary>
    public static class City3DPerformanceChecklistExport
    {
        static string Csv(params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
                cells[i] = "\"" + (cells[i] ?? "").Replace("\"", "\"\"") + "\"";
            return string.Join(",", cells);
        }

        static string Md(string text) =>
            string.IsNullOrEmpty(text) ? "—" :
            text.Replace("\\", "\\\\").Replace("|", "\\|")
                .Replace("\r", " ").Replace("\n", " ")
                .Replace("<", "&lt;").Replace(">", "&gt;");

        public static string State(City3DPerformanceSuiteReadiness.Item item) =>
            item.IsExtra ? "Extra" : !item.Present ? "Missing" :
            item.EnvironmentCompatible ? "Ready" : "Incompatible";

        public static string ToCsv(City3DPerformanceSuiteReadiness.Report report)
        {
            if (!report.HasBaseline || report.Items == null)
                throw new ArgumentException("A valid baseline checklist is required.");
            var output = new StringBuilder();
            output.AppendLine(Csv("City", "Quality", "Scenario", "State",
                "Present", "EnvironmentCompatible", "Reason"));
            foreach (var row in report.Items)
                output.AppendLine(Csv(row.City,
                    row.Quality.ToString(CultureInfo.InvariantCulture),
                    row.Scenario, State(row), row.Present ? "yes" : "no",
                    row.EnvironmentCompatible ? "yes" : "no", row.Reason));
            return output.ToString();
        }

        public static string ToMarkdown(City3DPerformanceSuiteReadiness.Report report)
        {
            if (!report.HasBaseline || report.Items == null)
                throw new ArgumentException("A valid baseline checklist is required.");
            var output = new StringBuilder();
            output.AppendLine("## City Highways — 3D benchmark capture checklist");
            output.AppendLine();
            output.AppendLine("**Ready:** " + report.Ready + "/" + report.Expected +
                "  |  **Missing:** " + report.Missing +
                "  |  **Incompatible:** " + report.Incompatible +
                "  |  **Extra:** " + report.Extra);
            output.AppendLine("**Unverified baseline labels:** " + report.UnverifiedLabels +
                "  |  **Collection complete:** " + (report.Complete ? "Yes" : "No"));
            if (report.CurrentSuiteInvalid)
                output.AppendLine("**Warning:** CURRENT suite is invalid; " +
                    "some rows are shown as missing until it is corrected.");
            output.AppendLine();
            output.AppendLine("| City | Quality | Scenario | State | Reason |");
            output.AppendLine("| --- | ---: | --- | --- | --- |");
            foreach (var row in report.Items)
                output.AppendLine("| " + Md(row.City) + " | " +
                    row.Quality.ToString(CultureInfo.InvariantCulture) +
                    " | " + Md(row.Scenario) + " | " + State(row) +
                    " | " + Md(row.Reason) + " |");
            output.AppendLine();
            output.AppendLine("> Readiness checks recorded environments and manual labels. " +
                "It does not prove camera, random seed, workload, frame-rate quality " +
                "or a performance PASS.");
            return output.ToString();
        }
    }
}
