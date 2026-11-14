using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>
    /// Взятки и разоблачения: эффект, тайна (объявление числом в начале следующего круга), окно раскрытия
    /// до конца следующего круга, улики, штрафы по числу игроков, взяток нет в дуэли, сохранение.
    /// </summary>
    public class BribeTests
    {
        static Game Fresh(int players = 3) => TestCities.Started(TestCities.Small(), players);

        static void SkipTurn(Game game)
        {
            int player = game.CurrentPlayerIndex;
            while (game.CurrentPlayerIndex == player && game.Phase != GamePhase.GameOver)
                Assert.IsTrue(game.DrawCardFromDeck().Success);
        }

        /// <summary>Доиграть ходами «взять карты» до начала следующего круга.</summary>
        static void SkipToNextRound(Game game)
        {
            int round = game.RoundNumber;
            while (game.RoundNumber == round && game.Phase != GamePhase.GameOver) SkipTurn(game);
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
            Assert.IsTrue(game.LooksSuspicious(0), "Улика: дорога дешевле своей длины");
            StringAssert.Contains("дешевле", game.SuspicionText(0));
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
            Assert.AreEqual(0, game.WhisperNumber, "Сразу никто ничего не узнаёт");
            Assert.AreEqual(0, game.AnnouncedBribes);

            SkipTurn(game); // взятая карта + две обычные = рука выросла на 3
            Assert.IsTrue(game.LooksSuspicious(0));
            StringAssert.Contains("рука", game.SuspicionText(0));
            Assert.IsFalse(game.LooksSuspicious(1));
            Assert.AreEqual("", game.SuspicionText(1));
        }

        [Test]
        public void Bribe_IsSilent_ThenAnnouncedNextRoundWithoutName()
        {
            var game = Fresh();
            game.Debug_SetInfluence(1, 5);
            var log = new System.Collections.Generic.List<string>();
            game.Log += log.Add;
            SkipTurn(game);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            int before = log.Count;
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            Assert.AreEqual(before, log.Count, "В момент взятки в журнале ничего");

            SkipToNextRound(game);
            Assert.AreEqual(1, game.WhisperNumber);
            Assert.AreEqual(1, game.LastWhisperCount);
            Assert.AreEqual(1, game.AnnouncedBribes);
            var whisper = log.Last(l => l.Contains("шептались"));
            StringAssert.Contains("1", whisper);
            foreach (var player in game.Players) StringAssert.DoesNotContain(player.Name, whisper, "Имён нет");
        }

        [Test]
        public void CorrectAccusation_FinesBriber_RewardsAccuser()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success, "Угадать взятку можно и до объявления");
            Assert.IsTrue(game.LastAccusation.Correct);
            Assert.AreEqual(-game.Config.BribeFineFor(3), game.Players[0].PenaltyPoints);
            Assert.AreEqual(6, game.Config.BribeFineFor(3));
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
        public void Bribe_CanBeExposedUntilEndOfNextRound()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success); // круг 1
            SkipToNextRound(game);                                  // круг 2: объявлено
            Assert.AreEqual(1, game.ActiveBribes, "Ход давшего не стирает следы");
            SkipTurn(game);
            SkipTurn(game);
            Assert.AreEqual(1, game.ActiveBribes, "До конца круга 2 ещё можно раскрыть");
            SkipToNextRound(game);                                  // круг 3
            Assert.AreEqual(0, game.ActiveBribes, "Поздно: следы пропали");
            Assert.AreEqual(0, game.AnnouncedBribes);
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.IsFalse(game.LastAccusation.Correct, "Обвинение уже ложное");
        }

        [Test]
        public void AccusationFinesEveryActiveBribe()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 10);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success); // круг 1
            SkipToNextRound(game);
            Assert.IsTrue(game.Bribe(BribeKind.Discount).Success);   // круг 2, снова игрок 0
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.AreEqual(2, game.LastAccusation.Exposed, "Раскрываются обе: прошлого круга и этого");
            Assert.AreEqual(-2 * game.Config.BribeFineFor(3), game.Players[0].PenaltyPoints);
        }

        [Test]
        public void Duel_HasNoBribes()
        {
            var game = Fresh(2);
            game.Debug_SetInfluence(0, 10);
            Assert.IsFalse(game.BribesAllowed);
            Assert.IsFalse(game.Bribe(BribeKind.ExtraCard).Success);
            Assert.IsNotNull(game.GetBribeBlocker(BribeKind.Discount));
            Assert.IsFalse(game.Accuse(1).Success);
            Assert.AreEqual(0, game.AnnouncedBribes);
        }

        [Test]
        public void Fine_DependsOnPlayerCount()
        {
            var config = new GameConfig();
            Assert.AreEqual(6, config.BribeFineFor(3));
            Assert.AreEqual(4, config.BribeFineFor(4));
            Assert.AreEqual(4, config.BribeFineFor(5));
            config.BribeFine = 5;
            Assert.AreEqual(5, config.BribeFineFor(3), "Явный штраф — для всех");

            var game = Fresh(4);
            game.Debug_SetInfluence(0, 6);
            Assert.IsTrue(game.Bribe(BribeKind.ExtraCard).Success);
            SkipTurn(game);
            Assert.IsTrue(game.Accuse(0).Success);
            Assert.AreEqual(-4, game.Players[0].PenaltyPoints);
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
            Assert.AreEqual(game.RoundNumber, restored.RoundNumber);
            Assert.IsTrue(restored.BribeDiscountActive);
            Assert.IsTrue(restored.BribedThisTurn);
            Assert.AreEqual(2, restored.BuildCost(restored.Map.GetRoad("r4")));
            SkipToNextRound(restored);
            Assert.AreEqual(1, restored.AnnouncedBribes, "Круг взятки сохранился — объявление вовремя");
        }

        [Test]
        public void BotGame_WithBribes_ReplaysExactly()
        {
            var game = TestCities.Started(TestCities.Moscow(), 4, new GameConfig(), seed: 31, events: true);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(i + 11)).ToArray();
            for (int step = 0; step < 5000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.Greater(game.Players.Sum(p => p.BribesGiven), 0, "Боты дают взятки");
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
