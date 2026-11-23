using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DArchivedReviewComparisonTests
    {
        static City3DPerformanceEnvironment Env() =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = "GPU", width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        static City3DPerformanceBaseline Capture(string scenario, float p95) =>
            City3DPerformanceBaseline.Create("Magnate", 1, scenario,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * .8f,
                    P95Ms = p95, P99Ms = p95 + 2f, MaxMs = p95 + 5f
                }, captureEnvironment: Env());

        static City3DPerformanceSuite Suite(params City3DPerformanceBaseline[] entries)
        {
            var suite = new City3DPerformanceSuite();
            foreach (var capture in entries)
                Assert.IsTrue(suite.TryAdd(capture, out _));
            return suite;
        }

        static City3DPerformanceReviewArtifact Review(string baselineLabel,
            string currentLabel, string scenario, float p95, float allowed = 10f) =>
            City3DPerformanceReviewArtifact.Build(
                Suite(Capture(scenario, 20f)),
                Suite(Capture(scenario, p95)),
                allowed, baselineLabel, currentLabel, "2026-11-23T20:00:00Z");

        [Test]
        public void SameBaselineShowsStatusChangeAndCurrentP95Movement()
        {
            var previous = Review("baseline-A", "build-1", "seed=42", 19f);
            var latest = Review("baseline-A", "build-2", "seed=42", 25f);
            var result = City3DArchivedReviewComparison.Compare(previous, latest);
            Assert.AreEqual(1, result.Matched);
            Assert.AreEqual(1, result.Changed);
            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, result.Removed);
            Assert.IsTrue(result.ComparableP95);
            Assert.AreEqual(City3DArchivedReviewComparison.Change.StatusChanged,
                result.Rows[0].Kind);
            Assert.AreEqual("Pass", result.Rows[0].PreviousStatus);
            Assert.AreEqual("Regression", result.Rows[0].LatestStatus);
            Assert.IsTrue(result.Rows[0].HasP95Movement);
            Assert.That(result.Rows[0].P95MovementPercent,
                Is.EqualTo((25f / 19f - 1f) * 100f).Within(.001f));
        }

        [Test]
        public void DifferentBaselineOrThresholdHidesP95Movement()
        {
            var previous = Review("baseline-A", "build-1", "seed=7", 19f);
            var differentBaseline = Review("baseline-B", "build-2", "seed=7", 18f);
            var result = City3DArchivedReviewComparison.Compare(
                previous, differentBaseline);
            Assert.IsFalse(result.ComparableP95);
            Assert.IsFalse(result.Rows[0].HasP95Movement);
            Assert.IsFalse(City3DArchivedReviewComparison.Compare(
                previous, Review("baseline-A", "build-2", "seed=7", 18f, 5f))
                    .ComparableP95);
            Assert.IsFalse(City3DArchivedReviewComparison.Compare(
                Review("", "build-1", "seed=7", 19f),
                Review("", "build-2", "seed=7", 18f)).ComparableP95);
        }

        [Test]
        public void DifferentSeedsAreAddedAndRemovedNotMatched()
        {
            var result = City3DArchivedReviewComparison.Compare(
                Review("baseline-A", "build-1", "seed=7", 19f),
                Review("baseline-A", "build-2", "seed=42", 19f));
            Assert.AreEqual(0, result.Matched);
            Assert.AreEqual(1, result.Added);
            Assert.AreEqual(1, result.Removed);
            Assert.AreEqual(2, result.Rows.Length);
            Assert.IsFalse(result.Rows[0].HasP95Movement);
            Assert.IsFalse(result.Rows[1].HasP95Movement);
        }

        [Test]
        public void MarkdownContainsRevisionTransitionsEscapedScenarioAndCaveat()
        {
            var previous = Review("baseline-A", "build|one", "seed=7|camera=<A>", 19f);
            var latest = Review("baseline-A", "build-two", "seed=7|camera=<A>", 25f);
            var markdown = City3DArchivedReviewComparison.ToMarkdown(previous, latest);
            StringAssert.Contains("build\\|one", markdown);
            StringAssert.Contains("seed=7\\|camera=&lt;A&gt;", markdown);
            StringAssert.Contains("Pass → Regression", markdown);
            StringAssert.Contains("not independently verified", markdown);
        }

        [Test]
        public void CorruptedReviewIsNotComparable()
        {
            var valid = Review("baseline-A", "build-1", "seed=7", 19f);
            Assert.Throws<ArgumentException>(() =>
                City3DArchivedReviewComparison.Compare(null, valid));
            var forged = Review("baseline-A", "build-2", "seed=7", 25f);
            forged.overallStatus = "Pass";
            Assert.Throws<ArgumentException>(() =>
                City3DArchivedReviewComparison.Compare(valid, forged));
        }
    }
}
