using System.Collections.Generic;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class PlayerIdentityTests
    {
        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 5)]
        public void StableSeatMatchesUniquePipCount(int index, int count)
        {
            Assert.AreEqual(count, PlayerIdentity.PipCount(index));
            Assert.AreEqual("[" + count + "] Анна", PlayerIdentity.Label(index, "Анна", true));
            Assert.AreEqual("Анна", PlayerIdentity.Label(index, "Анна", false));
            var positions = new HashSet<Vector2>();
            for (int i = 0; i < count; i++)
            {
                var point = PlayerIdentity.PipOffset(index, i);
                Assert.LessOrEqual(point.magnitude + 1.35f, 8f);
                Assert.IsTrue(positions.Add(point));
            }
            Assert.IsTrue(PlayerIdentity.HasRoadMarker(index, 5));
        }

        [TestCase(-1, 5)]
        [TestCase(2, 2)]
        [TestCase(5, 5)]
        [TestCase(0, 0)]
        public void PublicOrInvalidOwnerHasNoRoadMarker(int owner, int players)
        {
            Assert.IsFalse(PlayerIdentity.HasRoadMarker(owner, players));
        }

        [Test]
        public void AlternativePalettePreservesDefaultAndProvidesFiveDistinctColors()
        {
            var colors = new HashSet<Color>();
            for (int i = 0; i < Palette.Players.Length; i++)
            {
                Assert.AreEqual(Palette.Players[i], Palette.PlayerColor(i, false));
                var color = Palette.PlayerColor(i, true);
                Assert.IsTrue(colors.Add(color));
                Assert.AreEqual(1f, color.a);
                Assert.GreaterOrEqual(Palette.ContrastRatio(color, Palette.TextOn(color)), 4.5f);
            }
            Assert.AreEqual(5, colors.Count);
        }

        [Test]
        public void SettingsAreOptInIndependentAndRoundTrip()
        {
            var old = DisplaySettings.FromJson("{\"volume\":0.8}");
            Assert.IsFalse(old.accessiblePlayerColors);
            Assert.IsFalse(old.showOwnerMarkers);
            var settings = new DisplaySettings { accessiblePlayerColors = true, showOwnerMarkers = false };
            var restored = DisplaySettings.FromJson(JsonUtility.ToJson(settings));
            Assert.IsTrue(restored.accessiblePlayerColors);
            Assert.IsFalse(restored.showOwnerMarkers);
            settings.accessiblePlayerColors = false;
            settings.showOwnerMarkers = true;
            restored.CopyFrom(settings);
            Assert.IsFalse(restored.accessiblePlayerColors);
            Assert.IsTrue(restored.showOwnerMarkers);
            restored.CopyFrom(DisplaySettings.Defaults());
            Assert.IsFalse(restored.accessiblePlayerColors);
            Assert.IsFalse(restored.showOwnerMarkers);
        }

        [Test]
        public void ActiveSettingsRouteColorAndIdentityWithoutChangingSeat()
        {
            var old = DisplaySettings.Active;
            try
            {
                DisplaySettings.Active = new DisplaySettings { accessiblePlayerColors = true, showOwnerMarkers = true };
                Assert.AreEqual(Palette.PlayerColor(2, true), Palette.PlayerColor(2));
                Assert.AreEqual("[3] Player", PlayerIdentity.Label(2, "Player"));
                DisplaySettings.Active.accessiblePlayerColors = false;
                Assert.AreEqual(Palette.PlayerColor(2, false), Palette.PlayerColor(2));
                Assert.AreEqual("[3] Player", PlayerIdentity.Label(2, "Player"));
            }
            finally { DisplaySettings.Active = old; }
        }
    }
}
