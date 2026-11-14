using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Проезд при выполнении контракта, загрузка дорог, пробки и пропуска.</summary>
    public class TrafficTests
    {
        static Game Fresh(GameConfig config = null) => TestCities.Started(TestCities.Small(), 2, config);

        static void ClearContracts(Game game)
        {
            foreach (var player in game.Players) player.ContractList.Clear();
        }

        /// <summary>У игрока 1 мост r3 (Центр — Юг), у игрока 0 контракт k1 (Север — Юг); игрок 0 строит r1 и выполняет его.</summary>
        static Game CompletedThroughBridge(GameConfig config = null, params string[] extraContracts)
        {
            var game = Fresh(config);
            ClearContracts(game);
            game.Debug_GiveRoad(1, "r3");
            game.Debug_GiveContract(0, "k1");
            foreach (var id in extraContracts) game.Debug_GiveContract(0, id);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            return game;
        }

        [Test]
        public void Toll_PaidWhenContractCompleted()
        {
            var game = CompletedThroughBridge();
            var check = game.CheckContract(0, game.Map.GetContract("k1"));
            Assert.IsTrue(check.Completed);
            Assert.IsTrue(check.Paid, "Маршрут зафиксирован");
            Assert.AreEqual(2, game.Players[0].TollsPaid);
            Assert.AreEqual(2, game.Players[1].TollsEarned);
            Assert.AreEqual(1, game.TrafficOn("r3"));
            Assert.AreEqual(1, game.PassageNumber);
            Assert.AreEqual(-2, game.VisibleScore(game.Players[0]) - game.Players[0].RoadPoints, "Проезд виден всем");
        }

        [Test]
        public void Toll_FixedAtCompletion_LaterUpgradeDoesNotChargeMore()
        {
            var game = CompletedThroughBridge();
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            game.Debug_SetHand(1, Resource.Steel);
            Assert.IsTrue(game.UpgradeRoad("r3", Resource.Steel, 0).Success);
            Assert.AreEqual(4, game.TollFor("r3"), "Следующие проезды — по цене магистрали");

            game.Debug_Finish();
            var me = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            var owner = game.Result.Ranking.First(s => s.PlayerIndex == 1);
            Assert.AreEqual(2, me.TollsPaid, "Уже выполненный контракт заплатил по старой цене");
            Assert.AreEqual(2, owner.TollsEarned);
            Assert.AreEqual(5, me.ContractPoints);
        }

        [Test]
        public void Toll_CompletedContractStaysPaid_EvenIfCheaperRouteAppearsLater()
        {
            var game = CompletedThroughBridge();
            // Своя дорога в обход появляется потом — переплачивать назад город не будет.
            game.Debug_GiveRoad(0, "r2");
            game.Debug_GiveRoad(0, "r7");
            game.Debug_GiveRoad(0, "r5");
            game.Debug_Finish();
            Assert.AreEqual(2, game.Result.Ranking.First(s => s.PlayerIndex == 0).TollsPaid);
        }

        [Test]
        public void Jam_NextPassagePaysCity_AndUpgradeClearsIt()
        {
            var config = new GameConfig { JamThreshold = 1, JamFee = 2 };
            var game = Fresh(config);
            ClearContracts(game);
            game.Debug_GiveRoad(1, "r3");
            game.Debug_GiveRoad(0, "r6");
            game.Debug_GiveContract(0, "k1"); // Север — Юг через r1 и чужой r3
            game.Debug_GiveContract(0, "k6"); // Центр — Юг через чужой r3 и свой r6
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);

            var p0 = game.Players[0];
            Assert.AreEqual(2, game.TrafficOn("r3"));
            Assert.IsTrue(game.IsJammed("r3"));
            Assert.AreEqual(4, p0.TollsPaid, "Владельцу — обычная плата за оба проезда");
            Assert.AreEqual(2, p0.JamFees, "Второй проезд уже в пробке: ещё 2 городу");
            Assert.AreEqual(4, game.Players[1].TollsEarned, "Пробочный сбор владельцу не достаётся");
            Assert.AreEqual(4, game.PassageCost("r3", 0));

            game.Debug_SetHand(1, Resource.Steel);
            Assert.IsTrue(game.UpgradeRoad("r3", Resource.Steel, 0).Success);
            Assert.IsFalse(game.IsJammed("r3"), "Магистраль в пробку не встаёт");
            Assert.AreEqual(0, game.JamFeeFor("r3"));

            game.Debug_Finish();
            var score = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            Assert.AreEqual(2, score.JamFees);
            Assert.AreEqual(score.RoadPoints + score.ContractPoints - 4 - 2 + score.NetworkBonus + score.GoalPoints + score.LuckPoints
                            + score.CharacterPoints + score.MayorBonus + score.Penalties, score.Total);
        }

        [Test]
        public void Jam_DefaultThreshold_SingleRouteDoesNotJam()
        {
            var game = CompletedThroughBridge();
            Assert.AreEqual(2, game.Config.JamThreshold);
            Assert.IsFalse(game.IsJammed("r3"));
            Assert.AreEqual(0, game.Players[0].JamFees);
        }

        [Test]
        public void Jam_CheaperRouteAvoidsJam()
        {
            var config = new GameConfig { JamThreshold = 1, JamFee = 5 };
            var game = Fresh(config);
            ClearContracts(game);
            game.Debug_GiveRoad(1, "r3");
            game.Debug_GiveContract(0, "k1");
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success); // r3 теперь в пробке
            Assert.IsTrue(game.IsJammed("r3"));

            // k6 Центр — Юг: через пробку r3 (2 + 5) или по чужим r4 и r5 (2 + 2) со своей r6.
            game.Debug_GiveRoad(1, "r4");
            game.Debug_GiveRoad(1, "r5");
            game.Debug_GiveRoad(0, "r6");
            var check = game.CheckContract(0, game.Map.GetContract("k6"));
            Assert.IsTrue(check.Completed);
            Assert.IsFalse(check.ForeignRoads.Any(r => r.Id == "r3"), "Объезд дешевле пробки");
            Assert.AreEqual(4, check.Toll);
            Assert.AreEqual(0, check.JamFee);
        }

        [Test]
        public void Pass_BoughtInTrade_MakesTollFree()
        {
            var game = Fresh();
            ClearContracts(game);
            game.Debug_GiveRoad(1, "r3");
            game.Debug_GiveContract(0, "k1");
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt, Resource.Concrete);
            game.Debug_SetHand(1);

            Assert.IsTrue(game.ProposeTrade(1, Resource.Concrete, 1, Resource.Asphalt, 0, getPass: true).Success);
            Assert.IsTrue(game.PendingTrade.GetPass);
            Assert.IsTrue(game.RespondTrade(true).Success);
            Assert.IsTrue(game.HasPass(0, 1));
            Assert.IsFalse(game.HasPass(1, 0), "Пропуск в одну сторону");
            Assert.AreEqual(1, game.Players[1].CardCount(Resource.Concrete));
            Assert.AreEqual(0, game.TollFor("r3", 0));
            Assert.AreEqual(2, game.TollFor("r3"), "Остальные платят как обычно");

            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            Assert.AreEqual(0, game.Players[0].TollsPaid);
            Assert.AreEqual(1, game.TrafficOn("r3"), "Проезд по пропуску — тоже загрузка");
        }

        [Test]
        public void Pass_CannotBeSoldTwice_AndCountsAsTradeSide()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt);
            game.Debug_SetHand(1, Resource.Steel);
            // Пропуск на пропуск — можно: каждая сторона что-то отдаёт.
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 0, Resource.Steel, 0, givePass: true, getPass: true).Success);
            Assert.IsTrue(game.RespondTrade(true).Success);
            Assert.IsTrue(game.HasPass(0, 1) && game.HasPass(1, 0));

            game.Debug_ResetTradeTurn();
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, getPass: true).Success, "Пропуск уже есть");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, givePass: true).Success, "У соперника уже есть ваш пропуск");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 0, Resource.Steel, 1).Success, "Пустая сторона");
        }

        [Test]
        public void Pass_Disabled_Refused()
        {
            var game = Fresh(new GameConfig { PassesEnabled = false });
            game.Debug_SetHand(0, Resource.Asphalt);
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, getPass: true).Success);
        }

        [Test]
        public void Save_KeepsPaidRoutesTrafficAndPasses()
        {
            var config = new GameConfig { JamThreshold = 1 };
            var game = CompletedThroughBridge(config);
            game.Debug_ResetTradeTurn();
            game.Debug_SetHand(1, Resource.Steel);
            Assert.IsTrue(game.ProposeTrade(0, Resource.Steel, 1, Resource.Asphalt, 0, getPass: true).Success);
            Assert.IsTrue(game.RespondTrade(true).Success);

            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            Assert.AreEqual(game.Players[0].TollsPaid, restored.Players[0].TollsPaid);
            Assert.AreEqual(game.Players[1].TollsEarned, restored.Players[1].TollsEarned);
            Assert.AreEqual(game.TrafficOn("r3"), restored.TrafficOn("r3"));
            Assert.AreEqual(game.IsJammed("r3"), restored.IsJammed("r3"));
            Assert.IsTrue(restored.HasPass(1, 0));
            var route = restored.PaidRouteOf(0, game.Map.GetContract("k1"));
            Assert.IsNotNull(route);
            Assert.AreEqual("r3", route.Passages.Single().RoadId);
            CollectionAssert.AreEqual(game.PaidRouteOf(0, game.Map.GetContract("k1")).Path, route.Path);
        }

        [Test]
        public void BotGames_TollsBalance_AndReplayExactly()
        {
            var game = TestCities.Started(TestCities.Moscow(), 4, new GameConfig(), seed: 23, events: true);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(i + 7)).ToArray();
            for (int step = 0; step < 5000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.AreEqual(game.Players.Sum(p => p.TollsPaid), game.Players.Sum(p => p.TollsEarned));
            Assert.AreEqual(game.Result.Ranking.Sum(s => s.JamFees), game.Players.Sum(p => p.JamFees));
            foreach (var player in game.Players)
                foreach (var contract in player.Contracts)
                    Assert.AreEqual(player.PaidRouteList.ContainsKey(contract.Id), game.CheckContract(player.Index, contract).Completed,
                        "Выполненный контракт всегда оплачен");
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
