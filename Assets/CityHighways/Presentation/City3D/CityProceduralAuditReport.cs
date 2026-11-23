using System;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>Pure, spreadsheet-safe CSV formatting and audit issue categorization.</summary>
    public static class CityProceduralAuditReport
    {
        public enum Category
        {
            All,
            Configuration,
            Geometry,
            Oversized,
            Offset,
            BelowGround,
            MissingTerrain
        }

        public static bool Matches(Category selection, Category actual) =>
            selection == Category.All || selection == actual;

        /// <summary>
        /// Quotes every CSV cell, doubles interior quotes and protects against spreadsheet
        /// formula execution after import, including whitespace-prefixed formulas.
        /// </summary>
        public static string EscapeCell(string value)
        {
            value = value ?? "";
            var trimmed = value.TrimStart(' ', '\t', '\r', '\n');
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0)
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string CsvRow(params string[] values)
        {
            if (values == null || values.Length == 0) return "";
            var result = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i != 0) result.Append(',');
                result.Append(EscapeCell(values[i]));
            }
            return result.ToString();
        }
    }
}
