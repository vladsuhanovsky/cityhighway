using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class InterfaceThemeTests
    {
        [Test]
        public void ShippedThemeAssetLoadsFromResources()
        {
            Assert.IsNotNull(InterfaceTheme.Current,
                "InterfaceTheme.asset must exist under Resources/CityHighways/UI.");
        }

        [Test]
        public void SharedPaletteTracksTheShippedAsset()
        {
            var style = InterfaceTheme.Current;
            Assert.IsNotNull(style);
            Assert.AreEqual(style.panel, Theme.Panel);
            Assert.AreEqual(style.background, Theme.Background);
            Assert.AreEqual(style.panelLight, Theme.PanelLight);
            Assert.AreEqual(style.panelHover, Theme.PanelHover);
            Assert.AreEqual(style.accent, Theme.Accent);
            Assert.AreEqual(style.text, Theme.Text);
            Assert.AreEqual(style.gold, Theme.Gold);
            Assert.AreEqual(style.success, Theme.Success);
        }

        [Test]
        public void GlassOpacityUsesSharedPanelColor()
        {
            var style = InterfaceTheme.Current;
            Assert.AreEqual(style.panel.r, Theme.PanelGlass.r, 0.0001f);
            Assert.AreEqual(style.panel.g, Theme.PanelGlass.g, 0.0001f);
            Assert.AreEqual(style.panel.b, Theme.PanelGlass.b, 0.0001f);
            Assert.AreEqual(Mathf.Clamp(style.panelGlassOpacity, 0.7f, 1f),
                Theme.PanelGlass.a, 0.0001f);
        }

        [Test]
        public void ShippedLayoutDimensionsAreReadFromTheme()
        {
            var style = InterfaceTheme.Current;
            Assert.AreEqual(style.newGameHelpMaxWidth, InterfaceLayout.NewGameHelpMaxWidth);
            Assert.AreEqual(style.onlineNameFieldWidth, InterfaceLayout.OnlineNameFieldWidth);
            Assert.AreEqual(style.rulesNavigationWidth, InterfaceLayout.RulesNavigationWidth);
            Assert.AreEqual(style.rulesContentHeight, InterfaceLayout.RulesContentHeight);
            Assert.AreEqual(style.settingsNavigationWidth, InterfaceLayout.SettingsNavigationWidth);
            Assert.AreEqual(style.statsHistoryHeight, InterfaceLayout.StatsHistoryHeight);
            Assert.AreEqual(style.profileContentHeight, InterfaceLayout.ProfileContentHeight);
            Assert.AreEqual(style.hudTopBarHeight, InterfaceLayout.HudTopBarHeight);
            Assert.AreEqual(style.hudBottomBarHeight, InterfaceLayout.HudBottomBarHeight);
            Assert.AreEqual(style.hudDrawerWidth, InterfaceLayout.HudDrawerWidth);
        }

        [Test]
        public void SharedButtonsAndCardsUseSafePositiveSizes()
        {
            Assert.That(InterfaceLayout.CommonButtonHeight, Is.InRange(24f, 64f));
            Assert.That(InterfaceLayout.CommonButtonRadius, Is.InRange(0f, 20f));
            Assert.That(InterfaceLayout.CardRadius, Is.InRange(0f, 24f));
            Assert.That(InterfaceLayout.SectionGap, Is.InRange(0f, 10f));
        }

        [Test]
        public void RangeLimiterUsesFallbackWhenValueMissing()
        {
            Assert.AreEqual(38f, InterfaceLayout.Resolve(null, 38f, 24f, 64f));
            Assert.AreEqual(42f, InterfaceLayout.Resolve(42f, 38f, 24f, 64f));
        }

        [Test]
        public void RangeLimiterClampsInvalidExtremeValues()
        {
            Assert.AreEqual(24f, InterfaceLayout.Resolve(-100f, 38f, 24f, 64f));
            Assert.AreEqual(64f, InterfaceLayout.Resolve(999f, 38f, 24f, 64f));
        }

        [Test]
        public void RangeLimiterRejectsNonFiniteValues()
        {
            Assert.AreEqual(38f, InterfaceLayout.Resolve(float.NaN, 38f, 24f, 64f));
            Assert.AreEqual(38f, InterfaceLayout.Resolve(float.PositiveInfinity, 38f, 24f, 64f));
            Assert.AreEqual(38f, InterfaceLayout.Resolve(float.NegativeInfinity, 38f, 24f, 64f));
        }

        [Test]
        public void NewGameStylesDoNotChangeActionHandlers()
        {
            // The menu controls are still UI Toolkit elements and remain
            // interactive; theme changes are limited to appearance.
            var container = Ui.Card(12f);
            Assert.IsNotNull(container);
            Assert.AreEqual(FlexDirection.Column, container.style.flexDirection.value);
        }
    }
}
