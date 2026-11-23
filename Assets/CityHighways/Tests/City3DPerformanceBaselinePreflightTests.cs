using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceBaselinePreflightTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU A", int width = 1920) =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = gpu, width = width, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        static City3DPerformanceSuite Suite(City3DPerformanceEnvironment env)
        {
            var suite = new City3DPerformanceSuite();
            var capture = City3DPerformanceBaseline.Create("Magnate", 1,
                "seed=42;camera=A;viewport=1920x1080",
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = 15f,
                    P95Ms = 20f, P99Ms = 22f, MaxMs = 26f
                }, captureEnvironment: env);
            Assert.IsTrue(suite.TryAdd(capture, out _));
            return suite;
        }

        static City3DPerformanceBaselinePreflight.Result Check(
            City3DPerformanceSuite suite,
            City3DPerformanceEnvironment env,
            string scenario = "seed=42;camera=A;viewport=1920x1080",
            int quality = 1) =>
            City3DPerformanceBaselinePreflight.Check(
                suite, "Magnate", quality, scenario, env);

        [Test]
        public void MatchingScenarioAndRecordedEnvironmentIsReady()
        {
            var outcome = Check(Suite(Env()), Env());
            Assert.AreEqual(City3DPerformanceBaselinePreflight.Status.Ready,
                outcome.State);
            Assert.IsFalse(outcome.RequiresAcknowledgement);
            StringAssert.Contains("manual verification", outcome.Explanation);
        }

        [Test]
        public void EmptyBaselineDoesNotPreventNewSuiteCapture()
        {
            var outcome = Check(new City3DPerformanceSuite(), Env());
            Assert.AreEqual(City3DPerformanceBaselinePreflight.Status.NoBaseline,
                outcome.State);
            Assert.IsFalse(outcome.RequiresAcknowledgement);
        }

        [Test]
        public void DifferentSeedOrQualityIsNotAValidMatch()
        {
            var baseline = Suite(Env());
            var otherSeed = Check(baseline, Env(),
                "seed=99;camera=A;viewport=1920x1080");
            Assert.AreEqual(City3DPerformanceBaselinePreflight.Status.MissingScenario,
                otherSeed.State);
            Assert.IsTrue(otherSeed.RequiresAcknowledgement);
            var otherQuality = Check(baseline, Env(), quality: 2);
            Assert.AreEqual(City3DPerformanceBaselinePreflight.Status.MissingScenario,
                otherQuality.State);
        }

        [Test]
        public void DifferentObservedGpuOrViewportRequiresConfirmation()
        {
            var baseline = Suite(Env());
            var gpu = Check(baseline, Env(gpu: "GPU B"));
            Assert.AreEqual(
                City3DPerformanceBaselinePreflight.Status.IncompatibleEnvironment,
                gpu.State);
            Assert.IsTrue(gpu.RequiresAcknowledgement);
            var viewport = Check(baseline, Env(width: 1280));
            Assert.AreEqual(
                City3DPerformanceBaselinePreflight.Status.IncompatibleEnvironment,
                viewport.State);
        }

        [Test]
        public void LegacyBaselineEnvironmentIsUnknownNotReady()
        {
            var result = Check(Suite(null), Env());
            Assert.AreEqual(
                City3DPerformanceBaselinePreflight.Status.UnknownLegacyEnvironment,
                result.State);
            Assert.IsTrue(result.RequiresAcknowledgement);
        }

        [Test]
        public void AbsentCurrentEnvironmentIsUnknownNotReady()
        {
            var result = Check(Suite(Env()), null);
            Assert.AreEqual(
                City3DPerformanceBaselinePreflight.Status.UnknownLegacyEnvironment,
                result.State);
            Assert.IsTrue(result.RequiresAcknowledgement);
        }
    }
}
