using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class GameTooltipTextTests
    {
        [TestCase("ru", "Кликните, чтобы построить", "Построил: ")]
        [TestCase("en", "Click to build", "Built by: ")]
        public void RoadTextUsesCurrentCoreStateWithoutMutatingIt(string language, string buildHint, string ownerHint)
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage(language);
                var game = TestCities.Started(TestCities.Small());
                var road = game.Map.Roads.First(r => r.Id == "r1");
                game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
                string before = JsonUtility.ToJson(game.CreateSave());
                StringAssert.Contains(buildHint, GameTooltipText.Road(game, road, 0, false, true));
                StringAssert.DoesNotContain(buildHint, GameTooltipText.Road(game, road, 0, false, false));
                Assert.AreEqual(before, JsonUtility.ToJson(game.CreateSave()));
                var result = game.BuildRoad("r1", Resource.Asphalt, 0);
                Assert.IsTrue(result.Success, result.Error);
                string built = JsonUtility.ToJson(game.CreateSave());
                StringAssert.Contains(ownerHint + game.Players[0].Name, GameTooltipText.Road(game, road, 0, false, false));
                Assert.AreEqual(built, JsonUtility.ToJson(game.CreateSave()));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void HandoffHidesPrivateCardsAndContractLines(string language)
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage(language);
                var game = TestCities.Started(TestCities.Small());
                var contract = game.Players[0].Contracts.First();
                var location = game.Map.Locations.First(l => l.DistrictId == contract.DistrictA);
                string hidden = GameTooltipText.Location(game, location, 0, true);
                string visible = GameTooltipText.Location(game, location, 0, false);
                StringAssert.Contains(GameTooltipText.ContractTitle(game.Map, contract), visible);
                StringAssert.DoesNotContain(GameTooltipText.ContractTitle(game.Map, contract), hidden);
                Assert.AreEqual(LocalizationService.Get("game.tooltip.hand_hidden"),
                    GameTooltipText.ResourceCards(game, 0, true, Resource.Asphalt));
                game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
                StringAssert.Contains(language == "en" ? ": 2 cards" : ": 2 шт.",
                    GameTooltipText.ResourceCards(game, 0, false, Resource.Asphalt));
            }
            finally { LocalizationService.SetLanguage(original); }
        }

        [TestCase("ru", "очков")]
        [TestCase("en", "pts")]
        public void PlayerAndPiecesTextUseCoreValues(string language, string scoreLabel)
        {
            string original = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage(language);
                var game = TestCities.Started(TestCities.Small());
                var player = game.Players[0];
                string before = JsonUtility.ToJson(game.CreateSave());
                string text = GameTooltipText.Player(game, 0, " [tag]");
                StringAssert.Contains(player.Name, text);
                StringAssert.Contains(" [tag]", text);
                StringAssert.Contains(game.VisibleScore(player) + " " + scoreLabel, text);
                Assert.AreEqual(string.Format(LocalizationService.Get("game.tooltip.remaining_roads"),
                    player.RoadPieces, game.Config.RoadPiecesFor(game.Players.Count), game.Config.FinalRoundPieces),
                    GameTooltipText.Pieces(game, 0));
                Assert.AreEqual(before, JsonUtility.ToJson(game.CreateSave()));
            }
            finally { LocalizationService.SetLanguage(original); }
        }
    }
}
