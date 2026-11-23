using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceRegressionTests
    {
        static City3DFrameMonitor.Snapshot Snapshot(float ms, float p95, int frames = 120) =>
            new City3DFrameMonitor.Snapshot
            {
                Frames = frames, AverageMs = ms, P95Ms = p95, P99Ms = p95
            };

        static City3DPerformanceRegression.Result Compare(
            City3DFrameMonitor.Snapshot baseline, City3DFrameMonitor.Snapshot current,
            string baselineCity = "Magnate city", string currentCity = "Magnate city",
            int baselineQuality = 1, int currentQuality = 1,
            string baselineScenario = "seed=42;camera=A",
            string currentScenario = "seed=42;camera=A", float threshold = 10f) =>
            City3DPerformanceRegression.Compare(baseline, baselineCity,
                baselineQuality, baselineScenario,
                current, currentCity, currentQuality, currentScenario, threshold);

        [Test]
        public void SameScenarioBeyondP95ThresholdIsRegression()
        {
            var result = Compare(Snapshot(10f, 20f), Snapshot(11f, 23f));
            Assert.AreEqual(City3DPerformanceRegression.Verdict.Regression, result.Status);
            Assert.That(result.P95DeltaPercent, Is.EqualTo(15f).Within(0.001f));
            Assert.That(result.AverageDeltaPercent, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void BoundaryAndImprovementsPass()
        {
            var boundary = Compare(Snapshot(10f, 20f), Snapshot(11f, 22f));
            Assert.AreEqual(City3DPerformanceRegression.Verdict.Pass, boundary.Status);
            var improvement = Compare(Snapshot(20f, 20f), Snapshot(10f, 10f));
            Assert.AreEqual(City3DPerformanceRegression.Verdict.Pass, improvement.Status);
            Assert.That(improvement.P95DeltaPercent, Is.EqualTo(-50f).Within(0.001f));
        }

        [Test]
        public void DifferentCityQualityAndScenarioAreNotRegressions()
        {
            var before = Snapshot(10f, 20f);
            var after = Snapshot(20f, 40f);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.DifferentCity,
                Compare(before, after, currentCity: "Other city").Status);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.DifferentQuality,
                Compare(before, after, currentQuality: 2).Status);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.DifferentScenario,
                Compare(before, after, currentScenario: "seed=7;camera=A").Status);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.DifferentScenario,
                Compare(before, after, baselineScenario: "").Status);
        }

        [Test]
        public void InsufficientOrInvalidSamplesNeverPass()
        {
            Assert.AreEqual(City3DPerformanceRegression.Verdict.InsufficientSamples,
                Compare(Snapshot(10f, 10f, 59), Snapshot(20f, 20f)).Status);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.InvalidBaseline,
                Compare(Snapshot(10f, 0f), Snapshot(20f, 20f)).Status);
            Assert.AreEqual(City3DPerformanceRegression.Verdict.InvalidBaseline,
                Compare(Snapshot(10f, 20f), Snapshot(20f, float.NaN)).Status);
        }

        [Test]
        public void WhitespaceInScenarioLabelIsNormalized()
        {
            Assert.AreEqual(City3DPerformanceRegression.Verdict.Pass,
                Compare(Snapshot(10f, 10f), Snapshot(10f, 10f),
                    baselineScenario: "  seed=42;camera=A  ").Status);
        }
    }
}
