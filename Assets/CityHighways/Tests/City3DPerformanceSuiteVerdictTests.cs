using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceSuiteVerdictTests
    {
        static City3DPerformanceEnvironment Env() =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = "GPU 1", width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        static City3DPerformanceBaseline Sample(string scenario, float p95,
            bool legacy = false) => City3DPerformanceBaseline.Create(
                "Magnate", 1, scenario,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * 0.8f,
                    P95Ms = p95, P99Ms = p95 + 2f, MaxMs = p95 + 4f
                }, captureEnvironment: legacy ? null : Env());

        static City3DPerformanceSuite One(string seed, float p95,
            bool legacy = false)
        {
            var suite = new City3DPerformanceSuite();
            Assert.IsTrue(suite.TryAdd(Sample(seed, p95, legacy), out _));
            return suite;
        }

        static City3DPerformanceSuiteVerdict.Status Verdict(
            City3DPerformanceSuite baseline, City3DPerformanceSuite current)
        {
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            return City3DPerformanceSuiteVerdict.Evaluate(
                baseline.IsValid(out _), current.IsValid(out _), report);
        }

        [Test]
        public void FullCoverageWithMatchingEnvironmentsPasses()
        {
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.Pass,
                Verdict(One("seed=7", 20f), One("seed=7", 19f)));
        }

        [Test]
        public void P95ThresholdExceededIsRegression()
        {
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.Regression,
                Verdict(One("seed=7", 20f), One("seed=7", 25f)));
        }

        [Test]
        public void CompletelyDifferentSeedDoesNotGetNoDataOrPass()
        {
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.Incomplete,
                Verdict(One("seed=7", 20f), One("seed=42", 19f)));
        }

        [Test]
        public void BothLegacyScenariosCannotObtainVerifiedPass()
        {
            var old = One("seed=7", 20f, true);
            var now = One("seed=7", 19f, true);
            var report = City3DPerformanceDiagnostics.Build(old, now, 10f);
            Assert.AreEqual(1, report.LegacyMatches);
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.UnverifiedLegacy,
                Verdict(old, now));
        }

        [Test]
        public void MixedLegacyEnvironmentIsIncomplete()
        {
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.Incomplete,
                Verdict(One("seed=7", 20f, true), One("seed=7", 19f)));
        }

        [Test]
        public void EmptySuiteCannotProducePass()
        {
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.NoData,
                Verdict(new City3DPerformanceSuite(), One("seed=7", 20f)));
        }

        [Test]
        public void MissingScenarioIsIncompleteEvenIfOtherScenarioPasses()
        {
            var baseline = One("seed=7", 20f);
            baseline.TryAdd(Sample("seed=42", 20f), out _);
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.Incomplete,
                Verdict(baseline, One("seed=7", 19f)));
        }

        [Test]
        public void RegressionHasPriorityOverMissingScenario()
        {
            var baseline = One("seed=7", 20f);
            baseline.TryAdd(Sample("seed=42", 20f), out _);
            Assert.AreEqual(City3DPerformanceSuiteVerdict.Status.Regression,
                Verdict(baseline, One("seed=7", 25f)));
        }
    }
}
