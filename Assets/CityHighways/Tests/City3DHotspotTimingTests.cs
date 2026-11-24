using System;
using System.Diagnostics;
using System.Globalization;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DHotspotTimingTests
    {
        [Test]
        public void ExplicitSamplesUseMonotonicStopwatchAndCanBeNested()
        {
            long second = Stopwatch.Frequency;
            var timing = new City3DHotspotTiming("Москва", 1);
            timing.Add("Terrain base pixel fill", 0, second);
            timing.Add("Terrain albedo bake", 0, second * 2);
            Assert.AreEqual(2, timing.Samples.Count);
            Assert.AreEqual(1000d, timing.Samples[0].Milliseconds, 1e-5d);
            Assert.AreEqual(2000d, timing.Samples[1].Milliseconds, 1e-5d);
            Assert.AreEqual("Terrain albedo bake", timing.Samples[1].Name);
        }

        [Test]
        public void LiveScopeRecordsDurationOnDispose()
        {
            var timing = new City3DHotspotTiming("Москва", 1);
            using (timing.Track("Ground normal bake"))
                Assert.AreEqual(0, timing.Samples.Count);
            Assert.AreEqual(1, timing.Samples.Count);
            Assert.GreaterOrEqual(timing.Samples[0].Milliseconds, 0d);

            // A default scope is safe for optional tracking of flat terrain.
            using (default(City3DHotspotTiming.Scope)) { }
        }

        [Test]
        public void CsvEscapesFieldsAndKeepsInvariantNumericValues()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var timing = new City3DHotspotTiming("Москва, \"тест\"", 2);
                timing.Add("Terrain,\n\"footprints\"", 0, Stopwatch.Frequency / 2);
                string csv = timing.ExportCsv("6000.6.8f1");
                StringAssert.Contains("started_utc,unity_version,city,quality,hotspot,elapsed_ms", csv);
                StringAssert.Contains("\"Москва, \"\"тест\"\"\"", csv);
                StringAssert.Contains("\"Terrain,\n\"\"footprints\"\"\"", csv);
                StringAssert.Contains(",2,\"Terrain", csv);
                StringAssert.Contains(",500", csv);
                Assert.AreEqual(500d, timing.Samples[0].Milliseconds, 1e-3d);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void InvalidAndDuplicateSamplesDoNotAlterSuccessfulRecords()
        {
            Assert.Throws<ArgumentException>(() => new City3DHotspotTiming("", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new City3DHotspotTiming("City", 3));
            var timing = new City3DHotspotTiming("City", 1);
            Assert.Throws<ArgumentException>(() => timing.Track(""));
            Assert.Throws<ArgumentOutOfRangeException>(() => timing.Add("Bad", 12, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => timing.Add("Bad", -1, 10));
            timing.Add("Good", 1, 10);
            Assert.Throws<ArgumentException>(() => timing.Add("Good", 2, 12));
            Assert.AreEqual(1, timing.Samples.Count);
        }

        [Test]
        public void CsvRequiresUnityVersionAndSamples()
        {
            var timing = new City3DHotspotTiming("City", 0);
            Assert.Throws<ArgumentException>(() => timing.ExportCsv(""));
            Assert.Throws<InvalidOperationException>(() => timing.ExportCsv("6000.6.8f1"));
        }
    }
}
