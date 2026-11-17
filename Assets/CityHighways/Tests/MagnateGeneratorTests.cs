using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>Генератор городов «Логистического магната»: карта проходит проверку воды, русла — плотные.</summary>
    public class MagnateGeneratorTests
    {
        [Test]
        public void GeneratedCities_PassWaterCheck([Values(1, 7, 42, 2026, 31337)] int seed)
        {
            var city = CityGenerator.Generate(seed);
            var issues = CityGenerator.CheckWater(city.Map);
            Assert.IsEmpty(issues, string.Join("\n", issues));
            Assert.IsEmpty(city.Map.Validate(6));
        }

        [Test]
        public void GeneratedRivers_AreAlreadySmoothed()
        {
            // В карту уходят сглаженные русла (десятки точек), а не редкие опорные: тогда 3D-вода, берега
            // и застройка совпадают и дома не встают в реку на изгибах.
            for (int seed = 0; seed < 10; seed++)
            {
                var city = CityGenerator.Generate(seed);
                Assert.IsTrue(city.Map.Rivers.Count > 0);
                Assert.IsTrue(city.Map.Rivers.All(r => r.Count >= 20), $"seed {seed}: {string.Join(", ", city.Map.Rivers.Select(r => r.Count))}");
            }
        }

        [Test]
        public void WaterCheck_FindsStationInRiver()
        {
            var city = CityGenerator.Generate(5);
            var map = city.Map;
            var river = map.Rivers[0];
            var onWater = river[river.Count / 2];
            var location = map.Locations.First();
            // Карта с местом прямо на русле: проверка должна это заметить.
            var moved = new CityMap(map.Name, map.Districts,
                map.Locations.Select(l => l == location ? new Location(l.Id, l.Name, l.DistrictId, onWater.X, onWater.Y) : l).ToList(),
                map.Roads, new Contract[0]);
            moved.SetExtras(map.Rivers, 1f);
            Assert.IsTrue(CityGenerator.CheckWater(moved).Any(i => i.Contains(location.Name)));
        }
    }
}
