using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Recorded one-shot wall-clock substage durations for city startup.
    /// Includes Editor overhead; not a substitute for the Unity CPU Timeline.
    /// </summary>
    public sealed class City3DHotspotTiming
    {
        public struct Sample
        {
            public string Name;
            public double Milliseconds;
        }

        public readonly struct Scope : IDisposable
        {
            readonly City3DHotspotTiming _owner;
            readonly string _name;
            readonly long _started;

            internal Scope(City3DHotspotTiming owner, string name)
            {
                _owner = owner;
                _name = name;
                _started = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (_owner != null)
                    _owner.Add(_name, _started, Stopwatch.GetTimestamp());
            }
        }

        readonly List<Sample> _samples = new List<Sample>(12);
        readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

        public string City { get; }
        public int Quality { get; }
        public DateTime StartedUtc { get; }
        public IReadOnlyList<Sample> Samples => _samples;

        public City3DHotspotTiming(string city, int quality)
        {
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City name is required.", nameof(city));
            if (quality < 0 || quality > 2)
                throw new ArgumentOutOfRangeException(nameof(quality));
            City = city;
            Quality = quality;
            StartedUtc = DateTime.UtcNow;
        }

        public Scope Track(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Hotspot name is required.", nameof(name));
            return new Scope(this, name);
        }

        // Also used in deterministic unit tests to verify durations and CSV formatting.
        public void Add(string name, long startTimestamp, long endTimestamp)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Hotspot name is required.", nameof(name));
            if (startTimestamp < 0 || endTimestamp < startTimestamp)
                throw new ArgumentOutOfRangeException(nameof(endTimestamp));
            if (!_names.Add(name))
                throw new ArgumentException("Hotspot names must be unique.", nameof(name));
            _samples.Add(new Sample
            {
                Name = name,
                Milliseconds = (endTimestamp - startTimestamp) * 1000d /
                    Stopwatch.Frequency
            });
        }

        public string ExportCsv(string unityVersion)
        {
            if (string.IsNullOrWhiteSpace(unityVersion))
                throw new ArgumentException("Unity version is required.", nameof(unityVersion));
            if (_samples.Count == 0)
                throw new InvalidOperationException("No completed hotspot samples recorded.");

            var output = new StringBuilder();
            output.AppendLine("started_utc,unity_version,city,quality,hotspot,elapsed_ms");
            string timestamp = StartedUtc.ToString("O", CultureInfo.InvariantCulture);
            foreach (var sample in _samples)
            {
                output.Append(Quote(timestamp)).Append(',')
                    .Append(Quote(unityVersion)).Append(',')
                    .Append(Quote(City)).Append(',')
                    .Append(Quality.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Quote(sample.Name)).Append(',')
                    .Append(sample.Milliseconds.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine();
            }
            return output.ToString();
        }

        static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
