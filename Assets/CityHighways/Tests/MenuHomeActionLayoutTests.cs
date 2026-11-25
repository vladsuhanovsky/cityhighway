using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class MenuHomeActionLayoutTests
    {
        [Test]
        public void AllSixActionsReadOneEditableWidth()
        {
            Assert.AreEqual(480f, MenuHomeActionLayout.WidthFor(null));
            Assert.That(MenuHomeActionLayout.Width, Is.InRange(320f, 640f));
        }

        [Test]
        public void MainButtonsShareAnEditablePrimaryHeight()
        {
            Assert.AreEqual(58f, MenuHomeActionLayout.PrimaryHeightFor(null));
            Assert.That(MenuHomeActionLayout.PrimaryHeight, Is.InRange(40f, 85f));
        }

        [Test]
        public void SecondaryActionsHaveIndependentEditableHeightAndFontSize()
        {
            Assert.AreEqual(46f, MenuHomeActionLayout.SecondaryHeightFor(null));
            Assert.AreEqual(17f, MenuHomeActionLayout.SecondaryFontSizeFor(null));
            Assert.That(MenuHomeActionLayout.SecondaryHeight, Is.InRange(30f, 70f));
            Assert.That(MenuHomeActionLayout.SecondaryFontSize, Is.InRange(12f, 24f));
        }

        [Test]
        public void DefaultPairUsesFourPixelGapButUserMayCustomizeIt()
        {
            Assert.AreEqual(4f, MenuHomeActionLayout.InsidePairGapFor(null));
            Assert.AreEqual(
                MenuHomeActionLayout.PrimaryHeight + MenuHomeActionLayout.InsidePairGap +
                MenuHomeActionLayout.SecondaryHeight,
                MenuHomeActionLayout.PairHeight);
        }

        [Test]
        public void DefaultGroupsHaveTwelvePixelGapButUserMayCustomizeIt()
        {
            Assert.AreEqual(12f, MenuHomeActionLayout.BetweenGroupsGapFor(null));
            Assert.AreEqual(
                MenuHomeActionLayout.PairHeight * 3f +
                MenuHomeActionLayout.BetweenGroupsGap * 2f,
                MenuHomeActionLayout.ThreePairsHeight);
        }

        [Test]
        public void GroupAndInnerSpacingRespectInspectorRanges()
        {
            Assert.That(MenuHomeActionLayout.InsidePairGap, Is.InRange(0f, 20f));
            Assert.That(MenuHomeActionLayout.BetweenGroupsGap, Is.InRange(0f, 30f));
        }
    }
}
