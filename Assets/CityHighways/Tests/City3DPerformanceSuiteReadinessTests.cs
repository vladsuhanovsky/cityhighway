using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceSuiteReadinessTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU") =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = gpu, width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        static City3DPerformanceBaseline Capture(string scenario,
            City3DPerformanceEnvironment env = null) =>
            City3DPerformanceBaseline.Create("Magnate", 1, scenario,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = 16f, P95Ms = 20f,
                    P99Ms = 22f, MaxMs = 26f
                }, captureEnvironment: env ?? Env());

        static City3DPerformanceSuite Suite(params City3DPerformanceBaseline[] entries)
        {
            var suite = new City3DPerformanceSuite();
            foreach (var item in entries) Assert.IsTrue(suite.TryAdd(item, out _));
            return suite;
        }

        const string First = "magnate;seed=7;camera=central;viewport=1920x1080";
        const string Second = "magnate;seed=42;camera=central;viewport=1920x1080";

        [Test]
        public void EmptyBaselineShowsSetupHintNotFalseReadiness()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                new City3DPerformanceSuite(), Suite(Capture(First)));
            Assert.IsFalse(report.HasBaseline);
            Assert.IsFalse(report.Complete);
            Assert.AreEqual(0, report.Expected);
        }

        [Test]
        public void EmptyCurrentListsAllBaselineSeedsInStableOrder()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture(First), Capture(Second)), new City3DPerformanceSuite());
            Assert.AreEqual(2, report.Expected);
            Assert.AreEqual(2, report.Missing);
            Assert.AreEqual(First, report.Items[0].Scenario);
            Assert.AreEqual(Second, report.Items[1].Scenario);
            Assert.IsFalse(report.Complete);
        }

        [Test]
        public void CompatibleMatchingCaptureIsReady()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture(First)), Suite(Capture(First)));
            Assert.AreEqual(1, report.Ready);
            Assert.AreEqual(0, report.UnverifiedLabels);
            Assert.IsTrue(report.Complete);
            Assert.IsTrue(report.Items[0].EnvironmentCompatible);
        }

        [Test]
        public void DifferentGpuBlocksReadyDespiteMatchingSeedLabel()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture(First)), Suite(Capture(First, Env("GPU B"))));
            Assert.AreEqual(1, report.Incompatible);
            Assert.AreEqual(0, report.Ready);
            Assert.IsFalse(report.Complete);
        }

        [Test]
        public void ExtraCurrentSeedCountsSeparatelyFromMissingBaseline()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture(First)), Suite(Capture(Second)));
            Assert.AreEqual(1, report.Missing);
            Assert.AreEqual(1, report.Extra);
        }

        [Test]
        public void FreeFormBaselineLabelIsFlaggedWithoutRejectingSuite()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture("старый сценарий")), Suite(Capture("старый сценарий")));
            Assert.AreEqual(1, report.UnverifiedLabels);
            Assert.IsTrue(report.Complete);
        }
    }
}
