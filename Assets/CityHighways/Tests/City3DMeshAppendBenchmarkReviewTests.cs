using System;
using System.Globalization;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DMeshAppendBenchmarkReviewTests
    {
        static City3DMeshAppendBenchmarkComparison.Report Example()
        {
            var baseline = City3DMeshAppendBenchmarkCsv.Export(
                new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc),
                "6000.6.8f1", 12, 250,
                new[] { "Identity", "Rotation + scale" },
                new[] { new[] { 8d, 10d, 12d }, new[] { 20d, 22d, 24d } });
            var current = City3DMeshAppendBenchmarkCsv.Export(
                new DateTime(2026, 11, 24, 1, 0, 0, DateTimeKind.Utc),
                "6000.6.8f1", 12, 250,
                new[] { "Identity", "Rotation + scale" },
                new[] { new[] { 7d, 9d, 11d }, new[] { 19d, 21d, 23d } });
            Assert.IsTrue(City3DMeshAppendBenchmarkComparison.TryCompare(
                baseline, current, out var comparison, out var reason), reason);
            return comparison;
        }

        static string Export(City3DMeshAppendBenchmarkComparison.Report report) =>
            City3DMeshAppendBenchmarkReview.ToMarkdown(report,
                "abc123", "def456", "Windows / CPU Ryzen",
                "before.csv", "after.csv");

        [Test]
        public void MarkdownReportIncludesProvenanceAndIndividualRanges()
        {
            string markdown = Export(Example());
            StringAssert.Contains("Baseline revision: abc123", markdown);
            StringAssert.Contains("Current revision: def456", markdown);
            StringAssert.Contains("Windows / CPU Ryzen", markdown);
            StringAssert.Contains("Unity version (from CSV): 6000.6.8f1", markdown);
            StringAssert.Contains("Source quads: 12", markdown);
            StringAssert.Contains("Appends per run: 250", markdown);
            StringAssert.Contains("| Identity | 3 | 10.000000 | 9.000000 | -10.00% | 8.000000–12.000000 | 7.000000–11.000000 |", markdown);
            StringAssert.Contains("not FPS or GPU time", markdown);
            StringAssert.Contains("not verified", markdown);
        }

        [Test]
        public void MarkdownWarnsForNoisyAndUndersampledCaptures()
        {
            string markdown = Export(Example());
            StringAssert.Contains("## Sampling quality (advisory only)", markdown);
            StringAssert.Contains("Measured runs ≥ 7", markdown);
            StringAssert.Contains("| Identity | no | 40.00% | 44.44% | yes | yes |", markdown);
            StringAssert.Contains("non-overlapping ranges do not establish significance", markdown);
        }

        [Test]
        public void MarkdownMarksCleanRangesWithoutClaimingSignificance()
        {
            var report = Example();
            report.Scenarios[0].Runs = 7;
            report.Scenarios[0].BaselineMinMs = 9.8d;
            report.Scenarios[0].BaselineMedianMs = 10d;
            report.Scenarios[0].BaselineMaxMs = 10.2d;
            report.Scenarios[0].CurrentMinMs = 8.8d;
            report.Scenarios[0].CurrentMedianMs = 9d;
            report.Scenarios[0].CurrentMaxMs = 9.2d;
            string markdown = Export(report);
            StringAssert.Contains("| Identity | yes | 4.00% | 4.44% | no | no |", markdown);
            StringAssert.Contains("clean result", markdown);
            StringAssert.Contains("not statistical confidence intervals", markdown);
        }

        [Test]
        public void MarkdownEscapesUserLabelsAndRemovesEmbeddedLineBreaks()
        {
            var report = Example();
            report.Scenarios[0].Name = "Rotate | \\ scene\nvariant";
            string markdown = City3DMeshAppendBenchmarkReview.ToMarkdown(report,
                "branch|main", "PR\\#12", "CPU\r\nmachine",
                "before|capture.csv", "after.csv");
            StringAssert.Contains("branch\\|main", markdown);
            StringAssert.Contains("PR\\\\#12", markdown);
            StringAssert.Contains("CPU  machine", markdown);
            StringAssert.Contains("Rotate \\| \\\\ scene variant", markdown);
            StringAssert.Contains("before\\|capture.csv", markdown);
        }

        [Test]
        public void MarkdownUsesInvariantDecimalSeparators()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string markdown = Export(Example());
                StringAssert.Contains("10.000000", markdown);
                StringAssert.Contains("-10.00%", markdown);
                Assert.IsFalse(markdown.Contains("10,000000"));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Test]
        public void MarkdownRequiresExplicitManualProvenance()
        {
            var report = Example();
            Assert.Throws<ArgumentNullException>(() => Export(null));
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkReview.ToMarkdown(report,
                    "", "def", "CPU", "baseline.csv", "current.csv"));
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkReview.ToMarkdown(report,
                    "abc", "def", " ", "baseline.csv", "current.csv"));
        }

        [Test]
        public void MarkdownRejectsInvalidScenarioMeasurement()
        {
            var report = Example();
            report.Scenarios[0].CurrentMaxMs = -1d;
            Assert.Throws<ArgumentException>(() => Export(report));
            report = Example();
            report.Scenarios[0].ChangePercent = double.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => Export(report));
        }
    }
}
