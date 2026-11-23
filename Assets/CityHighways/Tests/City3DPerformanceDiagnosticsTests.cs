using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceDiagnosticsTests
    {
        static City3DPerformanceBaseline Capture(string scenario, float p95 = 20f,
            int quality = 1) => City3DPerformanceBaseline.Create("Magnate", quality, scenario,
            new City3DFrameMonitor.Snapshot
            {
                Frames = 120, AverageMs = p95 * 0.8f, P95Ms = p95,
                P99Ms = p95 + 2f, MaxMs = p95 + 4f
            });

        [Test]
        public void IdentifiesRegressionsAndCoverageWithoutHidingMissingSeeds()
        {
            var baseline = new City3DPerformanceSuite();
            baseline.TryAdd(Capture("seed=7"), out _);
            baseline.TryAdd(Capture("seed=42"), out _);
            baseline.TryAdd(Capture("seed=99"), out _);
            var current = new City3DPerformanceSuite();
            current.TryAdd(Capture("seed=7", 25f), out _);
            current.TryAdd(Capture("seed=42", 18f), out _);
            current.TryAdd(Capture("seed=100"), out _);
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            Assert.AreEqual(2, report.Matched);
            Assert.AreEqual(1, report.Regressions);
            Assert.AreEqual(1, report.Passed);
            Assert.AreEqual(1, report.Missing);
            Assert.AreEqual(1, report.Additional);
            Assert.IsFalse(report.CompleteCoverage);
            Assert.IsTrue(report.HasRegressions);
            Assert.AreEqual(City3DPerformanceDiagnostics.Kind.Regression,
                report.Rows[0].Status);
            Assert.That(report.CoveragePercent, Is.EqualTo(200f / 3f).Within(.001f));
            Assert.That(report.WorstP95Percent, Is.EqualTo(25f).Within(.001f));
            Assert.AreEqual(4, report.Rows.Length);
        }

        [Test]
        public void SameSeedDifferentQualityCountsAsMissingAndAdditional()
        {
            var baseline = new City3DPerformanceSuite();
            var current = new City3DPerformanceSuite();
            baseline.TryAdd(Capture("seed=42", quality: 0), out _);
            current.TryAdd(Capture("seed=42", quality: 2), out _);
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            Assert.AreEqual(0, report.Matched);
            Assert.AreEqual(1, report.Missing);
            Assert.AreEqual(1, report.Additional);
            Assert.AreEqual(0f, report.CoveragePercent);
            Assert.IsFalse(report.CompleteCoverage);
        }

        [Test]
        public void FullCoverageAndBoundaryPass()
        {
            var baseline = new City3DPerformanceSuite();
            var current = new City3DPerformanceSuite();
            baseline.TryAdd(Capture("seed=7", 20f), out _);
            current.TryAdd(Capture("seed=7", 22f), out _);
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            Assert.AreEqual(1, report.Passed);
            Assert.AreEqual(0, report.Regressions);
            Assert.IsTrue(report.CompleteCoverage);
            Assert.That(report.CoveragePercent, Is.EqualTo(100f));
        }

        [Test]
        public void InvalidAndEmptySuitesDoNotProduceFalseGreenReport()
        {
            var empty = new City3DPerformanceSuite();
            var valid = new City3DPerformanceSuite();
            valid.TryAdd(Capture("seed=7"), out _);
            var report = City3DPerformanceDiagnostics.Build(empty, valid, 10f);
            Assert.IsFalse(report.CompleteCoverage);
            Assert.AreEqual(0, report.Matched);
            Assert.IsNotNull(report.Rows);
            Assert.AreEqual(0, report.Rows.Length);
        }
    }
}
