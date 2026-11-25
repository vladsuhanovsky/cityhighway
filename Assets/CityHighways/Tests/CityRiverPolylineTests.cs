using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class CityRiverPolylineTests
    {
        static List<MapPoint> Dense()
        {
            var river = new List<MapPoint>();
            for (int i = 0; i <= 50; i++)
            {
                float t = i / 50f;
                river.Add(new MapPoint(t, 0.5f + 0.065f * (float)Math.Sin(t * 4f)));
            }
            return river;
        }

        [Test]
        public void DenseGeneratedShapeMustKeepItsExactSampledCenterline()
        {
            Assert.IsTrue(CityRiverPolyline.IsAlreadySmoothed(Dense()));
        }

        [Test]
        public void SparseAuthoredMapMustKeepLegacySmoothing()
        {
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(
                new[] { new MapPoint(0f, 0.5f), new MapPoint(0.5f, 0.6f),
                    new MapPoint(1f, 0.4f) }));
        }

        [Test]
        public void DenseCountCannotMaskAnUnexpectedLongGap()
        {
            var river = Dense();
            river[25] = new MapPoint(0.9f, 0.9f);
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(river));
        }

        [Test]
        public void DegenerateOrMalformedPathIsNotSafeToClassifyAsPreSmoothed()
        {
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(null));
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(
                Enumerable.Repeat(new MapPoint(0.5f, 0.5f), 40).ToList()));
            var river = Dense();
            river[4] = new MapPoint(float.NaN, 0.5f);
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(river));
            river = Dense();
            river[4] = new MapPoint(0.5f, float.PositiveInfinity);
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(river));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(2026)]
        [TestCase(31337)]
        public void MagnateRiversAreAlreadyDenseAndPassWaterChecks(int seed)
        {
            var city = CityGenerator.Generate(seed);
            Assert.IsNotNull(city);
            foreach (var river in city.Map.Rivers)
                Assert.IsTrue(CityRiverPolyline.IsAlreadySmoothed(river),
                    "seed " + seed + ": all generated rivers should already be smoothed");
            var issues = CityGenerator.CheckWater(city.Map);
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void ExistingSparseCityRiversAreStillIdentifiedAsHandAuthored()
        {
            var points = new[] { new MapPoint(0.015f, 0.355f),
                new MapPoint(0.093f, 0.358f), new MapPoint(0.178f, 0.358f),
                new MapPoint(0.262f, 0.335f), new MapPoint(0.37f, 0.34f) };
            Assert.IsFalse(CityRiverPolyline.IsAlreadySmoothed(points));
        }
    }
}
