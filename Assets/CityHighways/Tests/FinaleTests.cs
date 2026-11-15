using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Финал: «скоро финал», счётчик ходов, финальный заказ мэрии, проверка взяток перед итогами.</summary>
    public class FinaleTests
    {
        static Game Fresh(int players = 2, GameConfig config = null) => TestCities.Started(TestCities.Small(), players, config);

        static void SkipTurn(Game game)
        {
            int player = game.CurrentPlayerIndex;
            while (game.CurrentPlayerIndex == player && game.Phase != GamePhase.GameOver)
                Assert.IsTrue(game.DrawCardFromDeck().Success);
        }

        /// <summary>У игрока 0 дорога r1 и 4 участка, у игрока 1 — r6 и 10; игрок 0 строит r3 и запускает последний круг.</summary>
        static Game StartFinalRound()
        {
            var game = Fresh();
            foreach (var p in game.Players) p.ContractList.Clear();
            game.Debug_GiveRoad(0, "r1");
            game.Debug_GiveRoad(1, "r6");
            game.Debug_SetPieces(0, 4);
            game.Debug_SetPieces(1, 10);
            game.Debug_SetHand(0, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success);
            return game;
        }

        [Test]
        public void Warning_WhenSomeoneNearsTheEnd_Once()
        {
            var game = Fresh();
            foreach (var p in game.Players) p.ContractList.Clear();
            var log = new System.Collections.Generic.List<string>();
            game.Log += log.Add;
            game.Debug_SetPieces(0, 7);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            Assert.IsTrue(game.FinalWarned);
            Assert.AreEqual(1, game.FinalWarningNumber);
            Assert.IsFalse(game.IsFinalRound);
            Assert.AreEqual(0, game.FinalTurnsLeft);
            Assert.AreEqual(5, game.FewestPieces);
            Assert.IsTrue(log.Any(l => l.Contains("Скоро финал")));

            SkipTurn(game);
            SkipTurn(game);
            Assert.AreEqual(1, game.FinalWarningNumber, "Объявляется один раз");
        }

        [Test]
        public void FinalRound_CountsTurns_AndOpensOrderOnContestedRoad()
        {
            var game = StartFinalRound();
            Assert.IsTrue(game.IsFinalRound);
            Assert.AreEqual(2, game.FinalTurnsLeft, "Ход соперника и ещё один ход того, кто начал финал");
            Assert.AreEqual("r5", game.FinalOrderRoadId, "Дорога рядом с сетью соперника, на которую хватает участков");
            Assert.AreEqual(-1, game.FinalOrderWinner);
            Assert.AreEqual(1, game.FinalOrderNumber);

            game.Debug_SetHand(1, Resource.Gravel, Resource.Gravel);
            int goals = game.Players[1].GoalPoints;
            Assert.IsTrue(game.BuildRoad("r5", Resource.Gravel, 0).Success);
            Assert.AreEqual(1, game.FinalOrderWinner);
            Assert.AreEqual(goals + game.Config.FinalOrderBonus, game.Players[1].GoalPoints);
            Assert.AreEqual(2, game.FinalOrderNumber);
            Assert.AreEqual(1, game.FinalTurnsLeft, "Остался последний ход");

            SkipTurn(game);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            var score = game.Result.Ranking.First(s => s.PlayerIndex == 1);
            Assert.AreEqual(game.Config.FinalOrderBonus, score.GoalPoints);
        }

        [Test]
        public void FinalOrder_NotBuilt_NobodyGetsBonus()
        {
            var game = StartFinalRound();
            SkipTurn(game);
            SkipTurn(game);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.AreEqual(-1, game.FinalOrderWinner);
            Assert.IsTrue(game.Result.Ranking.All(s => s.GoalPoints == 0));
        }

        [Test]
        public void FinalOrder_Disabled()
        {
            var game = Fresh(2, new GameConfig { FinalOrderBonus = 0 });
            foreach (var p in game.Players) p.ContractList.Clear();
            game.Debug_GiveRoad(1, "r6");
            game.Debug_SetPieces(0, 2);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            Assert.IsTrue(game.IsFinalRound);
            Assert.IsNull(game.FinalOrderRoadId);
        }

        [Test]
        public void Audit_ExposesUnrevealedBribes_BeforeScoring()
        {
            var game = Fresh(3, new GameConfig { FinalAuditPercent = 100 });
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            game.Debug_Finish();
            Assert.AreEqual(1, game.FinalAudit.Count);
            Assert.AreEqual(0, game.FinalAudit[0].Player);
            Assert.AreEqual(1, game.FinalAudit[0].Exposed);
            int fine = game.Config.BribeFineFor(3);
            Assert.AreEqual(fine, game.FinalAudit[0].Fine);
            Assert.AreEqual(-fine, game.Result.Ranking.First(s => s.PlayerIndex == 0).Penalties);
            Assert.AreEqual(1, game.Players[0].BribesExposed);
        }

        [Test]
        public void Audit_Off_OrNoBribes_DoesNothing()
        {
            var off = Fresh(3, new GameConfig { FinalAuditPercent = 0 });
            off.Debug_SetInfluence(0, 6);
            Assert.IsTrue(off.Bribe(BribeKind.ExtraCard).Success);
            off.Debug_Finish();
            Assert.AreEqual(0, off.FinalAudit.Count);
            Assert.AreEqual(0, off.Players[0].PenaltyPoints);

            var clean = Fresh(3);
            clean.Debug_Finish();
            Assert.AreEqual(0, clean.FinalAudit.Count);
        }

        [Test]
        public void Save_KeepsFinale()
        {
            var game = StartFinalRound();
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            Assert.AreEqual("r5", restored.FinalOrderRoadId);
            Assert.AreEqual(-1, restored.FinalOrderWinner);
            Assert.AreEqual(game.FinalTurnsLeft, restored.FinalTurnsLeft);
            Assert.AreEqual(game.FinalWarned, restored.FinalWarned);

            restored.Debug_SetHand(1, Resource.Gravel, Resource.Gravel);
            Assert.IsTrue(restored.BuildRoad("r5", Resource.Gravel, 0).Success);
            Assert.AreEqual(1, restored.FinalOrderWinner);
        }

        [Test]
        public void BotGame_HasFinalOrder_AndReplaysExactly()
        {
            var game = TestCities.Started(TestCities.Moscow(), 4, new GameConfig(), seed: 41, events: true);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(i + 3)).ToArray();
            for (int step = 0; step < 5000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.IsTrue(game.FinalWarned, "Перед финалом было предупреждение");
            Assert.IsNotNull(game.FinalOrderRoadId, "Мэрия выставила финальный заказ");
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
