using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Validates and compares two raw MeshBuilder.Append CSV exports.
    /// This is an advisory synthetic CPU benchmark, not a gameplay performance gate.
    /// </summary>
    public static class City3DMeshAppendBenchmarkComparison
    {
        const string Header =
            "captured_utc,unity_version,scenario,run,elapsed_ms,source_quads,appends_per_run";
        const int MaximumCsvCharacters = 1024 * 1024;
        const int MaximumDataRows = 2000;

        public struct ScenarioResult
        {
            public string Name;
            public int Runs;
            public double BaselineMedianMs;
            public double CurrentMedianMs;
            public double BaselineMinMs;
            public double BaselineMaxMs;
            public double CurrentMinMs;
            public double CurrentMaxMs;
            public double ChangePercent;
        }

        public sealed class Report
        {
            public string UnityVersion;
            public int SourceQuads;
            public int AppendsPerRun;
            public ScenarioResult[] Scenarios;
        }

        sealed class Dataset
        {
            public string UnityVersion;
            public int SourceQuads;
            public int AppendsPerRun;
            public readonly List<string> Names = new List<string>();
            public readonly Dictionary<string, List<double>> Samples =
                new Dictionary<string, List<double>>(StringComparer.Ordinal);
        }

        public static bool TryCompare(string baselineCsv, string currentCsv,
            out Report report, out string reason)
        {
            report = null;
            if (!TryRead(baselineCsv, out var baseline, out reason))
            {
                reason = "Baseline: " + reason;
                return false;
            }
            if (!TryRead(currentCsv, out var current, out reason))
            {
                reason = "Current: " + reason;
                return false;
            }
            if (baseline.UnityVersion != current.UnityVersion ||
                baseline.SourceQuads != current.SourceQuads ||
                baseline.AppendsPerRun != current.AppendsPerRun)
            {
                reason = "Unity version or mesh append workload differs between exports.";
                return false;
            }
            if (baseline.Names.Count != current.Names.Count)
            {
                reason = "Scenario counts differ between exports.";
                return false;
            }

            var results = new ScenarioResult[baseline.Names.Count];
            for (int i = 0; i < baseline.Names.Count; i++)
            {
                string name = baseline.Names[i];
                if (!current.Samples.TryGetValue(name, out var newSamples))
                {
                    reason = "Missing scenario in current export: " + name;
                    return false;
                }
                var oldSamples = baseline.Samples[name];
                if (oldSamples.Count != newSamples.Count)
                {
                    reason = "Different measured run counts for scenario: " + name;
                    return false;
                }
                double before = Median(oldSamples);
                double after = Median(newSamples);
                if (before <= 0d)
                {
                    reason = "Cannot calculate a percentage from a zero baseline median: " + name;
                    return false;
                }
                double change = 100d * (after / before - 1d);
                if (double.IsNaN(change) || double.IsInfinity(change))
                {
                    reason = "Percentage change is not representable for scenario: " + name;
                    return false;
                }
                results[i] = new ScenarioResult
                {
                    Name = name,
                    Runs = oldSamples.Count,
                    BaselineMedianMs = before,
                    CurrentMedianMs = after,
                    BaselineMinMs = Min(oldSamples),
                    BaselineMaxMs = Max(oldSamples),
                    CurrentMinMs = Min(newSamples),
                    CurrentMaxMs = Max(newSamples),
                    ChangePercent = change
                };
            }

            report = new Report
            {
                UnityVersion = baseline.UnityVersion,
                SourceQuads = baseline.SourceQuads,
                AppendsPerRun = baseline.AppendsPerRun,
                Scenarios = results
            };
            reason = null;
            return true;
        }

        static double Min(List<double> samples)
        {
            double min = double.PositiveInfinity;
            foreach (double value in samples) if (value < min) min = value;
            return min;
        }

        static double Max(List<double> samples)
        {
            double max = double.NegativeInfinity;
            foreach (double value in samples) if (value > max) max = value;
            return max;
        }

        static double Median(List<double> samples)
        {
            var sorted = samples.ToArray();
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return (sorted.Length & 1) == 1
                ? sorted[middle]
                : (sorted[middle - 1] / 2d + sorted[middle] / 2d);
        }

        static bool TryRead(string csv, out Dataset data, out string reason)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(csv) || csv.Length > MaximumCsvCharacters)
            {
                reason = "CSV is empty or larger than the supported 1 MiB limit.";
                return false;
            }
            if (!TryParseRows(csv, out var rows, out reason)) return false;
            if (rows.Count < 2 || rows.Count > MaximumDataRows + 1 ||
                rows[0].Length != 7 ||
                string.Join(",", rows[0]) != Header)
            {
                reason = "Missing expected CSV header or unsupported row count.";
                return false;
            }

            var result = new Dataset();
            var nextRun = new Dictionary<string, int>(StringComparer.Ordinal);
            string capture = null;
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Length != 7 ||
                    string.IsNullOrWhiteSpace(row[1]) ||
                    string.IsNullOrWhiteSpace(row[2]) ||
                    !DateTimeOffset.TryParse(row[0], CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var capturedUtc) ||
                    capturedUtc.Offset != TimeSpan.Zero ||
                    !int.TryParse(row[3], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var run) ||
                    !double.TryParse(row[4], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var ms) ||
                    double.IsNaN(ms) || double.IsInfinity(ms) || ms < 0d ||
                    !int.TryParse(row[5], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var quads) || quads <= 0 ||
                    !int.TryParse(row[6], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var appends) || appends <= 0)
                {
                    reason = "Invalid measurement data at CSV row " + (i + 1) + ".";
                    return false;
                }
                if (i == 1)
                {
                    capture = row[0];
                    result.UnityVersion = row[1];
                    result.SourceQuads = quads;
                    result.AppendsPerRun = appends;
                }
                else if (row[0] != capture || row[1] != result.UnityVersion ||
                    quads != result.SourceQuads || appends != result.AppendsPerRun)
                {
                    reason = "Inconsistent capture metadata at CSV row " + (i + 1) + ".";
                    return false;
                }
                string name = row[2];
                if (!result.Samples.TryGetValue(name, out var samples))
                {
                    if (result.Names.Count >= 32)
                    {
                        reason = "Too many benchmark scenarios.";
                        return false;
                    }
                    samples = new List<double>();
                    result.Samples.Add(name, samples);
                    result.Names.Add(name);
                    nextRun.Add(name, 1);
                }
                if (run != nextRun[name] || samples.Count >= 200)
                {
                    reason = "Duplicate, missing or excessive run number for " + name + ".";
                    return false;
                }
                nextRun[name] = run + 1;
                samples.Add(ms);
            }
            data = result;
            reason = null;
            return true;
        }

        // RFC 4180-style field escaping, including commas, quotes and line breaks.
        // Explicitly reject misplaced quotes and text after a closing quote.
        static bool TryParseRows(string csv, out List<string[]> rows, out string reason)
        {
            rows = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            bool closedQuote = false;
            int start = csv.Length > 0 && csv[0] == '\uFEFF' ? 1 : 0;
            for (int i = start; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                            closedQuote = true;
                        }
                    }
                    else field.Append(ch);
                    continue;
                }
                if (ch == '"')
                {
                    if (field.Length != 0 || closedQuote)
                    {
                        reason = "Unexpected quote in CSV field.";
                        return false;
                    }
                    quoted = true;
                }
                else if (ch == ',' || ch == '\n' || ch == '\r')
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                    closedQuote = false;
                    if (ch != ',')
                    {
                        rows.Add(fields.ToArray());
                        fields.Clear();
                        if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                            i++;
                    }
                }
                else
                {
                    if (closedQuote)
                    {
                        reason = "Unexpected text after closing CSV quote.";
                        return false;
                    }
                    field.Append(ch);
                }
            }
            if (quoted)
            {
                reason = "Unterminated CSV quoted field.";
                return false;
            }
            if (fields.Count > 0 || field.Length > 0 || closedQuote)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }
            reason = null;
            return true;
        }
    }
}
