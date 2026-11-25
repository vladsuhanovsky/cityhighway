using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class MenuLegibilityVeilTests
    {
        [Test]
        public void DarkensMiddleWithoutBlackeningCityEdges()
        {
            float middle = MenuLegibilityVeil.Opacity(0.5f, 0.5f);
            float left = MenuLegibilityVeil.Opacity(0f, 0.5f);
            float right = MenuLegibilityVeil.Opacity(1f, 0.5f);
            Assert.Greater(middle, 0.35f);
            Assert.AreEqual(0f, left, 0.0001f);
            Assert.AreEqual(0f, right, 0.0001f);
        }

        [Test]
        public void RoadRemainsBrightBelowMenu()
        {
            Assert.AreEqual(0f, MenuLegibilityVeil.Opacity(0.5f, 0.85f), 0.0001f);
            Assert.AreEqual(0f, MenuLegibilityVeil.Opacity(0.5f, 1f), 0.0001f);
        }

        [Test]
        public void HeaderAndSkyDoNotReceiveHeavyDarkening()
        {
            Assert.AreEqual(0f, MenuLegibilityVeil.Opacity(0.5f, 0f), 0.0001f);
            Assert.AreEqual(0f, MenuLegibilityVeil.Opacity(0.5f, 0.07f), 0.0001f);
        }

        [Test]
        public void GradientIsHorizontallySymmetric()
        {
            foreach (float x in new[] { 0.0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f })
                Assert.AreEqual(MenuLegibilityVeil.Opacity(x, 0.5f),
                    MenuLegibilityVeil.Opacity(1f - x, 0.5f), 0.0001f);
        }

        [Test]
        public void OpacityIsAlwaysWithinDisplayableRange()
        {
            for (int iy = 0; iy <= 20; iy++)
                for (int ix = 0; ix <= 20; ix++)
                {
                    float opacity = MenuLegibilityVeil.Opacity(ix / 20f, iy / 20f);
                    Assert.GreaterOrEqual(opacity, 0f);
                    Assert.LessOrEqual(opacity, 0.50f);
                }
        }

        [Test]
        public void LeftAndRightWindowsRemainMoreVisibleThanTheMenuCenter()
        {
            float center = MenuLegibilityVeil.Opacity(0.5f, 0.55f);
            Assert.Greater(center, MenuLegibilityVeil.Opacity(0.15f, 0.55f));
            Assert.Greater(center, MenuLegibilityVeil.Opacity(0.85f, 0.55f));
        }
    }
}
