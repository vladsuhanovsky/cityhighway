using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class CityTreeCardRefreshPolicyTests
    {
        [Test]
        public void HiddenCardsNeverNeedRebuild()
        {
            Assert.IsFalse(CityTreeCardRefreshPolicy.ShouldRefresh(false,
                float.NaN, float.NaN, 65f, 100f));
        }

        [Test]
        public void VisibleCardsWithNoPreviouslyRenderedOrientationNeedRebuild()
        {
            Assert.IsTrue(CityTreeCardRefreshPolicy.ShouldRefresh(true,
                float.NaN, float.NaN, 60f, 25f));
        }

        [Test]
        public void SmallCameraChangesDoNotRebuildCards()
        {
            Assert.IsFalse(CityTreeCardRefreshPolicy.ShouldRefresh(true,
                50f, 120f, 52f, 123f));
        }

        [Test]
        public void TiltAndWrappedYawUseStableHysteresis()
        {
            Assert.IsTrue(CityTreeCardRefreshPolicy.ShouldRefresh(true,
                50f, 120f, 54f, 120f));
            Assert.IsFalse(CityTreeCardRefreshPolicy.ShouldRefresh(true,
                50f, 359f, 50f, 1f));
            Assert.IsTrue(CityTreeCardRefreshPolicy.ShouldRefresh(true,
                50f, 359f, 50f, 6f));
        }

        [Test]
        public void InvalidCameraValuesDoNotTriggerMeshWrite()
        {
            Assert.IsFalse(CityTreeCardRefreshPolicy.ShouldRefresh(true,
                45f, 30f, float.PositiveInfinity, 30f));
        }
    }
}
