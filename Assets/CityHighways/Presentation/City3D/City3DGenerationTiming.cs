using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Wall-clock checkpoints recorded while a real City3DScene is built.
    /// Timings include Unity and Editor overhead, not isolated GPU or CPU profiler time.
    /// </summary>
    public sealed class City3DGenerationTiming
    {
        public struct Stage
        {
            public string Name;
            public double Milliseconds;
        }

        readonly List<Stage> _stages = new List<Stage>();
        readonly HashSet<string> _stageNames = new HashSet<string>(StringComparer.Ordinal);
        long _lastTimestamp;
        double _totalMilliseconds;

        public string City { get; }
        public int Quality { get; }
        public DateTime StartedUtc { get; }
        public double TotalMilliseconds => _totalMilliseconds;
        public IReadOnlyList<Stage> Stages => _stages;

        public City3DGenerationTiming(string city, int quality, long startTimestamp)
        {
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City label is required.", nameof(city));
            if (startTimestamp < 0)
                throw new ArgumentOutOfRangeException(nameof(startTimestamp));
            City = city;
            Quality = quality;
            _lastTimestamp = startTimestamp;
            StartedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Complete the preceding named stage using Stopwatch's monotonic clock.
        /// Marking is only called during one-off city construction, not per frame.
        /// </summary>
        public void Mark(string name, long endTimestamp)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Stage label is required.", nameof(name));
            if (endTimestamp < _lastTimestamp)
                throw new ArgumentOutOfRangeException(nameof(endTimestamp),
                    "Stage timestamp cannot precede the previous checkpoint.");
            if (!_stageNames.Add(name))
                throw new ArgumentException("Stage names must be unique.", nameof(name));

            double milliseconds = (endTimestamp - _lastTimestamp) *
                1000d / Stopwatch.Frequency;
            _stages.Add(new Stage { Name = name, Milliseconds = milliseconds });
            _totalMilliseconds += milliseconds;
            _lastTimestamp = endTimestamp;
        }

        public string ExportCsv(string unityVersion)
        {
            if (string.IsNullOrWhiteSpace(unityVersion))
                throw new ArgumentException("Unity version is required.", nameof(unityVersion));
            if (_stages.Count == 0)
                throw new InvalidOperationException("No generation stages have been captured.");

            var result = new StringBuilder();
            result.AppendLine("started_utc,unity_version,city,quality,stage,elapsed_ms,share_percent");
            string timestamp = StartedUtc.ToString("O", CultureInfo.InvariantCulture);
            foreach (var stage in _stages)
            {
                double share = _totalMilliseconds > 0d
                    ? 100d * stage.Milliseconds / _totalMilliseconds : 0d;
                result.Append(Quote(timestamp)).Append(',')
                    .Append(Quote(unityVersion)).Append(',')
                    .Append(Quote(City)).Append(',')
                    .Append(Quality.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Quote(stage.Name)).Append(',')
                    .Append(stage.Milliseconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(share.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine();
            }
            return result.ToString();
        }

        static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
