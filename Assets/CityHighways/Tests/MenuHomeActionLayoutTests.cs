using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class MenuHomeActionLayoutTests
    {
        [Test]
        public void AllSixActionsShareTheWidthOfTheRedNewGameButton()
        {
            // Both primary MenuButton and secondary Unity Button apply
            // the same Width constant, not ad-hoc per-action widths.
            Assert.AreEqual(480f, MenuHomeActionLayout.Width);
        }

        [Test]
        public void PrimaryActionsRetainTheEstablishedVisualHeight()
        {
            Assert.AreEqual(58f, MenuHomeActionLayout.PrimaryHeight);
            Assert.Greater(MenuHomeActionLayout.PrimaryHeight,
                MenuHomeActionLayout.SecondaryHeight);
        }

        [Test]
        public void SecondaryActionsUseTheirOwnLegibleHeight()
        {
            Assert.AreEqual(46f, MenuHomeActionLayout.SecondaryHeight);
            Assert.GreaterOrEqual(MenuHomeActionLayout.SecondaryFontSize, 17f);
        }

        [Test]
        public void PairHasFourPixelInternalSpacing()
        {
            Assert.AreEqual(4f, MenuHomeActionLayout.InsidePairGap);
            Assert.AreEqual(108f, MenuHomeActionLayout.PairHeight);
        }

        [Test]
        public void ThreeGroupsHaveTwelvePixelSeparation()
        {
            Assert.AreEqual(12f, MenuHomeActionLayout.BetweenGroupsGap);
            Assert.AreEqual(348f, MenuHomeActionLayout.ThreePairsHeight);
        }

        [Test]
        public void SixButtonsFitWithinTheExistingHomeMenuColumn()
        {
            // This is the vertical budget from the first primary to the
            // last secondary. Avoid accidentally stacking extra margins.
            Assert.LessOrEqual(MenuHomeActionLayout.ThreePairsHeight, 350f);
        }
    }
}
