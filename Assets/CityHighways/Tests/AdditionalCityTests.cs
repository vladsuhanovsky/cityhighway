using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class AdditionalCityTests
    {
        [TestCase("paris", "Париж", "Paris")]
        [TestCase("berlin", "Берлин", "Berlin")]
        public void MapCatalogAndGeometryAreValid(string key, string russian, string english)
        {
            string path = "CityHighways/Maps/" + key;
            var map = CityHighwaysApp.LoadMap(path);
            Assert.AreEqual(russian, map.Name);
            Assert.IsEmpty(map.Validate(new GameConfig().MaxRoadLength));
            Assert.AreEqual(42, map.Locations.Count);
            Assert.AreEqual(3, map.Districts.Count(d => d.IsExpansion));
            Assert.GreaterOrEqual(map.Roads.Count(r => r.IsBridge), 5);
            Assert.GreaterOrEqual(map.Contracts.Count, 40);
            Assert.AreEqual(1, map.Rivers.Count);
            Assert.IsNull(map.Ring);
            Assert.IsNotNull(map.GetDistrict("center"));
            Assert.IsNotNull(map.GetDistrict("airports"));
            var choice = GameSetup.MapOf(path);
            Assert.AreEqual(path, choice.Path);
            string old = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                Assert.AreEqual(english, choice.DisplayTitle);
                Assert.AreEqual(english, CityMapText.Name(map.Name));
                Assert.IsTrue(map.Locations.All(l => !System.Text.RegularExpressions.Regex.IsMatch(CityMapText.Name(l.Name), "[а-яё]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
                LocalizationService.SetLanguage("ru");
                Assert.AreEqual(russian, choice.DisplayTitle);
            }
            finally { LocalizationService.SetLanguage(old); }
            var open = new HashSet<string>(map.Locations.Where(l => !map.GetDistrict(l.DistrictId).IsExpansion).Select(l => l.Id));
            var seen = new HashSet<string>();
            var pending = new Queue<string>();
            pending.Enqueue(open.First());
            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                if (!seen.Add(id)) continue;
                foreach (var road in map.RoadsAt(id))
                {
                    string next = road.A == id ? road.B : road.A;
                    if (open.Contains(next) && !seen.Contains(next)) pending.Enqueue(next);
                }
            }
            Assert.AreEqual(open.Count, seen.Count);
        }

        [TestCase("paris", 2, false)]
        [TestCase("paris", 3, false)]
        [TestCase("paris", 5, false)]
        [TestCase("paris", 3, true)]
        [TestCase("berlin", 2, false)]
        [TestCase("berlin", 3, false)]
        [TestCase("berlin", 5, false)]
        [TestCase("berlin", 3, true)]
        public void SeededBotGamesFinish(string key, int seats, bool coop)
        {
            var map = CityHighwaysApp.LoadMap("CityHighways/Maps/" + key);
            for (int seed = 0; seed < 3; seed++)
            {
                var config = new GameConfig();
                if (coop) config.MakeCooperative();
                var game = new Game(map, Enumerable.Range(1, seats).Select(n => "Bot " + n).ToArray(), config, seed);
                var bot = new RandomBot(seed);
                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bot.Act(game);
                    Assert.IsTrue(result.Success, key + " seed " + seed + ": " + result.Error);
                    Assert.Less(++steps, 5000);
                }
                Assert.IsNotNull(game.Result);
            }
        }
    }
}
