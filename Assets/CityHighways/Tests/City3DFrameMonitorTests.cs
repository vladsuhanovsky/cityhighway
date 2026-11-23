using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DFrameMonitorTests
    {
        GameObject root;
        City3DFrameMonitor monitor;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Frame Monitor Test");
            monitor = root.AddComponent<City3DFrameMonitor>();
            monitor.enabled = false;
            monitor.ResetSamples();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void EmptySnapshotContainsNoMeasurements()
        {
            var result = monitor.Capture();
            Assert.AreEqual(0, result.Frames);
            Assert.AreEqual(0f, result.AverageMs);
            Assert.AreEqual(0f, result.EstimatedFps);
        }

        [Test]
        public void ConstantFramesGiveExpectedFpsAndPercentiles()
        {
            for (int i = 0; i < 100; i++) monitor.RecordFrame(20f);
            var result = monitor.Capture();
            Assert.AreEqual(100, result.Frames);
            Assert.That(result.AverageMs, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(result.EstimatedFps, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(result.P95Ms, Is.EqualTo(20f));
            Assert.That(result.P99Ms, Is.EqualTo(20f));
            Assert.That(result.OnePercentLowFps, Is.EqualTo(50f));
        }

        [Test]
        public void RareSlowFrameAppearsInMaximumButNotP95OrP99()
        {
            for (int i = 0; i < 99; i++) monitor.RecordFrame(10f);
            monitor.RecordFrame(100f);
            var result = monitor.Capture();
            Assert.That(result.P95Ms, Is.EqualTo(10f));
            Assert.That(result.P99Ms, Is.EqualTo(10f));
            Assert.That(result.MaxMs, Is.EqualTo(100f));
        }

        [Test]
        public void CapacityRetainsMostRecent600FramesAndResetClearsHistory()
        {
            for (int i = 0; i < 700; i++) monitor.RecordFrame(i < 100 ? 100f : 10f);
            var result = monitor.Capture();
            Assert.AreEqual(600, result.Frames);
            Assert.That(result.AverageMs, Is.EqualTo(10f).Within(0.0001f));
            monitor.ResetSamples();
            Assert.AreEqual(0, monitor.Capture().Frames);
        }

        [Test]
        public void SampleReadinessRequiresMinimumAcceptedFrames()
        {
            Assert.AreEqual(0, monitor.SampleCount);
            Assert.IsFalse(monitor.HasMinimumSamples);
            for (int i = 0; i < City3DFrameMonitor.MinimumFrames - 1; i++)
                monitor.RecordFrame(16f);
            monitor.RecordFrame(float.NaN);
            Assert.AreEqual(City3DFrameMonitor.MinimumFrames - 1,
                monitor.SampleCount);
            Assert.IsFalse(monitor.HasMinimumSamples);
            monitor.RecordFrame(16f);
            Assert.IsTrue(monitor.HasMinimumSamples);
            Assert.AreEqual(City3DFrameMonitor.MinimumFrames, monitor.Capture().Frames);
        }

        [Test]
        public void FreshWindowDiscardsOldSamplesWithoutMutatingPriorSnapshot()
        {
            for (int i = 0; i < 120; i++) monitor.RecordFrame(18f);
            var previous = monitor.Capture();
            monitor.BeginFreshWindow();
            Assert.AreEqual(0, monitor.SampleCount);
            Assert.IsFalse(monitor.HasMinimumSamples);
            Assert.AreEqual(120, previous.Frames);
            Assert.AreEqual(18f, previous.P95Ms);
            monitor.RecordFrame(24f);
            Assert.AreEqual(1, monitor.Capture().Frames);
            Assert.AreEqual(24f, monitor.Capture().P95Ms);
        }

        [Test]
        public void FrameWindowCapacityIsExposedAndNeverExceeded()
        {
            for (int i = 0; i < 1000; i++) monitor.RecordFrame(20f);
            Assert.AreEqual(600, monitor.SampleCapacity);
            Assert.AreEqual(monitor.SampleCapacity, monitor.SampleCount);
            Assert.IsTrue(monitor.HasMinimumSamples);
        }

        [Test]
        public void UniformFramesHaveNoStabilityWarning()
        {
            for (int i = 0; i < 100; i++) monitor.RecordFrame(16f);
            var result = monitor.AnalyzeStability();
            Assert.IsTrue(result.HasEnoughSamples);
            Assert.IsFalse(result.HasWarning);
            Assert.AreEqual(16f, result.MedianMs);
            Assert.That(result.P95ToMedianRatio, Is.EqualTo(1f));
            Assert.AreEqual(0, result.SpikeFrames);
        }

        [Test]
        public void SlowFramesTriggerVarianceWarningWithoutDeletingSamples()
        {
            for (int i = 0; i < 90; i++) monitor.RecordFrame(10f);
            for (int i = 0; i < 10; i++) monitor.RecordFrame(40f);
            var result = monitor.AnalyzeStability();
            Assert.IsTrue(result.HasWarning);
            Assert.AreEqual(10, result.SpikeFrames);
            Assert.That(result.SpikePercent, Is.EqualTo(10f));
            Assert.That(result.P95ToMedianRatio, Is.EqualTo(4f));
            Assert.AreEqual(100, monitor.Capture().Frames);
        }

        [Test]
        public void SingleOutlierIsReportedButNotAutomaticallyRejected()
        {
            for (int i = 0; i < 99; i++) monitor.RecordFrame(10f);
            monitor.RecordFrame(200f);
            var result = monitor.AnalyzeStability();
            Assert.AreEqual(1, result.SpikeFrames);
            Assert.IsFalse(result.HasWarning);
            Assert.That(result.P95ToMedianRatio, Is.EqualTo(1f));
            Assert.AreEqual(200f, monitor.Capture().MaxMs);
        }

        [Test]
        public void SmallSamplesDoNotClaimStabilityAndResetClearsStatistics()
        {
            for (int i = 0; i < 59; i++) monitor.RecordFrame(20f);
            var insufficient = monitor.AnalyzeStability();
            Assert.IsFalse(insufficient.HasEnoughSamples);
            Assert.IsFalse(insufficient.HasWarning);
            monitor.RecordFrame(20f);
            Assert.IsTrue(monitor.AnalyzeStability().HasEnoughSamples);
            monitor.BeginFreshWindow();
            Assert.IsFalse(monitor.AnalyzeStability().HasEnoughSamples);
        }

        [Test]
        public void EvenMedianAndExactlyDoubleFrameThresholdAreDeterministic()
        {
            for (int i = 0; i < 59; i++) monitor.RecordFrame(10f);
            monitor.RecordFrame(20f);
            var result = monitor.AnalyzeStability();
            Assert.That(result.MedianMs, Is.EqualTo(10f));
            Assert.AreEqual(0, result.SpikeFrames);
        }

        [Test]
        public void InvalidIntervalsAreIgnored()
        {
            monitor.RecordFrame(0f);
            monitor.RecordFrame(-1f);
            monitor.RecordFrame(float.NaN);
            monitor.RecordFrame(float.PositiveInfinity);
            Assert.AreEqual(0, monitor.Capture().Frames);
        }
    }
}
