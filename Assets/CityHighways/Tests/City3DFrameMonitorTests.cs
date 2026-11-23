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
        public void SlowFrameAppearsInP99ButNotMedianOrP95()
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
