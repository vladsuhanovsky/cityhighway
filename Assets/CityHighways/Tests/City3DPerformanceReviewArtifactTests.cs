using System;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceReviewArtifactTests
    {
        static City3DPerformanceEnvironment Env() => new City3DPerformanceEnvironment
        {
            unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
            graphicsDevice = "GPU", width = 1920, height = 1080,
            vSyncCount = 0, targetFrameRate = -1
        };

        static City3DPerformanceBaseline Capture(string seed, float p95) =>
            City3DPerformanceBaseline.Create("Magnate", 1, seed,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * .8f, P95Ms = p95,
                    P99Ms = p95 + 2f, MaxMs = p95 + 4f
                }, captureEnvironment: Env());

        static City3DPerformanceSuite Suite(params City3DPerformanceBaseline[] entries)
        {
            var suite = new City3DPerformanceSuite();
            foreach (var entry in entries) Assert.IsTrue(suite.TryAdd(entry, out _));
            return suite;
        }

        [Test]
        public void ReviewJsonRetainsRevisionLabelsVerdictAndScenarioDetails()
        {
            var old = Suite(Capture("seed=7", 20f));
            var now = Suite(Capture("seed=7", 18f));
            var artifact = City3DPerformanceReviewArtifact.Build(
                old, now, 10f, "before-commit", "after-commit",
                "2026-11-23T12:00:00Z");
            var loaded = JsonUtility.FromJson<City3DPerformanceReviewArtifact>(
                artifact.ToJson());
            Assert.AreEqual(1, loaded.version);
            Assert.AreEqual("before-commit", loaded.baselineRevision);
            Assert.AreEqual("after-commit", loaded.currentRevision);
            Assert.AreEqual("Pass", loaded.overallStatus);
            Assert.AreEqual(1, loaded.matched);
            Assert.AreEqual(1, loaded.passed);
            Assert.That(loaded.coveragePercent, Is.EqualTo(100f));
            Assert.AreEqual("seed=7", loaded.scenarios[0].scenario);
            Assert.AreEqual(20f, loaded.scenarios[0].baselineP95Ms);
            Assert.AreEqual(18f, loaded.scenarios[0].currentP95Ms);
            Assert.IsTrue(loaded.scenarios[0].hasComparableDelta);
        }

        [Test]
        public void MissingAndAdditionalRowsHaveNoFabricatedComparisonDelta()
        {
            var old = Suite(Capture("seed=7", 20f));
            var now = Suite(Capture("seed=42", 18f));
            var report = City3DPerformanceReviewArtifact.Build(old, now, 10f, "", "");
            Assert.AreEqual("Incomplete", report.overallStatus);
            Assert.AreEqual(1, report.missing);
            Assert.AreEqual(1, report.additional);
            Assert.AreEqual(2, report.scenarios.Length);
            foreach (var row in report.scenarios)
                Assert.IsFalse(row.hasComparableDelta);
        }

        [Test]
        public void RegressionIncludesExactP95PercentChange()
        {
            var report = City3DPerformanceReviewArtifact.Build(
                Suite(Capture("seed=7", 20f)),
                Suite(Capture("seed=7", 25f)), 10f, null, null);
            Assert.AreEqual("Regression", report.overallStatus);
            Assert.AreEqual(1, report.regressions);
            Assert.IsTrue(report.scenarios[0].hasComparableDelta);
            Assert.That(report.scenarios[0].p95DeltaPercent,
                Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void InvalidInputCannotProduceReviewArtifact()
        {
            Assert.Throws<InvalidOperationException>(() =>
                City3DPerformanceReviewArtifact.Build(
                    new City3DPerformanceSuite(),
                    Suite(Capture("seed=7", 20f)), 10f, "", ""));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                City3DPerformanceReviewArtifact.Build(
                    Suite(Capture("seed=7", 20f)),
                    Suite(Capture("seed=7", 19f)), float.NaN, "", ""));
        }
    }
}
