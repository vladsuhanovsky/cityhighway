using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Editor-facing, manually collected benchmark suite for distinct city/seed scenarios.</summary>
    [Serializable]
    public sealed class City3DPerformanceSuite
    {
        public const int CurrentVersion = 1;
        public int version;
        public City3DPerformanceSuite() { version = CurrentVersion; }
        public List<City3DPerformanceBaseline> entries = new List<City3DPerformanceBaseline>();

        public bool TryAdd(City3DPerformanceBaseline capture, out string reason)
        {
            if (capture == null)
            {
                reason = "Capture is null.";
                return false;
            }
            if (!capture.IsValid(out reason)) return false;
            if (entries == null) entries = new List<City3DPerformanceBaseline>();
            for (int i = 0; i < entries.Count; i++)
                if (SameKey(entries[i], capture))
                {
                    reason = "Scenario already exists: " + capture.scenario +
                        ". Clear or create a separate suite for a fresh run.";
                    return false;
                }
            entries.Add(capture);
            reason = null;
            return true;
        }

        /// <summary>Explicitly replace a single matching capture after user confirmation.</summary>
        public bool TryReplace(City3DPerformanceBaseline capture, out string reason)
        {
            if (capture == null)
            {
                reason = "Capture is null.";
                return false;
            }
            if (!capture.IsValid(out reason)) return false;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                    if (SameKey(entries[i], capture))
                    {
                        entries[i] = capture;
                        reason = null;
                        return true;
                    }
            reason = "Scenario not present in suite.";
            return false;
        }

        /// <summary>Remove one scenario without clearing unrelated captures.</summary>
        public bool TryRemove(string city, int quality, string scenario)
        {
            if (entries == null) return false;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null &&
                    string.Equals(entries[i].city, city, StringComparison.Ordinal) &&
                    entries[i].quality == quality &&
                    string.Equals(entries[i].scenario, scenario, StringComparison.Ordinal))
                {
                    entries.RemoveAt(i);
                    return true;
                }
            return false;
        }

        [Serializable]
        sealed class VersionHeader { public int version; }

        static bool SameKey(City3DPerformanceBaseline a, City3DPerformanceBaseline b) =>
            a != null && string.Equals(a.city, b.city, StringComparison.Ordinal) &&
            a.quality == b.quality &&
            string.Equals(a.scenario, b.scenario, StringComparison.Ordinal);

        public bool IsValid(out string reason)
        {
            if (version != CurrentVersion || entries == null || entries.Count == 0)
            {
                reason = "Unsupported or empty performance suite.";
                return false;
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entries)
            {
                if (item == null)
                {
                    reason = "Suite contains an empty capture.";
                    return false;
                }
                if (!item.IsValid(out reason)) return false;
                var key = item.city + "\u001f" + item.quality + "\u001f" + item.scenario;
                if (!keys.Add(key))
                {
                    reason = "Duplicate scenario in benchmark suite.";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        public string ToJson()
        {
            if (!IsValid(out var reason)) throw new InvalidOperationException(reason);
            return JsonUtility.ToJson(this, true);
        }

        public static bool TryFromJson(string json, out City3DPerformanceSuite suite,
            out string reason)
        {
            suite = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "Suite JSON is empty.";
                return false;
            }
            try
            {
                // Parse an independent zero-default header: do not accept JSON missing
                // the explicit version even if Unity applies field initializers.
                var header = JsonUtility.FromJson<VersionHeader>(json);
                if (header == null || header.version != CurrentVersion)
                {
                    reason = "Unsupported or missing performance suite version.";
                    return false;
                }
                var parsed = JsonUtility.FromJson<City3DPerformanceSuite>(json);
                if (parsed == null)
                {
                    reason = "Suite JSON has no object.";
                    return false;
                }
                if (!parsed.IsValid(out reason)) return false;
                suite = parsed;
                return true;
            }
            catch (Exception e)
            {
                reason = "Invalid benchmark suite JSON: " + e.Message;
                return false;
            }
        }

        public struct Summary
        {
            public int Matched;
            public int Missing;
            public int Additional;
            public int Regressions;
            public int Passed;
            public int Rejected;
            public float WorstP95Percent;
        }

        /// <summary>
        /// Compare exact city + quality + manually labelled scenario keys, never
        /// aggregate unlike random Magnate seeds as interchangeable measurements.
        /// </summary>
        public static Summary Compare(City3DPerformanceSuite baseline,
            City3DPerformanceSuite current, float allowedP95Percent)
        {
            var result = new Summary();
            if (baseline == null || current == null ||
                !baseline.IsValid(out _) || !current.IsValid(out _)) return result;
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var now in current.entries)
            {
                var id = now.city + "\u001f" + now.quality + "\u001f" + now.scenario;
                found.Add(id);
                City3DPerformanceBaseline old = null;
                foreach (var candidate in baseline.entries)
                    if (SameKey(candidate, now)) { old = candidate; break; }
                if (old == null) { result.Additional++; continue; }
                result.Matched++;
                var comparison = City3DPerformanceRegression.Compare(old.snapshot,
                    old.city, old.quality, old.scenario, now.snapshot, now.city,
                    now.quality, now.scenario, allowedP95Percent);
                if (comparison.Status == City3DPerformanceRegression.Verdict.Regression)
                {
                    result.Regressions++;
                    result.WorstP95Percent = Mathf.Max(result.WorstP95Percent,
                        comparison.P95DeltaPercent);
                }
                else if (comparison.Status == City3DPerformanceRegression.Verdict.Pass)
                    result.Passed++;
                else result.Rejected++;
            }
            foreach (var old in baseline.entries)
                if (!found.Contains(old.city + "\u001f" + old.quality + "\u001f" + old.scenario))
                    result.Missing++;
            return result;
        }
    }
}
