using System;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class Magnate3DRiverAlignmentTests
    {
        const float WaterWithBanks = 0.155f;

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void GeneratedHubSitesRemainOutsideSavedRiverFootprint(int seed)
        {
            var city = CityGenerator.Generate(seed);
            var ids = city.Hubs.Select(h => h.LocationId).ToList();
            var hubs = HubPlacement.Plan(city.Map, ids, WaterWithBanks);
            Assert.AreEqual(ids.Count, hubs.Count);

            foreach (var pair in hubs)
            {
                float min = float.MaxValue;
                foreach (var river in city.Map.Rivers)
                for (int i = 0; i + 1 < river.Count; i++)
                {
                    Vector2 a = HubPlacement.World(river[i].X, river[i].Y);
                    Vector2 b = HubPlacement.World(river[i + 1].X, river[i + 1].Y);
                    float length = Vector2.Distance(a, b);
                    int steps = Math.Max(1, (int)Math.Ceiling(length / 0.025f));
                    for (int k = 0; k <= steps; k++)
                    {
                        float d = pair.Value.DistanceTo(Vector2.Lerp(a, b, k / (float)steps));
                        min = Mathf.Min(min, d);
                    }
                }

                Assert.GreaterOrEqual(min, WaterWithBanks - 0.005f,
                    "seed " + seed + ", hub " + pair.Key +
                    " overlaps mapped river or embankment");
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(2026)]
        public void StationMarkersAreDryOnSaved3DWorldLine(int seed)
        {
            var city = CityGenerator.Generate(seed);
            foreach (var hub in city.Hubs.Where(h => h.Kind == HubKind.Station))
            {
                var location = city.Map.GetLocation(hub.LocationId);
                Vector2 station = HubPlacement.World(location.X, location.Y);
                float nearest = float.MaxValue;
                foreach (var river in city.Map.Rivers)
                for (int i = 0; i + 1 < river.Count; i++)
                {
                    Vector2 a = HubPlacement.World(river[i].X, river[i].Y);
                    Vector2 b = HubPlacement.World(river[i + 1].X, river[i + 1].Y);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(station - a, ab) /
                        Mathf.Max(1e-10f, ab.sqrMagnitude));
                    nearest = Mathf.Min(nearest, (station - (a + ab * t)).magnitude);
                }
                Assert.Greater(nearest, WaterWithBanks + 0.1f,
                    "seed " + seed + ": station " + location.Name +
                    " is too close to the modeled water line");
            }
        }
    }
}
