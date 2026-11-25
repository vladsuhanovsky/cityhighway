using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class AccessibilitySettingsTests
    {
        [TestCase(0f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(0.1f, 0.9f)]
        [TestCase(0.92f, 0.9f)]
        [TestCase(1.08f, 1.1f)]
        [TestCase(1.24f, 1.2f)]
        [TestCase(1.9f, 1.3f)]
        public void ScaleMatchesMenuOption(float input, float expected)
        {
            Assert.AreEqual(expected, DisplaySettings.NormalizeUiScale(input), 0.0001f);
            Assert.AreEqual(expected, DisplaySettings.UiScales[DisplaySettings.UiScaleIndex(input)], 0.0001f);
        }

        [Test]
        public void NonFiniteScaleUsesDefault()
        {
            Assert.AreEqual(1f, DisplaySettings.NormalizeUiScale(float.NaN));
            Assert.AreEqual(1f, DisplaySettings.NormalizeUiScale(float.PositiveInfinity));
            Assert.AreEqual(1f, DisplaySettings.NormalizeUiScale(float.NegativeInfinity));
        }

        [Test]
        public void OldAndNewSettingsKeepCompatibleAccessibilityValues()
        {
            var old = DisplaySettings.FromJson("{\"volume\":0.8}");
            Assert.AreEqual(1f, old.uiScale);
            Assert.IsFalse(old.reducedMotion);
            var saved = new DisplaySettings { uiScale = 1.2f, reducedMotion = true, musicEnabled = true, musicVolume = 0f };
            var restored = DisplaySettings.FromJson(JsonUtility.ToJson(saved));
            Assert.AreEqual(1.2f, restored.uiScale, 0.0001f);
            Assert.IsTrue(restored.reducedMotion);
            Assert.IsTrue(restored.musicEnabled);
            Assert.AreEqual(0f, restored.musicVolume);
            var copy = new DisplaySettings();
            copy.CopyFrom(new DisplaySettings { uiScale = 1.08f, reducedMotion = true });
            Assert.AreEqual(1.1f, copy.uiScale, 0.0001f);
            Assert.IsTrue(copy.reducedMotion);
        }

        [Test]
        public void KnownContrastEndpointsAreCorrect()
        {
            Assert.AreEqual(21f, Palette.ContrastRatio(Color.white, Color.black), 0.001f);
            Assert.AreEqual(1f, Palette.ContrastRatio(Color.white, Color.white), 0.001f);
        }

        [Test]
        public void SolidPlayerAndResourceFillsHaveReadableForeground()
        {
            foreach (Color background in Palette.Players)
                Assert.GreaterOrEqual(Palette.ContrastRatio(background, Palette.TextOn(background)), 4.5f);
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                Color background = Palette.Card((Resource)i);
                Assert.GreaterOrEqual(Palette.ContrastRatio(background, Palette.TextOn(background)), 4.5f);
            }
        }

        [Test]
        public void SteelFillNoLongerGetsLowContrastWhiteText()
        {
            Color background = Palette.Card(Resource.Steel);
            Assert.Less(Palette.ContrastRatio(background, Color.white), 4.5f);
            Assert.GreaterOrEqual(Palette.ContrastRatio(background, Palette.TextOn(background)), 4.5f);
        }

        [Test]
        public void SolidSrgbSamplesMeetTarget()
        {
            for (int r = 0; r <= 10; r++)
            for (int g = 0; g <= 10; g++)
            for (int b = 0; b <= 10; b++)
            {
                var background = new Color(r / 10f, g / 10f, b / 10f);
                Assert.GreaterOrEqual(Palette.ContrastRatio(background, Palette.TextOn(background)), 4.5f);
            }
        }
    }
}
