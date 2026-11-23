using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceRepeatComparisonTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU") =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = gpu, width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        static City3DPerformanceRepeatSeries Series(float[] p95,
            string scenario = "seed=42;camera=A;viewport=1920x1080",
            string gpu = "GPU")
        {
            var series = new City3DPerformanceRepeatSeries();
            for (int i = 0; i < p95.Length; i++)
            {
                var capture = City3DPerformanceBaseline.Create("Magnate", 1, scenario,
                    new City3DFrameMonitor.Snapshot
                    {
                        Frames = 120, AverageMs = 10f, P95Ms = p95[i],
                        P99Ms = p95[i] + 1f, MaxMs = p95[i] + 2f
                    }, "2026-11-23T12:00:" + i.ToString("D2") + "Z",
                    Env(gpu));
                Assert.IsTrue(series.TryAdd(capture, out var reason), reason);
            }
            return series;
        }

        [Test]
        public void StableSeriesRegressionReportsSignedMedianChange()
        {
            var report = City3DPerformanceRepeatComparison.Compare(
                Series(new[] { 20f, 20f, 20f }),
                Series(new[] { 24f, 24f, 24f }));
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.Regression,
                report.Status);
            Assert.IsTrue(report.HasComparableDelta);
            Assert.AreEqual(3, report.BaselineRuns);
            Assert.That(report.P95ChangePercent, Is.EqualTo(20f).Within(.001f));
        }

        [Test]
        public void StableImprovementAndThresholdBoundary()
        {
            var baseline = Series(new[] { 20f, 20f, 20f });
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.Improvement,
                City3DPerformanceRepeatComparison.Compare(baseline,
                    Series(new[] { 16f, 16f, 16f })).Status);
            var boundary = City3DPerformanceRepeatComparison.Compare(baseline,
                Series(new[] { 22f, 22f, 22f }), 10f);
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.WithinThreshold,
                boundary.Status);
        }

        [Test]
        public void DifferentScenarioOrGpuSuppressesDifference()
        {
            var baseline = Series(new[] { 20f, 20f, 20f });
            var seed = City3DPerformanceRepeatComparison.Compare(baseline,
                Series(new[] { 22f, 22f, 22f }, "seed=99"));
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.DifferentScenario,
                seed.Status);
            Assert.IsFalse(seed.HasComparableDelta);
            var gpu = City3DPerformanceRepeatComparison.Compare(baseline,
                Series(new[] { 22f, 22f, 22f }, gpu: "Other GPU"));
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.DifferentEnvironment,
                gpu.Status);
            Assert.IsFalse(gpu.HasComparableDelta);
        }

        [Test]
        public void FewerThanThreeRunsNeverShowsComparativeDelta()
        {
            var result = City3DPerformanceRepeatComparison.Compare(
                Series(new[] { 20f, 20f, 20f }), Series(new[] { 22f, 22f }));
            Assert.AreEqual(
                City3DPerformanceRepeatComparison.Verdict.InsufficientRepeats,
                result.Status);
            Assert.IsFalse(result.HasComparableDelta);
        }

        [Test]
        public void NoisyRunSeriesDoesNotBecomeAFalseRegression()
        {
            var result = City3DPerformanceRepeatComparison.Compare(
                Series(new[] { 20f, 20f, 40f }),
                Series(new[] { 25f, 25f, 25f }));
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.UnstableRuns,
                result.Status);
            Assert.IsFalse(result.HasComparableDelta);
        }

        [Test]
        public void InvalidThresholdOrTamperedSeriesIsRejected()
        {
            var baseline = Series(new[] { 20f, 20f, 20f });
            var current = Series(new[] { 22f, 22f, 22f });
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.Invalid,
                City3DPerformanceRepeatComparison.Compare(
                    baseline, current, float.NaN).Status);
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.Invalid,
                City3DPerformanceRepeatComparison.Compare(
                    baseline, current, -1f).Status);
            current.environmentSignature = "Tampered";
            Assert.AreEqual(City3DPerformanceRepeatComparison.Verdict.Invalid,
                City3DPerformanceRepeatComparison.Compare(baseline, current).Status);
        }

        [Test]
        public void CsvAndMarkdownExposeVerdictAndEscapedScenario()
        {
            const string label = "seed=42|camera=<A>";
            var baseline = Series(new[] { 20f, 20f, 20f }, label);
            var current = Series(new[] { 24f, 24f, 24f }, label);
            var markdown = City3DPerformanceRepeatComparisonExport.ToMarkdown(
                baseline, current, 10f);
            var csv = City3DPerformanceRepeatComparisonExport.ToCsv(
                baseline, current, 10f);
            StringAssert.Contains("seed=42\\|camera=&lt;A&gt;", markdown);
            StringAssert.Contains("**Verdict:** Regression", markdown);
            StringAssert.Contains("+20.00%", markdown);
            StringAssert.Contains("\"Regression\"", csv);
            Assert.AreEqual(3, csv.Trim().Split('\n').Length);
        }

        [Test]
        public void IncomparableSeriesHasNoInventedPercentInExports()
        {
            var baseline = Series(new[] { 20f, 20f, 20f });
            var current = Series(new[] { 23f, 23f, 23f }, gpu: "Other GPU");
            var markdown = City3DPerformanceRepeatComparisonExport.ToMarkdown(
                baseline, current, 10f);
            var csv = City3DPerformanceRepeatComparisonExport.ToCsv(
                baseline, current, 10f);
            StringAssert.Contains("Not comparable", markdown);
            StringAssert.Contains("\"DifferentEnvironment\"", csv);
            StringAssert.Contains("\"\"", csv);
        }

        [Test]
        public void InvalidSeriesCannotBeExported()
        {
            var current = Series(new[] { 20f, 20f, 20f });
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceRepeatComparisonExport.ToMarkdown(
                    new City3DPerformanceRepeatSeries(), current, 10f));
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceRepeatComparisonExport.ToCsv(
                    current, null, 10f));
        }
    }
}
