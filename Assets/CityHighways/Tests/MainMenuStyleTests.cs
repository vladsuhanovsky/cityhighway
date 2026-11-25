using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class MainMenuStyleTests
    {
        [Test]
        public void ShippedStyleCanBeOpenedAsAResourcesAsset()
        {
            var style = MainMenuStyle.Current;
            Assert.IsNotNull(style,
                "MainMenuStyle.asset must be under Resources/CityHighways/UI.");
        }

        [Test]
        public void MissingStyleHasBackwardsCompatibleFallbacks()
        {
            Assert.AreEqual(480f, MenuHomeActionLayout.WidthFor(null));
            Assert.AreEqual(58f, MenuHomeActionLayout.PrimaryHeightFor(null));
            Assert.AreEqual(46f, MenuHomeActionLayout.SecondaryHeightFor(null));
            Assert.AreEqual(4f, MenuHomeActionLayout.InsidePairGapFor(null));
            Assert.AreEqual(12f, MenuHomeActionLayout.BetweenGroupsGapFor(null));
            Assert.AreEqual(17f, MenuHomeActionLayout.SecondaryFontSizeFor(null));
        }

        [Test]
        public void IndependentEditorChangesFlowIntoRuntimeMetrics()
        {
            var style = ScriptableObject.CreateInstance<MainMenuStyle>();
            try
            {
                style.buttonWidth = 530f;
                style.primaryHeight = 64f;
                style.secondaryHeight = 38f;
                style.insidePairGap = 6f;
                style.betweenGroupsGap = 15f;
                style.secondaryFontSize = 19f;
                Assert.AreEqual(530f, MenuHomeActionLayout.WidthFor(style));
                Assert.AreEqual(64f, MenuHomeActionLayout.PrimaryHeightFor(style));
                Assert.AreEqual(38f, MenuHomeActionLayout.SecondaryHeightFor(style));
                Assert.AreEqual(6f, MenuHomeActionLayout.InsidePairGapFor(style));
                Assert.AreEqual(15f, MenuHomeActionLayout.BetweenGroupsGapFor(style));
                Assert.AreEqual(19f, MenuHomeActionLayout.SecondaryFontSizeFor(style));
            }
            finally { Object.DestroyImmediate(style); }
        }

        [Test]
        public void InvalidManualAssetEditsAreClamped()
        {
            var style = ScriptableObject.CreateInstance<MainMenuStyle>();
            try
            {
                style.buttonWidth = -999f;
                style.primaryHeight = 999f;
                style.secondaryHeight = -2f;
                style.insidePairGap = -10f;
                style.betweenGroupsGap = 900f;
                style.secondaryFontSize = 99f;
                Assert.AreEqual(320f, MenuHomeActionLayout.WidthFor(style));
                Assert.AreEqual(85f, MenuHomeActionLayout.PrimaryHeightFor(style));
                Assert.AreEqual(30f, MenuHomeActionLayout.SecondaryHeightFor(style));
                Assert.AreEqual(0f, MenuHomeActionLayout.InsidePairGapFor(style));
                Assert.AreEqual(30f, MenuHomeActionLayout.BetweenGroupsGapFor(style));
                Assert.AreEqual(24f, MenuHomeActionLayout.SecondaryFontSizeFor(style));
            }
            finally { Object.DestroyImmediate(style); }
        }

        [Test]
        public void RealAssetUsesSameRuntimeValues()
        {
            var style = MainMenuStyle.Current;
            Assert.IsNotNull(style);
            Assert.AreEqual(MenuHomeActionLayout.WidthFor(style), MenuHomeActionLayout.Width);
            Assert.AreEqual(MenuHomeActionLayout.PrimaryHeightFor(style),
                MenuHomeActionLayout.PrimaryHeight);
            Assert.AreEqual(MenuHomeActionLayout.SecondaryHeightFor(style),
                MenuHomeActionLayout.SecondaryHeight);
        }

        [Test]
        public void EditorCanChangeTitleAndNewsDimensionsWithoutCodeUpdates()
        {
            Assert.That(MenuHomeActionLayout.LogoFontSize, Is.InRange(42f, 90f));
            Assert.That(MenuHomeActionLayout.SubtitleFontSize, Is.InRange(12f, 25f));
            Assert.That(MenuHomeActionLayout.NewsWidth, Is.InRange(450f, 1000f));
            Assert.That(MenuHomeActionLayout.NewsMinHeight, Is.InRange(36f, 90f));
            Assert.That(MenuHomeActionLayout.NewsBottomGap, Is.InRange(0f, 32f));
        }

        [Test]
        public void CenterVeilAllowsUserToRemoveDarkening()
        {
            Assert.AreEqual(0f, MenuLegibilityVeil.Opacity(0.5f, 0.5f, 0f), 0.0001f);
            Assert.Greater(MenuLegibilityVeil.Opacity(0.5f, 0.5f, 0.7f),
                MenuLegibilityVeil.Opacity(0.5f, 0.5f, 0.2f));
        }

        [Test]
        public void CenterVeilOpacityIsClampedToReasonableLimits()
        {
            Assert.AreEqual(MenuLegibilityVeil.Opacity(0.5f, 0.5f, 0.8f),
                MenuLegibilityVeil.Opacity(0.5f, 0.5f, 99f), 0.0001f);
            Assert.AreEqual(0f, MenuLegibilityVeil.Opacity(0.5f, 0.5f, -1f), 0.0001f);
        }
    }
}
