using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceEnvironmentTests
    {
        static City3DPerformanceEnvironment Environment(string gpu = "GPU 1",
            int width = 1920, int vSync = 0) =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1",
                platform = "WindowsPlayer",
                graphicsDevice = gpu,
                width = width,
                height = 1080,
                vSyncCount = vSync,
                targetFrameRate = -1
            };

        static City3DPerformanceBaseline Sample(
            City3DPerformanceEnvironment environment, float p95 = 20f) =>
            City3DPerformanceBaseline.Create("Magnate", 1,
                "seed=42;camera=A;viewport=1920x1080",
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * .8f,
                    P95Ms = p95, P99Ms = p95 + 2f, MaxMs = p95 + 4f
                }, "2026-11-23T00:00:00Z", environment);

        [Test]
        public void IdenticalEnvironmentIsComparableAfterJsonRoundTrip()
        {
            var first = Sample(Environment());
            Assert.IsTrue(City3DPerformanceBaseline.TryFromJson(
                first.ToJson(), out var reloaded, out var reason), reason);
            Assert.AreEqual(City3DPerformanceEnvironment.Compatibility.Match,
                City3DPerformanceEnvironment.Compare(reloaded.environment, Environment()));
            Assert.AreEqual("GPU 1", reloaded.environment.graphicsDevice);
        }

        [Test]
        public void ChangedGpuResolutionOrVSyncRejectsDirectComparison()
        {
            Assert.AreEqual(City3DPerformanceEnvironment.Compatibility.Different,
                City3DPerformanceEnvironment.Compare(Environment(), Environment("GPU 2")));
            Assert.AreEqual(City3DPerformanceEnvironment.Compatibility.Different,
                City3DPerformanceEnvironment.Compare(Environment(), Environment(width: 1280)));
            Assert.AreEqual(City3DPerformanceEnvironment.Compatibility.Different,
                City3DPerformanceEnvironment.Compare(Environment(), Environment(vSync: 1)));
        }

        [Test]
        public void MissingOrInvalidMetadataCannotProveEquality()
        {
            Assert.AreEqual(City3DPerformanceEnvironment.Compatibility.BothLegacy,
                City3DPerformanceEnvironment.Compare(null, null));
            Assert.AreEqual(City3DPerformanceEnvironment.Compatibility.Incomplete,
                City3DPerformanceEnvironment.Compare(null, Environment()));
            var invalid = Environment(width: 0);
            Assert.IsFalse(invalid.IsValid());
            Assert.IsFalse(Sample(invalid).IsValid(out _));
        }

        [Test]
        public void SuiteAndDiagnosticsRefuseMismatchedHardware()
        {
            var baseline = new City3DPerformanceSuite();
            var current = new City3DPerformanceSuite();
            Assert.IsTrue(baseline.TryAdd(Sample(Environment(), 20f), out _));
            Assert.IsTrue(current.TryAdd(Sample(Environment("GPU 2"), 10f), out _));
            var summary = City3DPerformanceSuite.Compare(baseline, current, 10f);
            Assert.AreEqual(1, summary.Matched);
            Assert.AreEqual(0, summary.Passed);
            Assert.AreEqual(1, summary.Rejected);
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            Assert.AreEqual(1, report.Invalid);
            Assert.AreEqual(0, report.Passed);
            Assert.IsFalse(report.CompleteCoverage);
        }

        [Test]
        public void LegacyBaselineRemainsReadableButMixedMetadataIsRejected()
        {
            var old = Sample(null);
            Assert.IsTrue(City3DPerformanceBaseline.TryFromJson(
                old.ToJson(), out var loaded, out var reason), reason);
            Assert.IsNull(loaded.environment);
            var baseline = new City3DPerformanceSuite();
            var current = new City3DPerformanceSuite();
            baseline.TryAdd(loaded, out _);
            current.TryAdd(Sample(Environment()), out _);
            Assert.AreEqual(1, City3DPerformanceSuite.Compare(baseline, current, 10f).Rejected);
            Assert.AreEqual(1,
                City3DPerformanceDiagnostics.Build(baseline, current, 10f).Invalid);
        }
    }
}
