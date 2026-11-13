using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Общие цели мэрии и колода удачи. Карта — TestCities.SmallWithGoals().</summary>
    public class GoalsAndLuckTests
    {
        static Game Fresh(GameConfig config = null) => TestCities.Started(TestCities.SmallWithGoals(), 2, config);

        static void ClearContracts(Game game)
        {
            foreach (var player in game.Players) player.ContractList.Clear();
        }

        // ================================================================== Цели мэрии

        [Test]
        public void Goals_DealtFromMapPool()
        {
            var game = TestCities.Started(TestCities.Moscow(), 3);
            Assert.AreEqual(7, game.Map.Goals.Count);
            Assert.AreEqual(game.Config.MayorGoalsDealt, game.ActiveGoals.Count);
            Assert.AreEqual(game.ActiveGoals.Count, game.ActiveGoals.Select(g => g.Id).Distinct().Count());
            Assert.IsTrue(game.ActiveGoals.All(g => !game.GoalOwner(g.Id).HasValue));

            var heart = game.Map.GetGoal("g6");
            Assert.AreEqual("center", heart.District);
            Assert.AreEqual(game.Map.LocationsIn("center").Count, heart.Count, "«Сердце Москвы» — все места Центра");
        }

        [Test]
        public void Goal_FirstBuilderTakesReward_SecondGetsNothing()
        {
            var game = Fresh();
            game.Debug_SetGoals("t3");

            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete, Resource.Concrete);
            Assert.IsTrue(game.BuildRoad("r4", Resource.Concrete, 0).Success); // длина 3
            Assert.AreEqual(0, game.GoalOwner("t3"));
            Assert.AreEqual(3, game.Players[0].GoalPoints);

            game.Debug_SetHand(1, Resource.Concrete, Resource.Concrete, Resource.Concrete, Resource.Concrete);
            Assert.IsTrue(game.BuildRoad("r7", Resource.Concrete, 0).Success); // длина 4 — тоже подходит, но поздно
            Assert.AreEqual(0, game.GoalOwner("t3"), "Цель достаётся только первому");
            Assert.AreEqual(0, game.Players[1].GoalPoints);
        }

        [Test]
        public void Goal_Locations_CountsPlacesOfDistrict()
        {
            var game = Fresh();
            game.Debug_SetGoals("t1");
            var goal = game.Map.GetGoal("t1");

            game.Debug_GiveRoad(0, "r1"); // n1
            game.Debug_CheckGoals(0);
            Assert.AreEqual(1, game.GoalProgress(0, goal));
            Assert.IsFalse(game.GoalOwner("t1").HasValue);

            game.Debug_GiveRoad(0, "r2"); // n2
            game.Debug_CheckGoals(0);
            Assert.AreEqual(2, game.GoalProgress(0, goal));
            Assert.AreEqual(0, game.GoalOwner("t1"));
        }

        [Test]
        public void Goal_Bridges_And_FinalScore()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_SetGoals("t2");
            game.Debug_SetHand(0, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success);

            game.Debug_Finish();
            var p0 = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            Assert.AreEqual(4, p0.GoalPoints);
            Assert.AreEqual(p0.RoadPoints + p0.NetworkBonus + 4, p0.Total);
        }

        [Test]
        public void Goal_Kinds_Progress()
        {
            var map = TestCities.Moscow();
            var roads = map.Roads.Where(r => r.Resource == Resource.Gravel).Take(3).ToList();
            var gravel = new MayorGoal("x", "x", GoalKind.ResourcePieces, 4, 1, resource: Resource.Gravel);
            Assert.AreEqual(roads.Sum(r => r.Length), gravel.Progress(map, roads));
            var network = new MayorGoal("y", "y", GoalKind.Network, 2, 1);
            Assert.AreEqual(Scoring.NetworkDistricts(map, roads), network.Progress(map, roads));
        }

        // ================================================================== Удача

        [Test]
        public void Luck_ReplacesOneOfTwoCards_OnlyOncePerTurn()
        {
            var game = Fresh();
            game.Debug_PutLuckOnTop(LuckCardKind.Grant);
            Assert.IsTrue(game.CanDrawLuckCard());

            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(GamePhase.DrawingSecondCard, game.Phase, "После удачи берут ещё одну карту ресурса");
            Assert.AreEqual(5, game.Players[0].LuckPoints);
            Assert.AreEqual(LuckCardKind.Grant, game.LastLuckCard);
            Assert.AreEqual(1, game.Players[0].LuckBuffs);
            Assert.AreEqual(0, game.Players[0].LuckDebuffs);

            Assert.IsFalse(game.CanDrawLuckCard());
            Assert.IsFalse(game.DrawLuckCard().Success, "Вторая карта удачи за ход запрещена");

            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            Assert.IsTrue(game.CanDrawLuckCard(), "У следующего игрока снова можно");
        }

        [Test]
        public void Luck_AsSecondCard_EndsTurn()
        {
            var game = Fresh();
            game.Debug_PutLuckOnTop(LuckCardKind.Fine);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            Assert.AreEqual(-3, game.Players[0].LuckPoints);
            Assert.AreEqual(1, game.Players[0].LuckDebuffs);
        }

        [Test]
        public void Luck_Supply_GivesCards_CardsNotLost()
        {
            var game = Fresh();
            int hand = game.Players[0].HandSize;
            game.Debug_PutLuckOnTop(LuckCardKind.Supply);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(hand + game.Config.LuckSupply, game.Players[0].HandSize);
            Assert.AreEqual(game.Config.TotalCards, game.Debug_CountAllCards());
        }

        [Test]
        public void Luck_Theft_DiscardsCardFromHand()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            int total = game.Debug_CountAllCards();
            int discard = game.DiscardCount;
            game.Debug_PutLuckOnTop(LuckCardKind.Theft);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(1, game.Players[0].HandSize);
            Assert.AreEqual(discard + 1, game.DiscardCount);
            Assert.AreEqual(total, game.Debug_CountAllCards());
        }

        [Test]
        public void Luck_Premium_PaysForCompletedContracts()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(0, "r3");
            game.Debug_GiveContract(0, "k1"); // выполнен
            game.Debug_GiveContract(0, "k2"); // не выполнен
            game.Debug_PutLuckOnTop(LuckCardKind.Premium);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(game.Config.LuckPremiumPerContract, game.Players[0].LuckPoints);
        }

        [Test]
        public void Luck_Recall_RemovesCheapestOpenContract()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_GiveContract(0, "k1"); // 5 очков
            game.Debug_GiveContract(0, "k2"); // 6 очков
            int deck = game.ContractDeckCount;
            game.Debug_PutLuckOnTop(LuckCardKind.Recall);
            Assert.IsTrue(game.DrawLuckCard().Success);

            CollectionAssert.AreEqual(new[] { "k2" }, game.Players[0].Contracts.Select(c => c.Id).ToArray());
            Assert.AreEqual(deck + 1, game.ContractDeckCount, "Контракт ушёл под колоду");
            Assert.AreEqual(0, game.Players[0].LuckPoints, "Без штрафа");
        }

        [Test]
        public void Luck_Recall_WithoutOpenContracts_Fines()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_PutLuckOnTop(LuckCardKind.Recall);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(-game.Config.LuckFine, game.Players[0].LuckPoints);
        }

        [Test]
        public void Luck_NeedsResourceCardsToReplace()
        {
            var game = Fresh();
            game.Debug_EmptyCardSupply();
            Assert.IsFalse(game.CanDrawLuckCard());
            Assert.IsFalse(game.DrawLuckCard().Success);
        }

        [Test]
        public void Luck_Disabled()
        {
            var game = Fresh(new GameConfig { LuckEnabled = false });
            Assert.AreEqual(0, game.LuckDeckCount);
            Assert.IsFalse(game.CanDrawLuckCard());
        }

        [Test]
        public void Luck_DeckComposition()
        {
            var game = Fresh();
            var config = game.Config;
            int expected = Enumerable.Range(0, LuckInfo.KindCount).Sum(k => LuckInfo.CountIn(config, (LuckCardKind)k));
            Assert.AreEqual(18, expected);
            Assert.AreEqual(expected, game.LuckDeckCount);
        }

        [Test]
        public void Final_LuckPointsCount()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_PutLuckOnTop(LuckCardKind.Grant);
            Assert.IsTrue(game.DrawLuckCard().Success);
            game.Debug_Finish();
            var p0 = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            Assert.AreEqual(5, p0.LuckPoints);
            Assert.AreEqual(p0.RoadPoints + p0.NetworkBonus + 5, p0.Total);
        }

        [Test]
        public void Luck_OddsCountRemainingCards_AndReshuffle()
        {
            var game = Fresh();
            Assert.AreEqual(11, game.LuckBuffsLeft);
            Assert.AreEqual(7, game.LuckDebuffsLeft);

            game.Debug_PutLuckOnTop(LuckCardKind.Fine); // 19 карт, сверху дебаф
            Assert.AreEqual(8, game.LuckDebuffsLeft);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(7, game.LuckDebuffsLeft, "Вышедшая карта больше не считается");
            Assert.AreEqual(18, game.LuckDeckCount);
            Assert.AreEqual(1, game.LuckDiscardCount);

            // Выбираем колоду до конца: тогда шансы считаются по сбросу, который перемешается.
            while (game.LuckDeckCount > 0)
            {
                game.Debug_ResetLuckTurn();
                Assert.IsTrue(game.DrawLuckCard().Success);
            }
            Assert.IsTrue(game.LuckWillReshuffle);
            Assert.AreEqual(19, game.LuckBuffsLeft + game.LuckDebuffsLeft, "Шансы — по сбросу из всех 19 карт");
            Assert.AreEqual(8, game.LuckDebuffsLeft);
        }

        // ================================================================== Сохранение

        [Test]
        public void Save_KeepsGoalsAndLuck()
        {
            var map = TestCities.SmallWithGoals();
            var game = TestCities.Started(map);
            game.Debug_SetGoals("t2", "t3");
            game.Debug_PutLuckOnTop(LuckCardKind.Grant);
            Assert.IsTrue(game.DrawLuckCard().Success);
            game.Debug_GiveRoad(1, "r3");
            game.Debug_CheckGoals(1);

            string json = JsonUtility.ToJson(game.CreateSave());
            var restored = Game.FromSave(map, JsonUtility.FromJson<GameSaveData>(json));

            Assert.AreEqual(json, JsonUtility.ToJson(restored.CreateSave()));
            CollectionAssert.AreEqual(new[] { "t2", "t3" }, restored.ActiveGoals.Select(g => g.Id).ToArray());
            Assert.AreEqual(1, restored.GoalOwner("t2"));
            Assert.AreEqual(4, restored.Players[1].GoalPoints);
            Assert.AreEqual(5, restored.Players[0].LuckPoints);
            Assert.AreEqual(1, restored.Players[0].LuckBuffs);
            Assert.IsTrue(restored.LuckTakenThisTurn);
            Assert.AreEqual(game.LuckDeckCount, restored.LuckDeckCount);
            Assert.AreEqual(GamePhase.DrawingSecondCard, restored.Phase);
        }

        [Test]
        public void Save_Version2_LoadsWithFreshGoalsAndLuck()
        {
            var map = TestCities.SmallWithGoals();
            var save = TestCities.Started(map).CreateSave();
            save.version = 2;
            save.goals = null;
            save.luckDeck = null;
            save.luckDiscard = null;

            var restored = Game.FromSave(map, save, 3);
            Assert.AreEqual(3, restored.ActiveGoals.Count);
            Assert.AreEqual(18, restored.LuckDeckCount);
        }

        [Test]
        public void Save_UnknownVersion_Throws()
        {
            var map = TestCities.SmallWithGoals();
            var save = TestCities.Started(map).CreateSave();
            save.version = 1;
            Assert.Throws<System.FormatException>(() => Game.FromSave(map, save));
        }
    }
}
