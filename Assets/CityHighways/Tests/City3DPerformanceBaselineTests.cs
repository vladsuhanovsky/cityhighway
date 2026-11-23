using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceBaselineTests
    {
        static City3DFrameMonitor.Snapshot Valid() =>
            new City3DFrameMonitor.Snapshot
            {
                Frames = 120, AverageMs = 16f, P95Ms = 19f,
                P99Ms = 22f, MaxMs = 30f,
                EstimatedFps = 62.5f, OnePercentLowFps = 1000f / 22f
            };

        [Test]
        public void JsonRoundTripPreservesCyrillicCityScenarioAndSamples()
        {
            var data = City3DPerformanceBaseline.Create("Случайный город",
                2, "магнат;seed=42;camera=центр;viewport=1920x1080",
                Valid(), "2026-11-23T12:00:00.0000000Z");

            Assert.IsTrue(City3DPerformanceBaseline.TryFromJson(
                data.ToJson(), out var loaded, out var problem), problem);
            Assert.AreEqual(City3DPerformanceBaseline.CurrentVersion, loaded.version);
            Assert.AreEqual(data.city, loaded.city);
            Assert.AreEqual(data.scenario, loaded.scenario);
            Assert.AreEqual(data.quality, loaded.quality);
            Assert.AreEqual(data.capturedUtc, loaded.capturedUtc);
            Assert.AreEqual(data.snapshot.Frames, loaded.snapshot.Frames);
            Assert.That(loaded.snapshot.P95Ms, Is.EqualTo(19f));
            Assert.That(loaded.snapshot.P99Ms, Is.EqualTo(22f));
        }

        [Test]
        public void MissingOrUnknownVersionRejectsFile()
        {
            var data = City3DPerformanceBaseline.Create("Magnate", 1,
                "seed=7;camera=A", Valid());
            Assert.IsFalse(City3DPerformanceBaseline.TryFromJson(
                data.ToJson().Replace("\"version\": 1", "\"version\": 9"),
                out _, out _));
            Assert.IsFalse(City3DPerformanceBaseline.TryFromJson(
                data.ToJson().Replace("\"version\": 1,", ""),
                out _, out _));
        }

        [Test]
        public void MissingScenarioOrCityAndShortCaptureCannotBeSaved()
        {
            var blank = City3DPerformanceBaseline.Create("Magnate", 1, "", Valid());
            Assert.IsFalse(blank.IsValid(out _));
            var missingCity = City3DPerformanceBaseline.Create("", 1,
                "seed=42", Valid());
            Assert.IsFalse(missingCity.IsValid(out _));
            var capture = Valid();
            capture.Frames = 20;
            Assert.IsFalse(City3DPerformanceBaseline.Create(
                "Magnate", 1, "seed=42", capture).IsValid(out _));
        }

        [Test]
        public void InvalidFrameTimeAndUnsortedPercentilesAreRejected()
        {
            var sample = Valid();
            sample.P95Ms = float.NaN;
            Assert.IsFalse(City3DPerformanceBaseline.Create(
                "Magnate", 1, "seed=42", sample).IsValid(out _));
            sample = Valid();
            sample.P95Ms = 40f;
            Assert.IsFalse(City3DPerformanceBaseline.Create(
                "Magnate", 1, "seed=42", sample).IsValid(out _));
            Assert.IsFalse(City3DPerformanceBaseline.TryFromJson(
                "not a JSON object", out _, out _));
            Assert.IsFalse(City3DPerformanceBaseline.TryFromJson(
                "", out _, out _));
        }

        [Test]
        public void LoadedBaselineWorksForMatchedRegressionComparison()
        {
            var data = City3DPerformanceBaseline.Create("Magnate", 0,
                "seed=2026;camera=A", Valid());
            Assert.IsTrue(City3DPerformanceBaseline.TryFromJson(
                data.ToJson(), out var loaded, out _));
            var slower = Valid();
            slower.P95Ms = 25f;
            slower.P99Ms = 27f;
            slower.MaxMs = 35f;
            var comparison = City3DPerformanceRegression.Compare(
                loaded.snapshot, loaded.city, loaded.quality, loaded.scenario,
                slower, "Magnate", 0, "seed=2026;camera=A", 10f);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.Regression,
                comparison.Status);
        }
    }
}
