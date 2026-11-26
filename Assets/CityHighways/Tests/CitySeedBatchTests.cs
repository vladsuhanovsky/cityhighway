using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class CitySeedBatchTests
    {
        [TestCase(0, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 513)]
        [TestCase(int.MaxValue, 2)]
        public void InvalidRangesAreRejected(int first, int count)
        {
            Assert.IsFalse(CitySeedBatch.IsValid(first, count));
            Assert.Throws<ArgumentOutOfRangeException>(() => CitySeedBatch.Seeds(first, count));
        }

        [TestCase(int.MinValue, 512)]
        [TestCase(-2, 5)]
        [TestCase(int.MaxValue, 1)]
        [TestCase(int.MaxValue - 511, 512)]
        public void ValidRangesAreOrderedAndInclusive(int first, int count)
        {
            Assert.IsTrue(CitySeedBatch.IsValid(first, count));
            var seeds = CitySeedBatch.Seeds(first, count);
            Assert.AreEqual(count, seeds.Length);
            for (int i = 0; i < count; i++)
                Assert.AreEqual((long)first + i, (long)seeds[i]);
        }
    }
}
