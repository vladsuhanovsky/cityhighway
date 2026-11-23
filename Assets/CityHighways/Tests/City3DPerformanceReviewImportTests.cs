using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceReviewImportTests
    {
        static City3DPerformanceEnvironment Env() => new City3DPerformanceEnvironment
        {
            unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
            graphicsDevice = "GPU", width = 1920, height = 1080,
            vSyncCount = 0, targetFrameRate = -1
        };

        static City3DPerformanceSuite Suite(string label, float p95)
        {
            var suite = new City3DPerformanceSuite();
            Assert.IsTrue(suite.TryAdd(City3DPerformanceBaseline.Create(
                "Москва", 1, label, new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * 0.8f, P95Ms = p95,
                    P99Ms = p95 + 2f, MaxMs = p95 + 5f
                }, captureEnvironment: Env()), out _));
            return suite;
        }

        static City3DPerformanceReviewArtifact Valid() =>
            City3DPerformanceReviewArtifact.Build(
                Suite("seed=42", 20f), Suite("seed=42", 18f),
                10f, "before", "after", "2026-11-23T20:00:00Z");

        [Test]
        public void ValidReviewRoundTripsAndKeepsCyrillicScenario()
        {
            var original = Valid();
            Assert.IsTrue(City3DPerformanceReviewArtifact.TryFromJson(
                original.ToJson(), out var parsed, out var reason), reason);
            Assert.AreEqual("Pass", parsed.overallStatus);
            Assert.AreEqual("Москва", parsed.scenarios[0].city);
            Assert.AreEqual(18f, parsed.scenarios[0].currentP95Ms);
            StringAssert.Contains("Москва",
                City3DPerformanceMarkdownReport.Render(parsed));
        }

        [Test]
        public void CorruptMissingOrUnsupportedVersionIsRejected()
        {
            Assert.IsFalse(City3DPerformanceReviewArtifact.TryFromJson(
                "null", out _, out _));
            Assert.IsFalse(City3DPerformanceReviewArtifact.TryFromJson(
                "{broken", out _, out _));
            Assert.IsFalse(City3DPerformanceReviewArtifact.TryFromJson(
                "", out _, out _));
            string json = Valid().ToJson();
            string missing = json.Replace("\"version\": 1,", "");
            Assert.AreNotEqual(json, missing);
            Assert.IsFalse(City3DPerformanceReviewArtifact.TryFromJson(
                missing, out var parsed, out var reason));
            Assert.IsNull(parsed);
            Assert.IsNotEmpty(reason);
            Assert.IsFalse(City3DPerformanceReviewArtifact.TryFromJson(
                json.Replace("\"version\": 1", "\"version\": 99"),
                out _, out _));
        }

        [Test]
        public void ForgedPassWithRegressionCannotLoad()
        {
            var artifact = City3DPerformanceReviewArtifact.Build(
                Suite("seed=42", 20f), Suite("seed=42", 25f),
                10f, "before", "after");
            artifact.overallStatus = "Pass";
            Assert.IsFalse(City3DPerformanceReviewArtifact.TryFromJson(
                UnityEngine.JsonUtility.ToJson(artifact), out _, out var reason));
            StringAssert.Contains("Overall", reason);
        }

        [Test]
        public void InconsistentTotalsCoverageAndDuplicateKeysAreRejected()
        {
            var artifact = Valid();
            artifact.passed = 2;
            Assert.IsFalse(artifact.IsValid(out _));
            artifact = Valid();
            artifact.coveragePercent = 40f;
            Assert.IsFalse(artifact.IsValid(out _));
            artifact = Valid();
            artifact.scenarios = new[] { artifact.scenarios[0], artifact.scenarios[0] };
            Assert.IsFalse(artifact.IsValid(out _));
        }

        [Test]
        public void AbsentSamplesCannotPretendToHaveMeasuredDelta()
        {
            var artifact = City3DPerformanceReviewArtifact.Build(
                Suite("seed=7", 20f), Suite("seed=42", 18f),
                10f, "before", "after");
            Assert.IsTrue(artifact.IsValid(out var reason), reason);
            var row = artifact.scenarios[0];
            row.hasComparableDelta = true;
            artifact.scenarios[0] = row;
            Assert.IsFalse(artifact.IsValid(out _));
        }

        [Test]
        public void InvalidSavedP95DeltaIsRejectedButRealRegressionAllowed()
        {
            var artifact = City3DPerformanceReviewArtifact.Build(
                Suite("seed=42", 20f), Suite("seed=42", 25f),
                10f, "", "");
            Assert.IsTrue(City3DPerformanceReviewArtifact.TryFromJson(
                artifact.ToJson(), out _, out var reason), reason);
            var row = artifact.scenarios[0];
            row.p95DeltaPercent = -20f;
            artifact.scenarios[0] = row;
            Assert.IsFalse(artifact.IsValid(out _));
        }
    }
}
