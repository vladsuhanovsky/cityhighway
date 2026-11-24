using System;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DOutskirtsTextureReuseTests
    {
        static CityMap Map(string name) =>
            new CityMap(name, Array.Empty<District>(), Array.Empty<Location>(),
                Array.Empty<Road>(), Array.Empty<Contract>());

        [Test]
        public void OutskirtsTextureIsSharedAcrossDifferentMapInstances()
        {
            var moscow = Map("Москва");
            var amsterdam = Map("Амстердам");
            Assert.AreNotSame(moscow, amsterdam);
            Texture2D first = CityTextures.Outskirts(moscow);
            Texture2D second = CityTextures.Outskirts(amsterdam);
            Assert.IsNotNull(first);
            Assert.AreSame(first, second,
                "Outskirts texture depends on fixed world palette/noise, not a CityMap.");
            Assert.AreEqual(1024, first.width);
            Assert.AreEqual(1024, first.height);
        }

        [Test]
        public void FullySeamCoveredInteriorKeepsOriginalColorAndGrain()
        {
            var texture = CityTextures.Outskirts(Map("Москва"));
            const int x = 512, y = 512;
            float grain = (Hash(x, y, 403) - 0.5f) * 0.03f;
            var seam = TerrainTexture.SeamColor;
            var expected = new Color32(
                Channel(seam.r + grain),
                Channel(seam.g + grain),
                Channel(seam.b + grain), 255);
            Color32 actual = texture.GetPixels32()[y * texture.width + x];
            Assert.AreEqual(expected, actual,
                "The early-out must retain the exact seam and deterministic grain.");
        }

        [Test]
        public void RepeatedCallsDoNotRebuildOrChangePreviouslyReturnedPixels()
        {
            var first = CityTextures.Outskirts(Map("Москва"));
            Color32 original = first.GetPixels32()[0];
            for (int i = 0; i < 3; i++)
                Assert.AreSame(first, CityTextures.Outskirts(Map("Different " + i)));
            Assert.AreEqual(original, first.GetPixels32()[0]);
        }

        static byte Channel(float value) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

        static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^
                    (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
