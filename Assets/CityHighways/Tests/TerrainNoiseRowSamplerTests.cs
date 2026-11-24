using System;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class TerrainNoiseRowSamplerTests
    {
        // Exact pre-optimization TerrainTexture.ValueNoise formula,
        // deliberately kept separate from the optimized sampler.
        static float OldHash(long x, long y, int salt)
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

        static float OldValueNoise(float x, float y, float cell, int salt)
        {
            float gx = x / cell, gy = y / cell;
            float ix = Mathf.Floor(gx), iy = Mathf.Floor(gy);
            float fx = gx - ix, fy = gy - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            long lx = (long)ix, ly = (long)iy;
            float a = OldHash(lx, ly, salt), b = OldHash(lx + 1, ly, salt);
            float c = OldHash(lx, ly + 1, salt), d = OldHash(lx + 1, ly + 1, salt);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        [Test]
        public void CachedRowMatchesOriginalNoiseAcrossMultipleCellsAndRows()
        {
            var cells = new[] { 90f, 34f, 11f, 70f, 40f };
            var salts = new[] { 11, 12, 13, 21, 31 };
            var ys = new[] { -63f, 0f, 10.99f, 34f, 69.99f, 107f, 966f };
            foreach (float y in ys)
            for (int layer = 0; layer < cells.Length; layer++)
            {
                float cell = cells[layer];
                int salt = salts[layer];
                var row = new TerrainNoiseRowSampler(y, cell, salt);
                for (int x = -117; x <= 1779; x++)
                {
                    float expected = OldValueNoise(x, y, cell, salt);
                    Assert.That(row.Sample(x), Is.EqualTo(expected).Within(1e-7f),
                        "Row " + y + ", x " + x + ", cell " + cell);
                }
            }
        }

        [Test]
        public void OutOfOrderSamplesAndCellBoundariesRemainDeterministic()
        {
            const float y = 67.25f, cell = 34f;
            var row = new TerrainNoiseRowSampler(y, cell, 12);
            float[] xs =
            {
                33.99f, 34f, 34.01f, 68f, 67.99f, 0f,
                -0.01f, -34f, -34.01f, 35f, 35f, 1778f
            };
            foreach (float x in xs)
                Assert.That(row.Sample(x),
                    Is.EqualTo(OldValueNoise(x, y, cell, 12)).Within(1e-7f));
        }

        [Test]
        public void ThreeOctaveGrassCombinationRetainsOriginalWeights()
        {
            foreach (float y in new[] { 0f, 41f, 485f, 966f })
            {
                var large = new TerrainNoiseRowSampler(y, 90f, 11);
                var medium = new TerrainNoiseRowSampler(y, 34f, 12);
                var small = new TerrainNoiseRowSampler(y, 11f, 13);
                for (int x = 0; x < 1736; x += 7)
                {
                    float expected = OldValueNoise(x, y, 90f, 11) * 0.5f +
                        OldValueNoise(x, y, 34f, 12) * 0.3f +
                        OldValueNoise(x, y, 11f, 13) * 0.2f;
                    float result = large.Sample(x) * 0.5f +
                        medium.Sample(x) * 0.3f +
                        small.Sample(x) * 0.2f;
                    Assert.That(result, Is.EqualTo(expected).Within(1e-7f));
                }
            }
        }

        [Test]
        public void RowSamplerRejectsInvalidParameters()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TerrainNoiseRowSampler(0f, 0f, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TerrainNoiseRowSampler(0f, float.NaN, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TerrainNoiseRowSampler(0f, float.PositiveInfinity, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TerrainNoiseRowSampler(float.NaN, 40f, 11));
        }
    }
}
