using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class GameHudLayoutTests
    {
        [Test]
        public void ScreenWidthSelectsStableResponsiveTier()
        {
            Assert.AreEqual(GameHudLayout.Tier.Narrow, GameHudLayout.ForWidth(759.99f));
            Assert.AreEqual(GameHudLayout.Tier.Deck, GameHudLayout.ForWidth(760f));
            Assert.AreEqual(GameHudLayout.Tier.Deck, GameHudLayout.ForWidth(800f));
            Assert.AreEqual(GameHudLayout.Tier.Compact, GameHudLayout.ForWidth(1120f));
            Assert.AreEqual(GameHudLayout.Tier.Compact, GameHudLayout.ForWidth(1280f));
            Assert.AreEqual(GameHudLayout.Tier.Wide, GameHudLayout.ForWidth(1540f));
            Assert.AreEqual(GameHudLayout.Tier.Wide, GameHudLayout.ForWidth(1920f));
        }

        [Test]
        public void ScaledDeckUsesAvailablePanelUnitsNotPhysicalPixels()
        {
            // A 1280px-wide Deck with 130% UI scaling has fewer panel units.
            var scaled = GameHudLayout.ForWidth(1280f / 1.3f);
            Assert.AreEqual(GameHudLayout.Tier.Deck, scaled);
            Assert.IsFalse(GameHudLayout.ShowClassicLogo(scaled));
            Assert.IsFalse(GameHudLayout.ShowClassicEventText(scaled));
            Assert.IsFalse(GameHudLayout.ShowMagnateTitle(scaled));
        }

        [Test]
        public void TallOrInvalidGeometryDoesNotExposeLargeScreenElements()
        {
            foreach (float width in new[]
            {
                0f, -100f, float.NaN, float.PositiveInfinity, float.NegativeInfinity
            })
                Assert.AreEqual(GameHudLayout.Tier.Narrow, GameHudLayout.ForWidth(width));
        }

        [Test]
        public void FullDesktopKeepsBrandAndRoundEventText()
        {
            var tier = GameHudLayout.ForWidth(1920f);
            Assert.IsTrue(GameHudLayout.ShowClassicLogo(tier));
            Assert.IsTrue(GameHudLayout.ShowClassicEventText(tier));
            Assert.IsTrue(GameHudLayout.ShowMagnateTitle(tier));
        }

        [Test]
        public void CompactHidesOnlyLowPriorityBranding()
        {
            var tier = GameHudLayout.ForWidth(1280f);
            Assert.IsFalse(GameHudLayout.ShowClassicLogo(tier));
            Assert.IsTrue(GameHudLayout.ShowClassicEventText(tier));
            Assert.IsTrue(GameHudLayout.ShowMagnateTitle(tier));
        }

        [Test]
        public void DeckHidesLongBrandAndEventCaptionsWithoutDisablingButtons()
        {
            var tier = GameHudLayout.ForWidth(800f);
            Assert.IsFalse(GameHudLayout.ShowClassicLogo(tier));
            Assert.IsFalse(GameHudLayout.ShowClassicEventText(tier));
            Assert.IsFalse(GameHudLayout.ShowMagnateTitle(tier));
            Assert.Greater(GameHudLayout.ClassicTurnMaxWidth(tier), 0f);
            Assert.Greater(GameHudLayout.MagnateRoundMaxWidth(tier), 0f);
            Assert.Greater(GameHudLayout.MagnateEventMaxWidth(tier), 0f);
        }

        [Test]
        public void HeadlineSpaceIncreasesMonotonicallyWithViewport()
        {
            var tiers = new[]
            {
                GameHudLayout.Tier.Narrow,
                GameHudLayout.Tier.Deck,
                GameHudLayout.Tier.Compact,
                GameHudLayout.Tier.Wide
            };
            for (int i = 1; i < tiers.Length; i++)
            {
                Assert.GreaterOrEqual(GameHudLayout.HorizontalPadding(tiers[i]),
                    GameHudLayout.HorizontalPadding(tiers[i - 1]));
                Assert.GreaterOrEqual(GameHudLayout.ActionGap(tiers[i]),
                    GameHudLayout.ActionGap(tiers[i - 1]));
                Assert.GreaterOrEqual(GameHudLayout.ClassicTurnMaxWidth(tiers[i]),
                    GameHudLayout.ClassicTurnMaxWidth(tiers[i - 1]));
                Assert.GreaterOrEqual(GameHudLayout.MagnateRoundMaxWidth(tiers[i]),
                    GameHudLayout.MagnateRoundMaxWidth(tiers[i - 1]));
                Assert.GreaterOrEqual(GameHudLayout.MagnateEventMaxWidth(tiers[i]),
                    GameHudLayout.MagnateEventMaxWidth(tiers[i - 1]));
            }
        }

        [Test]
        public void GuideMaxWidthAvoidsOffscreenTextAndIsCappedOnDesktop()
        {
            Assert.AreEqual(580f, GameHudLayout.GuideMaxWidth(1920f));
            Assert.AreEqual(580f, GameHudLayout.GuideMaxWidth(800f));
            Assert.AreEqual(368f, GameHudLayout.GuideMaxWidth(400f));
            Assert.AreEqual(120f, GameHudLayout.GuideMaxWidth(120f));
            Assert.AreEqual(580f, GameHudLayout.GuideMaxWidth(float.NaN));
        }
    }
}
