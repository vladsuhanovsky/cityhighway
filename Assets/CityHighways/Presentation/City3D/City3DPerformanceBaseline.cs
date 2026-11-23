using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Portable, versioned frame baseline. This is a benchmark annotation, not save-game data.
    /// Deserialization is fail-closed to avoid silently comparing incomplete captures.
    /// </summary>
    [Serializable]
    public sealed class City3DPerformanceBaseline
    {
        public const int CurrentVersion = 1;
        public int version;
        public string city;
        public string scenario;
        public string capturedUtc;
        public int quality;
        public City3DFrameMonitor.Snapshot snapshot;
        // Optional in v1 files; historical captures remain readable.
        public City3DPerformanceEnvironment environment;

        public static City3DPerformanceBaseline Create(string cityName, int graphicsQuality,
            string scenarioLabel, City3DFrameMonitor.Snapshot captured,
            string utcTimestamp = null,
            City3DPerformanceEnvironment captureEnvironment = null)
        {
            return new City3DPerformanceBaseline
            {
                version = CurrentVersion,
                city = cityName,
                quality = graphicsQuality,
                scenario = scenarioLabel?.Trim(),
                snapshot = captured,
                environment = captureEnvironment,
                capturedUtc = utcTimestamp ?? DateTime.UtcNow.ToString("o",
                    System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        public bool IsValid(out string reason)
        {
            if (version != CurrentVersion)
            {
                reason = "Unsupported performance baseline format version.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(scenario))
            {
                reason = "City and explicit scenario are required; include Magnate seed and camera.";
                return false;
            }
            if (quality < 0 || quality > 2)
            {
                reason = "Invalid graphics quality.";
                return false;
            }
            if (snapshot.Frames < 60 ||
                !FinitePositive(snapshot.AverageMs) ||
                !FinitePositive(snapshot.P95Ms) ||
                !FinitePositive(snapshot.P99Ms) ||
                !FinitePositive(snapshot.MaxMs) ||
                snapshot.P95Ms > snapshot.P99Ms ||
                snapshot.P99Ms > snapshot.MaxMs)
            {
                reason = "Baseline needs 60+ samples and consistent, positive frame times.";
                return false;
            }
            if (environment != null && !environment.IsValid())
            {
                reason = "Invalid capture environment metadata.";
                return false;
            }
            reason = null;
            return true;
        }

        static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        public string ToJson()
        {
            if (!IsValid(out var reason))
                throw new InvalidOperationException(reason);
            return JsonUtility.ToJson(this, true);
        }

        public static bool TryFromJson(string json, out City3DPerformanceBaseline baseline,
            out string reason)
        {
            baseline = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "Baseline file is empty.";
                return false;
            }
            try
            {
                var parsed = JsonUtility.FromJson<City3DPerformanceBaseline>(json);
                if (parsed == null)
                {
                    reason = "Baseline JSON does not contain a valid object.";
                    return false;
                }
                if (!parsed.IsValid(out reason)) return false;
                baseline = parsed;
                return true;
            }
            catch (Exception e)
            {
                reason = "Invalid performance baseline JSON: " + e.Message;
                return false;
            }
        }
    }
}
