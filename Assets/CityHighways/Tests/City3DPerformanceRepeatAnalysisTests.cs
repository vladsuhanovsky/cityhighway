using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceRepeatAnalysisTests
    {
        static City3DPerformanceRepeatSeries Series(params float[] values)
        {
            var series = new City3DPerformanceRepeatSeries();
            for (int i = 0; i < values.Length; i++)
                Assert.IsTrue(series.TryAdd(
                    City3DPerformanceRepeatSeriesTests.Capture(values[i], i + 1),
                    out var reason), reason);
            return series;
        }

        [Test]
        public void ThreeIdenticalRunsShowNoVariability()
        {
            var report = City3DPerformanceRepeatAnalysis.Evaluate(
                Series(20f, 20f, 20f));
            Assert.IsTrue(report.HasEnoughRepeats);
            Assert.IsFalse(report.HasVariabilityWarning);
            Assert.AreEqual(20f, report.MedianP95Ms);
            Assert.AreEqual(0f, report.RangePercent);
            Assert.AreEqual(0f, report.MedianAbsoluteDeviationMs);
        }

        [Test]
        public void OneLargeOutlierTriggersWarningButKeepsRobustMedian()
        {
            var report = City3DPerformanceRepeatAnalysis.Evaluate(
                Series(20f, 20f, 40f));
            Assert.IsTrue(report.HasVariabilityWarning);
            Assert.AreEqual(20f, report.MedianP95Ms);
            Assert.That(report.RangePercent, Is.EqualTo(100f));
            Assert.AreEqual(0f, report.MedianAbsoluteDeviationMs);
        }

        [Test]
        public void SmallNumberOfRunsIsNotCalledStable()
        {
            var report = City3DPerformanceRepeatAnalysis.Evaluate(
                Series(10f, 20f));
            Assert.IsFalse(report.HasEnoughRepeats);
            Assert.IsFalse(report.HasVariabilityWarning);
            Assert.AreEqual(15f, report.MedianP95Ms);
        }

        [Test]
        public void EvenCountMedianAndMedianAbsoluteDeviationAreCorrect()
        {
            var report = City3DPerformanceRepeatAnalysis.Evaluate(
                Series(10f, 20f, 30f, 40f));
            Assert.AreEqual(25f, report.MedianP95Ms);
            Assert.AreEqual(10f, report.MedianAbsoluteDeviationMs);
            Assert.AreEqual(10f, report.MinP95Ms);
            Assert.AreEqual(40f, report.MaxP95Ms);
        }

        [Test]
        public void EmptyJournalYieldsNoFalseStableVerdict()
        {
            var report = City3DPerformanceRepeatAnalysis.Evaluate(
                new City3DPerformanceRepeatSeries());
            Assert.AreEqual(0, report.Count);
            Assert.IsFalse(report.HasEnoughRepeats);
            Assert.IsFalse(report.HasVariabilityWarning);
        }

        [Test]
        public void CsvAndMarkdownExportEscapesUserLabels()
        {
            var series = new City3DPerformanceRepeatSeries();
            Assert.IsTrue(series.TryAdd(
                City3DPerformanceRepeatSeriesTests.Capture(20f, 1,
                    "seed=7|camera=\"A\";<center>"), out _));
            Assert.IsTrue(series.TryAdd(
                City3DPerformanceRepeatSeriesTests.Capture(21f, 2,
                    "seed=7|camera=\"A\";<center>"), out _));
            Assert.IsTrue(series.TryAdd(
                City3DPerformanceRepeatSeriesTests.Capture(22f, 3,
                    "seed=7|camera=\"A\";<center>"), out _));
            var csv = City3DPerformanceRepeatExport.ToCsv(series);
            var md = City3DPerformanceRepeatExport.ToMarkdown(series);
            StringAssert.Contains("camera=\"\"A\"\"", csv);
            StringAssert.Contains("seed=7\\|camera=", md);
            StringAssert.Contains("&lt;center&gt;", md);
            StringAssert.Contains("Median P95:** 21.000", md);
            Assert.AreEqual(4, csv.Trim().Split('\n').Length);
        }

        [Test]
        public void ExportsRejectInvalidOrEmptyJournal()
        {
            var empty = new City3DPerformanceRepeatSeries();
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceRepeatExport.ToCsv(empty));
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceRepeatExport.ToMarkdown(empty));
        }
    }
}
