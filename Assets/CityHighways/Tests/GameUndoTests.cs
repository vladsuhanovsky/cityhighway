using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class GameUndoTests
    {
        static Game Started() => TestCities.Started(TestCities.Small());
        static string State(Game game) => JsonUtility.ToJson(game.CreateSave());

        [Test]
        public void UndoFirstCardRestoresExactDeckAndHandState()
        {
            var game = Started();
            string before = State(game);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            string after = State(game);
            Assert.AreNotEqual(before, after);

            Assert.IsTrue(GameUndo.TryUndo(game, false, false, false, false, false,
                out var restored, out var journal));
            Assert.AreEqual(before, State(restored));
            Assert.AreEqual(after, State(game), "Original authoritative game must remain unchanged");
            Assert.AreEqual(game.History.Count - 1, restored.History.Count);
            Assert.IsNotNull(journal);
        }

        [Test]
        public void RedoingIdenticalActionAfterUndoIsDeterministic()
        {
            var game = Started();
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            string expected = State(game);
            Assert.IsTrue(GameUndo.TryUndo(game, false, false, false, false, false,
                out var restored, out _));
            Assert.IsTrue(restored.DrawCardFromDeck().Success);
            Assert.AreEqual(expected, State(restored));
        }

        [Test]
        public void UndoSecondCardRestoresFirstDrawAndPreviousPlayer()
        {
            var game = Started();
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            string firstCard = State(game);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            Assert.IsTrue(GameUndo.TryUndo(game, false, false, false, false, false,
                out var restored, out _));
            Assert.AreEqual(firstCard, State(restored));
            Assert.AreEqual(0, restored.ActingPlayerIndex);
        }

        [Test]
        public void UndoRejectsOnlineBotsTutorialTimersAndHiddenHotseatCards()
        {
            var game = Started();
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            foreach (int restriction in Enumerable.Range(0, 5))
            {
                bool online = restriction == 0;
                bool bots = restriction == 1;
                bool tutorial = restriction == 2;
                bool timer = restriction == 3;
                bool hidden = restriction == 4;
                Assert.IsFalse(GameUndo.CanUndo(game, online, bots, tutorial, timer, hidden));
                Assert.IsFalse(GameUndo.TryUndo(game, online, bots, tutorial, timer, hidden,
                    out var restored, out var journal));
                Assert.IsNull(restored);
                Assert.IsEmpty(journal);
            }
        }

        [Test]
        public void UndoRejectsNullAndUnsupportedSetupCommands()
        {
            Assert.IsFalse(GameUndo.CanUndo(null, false, false, false, false, false));
            var game = Started();
            Assert.IsFalse(GameUndo.CanUndo(game, false, false, false, false, false),
                "The last setup contract choice cannot be undone");
            Assert.IsFalse(GameUndo.Reversible(null));
            Assert.IsFalse(GameUndo.Reversible(GameCommand.KeepInitialContracts(0, new string[0])));
            Assert.IsFalse(GameUndo.Reversible(GameCommand.ProposeTrade(
                0, 1, Resource.Asphalt, 1, Resource.Concrete, 1)));
            Assert.IsFalse(GameUndo.Reversible(GameCommand.Bid(0, 2)));
            Assert.IsFalse(GameUndo.Reversible(GameCommand.DrawLuckCard(0)));
        }

        [Test]
        public void OnlyReversibleCardRoadAndUpgradeActionsAreWhitelisted()
        {
            foreach (var command in new[]
            {
                GameCommand.DrawCardFromDeck(0),
                GameCommand.DrawFaceUpCard(0, 0),
                GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 0),
                GameCommand.BuildRoadFree(0, "r1"),
                GameCommand.UpgradeRoad(0, "r1", Resource.Asphalt, 0)
            })
                Assert.IsTrue(GameUndo.Reversible(command), command.ToString());
            Assert.IsFalse(GameUndo.Reversible(new GameCommand { kind = "unknown" }));
        }

        [Test]
        public void LegacySnapshotWithoutReplayHistoryCannotBeUndone()
        {
            var game = Started();
            var snapshot = game.CreateSave();
            snapshot.version = 4;
            snapshot.start = null;
            snapshot.history = null;
            var legacy = Game.Load(game.Map, snapshot);
            Assert.IsFalse(legacy.CanReplay);
            Assert.IsFalse(GameUndo.CanUndo(legacy, false, false, false, false, false));
        }

        [Test]
        public void FailureDoesNotModifyOriginalGameOrHistory()
        {
            var game = Started();
            string old = State(game);
            Assert.IsFalse(GameUndo.TryUndo(game, false, false, false, false, false,
                out var restored, out var journal));
            Assert.IsNull(restored);
            Assert.IsEmpty(journal);
            Assert.AreEqual(old, State(game));
        }

        [Test]
        public void ReplayedJournalContainsPastActionsOnly()
        {
            var game = Started();
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(GameUndo.TryUndo(game, false, false, false, false, false,
                out var restored, out var journal));
            Assert.AreEqual(game.History.Count - 1, restored.History.Count);
            Assert.Greater(journal.Count, 0, "Replay observer must rebuild past action log");
        }
    }
}
