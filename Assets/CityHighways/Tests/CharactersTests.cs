using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Персонажи и их способности. Карта — TestCities.Small(), события выключены.</summary>
    public class CharactersTests
    {
        static Game WithCharacters(CharacterKind first, CharacterKind second, GameConfig config = null)
        {
            config = config ?? new GameConfig();
            config.EventsEnabled = false;
            var game = new Game(TestCities.Small(), new[] { "Аня", "Борис" }, config, 1, new[] { first, second });
            foreach (var player in game.Players)
                game.KeepInitialContracts(player.Index, player.PendingContracts.Select(c => c.Id).ToList());
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            foreach (var player in game.Players) player.ContractList.Clear();
            return game;
        }

        [Test]
        public void Characters_AssignedAndDefaultNone()
        {
            var game = WithCharacters(CharacterKind.Foreman, CharacterKind.Lucky);
            Assert.AreEqual(CharacterKind.Foreman, game.Players[0].Character);
            Assert.AreEqual(CharacterKind.Lucky, game.Players[1].Character);
            Assert.AreEqual(1, game.Players[0].FreeRoadsLeft);
            Assert.AreEqual(0, game.Players[1].FreeRoadsLeft);

            var plain = TestCities.Started(TestCities.Small());
            Assert.IsTrue(plain.Players.All(p => p.Character == CharacterKind.None));
            Assert.AreEqual(6, Characters.All.Count);
        }

        [Test]
        public void Foreman_BuildsShortRoadForFree_Once()
        {
            var game = WithCharacters(CharacterKind.Foreman, CharacterKind.None);
            game.Debug_SetHand(0);
            Assert.IsTrue(game.CanBuildRoadFree("r1"), "r1 длины 2 — можно");
            Assert.IsFalse(game.CanBuildRoadFree("r4"), "r4 длины 3 — нельзя");
            Assert.IsFalse(game.BuildRoadFree("r4").Success);

            int pieces = game.Players[0].RoadPieces;
            Assert.IsTrue(game.BuildRoadFree("r1").Success);
            Assert.AreEqual(0, game.GetRoadOwner("r1"));
            Assert.AreEqual(pieces - 2, game.Players[0].RoadPieces);
            Assert.AreEqual(game.Config.PointsForLength(2), game.Players[0].RoadPoints);
            Assert.AreEqual(0, game.Players[0].FreeRoadsLeft);
            Assert.AreEqual(1, game.CurrentPlayerIndex, "Бесплатная стройка — это ход");

            Assert.IsFalse(game.BuildRoadFree("r2").Success, "Не Прораб");
            game.Debug_SetHand(1, Resource.Asphalt);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsFalse(game.CanBuildRoadFree("r2"), "Бесплатная стройка была одна");
        }

        [Test]
        public void Lucky_ChoosesOneOfTwoLuckCards()
        {
            var game = WithCharacters(CharacterKind.Lucky, CharacterKind.None);
            game.Debug_PutLuckOnTop(LuckCardKind.Fine, LuckCardKind.Grant);
            int discard = game.LuckDiscardCount;

            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(GamePhase.ChoosingLuck, game.Phase);
            CollectionAssert.AreEqual(new[] { LuckCardKind.Fine, LuckCardKind.Grant }, game.PendingLuckChoice.ToArray());
            Assert.AreEqual(discard + 2, game.LuckDiscardCount, "Обе карты уходят в сброс");

            Assert.IsTrue(game.ChooseLuckCard(1).Success);
            Assert.AreEqual(game.Config.LuckGrant, game.Players[0].LuckPoints);
            Assert.AreEqual(GamePhase.DrawingSecondCard, game.Phase);
            Assert.AreEqual(0, game.PendingLuckChoice.Count);
        }

        [Test]
        public void Lucky_AsSecondCard_EndsTurnAfterChoice()
        {
            var game = WithCharacters(CharacterKind.Lucky, CharacterKind.None);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            game.Debug_PutLuckOnTop(LuckCardKind.Grant, LuckCardKind.Supply);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.IsTrue(game.ChooseLuckCard(0).Success);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void Lucky_BotPicksBetterCard()
        {
            var game = WithCharacters(CharacterKind.Lucky, CharacterKind.None);
            game.Debug_PutLuckOnTop(LuckCardKind.Fine, LuckCardKind.Grant);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.IsTrue(new SmartBot(1).Act(game).Success);
            Assert.AreEqual(game.Config.LuckGrant, game.Players[0].LuckPoints, "Бот оставил грант, а не штраф");
        }

        [Test]
        public void Insurer_DebuffBecomesSmallFine()
        {
            var game = WithCharacters(CharacterKind.Insurer, CharacterKind.None);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            game.Debug_PutLuckOnTop(LuckCardKind.Theft);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(2, game.Players[0].HandSize, "Кража не сработала");
            Assert.AreEqual(-game.Config.InsurerFine, game.Players[0].LuckPoints);
            Assert.AreEqual(1, game.Players[0].LuckDebuffs);
        }

        [Test]
        public void Tollman_ChargesMoreForHisRoads()
        {
            var game = WithCharacters(CharacterKind.None, CharacterKind.Tollman);
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(1, "r3");
            Assert.AreEqual(game.Config.TollPerRoad, game.TollFor("r1"));
            Assert.AreEqual(game.Config.TollmanToll, game.TollFor("r3"));

            game.Debug_GiveContract(0, "k1"); // Север — Юг: своя r1 + чужая r3
            var check = game.CheckContract(0, game.Map.GetContract("k1"));
            Assert.IsTrue(check.Completed);
            Assert.AreEqual(game.Config.TollmanToll, check.Toll);
        }

        [Test]
        public void Official_GetsBonusPerCompletedContract()
        {
            var game = WithCharacters(CharacterKind.Official, CharacterKind.None);
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(0, "r3");
            game.Debug_GiveContract(0, "k1"); // выполнен
            game.Debug_GiveContract(0, "k2"); // не выполнен
            game.Debug_Finish();
            var me = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            Assert.AreEqual(CharacterKind.Official, me.Character);
            Assert.AreEqual(game.Config.OfficialBonusPerContract, me.CharacterPoints);
        }

        [Test]
        public void Bridger_CheaperBridges_AndPointsPerBridge()
        {
            var game = WithCharacters(CharacterKind.Bridger, CharacterKind.None);
            var bridge = game.Map.GetRoad("r3");
            Assert.IsTrue(bridge.IsBridge);
            Assert.AreEqual(bridge.Length - 1, game.BuildCost(bridge));
            Assert.AreEqual(game.Map.GetRoad("r1").Length, game.BuildCost(game.Map.GetRoad("r1")), "Обычные дороги не дешевеют");
            Assert.AreEqual(bridge.Length, game.BuildCost(bridge, game.Players[1]), "Сопернику мост по полной цене");

            game.Debug_SetHand(0, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success, "Мост длины 2 за одну карту стали");
            Assert.AreEqual(0, game.Players[0].HandSize);

            game.Debug_Finish();
            Assert.AreEqual(game.Config.BridgerPointsPerBridge, game.Result.Ranking.First(s => s.PlayerIndex == 0).CharacterPoints);
        }

        [Test]
        public void Save_KeepsCharactersAndLuckChoice()
        {
            var game = WithCharacters(CharacterKind.Lucky, CharacterKind.Foreman);
            game.Debug_PutLuckOnTop(LuckCardKind.Grant, LuckCardKind.Fine);
            Assert.IsTrue(game.DrawLuckCard().Success);

            string json = JsonUtility.ToJson(game.CreateSave());
            var restored = Game.FromSave(game.Map, JsonUtility.FromJson<GameSaveData>(json));
            Assert.AreEqual(json, JsonUtility.ToJson(restored.CreateSave()));
            Assert.AreEqual(CharacterKind.Lucky, restored.Players[0].Character);
            Assert.AreEqual(CharacterKind.Foreman, restored.Players[1].Character);
            Assert.AreEqual(1, restored.Players[1].FreeRoadsLeft);
            Assert.AreEqual(GamePhase.ChoosingLuck, restored.Phase);
            CollectionAssert.AreEqual(new[] { LuckCardKind.Grant, LuckCardKind.Fine }, restored.PendingLuckChoice.ToArray());
            Assert.IsTrue(restored.ChooseLuckCard(0).Success);
        }

        [Test]
        public void SmartBots_WithAllCharacters_PlayLegalGames([Values(0, 1, 2)] int seed)
        {
            var map = TestCities.Moscow();
            var characters = new[] { CharacterKind.Lucky, CharacterKind.Foreman, CharacterKind.Insurer, CharacterKind.Tollman };
            if (seed == 1) characters = new[] { CharacterKind.Official, CharacterKind.Bridger, CharacterKind.Lucky, CharacterKind.Foreman };
            var game = new Game(map, new[] { "А", "Б", "В", "Г" }, null, seed, characters);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(seed * 10 + i)).ToArray();
            int guard = 0;
            while (game.Phase != GamePhase.GameOver)
            {
                var result = bots[game.ActingPlayerIndex].Act(game);
                Assert.IsTrue(result.Success, result.Error);
                Assert.Less(++guard, 6000);
            }
            Assert.AreEqual(game.Config.TotalCards, game.Debug_CountAllCards());
        }
    }
}
