using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceChecklistExportTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU") =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = gpu, width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        static City3DPerformanceBaseline Capture(string label,
            City3DPerformanceEnvironment env = null) =>
            City3DPerformanceBaseline.Create("Magnate", 1, label,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = 15f,
                    P95Ms = 20f, P99Ms = 22f, MaxMs = 26f
                }, captureEnvironment: env ?? Env());

        static City3DPerformanceSuite Suite(params City3DPerformanceBaseline[] entries)
        {
            var suite = new City3DPerformanceSuite();
            foreach (var entry in entries)
                Assert.IsTrue(suite.TryAdd(entry, out _));
            return suite;
        }

        [Test]
        public void CsvIncludesMissingIncompatibleExtraAndReadyRows()
        {
            var baseline = Suite(
                Capture("seed=1;camera=A;viewport=1920x1080"),
                Capture("seed=2;camera=A;viewport=1920x1080"),
                Capture("seed=3;camera=A;viewport=1920x1080"));
            var current = Suite(
                Capture("seed=1;camera=A;viewport=1920x1080"),
                Capture("seed=2;camera=A;viewport=1920x1080", Env("Other GPU")),
                Capture("seed=4;camera=A;viewport=1920x1080"));
            var report = City3DPerformanceSuiteReadiness.Check(baseline, current);
            Assert.AreEqual(4, report.Items.Length);
            Assert.AreEqual(1, report.Ready);
            Assert.AreEqual(1, report.Missing);
            Assert.AreEqual(1, report.Incompatible);
            Assert.AreEqual(1, report.Extra);
            var csv = City3DPerformanceChecklistExport.ToCsv(report);
            StringAssert.Contains("\"Extra\"", csv);
            StringAssert.Contains("\"Missing\"", csv);
            StringAssert.Contains("\"Incompatible\"", csv);
            StringAssert.Contains("\"Ready\"", csv);
            Assert.AreEqual(5, csv.Trim().Split('\n').Length);
        }

        [Test]
        public void MarkdownEscapesScenarioPipeAngleBracketsAndNewlines()
        {
            string label = "seed=7|camera=<A>\nnext";
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture(label)), Suite(Capture(label)));
            var md = City3DPerformanceChecklistExport.ToMarkdown(report);
            StringAssert.Contains("seed=7\\|camera=&lt;A&gt; next", md);
            StringAssert.Contains("**Collection complete:** Yes", md);
            StringAssert.Contains("not prove camera", md);
            StringAssert.Contains("| --- | ---: | --- | --- | --- |", md);
        }

        [Test]
        public void CsvQuotesCommasQuotesAndLineBreaksInUserText()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                Suite(Capture("seed=7;\"camera,A\"\nnext")),
                new City3DPerformanceSuite());
            var csv = City3DPerformanceChecklistExport.ToCsv(report);
            StringAssert.Contains("\"\"camera,A\"\"", csv);
            StringAssert.Contains("\"Missing\"", csv);
        }

        [Test]
        public void InvalidCurrentSuiteDoesNotClaimCollectionComplete()
        {
            var baseline = Suite(Capture("seed=7"));
            var current = Suite(Capture("seed=7"));
            current.entries[0].snapshot.Frames = 2;
            var report = City3DPerformanceSuiteReadiness.Check(baseline, current);
            Assert.IsTrue(report.CurrentSuiteInvalid);
            Assert.IsFalse(report.Complete);
            StringAssert.Contains("CURRENT suite is invalid",
                City3DPerformanceChecklistExport.ToMarkdown(report));
        }

        [Test]
        public void ExportsRejectMissingBaseline()
        {
            var report = City3DPerformanceSuiteReadiness.Check(
                new City3DPerformanceSuite(), new City3DPerformanceSuite());
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceChecklistExport.ToCsv(report));
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceChecklistExport.ToMarkdown(report));
        }
    }
}
