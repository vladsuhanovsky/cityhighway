using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Влияние: начисление, траты (рынок, контракты, разрешение, отмена события), сделки, сохранение.</summary>
    public class InfluenceTests
    {
        static Game Fresh(int players = 2) => TestCities.Started(TestCities.Small(), players);

        /// <summary>Передать ход дальше, ничего не меняя для проверок: игрок берёт карту и вторую.</summary>
        static void SkipTurn(Game game)
        {
            int player = game.CurrentPlayerIndex;
            while (game.CurrentPlayerIndex == player && game.Phase != GamePhase.GameOver)
                Assert.IsTrue(game.DrawCardFromDeck().Success);
        }

        // ================================================================== Начисление

        [Test]
        public void FirstRoad_GivesNoDistrictInfluence_NextNewDistrictGivesOne()
        {
            var game = Fresh();
            foreach (var p in game.Players) p.ContractList.Clear(); // чтобы выполненные контракты не мешали подсчёту
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success); // Север — Центр: первая дорога
            Assert.AreEqual(0, game.Players[0].Influence, "Первая дорога за районы не платит");

            SkipTurn(game);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success); // Центр — Юг: новый район
            Assert.AreEqual(1, game.Players[0].Influence);
            Assert.AreEqual(1, game.Players[0].InfluenceEarned);
        }

        [Test]
        public void CompletedContract_GivesInfluenceOnce()
        {
            var game = Fresh();
            foreach (var p in game.Players) p.ContractList.Clear();
            game.Debug_GiveContract(0, "k5"); // Север — Центр
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            Assert.AreEqual(game.Config.InfluencePerContract, game.Players[0].Influence, "Контракт выполнен — +1 (районы за первую дорогу не платят)");

            SkipTurn(game);
            game.Debug_SetHand(0, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r2", Resource.Asphalt, 0).Success); // тот же контракт ещё раз не считается
            Assert.AreEqual(game.Config.InfluencePerContract, game.Players[0].Influence);
        }

        [Test]
        public void ComplexContract_GivesMoreInfluence()
        {
            var game = TestCities.Started(TestCities.SmallWithComplex());
            foreach (var p in game.Players) p.ContractList.Clear();
            game.Debug_GiveContract(0, "k9"); // узел: Центр — Север и Центр — Восток
            game.Debug_GiveRoad(0, "r4");     // Центр — Восток уже есть
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            // r1 — первая дорога, построенная по правилам (Debug_GiveRoad районы не отмечает), за районы не платит.
            Assert.AreEqual(game.Config.InfluencePerComplexContract, game.Players[0].Influence);
        }

        [Test]
        public void Grant_GivesInfluenceInsteadOfPoints()
        {
            var game = Fresh();
            game.Debug_PutLuckOnTop(LuckCardKind.Grant);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(game.Config.LuckGrantInfluence, game.Players[0].Influence);
            Assert.AreEqual(0, game.Players[0].LuckPoints);
        }

        [Test]
        public void Grant_WithInfluenceOff_GivesPoints()
        {
            var config = new GameConfig { InfluenceEnabled = false };
            var game = TestCities.Started(TestCities.Small(), config: config);
            game.Debug_PutLuckOnTop(LuckCardKind.Grant);
            Assert.IsTrue(game.DrawLuckCard().Success);
            Assert.AreEqual(0, game.Players[0].Influence);
            Assert.AreEqual(config.LuckGrant, game.Players[0].LuckPoints);
        }

        [Test]
        public void CatchUp_OnlyForPlayersFarBehind()
        {
            var game = Fresh();
            game.Players[0].RoadPoints = game.Config.InfluenceCatchUpGap; // лидер с отрывом ровно в порог
            SkipTurn(game);
            SkipTurn(game); // новый круг
            Assert.AreEqual(0, game.Players[0].Influence);
            Assert.AreEqual(game.Config.InfluenceCatchUp, game.Players[1].Influence, "Отстающий получает поддержку");

            game.Players[1].RoadPoints = game.Config.InfluenceCatchUpGap - 1; // отрыв меньше порога
            SkipTurn(game);
            SkipTurn(game);
            Assert.AreEqual(game.Config.InfluenceCatchUp, game.Players[1].Influence, "Без большого отрыва — ничего");
        }

        // ================================================================== Траты

        [Test]
        public void RefreshMarket_ReplacesRow_KeepsTurn_OncePerTurn()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 10);
            game.Debug_SetFaceUp(Resource.Gravel, Resource.Gravel, Resource.Gravel, Resource.Gravel, Resource.Gravel);
            int total = game.Debug_CountAllCards();

            Assert.IsTrue(game.UseInfluence(InfluenceAction.RefreshMarket).Success);
            Assert.AreEqual(10 - game.Config.InfluenceRefreshCost, game.Players[0].Influence);
            Assert.AreEqual(0, game.CurrentPlayerIndex, "Ход не тратится");
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            Assert.AreEqual(total, game.Debug_CountAllCards(), "Карты не теряются: старый ряд в сбросе");
            Assert.AreEqual(game.Config.InfluenceRefreshCost, game.Players[0].InfluenceSpent);

            Assert.IsFalse(game.UseInfluence(InfluenceAction.RefreshMarket).Success, "Раз в ход");
            Assert.IsTrue(game.InfluenceUsedThisTurn);
            SkipTurn(game);
            Assert.IsFalse(game.InfluenceUsedThisTurn, "У следующего игрока снова можно");
        }

        [Test]
        public void Spending_WithoutEnoughInfluence_Fails()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, game.Config.InfluenceRefreshCost - 1);
            var result = game.UseInfluence(InfluenceAction.RefreshMarket);
            Assert.IsFalse(result.Success);
            StringAssert.Contains("влияния", result.Error);
        }

        [Test]
        public void ExtraContracts_DrawsOneMore()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 5);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.ExtraContracts).Success);
            Assert.AreEqual(GamePhase.ChoosingContracts, game.Phase);
            int expected = System.Math.Min(game.Config.ContractsDrawn + 1, 6);
            Assert.LessOrEqual(game.Players[0].PendingContracts.Count, expected);
            Assert.Greater(game.Players[0].PendingContracts.Count, 0);
            Assert.AreEqual(5 - game.Config.InfluenceExtraContractCost, game.Players[0].Influence);
        }

        [Test]
        public void Permit_LetsBuildDuringParade_AndIgnoresSnow()
        {
            var game = Fresh();
            game.Debug_StartEvent(CityEventKind.Parade);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsFalse(game.BuildRoad("r1", Resource.Asphalt, 0).Success, "Парад закрывает дороги у Центра");

            Assert.IsFalse(game.CanUseInfluence(InfluenceAction.Permit), "Влияния нет");
            game.Debug_SetInfluence(0, 1);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.Permit).Success);
            Assert.IsTrue(game.PermitActive);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success, "С разрешением можно");
            Assert.IsFalse(game.PermitActive, "Разрешение — только на этот ход");

            // Снег: щебёнка дороже, но не с разрешением.
            var snowy = Fresh();
            snowy.Debug_StartEvent(CityEventKind.Snow);
            var r5 = snowy.Map.GetRoad("r5");
            Assert.AreEqual(2 + snowy.Config.SnowExtraCost, snowy.BuildCost(r5));
            snowy.Debug_SetInfluence(0, 1);
            Assert.IsTrue(snowy.UseInfluence(InfluenceAction.Permit).Success);
            Assert.AreEqual(2, snowy.BuildCost(r5));
            Assert.AreEqual(2 + snowy.Config.SnowExtraCost, snowy.BuildCost(r5, snowy.Players[1]), "Сопернику по-прежнему дороже");
        }

        [Test]
        public void Permit_OnlyDuringParadeOrSnow()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 5);
            Assert.IsFalse(game.CanUseInfluence(InfluenceAction.Permit));
            game.Debug_StartEvent(CityEventKind.Investor);
            Assert.IsFalse(game.CanUseInfluence(InfluenceAction.Permit));
        }

        [Test]
        public void CancelEvent_EndsEventForEveryone()
        {
            var game = Fresh();
            game.Debug_StartEvent(CityEventKind.Repair, "r1");
            Assert.IsTrue(game.IsUnderRepair("r1"));
            game.Debug_SetInfluence(0, game.Config.InfluenceCancelEventCost);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.CancelEvent).Success);
            Assert.IsNull(game.CurrentEvent);
            Assert.IsFalse(game.IsUnderRepair("r1"));
            Assert.AreEqual(0, game.Players[0].Influence);
        }

        // ================================================================== Сделки

        [Test]
        public void Trade_InfluenceForCards()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 4);
            game.Debug_SetHand(1, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 0, Resource.Steel, 2, 3, 0).Success);
            Assert.IsTrue(game.RespondTrade(true).Success);
            Assert.AreEqual(1, game.Players[0].Influence);
            Assert.AreEqual(3, game.Players[1].Influence);
            Assert.AreEqual(2, game.Players[0].CardCount(Resource.Steel));
            Assert.AreEqual(0, game.Players[1].CardCount(Resource.Steel));
        }

        [Test]
        public void Trade_Validation()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt);
            game.Debug_SetInfluence(0, 2);
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 0, Resource.Steel, 1, 0, 0).Success, "Отдать нужно хоть что-то");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, 0, 0).Success, "Получить нужно хоть что-то");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, 3, 1).Success, "Влияния не хватает");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, 0, game.Config.TradeMaxInfluence + 1).Success);
            // Получатель без влияния — сделка сразу не состоялась.
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 0, 0, 2).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            Assert.IsFalse(game.LastTradeAccepted);
        }

        // ================================================================== Сохранение и воспроизведение

        [Test]
        public void Influence_SurvivesSnapshotAndReplay()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            SkipTurn(game);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success);
            Assert.Greater(game.Players[0].Influence, 0);

            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            Assert.AreEqual(game.Players[0].Influence, restored.Players[0].Influence);
            Assert.AreEqual(game.Players[0].InfluenceEarned, restored.Players[0].InfluenceEarned);
            CollectionAssert.AreEquivalent(game.Players[0].InfluenceDistricts, restored.Players[0].InfluenceDistricts);
        }

        [Test]
        public void BotGame_WithInfluence_FinishesAndReplaysExactly()
        {
            var config = new GameConfig();
            var game = TestCities.Started(TestCities.Moscow(), 3, config, seed: 17, events: true);
            var bots = Enumerable.Range(0, 3).Select(i => (IBot)new SmartBot(i + 1)).ToArray();
            for (int step = 0; step < 4000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.Greater(game.Players.Sum(p => p.InfluenceEarned), 0, "Влияние начисляется");
            Assert.Greater(game.Players.Sum(p => p.InfluenceSpent), 0, "Боты тратят влияние");
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
