using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Взятки и разоблачения: эффект, тайна, окно раскрытия, штрафы, награда, сохранение.</summary>
    public class BribeTests
    {
        static Game Fresh(int players = 3) => TestCities.Started(TestCities.Small(), players);

        static void SkipTurn(Game game)
        {
            int player = game.CurrentPlayerIndex;
            while (game.CurrentPlayerIndex == player && game.Phase != GamePhase.GameOver)
                Assert.IsTrue(game.DrawCardFromDeck().Success);
        }

        [Test]
        public void DiscountBribe_MakesRoadsCheaperThisTurnOnly()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 5);
            var r4 = game.Map.GetRoad("r4"); // бетон, длина 3
            Assert.AreEqual(3, game.BuildCost(r4));
            Assert.IsTrue(game.Bribe(BribeKind.Discount).Success);
            Assert.AreEqual(2, game.BuildCost(r4));
            Assert.AreEqual(3, game.BuildCost(r4, game.Players[1]), "Сопернику скидки нет");
            Assert.AreEqual(1, game.BuildCost(game.Map.GetRoad("r2")), "Не меньше 1 карты");
            Assert.AreEqual(5 - game.Config.BribeDiscountCost, game.Players[0].Influence);

            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete);
            Assert.IsTrue(game.BuildRoad("r4", Resource.Concrete, 0).Success, "Хватает двух карт вместо трёх");
            Assert.IsFalse(game.BribeDiscountActive, "Скидка — только на этот ход");
        }

        [Test]
        public void ExtraCardBribe_AddsCard_KeepsTurn_OncePerTurn()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 5);
            int hand = game.Players[0].HandSize;
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            Assert.AreEqual(hand + 1, game.Players[0].HandSize);
            Assert.AreEqual(0, game.CurrentPlayerIndex);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            Assert.IsFalse(game.Bribe(BribeKind.Discount).Success, "Одна взятка за ход");
            Assert.AreEqual(1, game.ActiveBribes);
            Assert.AreEqual(1, game.WhisperNumber);
        }

        [Test]
        public void Bribe_IsSecretInLog()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 5);
            string log = null;
            game.Log += text => log = text;
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            StringAssert.Contains("кто-то", log);
            StringAssert.DoesNotContain(game.Players[0].Name, log, "Имя давшего не раскрывается");
        }

        [Test]
        public void CorrectAccusation_FinesBriber_RewardsAccuser()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.IsTrue(game.LastAccusation.Correct);
            Assert.AreEqual(-game.Config.BribeFine, game.Players[0].PenaltyPoints);
            Assert.AreEqual(game.Config.BribeWhistleReward, game.Players[1].Influence);
            Assert.AreEqual(0, game.ActiveBribes, "Раскрытая взятка больше не висит");
            Assert.IsFalse(game.Accuse(2).Success, "Одно обвинение за ход");
            Assert.AreEqual(1, game.CurrentPlayerIndex, "Обвинение не тратит ход");
        }

        [Test]
        public void WrongAccusation_FinesAccuser()
        {
            var game = Fresh();
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.IsFalse(game.LastAccusation.Correct);
            Assert.AreEqual(-game.Config.FalseAccusationFine, game.Players[1].PenaltyPoints);
            Assert.AreEqual(0, game.Players[0].PenaltyPoints);
        }

        [Test]
        public void BribeTraces_DisappearWhenBriberStartsNextTurn()
        {
            var game = Fresh(2);
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            SkipTurn(game); // ход игрока 0 закончился
            Assert.AreEqual(1, game.ActiveBribes, "Сопернику ещё можно раскрыть");
            SkipTurn(game); // игрок 1 не обвинил — снова ход игрока 0
            Assert.AreEqual(0, game.ActiveBribes, "Следы пропали");
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.IsFalse(game.LastAccusation.Correct, "Поздно: обвинение уже ложное");
        }

        [Test]
        public void AccusationFinesEveryActiveBribe()
        {
            var game = Fresh(2);
            game.Debug_SetInfluence(0, 10);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            SkipTurn(game);
            SkipTurn(game);
            // Новый ход игрока 0: старая взятка пропала, даём новую.
            Assert.IsTrue(game.Bribe(BribeKind.Discount).Success);
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.AreEqual(1, game.LastAccusation.Exposed);
            Assert.AreEqual(-game.Config.BribeFine, game.Players[0].PenaltyPoints);
        }

        [Test]
        public void Penalties_CountInFinalScore()
        {
            var game = Fresh();
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success); // ложное обвинение: игрок 1 теряет очки
            game.Debug_Finish();
            var score = game.Result.Ranking.First(s => s.PlayerIndex == 1);
            Assert.AreEqual(-game.Config.FalseAccusationFine, score.Penalties);
        }

        [Test]
        public void Bribes_SurviveSave()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.Discount).Success);
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            Assert.AreEqual(1, restored.ActiveBribes);
            Assert.IsTrue(restored.BribeDiscountActive);
            Assert.IsTrue(restored.BribedThisTurn);
            Assert.AreEqual(2, restored.BuildCost(restored.Map.GetRoad("r4")));
        }

        [Test]
        public void BotGame_WithBribes_ReplaysExactly()
        {
            var game = TestCities.Started(TestCities.Moscow(), 4, new GameConfig(), seed: 31, events: true);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(i + 11)).ToArray();
            for (int step = 0; step < 5000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
