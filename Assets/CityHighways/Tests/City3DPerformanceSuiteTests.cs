using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceSuiteTests
    {
        static City3DPerformanceBaseline Capture(string scenario, float p95 = 20f,
            int quality = 1, string city = "Magnate") =>
            City3DPerformanceBaseline.Create(city, quality, scenario,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * .8f, P95Ms = p95,
                    P99Ms = p95 + 2f, MaxMs = p95 + 5f,
                    EstimatedFps = 1000f / (p95 * .8f),
                    OnePercentLowFps = 1000f / (p95 + 2f)
                }, "2026-11-23T12:00:00Z");

        [Test]
        public void RoundTripPreservesDifferentMagnateSeedsAndQuality()
        {
            var suite = new City3DPerformanceSuite();
            Assert.IsTrue(suite.TryAdd(Capture("seed=7;camera=A"), out _));
            Assert.IsTrue(suite.TryAdd(Capture("seed=42;camera=A"), out _));
            Assert.IsTrue(suite.TryAdd(Capture("seed=7;camera=A", quality: 2), out _));
            Assert.IsTrue(City3DPerformanceSuite.TryFromJson(suite.ToJson(),
                out var parsed, out var reason), reason);
            Assert.AreEqual(3, parsed.entries.Count);
            Assert.AreEqual("seed=42;camera=A", parsed.entries[1].scenario);
        }

        [Test]
        public void DuplicateCaptureDoesNotOverrideFirstMeasurement()
        {
            var suite = new City3DPerformanceSuite();
            Assert.IsTrue(suite.TryAdd(Capture("seed=42", 20f), out _));
            Assert.IsFalse(suite.TryAdd(Capture("seed=42", 40f), out var why));
            Assert.IsNotEmpty(why);
            Assert.AreEqual(1, suite.entries.Count);
            Assert.AreEqual(20f, suite.entries[0].snapshot.P95Ms);
        }

        [Test]
        public void MatchedSuiteReportsRegressionsMissingAndAdditionalSeeds()
        {
            var old = new City3DPerformanceSuite();
            var now = new City3DPerformanceSuite();
            old.TryAdd(Capture("seed=7"), out _);
            old.TryAdd(Capture("seed=42"), out _);
            old.TryAdd(Capture("seed=99"), out _);
            now.TryAdd(Capture("seed=7", 23f), out _);
            now.TryAdd(Capture("seed=42", 18f), out _);
            now.TryAdd(Capture("seed=100"), out _);
            var summary = City3DPerformanceSuite.Compare(old, now, 10f);
            Assert.AreEqual(2, summary.Matched);
            Assert.AreEqual(1, summary.Regressions);
            Assert.AreEqual(1, summary.Passed);
            Assert.AreEqual(1, summary.Missing);
            Assert.AreEqual(1, summary.Additional);
            Assert.That(summary.WorstP95Percent, Is.EqualTo(15f).Within(.001f));
        }

        [Test]
        public void InvalidFilesAndMissingVersionAreRejected()
        {
            Assert.IsFalse(City3DPerformanceSuite.TryFromJson("null", out _, out _));
            Assert.IsFalse(City3DPerformanceSuite.TryFromJson("", out _, out _));
            Assert.IsFalse(City3DPerformanceSuite.TryFromJson(
                "{\"version\":99,\"entries\":[]}", out _, out _));
            Assert.IsFalse(City3DPerformanceSuite.TryFromJson(
                "{\"version\":1,\"entries\":[]}", out _, out _));
            var suite = new City3DPerformanceSuite();
            Assert.IsFalse(suite.TryAdd(null, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void DifferentQualityAndCityAreNotSameScenarioKey()
        {
            var baseline = new City3DPerformanceSuite();
            var current = new City3DPerformanceSuite();
            baseline.TryAdd(Capture("seed=42", quality: 1), out _);
            current.TryAdd(Capture("seed=42", quality: 2), out _);
            var summary = City3DPerformanceSuite.Compare(baseline, current, 10f);
            Assert.AreEqual(0, summary.Matched);
            Assert.AreEqual(1, summary.Missing);
            Assert.AreEqual(1, summary.Additional);
        }
    }
}
