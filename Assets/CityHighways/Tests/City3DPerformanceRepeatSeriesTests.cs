using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceRepeatSeriesTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU") =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = gpu, width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        internal static City3DPerformanceBaseline Capture(float p95, int index,
            string label = "seed=42;camera=A;viewport=1920x1080",
            City3DPerformanceEnvironment environment = null)
        {
            return City3DPerformanceBaseline.Create("Magnate", 1, label,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = 14f, P95Ms = p95,
                    P99Ms = p95 + 1f, MaxMs = p95 + 2f
                }, "2026-11-23T12:00:" + index.ToString("D2") + "Z",
                environment ?? Env());
        }

        [Test]
        public void FirstRepeatSetsIdentityAndCapturesAreDeepCopied()
        {
            var series = new City3DPerformanceRepeatSeries();
            var capture = Capture(20f, 1);
            Assert.IsTrue(series.TryAdd(capture, out var reason), reason);
            capture.snapshot.P95Ms = 42f;
            Assert.AreEqual("Magnate", series.city);
            Assert.AreEqual(20f, series.repeats[0].snapshot.P95Ms);
            Assert.IsTrue(series.IsValid(out reason), reason);
        }

        [Test]
        public void DifferentSeedAndGpuAreRejectedWithoutModifyingJournal()
        {
            var series = new City3DPerformanceRepeatSeries();
            Assert.IsTrue(series.TryAdd(Capture(20f, 1), out _));
            Assert.IsFalse(series.TryAdd(Capture(21f, 2, "seed=99"), out _));
            Assert.IsFalse(series.TryAdd(Capture(21f, 3,
                environment: Env("Other GPU")), out _));
            Assert.AreEqual(1, series.repeats.Count);
        }

        [Test]
        public void DuplicateTimestampNullAndMissingEnvironmentAreRejected()
        {
            var series = new City3DPerformanceRepeatSeries();
            Assert.IsFalse(series.TryAdd(null, out _));
            Assert.IsTrue(series.TryAdd(Capture(20f, 1), out _));
            Assert.IsFalse(series.TryAdd(Capture(21f, 1), out _));
            var legacy = Capture(21f, 2);
            legacy.environmentRecorded = false;
            legacy.environment = null;
            Assert.IsFalse(series.TryAdd(legacy, out _));
            Assert.AreEqual(1, series.repeats.Count);
        }

        [Test]
        public void JsonRoundTripPreservesRunsAndRejectsTamperedMetadata()
        {
            var series = new City3DPerformanceRepeatSeries();
            Assert.IsTrue(series.TryAdd(Capture(20f, 1), out _));
            Assert.IsTrue(series.TryAdd(Capture(21f, 2), out _));
            Assert.IsTrue(City3DPerformanceRepeatSeries.TryFromJson(series.ToJson(),
                out var loaded, out var reason), reason);
            Assert.AreEqual(2, loaded.repeats.Count);
            Assert.AreEqual(21f, loaded.repeats[1].snapshot.P95Ms);
            loaded.environmentSignature = "Changed";
            Assert.IsFalse(loaded.IsValid(out _));
            Assert.IsFalse(City3DPerformanceRepeatSeries.TryFromJson(
                UnityEngine.JsonUtility.ToJson(loaded), out _, out _));
        }

        [Test]
        public void MalformedAndUnsupportedJsonCannotLoad()
        {
            Assert.IsFalse(City3DPerformanceRepeatSeries.TryFromJson(
                "broken", out _, out _));
            Assert.IsFalse(City3DPerformanceRepeatSeries.TryFromJson(
                "{}", out _, out _));
            Assert.IsFalse(City3DPerformanceRepeatSeries.TryFromJson(
                "", out _, out _));
            var series = new City3DPerformanceRepeatSeries();
            Assert.IsTrue(series.TryAdd(Capture(20f, 1), out _));
            var wrongVersion = series.ToJson().Replace("\"version\": 1",
                "\"version\": 99");
            Assert.IsFalse(City3DPerformanceRepeatSeries.TryFromJson(
                wrongVersion, out _, out _));
        }

        [Test]
        public void RemoveLastPreservesEarlierAndEmptyJournalCanStartNewScenario()
        {
            var series = new City3DPerformanceRepeatSeries();
            Assert.IsTrue(series.TryAdd(Capture(20f, 1), out _));
            Assert.IsTrue(series.TryAdd(Capture(21f, 2), out _));
            Assert.IsTrue(series.TryRemoveLast());
            Assert.AreEqual(20f, series.repeats[0].snapshot.P95Ms);
            Assert.IsTrue(series.TryRemoveLast());
            Assert.IsFalse(series.TryRemoveLast());
            Assert.IsTrue(series.TryAdd(Capture(22f, 3, "seed=99"), out _));
            Assert.AreEqual("seed=99", series.scenario);
        }
    }
}
