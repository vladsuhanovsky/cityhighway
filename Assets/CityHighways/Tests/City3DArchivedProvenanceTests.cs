using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DArchivedProvenanceTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU", int width = 1920)
        {
            return new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = gpu, width = width, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };
        }

        static City3DPerformanceReviewArtifact Review(string revision,
            City3DPerformanceEnvironment environment, float p95)
        {
            var old = new City3DPerformanceSuite();
            var now = new City3DPerformanceSuite();
            City3DPerformanceBaseline Capture(float value) =>
                City3DPerformanceBaseline.Create("Magnate", 1, "seed=42",
                    new City3DFrameMonitor.Snapshot
                    {
                        Frames = 120, AverageMs = value * .8f,
                        P95Ms = value, P99Ms = value + 2f, MaxMs = value + 5f
                    }, captureEnvironment: environment);
            Assert.IsTrue(old.TryAdd(Capture(20f), out _));
            Assert.IsTrue(now.TryAdd(Capture(p95), out _));
            return City3DPerformanceReviewArtifact.Build(old, now, 10f,
                "baseline-A", revision, "2026-11-23T20:00:00Z");
        }

        [Test]
        public void SameObservedEnvironmentAllowsArchivedP95Movement()
        {
            var previous = Review("old", Env(), 19f);
            var latest = Review("new", Env(), 25f);
            var report = City3DArchivedReviewComparison.Compare(previous, latest);
            Assert.AreEqual(1, report.ComparableRows);
            Assert.AreEqual(0, report.BlockedRows);
            Assert.IsTrue(report.Rows[0].HasP95Movement);
            Assert.That(report.Rows[0].P95MovementPercent,
                Is.EqualTo((25f / 19f - 1f) * 100f).Within(.001f));
        }

        [Test]
        public void DifferentGpuOrResolutionBlocksP95ButRetainsStatusChange()
        {
            var previous = Review("old", Env(), 19f);
            var otherGpu = City3DArchivedReviewComparison.Compare(previous,
                Review("new", Env("GPU 2"), 25f));
            Assert.AreEqual(1, otherGpu.Changed);
            Assert.AreEqual(1, otherGpu.BlockedRows);
            Assert.IsFalse(otherGpu.Rows[0].HasP95Movement);
            Assert.AreEqual("Archived capture environments differ.",
                otherGpu.Rows[0].P95ComparisonReason);
            var otherResolution = City3DArchivedReviewComparison.Compare(previous,
                Review("new", Env(width: 1280), 25f));
            Assert.IsFalse(otherResolution.Rows[0].HasP95Movement);
        }

        [Test]
        public void LegacyArchiveWithoutProvenanceStillLoadsButCannotShowP95Movement()
        {
            var previous = Review("old", Env(), 19f);
            var latest = Review("new", Env(), 25f);
            var item = previous.scenarios[0];
            item.hasCurrentEnvironment = false;
            item.currentEnvironmentSignature = null;
            previous.scenarios[0] = item;
            Assert.IsTrue(City3DPerformanceReviewArtifact.TryFromJson(
                previous.ToJson(), out var loaded, out var reason), reason);
            var report = City3DArchivedReviewComparison.Compare(loaded, latest);
            Assert.AreEqual(1, report.BlockedRows);
            Assert.IsFalse(report.Rows[0].HasP95Movement);
            Assert.AreEqual("Archived capture environment is unknown.",
                report.Rows[0].P95ComparisonReason);
        }

        [Test]
        public void ForgedOrIncompleteEnvironmentProvenanceIsRejected()
        {
            var report = Review("new", Env(), 19f);
            var item = report.scenarios[0];
            item.currentEnvironmentSignature = null;
            report.scenarios[0] = item;
            Assert.IsFalse(report.IsValid(out _));
            report = Review("new", Env(), 19f);
            item = report.scenarios[0];
            item.hasCurrentEnvironment = false;
            report.scenarios[0] = item;
            Assert.IsFalse(report.IsValid(out _));
        }

        [Test]
        public void MarkdownShowsSuppressedMovementAndReason()
        {
            var markdown = City3DArchivedReviewComparison.ToMarkdown(
                Review("before", Env(), 19f),
                Review("after", Env("different"), 25f));
            StringAssert.Contains("P95 blocked rows:** 1", markdown);
            StringAssert.Contains("Archived capture environments differ.", markdown);
            StringAssert.Contains(
                "| --- | --- | --- | --- | --- | --- | ---: | --- |", markdown);
            StringAssert.Contains("not independently verified", markdown);
        }
    }
}
