using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DMeshAppendBenchmarkAdvisoryTests
    {
        static City3DMeshAppendBenchmarkComparison.ScenarioResult Sample(
            int runs = 7, double oldMin = 9.8d, double oldMedian = 10d,
            double oldMax = 10.2d, double newMin = 8.8d,
            double newMedian = 9d, double newMax = 9.2d)
        {
            return new City3DMeshAppendBenchmarkComparison.ScenarioResult
            {
                Name = "Identity",
                Runs = runs,
                BaselineMinMs = oldMin,
                BaselineMedianMs = oldMedian,
                BaselineMaxMs = oldMax,
                CurrentMinMs = newMin,
                CurrentMedianMs = newMedian,
                CurrentMaxMs = newMax
            };
        }

        [Test]
        public void SevenRunsWithNarrowDisjointRangesHaveNoAdvisoryWarning()
        {
            var report = City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample());
            Assert.IsTrue(report.HasEnoughRuns);
            Assert.IsFalse(report.RangesOverlap);
            Assert.IsFalse(report.HasLargeWithinCaptureRange);
            Assert.IsFalse(report.NeedsReview);
            Assert.AreEqual(4d, report.BaselineRangePercent, 0.0001d);
            StringAssert.Contains("not overlap", report.Explanation);
            StringAssert.Contains("statistical significance", report.Explanation);
        }

        [Test]
        public void TouchingRangeEndpointsAreConsideredOverlap()
        {
            var report = City3DMeshAppendBenchmarkAdvisory.Evaluate(
                Sample(newMin: 10.2d, newMedian: 10.3d, newMax: 10.4d));
            Assert.IsTrue(report.RangesOverlap);
            Assert.IsTrue(report.NeedsReview);
            StringAssert.Contains("ranges overlap", report.Explanation);
        }

        [Test]
        public void WideBaselineOrCurrentRangesCauseAdvisoryWarning()
        {
            var baselineWide = City3DMeshAppendBenchmarkAdvisory.Evaluate(
                Sample(oldMin: 7d, oldMedian: 10d, oldMax: 12d));
            Assert.IsTrue(baselineWide.HasLargeWithinCaptureRange);
            Assert.IsTrue(baselineWide.NeedsReview);
            Assert.AreEqual(50d, baselineWide.BaselineRangePercent, 0.00001d);

            var currentWide = City3DMeshAppendBenchmarkAdvisory.Evaluate(
                Sample(newMin: 7d, newMedian: 9d, newMax: 11d));
            Assert.IsTrue(currentWide.HasLargeWithinCaptureRange);
            Assert.IsTrue(currentWide.NeedsReview);
        }

        [Test]
        public void LowRunCountIsFlaggedEvenWithNarrowDisjointRanges()
        {
            var result = City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample(runs: 3));
            Assert.IsFalse(result.HasEnoughRuns);
            Assert.IsTrue(result.NeedsReview);
            StringAssert.Contains("Fewer than seven", result.Explanation);
        }

        [Test]
        public void ZeroCurrentMedianIsHandledWithoutDividingByZero()
        {
            var result = City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample(
                newMin: 0d, newMedian: 0d, newMax: 0d));
            Assert.AreEqual(0d, result.CurrentRangePercent);
            Assert.IsFalse(result.RangesOverlap);

            result = City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample(
                newMin: 0d, newMedian: 0d, newMax: 1d));
            Assert.IsTrue(double.IsPositiveInfinity(result.CurrentRangePercent));
            Assert.IsTrue(result.HasLargeWithinCaptureRange);
        }

        [Test]
        public void MalformedTimingRangesAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample(runs: 0)));
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample(oldMin: 11d)));
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkAdvisory.Evaluate(Sample(newMax: double.NaN)));
        }
    }
}
