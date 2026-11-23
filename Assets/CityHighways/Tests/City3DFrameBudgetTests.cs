using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DFrameBudgetTests
    {
        static City3DFrameMonitor.Snapshot Snapshot(int frames, float p95) =>
            new City3DFrameMonitor.Snapshot { Frames = frames, P95Ms = p95 };

        [Test]
        public void InsufficientSamplesDoNotProduceFalseWarnings()
        {
            Assert.AreEqual(City3DFrameBudget.Result.InsufficientSamples,
                City3DFrameBudget.Evaluate(Snapshot(59, 100f), 60));
            Assert.AreEqual(City3DFrameBudget.Result.InsufficientSamples,
                City3DFrameBudget.Evaluate(Snapshot(0, 0f), 60));
            Assert.AreEqual(City3DFrameBudget.Result.InsufficientSamples,
                City3DFrameBudget.Evaluate(Snapshot(100, float.NaN), 60));
        }

        [Test]
        public void P95ExactlyAtFrameBudgetPasses()
        {
            Assert.AreEqual(City3DFrameBudget.Result.Pass,
                City3DFrameBudget.Evaluate(Snapshot(100, 20f), 50));
            Assert.AreEqual(City3DFrameBudget.Result.OverBudget,
                City3DFrameBudget.Evaluate(Snapshot(100, 20.01f), 50));
        }

        [Test]
        public void ZeroTargetDisablesWarningsWithEnoughSamples()
        {
            Assert.AreEqual(City3DFrameBudget.Result.Disabled,
                City3DFrameBudget.Evaluate(Snapshot(120, 50f), 0));
        }

        [Test]
        public void QualitySelectionHasIndependentThresholds()
        {
            Assert.AreEqual(30, City3DFrameBudget.TargetForQuality(0, 30, 45, 60));
            Assert.AreEqual(45, City3DFrameBudget.TargetForQuality(1, 30, 45, 60));
            Assert.AreEqual(60, City3DFrameBudget.TargetForQuality(2, 30, 45, 60));
            Assert.AreEqual(60, City3DFrameBudget.TargetForQuality(10, 30, 45, 60));
            Assert.That(City3DFrameBudget.LimitMs(50), Is.EqualTo(20f));
        }
    }
}
