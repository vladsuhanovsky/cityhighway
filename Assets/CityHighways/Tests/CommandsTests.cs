using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Действия как команды: запись, выполнение, воспроизведение партии и точная загрузка.</summary>
    public class CommandsTests
    {
        /// <summary>Партия умных ботов на Москве с персонажами — всё, что умеет игра.</summary>
        static Game PlayBots(int seed, int maxActions = int.MaxValue)
        {
            var characters = new[] { CharacterKind.Lucky, CharacterKind.Foreman, CharacterKind.Insurer };
            var game = new Game(TestCities.Moscow(), new[] { "А", "Б", "В" }, null, seed, characters);
            var bots = Enumerable.Range(0, 3).Select(i => (IBot)new SmartBot(seed * 10 + i)).ToArray();
            int actions = 0;
            while (game.Phase != GamePhase.GameOver && actions < maxActions)
            {
                var result = bots[game.ActingPlayerIndex].Act(game);
                Assert.IsTrue(result.Success, result.Error);
                actions++;
                Assert.Less(actions, 8000);
            }
            return game;
        }

        static string Json(Game game) => JsonUtility.ToJson(game.CreateSave());

        [Test]
        public void EverySuccessfulAction_IsRecorded()
        {
            var game = TestCities.Started(TestCities.Small());
            int before = game.History.Count;
            Assert.AreEqual(2, before, "Два игрока выбрали стартовые контракты");
            Assert.AreEqual(GameCommand.KeepInitial, game.History[0].kind);

            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsFalse(game.BuildRoad("нет-такой", Resource.Asphalt, 0).Success, "Неудачное действие не записывается");
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreEqual(before + 2, game.History.Count);
            Assert.AreEqual(0, game.History[before].player);
            Assert.AreEqual(0, game.History[before + 1].player, "Вторая карта — того же игрока, хотя ход уже перешёл");
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void Execute_RunsCommand_AndChecksPlayer()
        {
            var game = TestCities.Started(TestCities.Small());
            Assert.IsFalse(game.Execute(GameCommand.DrawCardFromDeck(1)).Success, "Сейчас ход игрока 0");
            Assert.IsTrue(game.Execute(GameCommand.DrawCardFromDeck(0)).Success);
            Assert.IsFalse(game.Execute(new GameCommand { kind = "teleport", player = 0 }).Success);
            Assert.IsFalse(game.Execute(null).Success);

            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.Execute(GameCommand.DrawCardFromDeck(0)).Success);
            game.Debug_SetHand(1, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.Execute(GameCommand.BuildRoad(1, "r1", Resource.Asphalt, 0)).Success);
            Assert.AreEqual(1, game.GetRoadOwner("r1"));
        }

        [Test]
        public void Command_SurvivesJson_AndReadsWell()
        {
            var command = GameCommand.BuildRoad(2, "r12", Resource.Concrete, 1);
            var back = JsonUtility.FromJson<GameCommand>(JsonUtility.ToJson(command));
            Assert.AreEqual(GameCommand.Build, back.kind);
            Assert.AreEqual(2, back.player);
            Assert.AreEqual("r12", back.road);
            Assert.AreEqual((int)Resource.Concrete, back.resource);
            Assert.AreEqual(1, back.machinery);
            StringAssert.Contains("r12", back.ToString());
        }

        [Test]
        public void Replay_ReproducesWholeGame([Values(1, 2)] int seed)
        {
            var game = PlayBots(seed);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.Greater(game.History.Count, 50);

            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(Json(game), Json(replayed), "Те же команды — то же состояние, вплоть до колод");
            Assert.AreEqual(game.Result.Ranking.Select(s => s.Total).ToArray(), replayed.Result.Ranking.Select(s => s.Total).ToArray());
        }

        [Test]
        public void Replay_FailsLoudly_OnIllegalCommand()
        {
            var game = PlayBots(3, 40);
            var history = game.History.ToList();
            history.Insert(10, GameCommand.Pass(history[10].player));
            Assert.Throws<System.InvalidOperationException>(() => Game.Replay(game.Map, game.StartInfo, history));
        }

        [Test]
        public void Load_WithHistory_ContinuesExactlyLikeOriginal()
        {
            // Полная партия — эталон. Берём первые 120 команд, сохраняем, загружаем и доигрываем остальные.
            var full = PlayBots(4);
            var all = full.History.ToList();
            const int cut = 120;
            var half = Game.Replay(full.Map, full.StartInfo, all.Take(cut));

            var save = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(half.CreateSave()));
            var loaded = Game.Load(full.Map, save);
            Assert.IsTrue(loaded.CanReplay);
            Assert.AreEqual(cut, loaded.History.Count);

            foreach (var command in all.Skip(cut))
                Assert.IsTrue(loaded.Execute(command).Success, command.ToString());
            Assert.AreEqual(Json(full), Json(loaded), "После загрузки партия идёт ровно так же — генератор тоже восстановлен");
        }

        [Test]
        public void Load_OldSaveWithoutHistory_FallsBackToSnapshot()
        {
            var game = PlayBots(5, 60);
            var save = game.CreateSave();
            save.version = 4;
            save.start = null;
            save.history = null;
            var loaded = Game.Load(game.Map, save);
            Assert.AreEqual(game.CurrentPlayerIndex, loaded.CurrentPlayerIndex);
            Assert.IsFalse(loaded.CanReplay);
        }

        [Test]
        public void Load_BrokenHistory_FallsBackToSnapshot()
        {
            var game = PlayBots(6, 60);
            var save = game.CreateSave();
            save.history = save.history.Take(5).ToArray(); // история не сходится со снимком
            var loaded = Game.Load(game.Map, save);
            Assert.AreEqual(game.CurrentPlayerIndex, loaded.CurrentPlayerIndex);
            Assert.AreEqual(save.turnNumber, loaded.TurnNumber);
        }
    }
}
