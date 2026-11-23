using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Independent repeated-capture journal, never part of the game's save schema.
    /// Every repeat has the exact same declared city/quality/scenario and environment.
    /// </summary>
    [Serializable]
    public sealed class City3DPerformanceRepeatSeries
    {
        public const int CurrentVersion = 1;
        public const int MaximumRepeats = 20;
        public int version = CurrentVersion;
        public string city;
        public int quality;
        public string scenario;
        public string environmentSignature;
        public List<City3DPerformanceBaseline> repeats =
            new List<City3DPerformanceBaseline>();

        public bool TryAdd(City3DPerformanceBaseline capture, out string reason)
        {
            if (capture == null)
            {
                reason = "Repeat capture is null.";
                return false;
            }
            if (!capture.IsValid(out reason)) return false;
            if (!capture.environmentRecorded || capture.environment == null ||
                !capture.environment.IsValid())
            {
                reason = "Repeat captures require valid recorded runtime conditions.";
                return false;
            }
            if (repeats == null) repeats = new List<City3DPerformanceBaseline>();
            if (repeats.Count >= MaximumRepeats)
            {
                reason = "Maximum repeat count reached.";
                return false;
            }
            string signature = capture.environment.Describe();
            foreach (var existing in repeats)
                if (string.Equals(existing.capturedUtc, capture.capturedUtc,
                    StringComparison.Ordinal))
                {
                    reason = "Capture timestamp is already in the repeat journal.";
                    return false;
                }
            if (repeats.Count == 0)
            {
                city = capture.city;
                quality = capture.quality;
                scenario = capture.scenario;
                environmentSignature = signature;
            }
            else if (city != capture.city || quality != capture.quality ||
                scenario != capture.scenario ||
                environmentSignature != signature)
            {
                reason = "Repeat differs in city, quality, exact scenario or observed environment.";
                return false;
            }
            // Deep copy: later mutation of CURRENT does not change this journal.
            repeats.Add(JsonUtility.FromJson<City3DPerformanceBaseline>(
                JsonUtility.ToJson(capture)));
            reason = null;
            return true;
        }

        public bool TryRemoveLast()
        {
            if (repeats == null || repeats.Count == 0) return false;
            repeats.RemoveAt(repeats.Count - 1);
            if (repeats.Count == 0)
            {
                city = null;
                scenario = null;
                environmentSignature = null;
                quality = 0;
            }
            return true;
        }

        [Serializable]
        sealed class Header { public int version; }

        public string ToJson()
        {
            if (!IsValid(out var reason)) throw new InvalidOperationException(reason);
            return JsonUtility.ToJson(this, true);
        }

        public static bool TryFromJson(string json,
            out City3DPerformanceRepeatSeries series, out string reason)
        {
            series = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "Repeat journal JSON is empty.";
                return false;
            }
            try
            {
                var header = JsonUtility.FromJson<Header>(json);
                if (header == null || header.version != CurrentVersion)
                {
                    reason = "Missing or unsupported repeat journal version.";
                    return false;
                }
                var parsed = JsonUtility.FromJson<City3DPerformanceRepeatSeries>(json);
                if (parsed == null || !parsed.IsValid(out reason)) return false;
                series = parsed;
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                reason = "Malformed repeat journal: " + ex.Message;
                return false;
            }
        }

        public bool IsValid(out string reason)
        {
            if (version != CurrentVersion || repeats == null ||
                repeats.Count == 0 || repeats.Count > MaximumRepeats ||
                string.IsNullOrWhiteSpace(city) ||
                string.IsNullOrWhiteSpace(scenario) ||
                string.IsNullOrWhiteSpace(environmentSignature))
            {
                reason = "Repeat series has missing or unsupported fields.";
                return false;
            }
            var times = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in repeats)
            {
                if (item == null || !item.IsValid(out reason) ||
                    !item.environmentRecorded || item.environment == null ||
                    !item.environment.IsValid() ||
                    item.city != city || item.quality != quality ||
                    item.scenario != scenario ||
                    item.environment.Describe() != environmentSignature)
                {
                    reason = "Repeat series contains an invalid or mismatched capture.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(item.capturedUtc) ||
                    !times.Add(item.capturedUtc))
                {
                    reason = "Repeat captures need unique timestamps.";
                    return false;
                }
            }
            reason = null;
            return true;
        }
    }
}
