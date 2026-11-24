using System;
using System.Linq;
using System.Reflection;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DTerrainTextureOptimizationTests
    {
        static CityMap Sample()
        {
            return new CityMap("Москва", Array.Empty<District>(), new[]
            {
                new Location("sokolniki", "Park", null, .35f, .42f),
                new Location("other", "Other", null, .65f, .56f)
            }, new[]
            {
                new Road("r", "sokolniki", "other", 10, null)
            }, Array.Empty<Contract>(), new[]
            {
                new MapPoint(.20f, .20f),
                new MapPoint(.75f, .75f)
            });
        }

        [Test]
        public void ThreeDimensionalBakeRetainsParkGroundAndMapCache()
        {
            var map = Sample();
            var footprints = new[] { new Vector4(.50f, .50f, .04f, .05f) };
            var terrain = TerrainTexture.ForCity3D(map, footprints);
            Assert.IsNotNull(terrain);
            Assert.AreEqual("CityHighways_Terrain3D", terrain.name);
            Assert.AreEqual(1736, terrain.width);
            Assert.AreEqual(967, terrain.height);

            // The original 3D cache remains intact; flat tree candidates are
            // never needed to recreate or return the same generated map.
            Assert.AreSame(terrain, TerrainTexture.ForCity3D(map, footprints));
        }

        [Test]
        public void FlatTerrainRemainsAvailableSeparatelyFromThe3DTexture()
        {
            var map = Sample();
            var city = TerrainTexture.ForCity3D(map, Array.Empty<Vector4>());
            var flat = TerrainTexture.For(map, 160f, 80f, 80f);

            Assert.IsNotNull(flat);
            Assert.AreNotSame(city, flat);
            Assert.Greater(flat.width, 0);
            Assert.Greater(flat.height, 0);
            Assert.AreSame(flat, TerrainTexture.For(map, 160f, 80f, 80f));
        }

        [Test]
        public void TenDetailedHotspotMarkersCanBeOpenedInEditMode()
        {
            var type = typeof(City3DScene).Assembly.GetType(
                "CityHighways.Presentation.City3DHotspotMarkers");
            Assert.IsNotNull(type);
            var markers = type.GetFields(BindingFlags.Static |
                BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(ProfilerMarker)).ToArray();
            Assert.AreEqual(10, markers.Length);
            foreach (var field in markers)
            {
                var marker = (ProfilerMarker)field.GetValue(null);
                using (marker.Auto()) Assert.IsNotNull(field.Name);
            }
        }
    }
}
