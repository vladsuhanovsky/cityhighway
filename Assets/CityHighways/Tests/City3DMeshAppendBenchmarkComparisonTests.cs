using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DMeshAppendBenchmarkComparisonTests
    {
        static readonly DateTime CaptureTime =
            new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);

        static string Export(string version = "6000.6.8f1", int quads = 12,
            int appends = 250)
        {
            return City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                version, quads, appends,
                new[] { "Identity", "Translation", "Rotation + scale" },
                new[]
                {
                    new[] { 10d, 9d, 11d },
                    new[] { 20d, 19d, 21d },
                    new[] { 30d, 29d, 31d }
                });
        }

        [Test]
        public void ParsedSevenRunComparisonCanBeReviewedForSamplingQuality()
        {
            string before = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "6000.6.8f1", 12, 250, new[] { "Identity" },
                new[] { new[] { 9.8d, 9.9d, 10d, 10d, 10d, 10.1d, 10.2d } });
            string after = City3DMeshAppendBenchmarkCsv.Export(CaptureTime.AddMinutes(2),
                "6000.6.8f1", 12, 250, new[] { "Identity" },
                new[] { new[] { 8.8d, 8.9d, 9d, 9d, 9d, 9.1d, 9.2d } });
            Assert.IsTrue(City3DMeshAppendBenchmarkComparison.TryCompare(
                before, after, out var report, out var reason), reason);
            var advisory = City3DMeshAppendBenchmarkAdvisory.Evaluate(report.Scenarios[0]);
            Assert.AreEqual(7, advisory.Runs);
            Assert.IsFalse(advisory.NeedsReview);
            Assert.IsFalse(advisory.RangesOverlap);
            Assert.AreEqual(-10d, report.Scenarios[0].ChangePercent, 1e-9d);
        }

        [Test]
        public void CompareReportsMedianElapsedTimePercentChangePerScenario()
        {
            string before = Export();
            string after = City3DMeshAppendBenchmarkCsv.Export(CaptureTime.AddMinutes(1),
                "6000.6.8f1", 12, 250,
                new[] { "Identity", "Translation", "Rotation + scale" },
                new[]
                {
                    new[] { 8d, 7d, 9d },
                    new[] { 21d, 20d, 22d },
                    new[] { 30d, 29d, 31d }
                });

            Assert.IsTrue(City3DMeshAppendBenchmarkComparison.TryCompare(
                before, after, out var report, out var reason), reason);
            Assert.AreEqual(3, report.Scenarios.Length);
            Assert.AreEqual("Identity", report.Scenarios[0].Name);
            Assert.AreEqual(3, report.Scenarios[0].Runs);
            Assert.AreEqual(10d, report.Scenarios[0].BaselineMedianMs, 1e-9d);
            Assert.AreEqual(8d, report.Scenarios[0].CurrentMedianMs, 1e-9d);
            Assert.AreEqual(9d, report.Scenarios[0].BaselineMinMs, 1e-9d);
            Assert.AreEqual(11d, report.Scenarios[0].BaselineMaxMs, 1e-9d);
            Assert.AreEqual(7d, report.Scenarios[0].CurrentMinMs, 1e-9d);
            Assert.AreEqual(9d, report.Scenarios[0].CurrentMaxMs, 1e-9d);
            Assert.AreEqual(-20d, report.Scenarios[0].ChangePercent, 1e-9d);
            Assert.AreEqual(5d, report.Scenarios[1].ChangePercent, 1e-9d);
            Assert.AreEqual(0d, report.Scenarios[2].ChangePercent, 1e-9d);
        }

        [Test]
        public void CompareAcceptsEscapedScenarioNamesAndDifferentRowOrder()
        {
            string name = "Rotate,\"Scale\"\nvariant";
            string before = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "Unity,\"Editor\"", 12, 250,
                new[] { name, "Identity" },
                new[] { new[] { 4d, 6d }, new[] { 10d, 12d } });
            string after = City3DMeshAppendBenchmarkCsv.Export(CaptureTime.AddHours(1),
                "Unity,\"Editor\"", 12, 250,
                new[] { "Identity", name },
                new[] { new[] { 9d, 11d }, new[] { 2d, 4d } });

            Assert.IsTrue(City3DMeshAppendBenchmarkComparison.TryCompare(
                before, after, out var report, out var reason), reason);
            Assert.AreEqual(name, report.Scenarios[0].Name);
            Assert.AreEqual(-40d, report.Scenarios[0].ChangePercent, 1e-9d);
            Assert.AreEqual(2, report.Scenarios[0].Runs);
        }

        [Test]
        public void CompareRejectsDifferentUnityVersionsAndWorkloads()
        {
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                Export(), Export(version: "6000.0.0f1"), out _, out _));
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                Export(), Export(quads: 13), out _, out _));
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                Export(), Export(appends: 251), out _, out _));
        }

        [Test]
        public void CompareRejectsMissingScenariosAndUnequalSampleCounts()
        {
            string before = Export();
            string fewer = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "6000.6.8f1", 12, 250,
                new[] { "Identity", "Translation" },
                new[] { new[] { 10d, 9d, 11d }, new[] { 20d, 19d, 21d } });
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                before, fewer, out _, out _));

            string unequal = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "6000.6.8f1", 12, 250,
                new[] { "Identity", "Translation", "Rotation + scale" },
                new[]
                {
                    new[] { 10d },
                    new[] { 20d, 19d, 21d },
                    new[] { 30d, 29d, 31d }
                });
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                before, unequal, out _, out _));
        }

        [Test]
        public void CompareRejectsDuplicateRunNumbersAndMalformedQuoting()
        {
            string before = Export();
            string repeated = before.Replace("\"Identity\",2,", "\"Identity\",1,");
            Assert.AreNotEqual(before, repeated);
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                repeated, before, out _, out var duplicateReason));
            StringAssert.Contains("run number", duplicateReason);

            string broken = before.Replace("\"Identity\"", "\"Identity\"oops");
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                broken, before, out _, out var brokenReason));
            StringAssert.Contains("quote", brokenReason);
        }

        [Test]
        public void CompareRejectsNonFiniteSamplesAndZeroBaselineMedian()
        {
            string before = Export();
            string invalid = before.Replace(",10,12,250", ",NaN,12,250");
            Assert.AreNotEqual(before, invalid);
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                invalid, before, out _, out _));

            string zero = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "6000.6.8f1", 12, 250,
                new[] { "Identity" }, new[] { new[] { 0d, 0d, 1d } });
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                zero, zero, out _, out var reason));
            StringAssert.Contains("zero baseline median", reason);
        }

        [Test]
        public void CompareRejectsCorruptedHeadersAndMixedCaptureMetadata()
        {
            string before = Export();
            string brokenHeader = before.Replace("elapsed_ms", "elapsed_seconds");
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                brokenHeader, before, out _, out _));

            string mixedVersion = before.Replace("\"Translation\"", "\"Translation\"");
            // Change only a single data row's version, not the entire capture.
            string[] lines = mixedVersion.Split('\n');
            lines[2] = lines[2].Replace("6000.6.8f1", "6000.0.0f1");
            mixedVersion = string.Join("\n", lines);
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                mixedVersion, before, out _, out var reason));
            StringAssert.Contains("metadata", reason);
        }

        [Test]
        public void CompareAcceptsBomAndWindowsLineEndings()
        {
            string before = Export();
            string windowsCsv = "\uFEFF" + before.Replace("\r\n", "\n").Replace("\n", "\r\n");
            Assert.IsTrue(City3DMeshAppendBenchmarkComparison.TryCompare(
                windowsCsv, before, out var report, out var reason), reason);
            Assert.AreEqual(3, report.Scenarios.Length);
            Assert.AreEqual(0d, report.Scenarios[0].ChangePercent, 1e-9d);
        }

        [Test]
        public void CompareRejectsNonrepresentablePercentageChange()
        {
            string before = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "6000.6.8f1", 12, 250,
                new[] { "Identity" }, new[] { new[] { 1e-300d } });
            string after = City3DMeshAppendBenchmarkCsv.Export(CaptureTime,
                "6000.6.8f1", 12, 250,
                new[] { "Identity" }, new[] { new[] { 1e300d } });
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                before, after, out _, out var reason));
            StringAssert.Contains("not representable", reason);
        }

        [Test]
        public void CompareRejectsEmptyInputWithoutThrowing()
        {
            Assert.IsFalse(City3DMeshAppendBenchmarkComparison.TryCompare(
                "", Export(), out var report, out var reason));
            Assert.IsNull(report);
            StringAssert.Contains("Baseline", reason);
        }
    }
}
