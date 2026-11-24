using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class GameRedoTests
    {
        static Game Started() => TestCities.Started(TestCities.Small());
        static string Saved(Game game) => JsonUtility.ToJson(game.CreateSave());

        static Game Undo(Game game)
        {
            Assert.IsTrue(GameUndo.TryUndo(game, false, false, false, false, false,
                out var previous, out _));
            return previous;
        }

        [Test]
        public void RedoRestoresExactHandDeckAndCommandHistory()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            string expected = Saved(original);
            var undone = Undo(original);

            Assert.IsTrue(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out var redone, out var journal));
            Assert.AreEqual(expected, Saved(redone));
            Assert.AreEqual(original.History.Count, redone.History.Count);
            Assert.Greater(journal.Count, 0);
        }

        [Test]
        public void RedoSecondDrawRestoresTurnOwnership()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            Assert.AreEqual(1, original.ActingPlayerIndex);
            var undone = Undo(original);
            Assert.AreEqual(0, undone.ActingPlayerIndex);

            Assert.IsTrue(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out var redone, out _));
            Assert.AreEqual(1, redone.ActingPlayerIndex);
            Assert.AreEqual(Saved(original), Saved(redone));
        }

        [Test]
        public void RedoLeavesBothInputGamesUnchanged()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            string originalSaved = Saved(original);
            string undoneSaved = Saved(undone);

            Assert.IsTrue(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out _, out _));
            Assert.AreEqual(originalSaved, Saved(original));
            Assert.AreEqual(undoneSaved, Saved(undone));
        }

        [Test]
        public void RedoRefusesAllUnsupportedSessionModes()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            for (int i = 0; i < 5; i++)
            {
                bool online = i == 0, bots = i == 1, tutorial = i == 2;
                bool timer = i == 3, hidden = i == 4;
                Assert.IsFalse(GameRedo.CanRedo(undone, original,
                    online, bots, tutorial, timer, hidden));
                Assert.IsFalse(GameRedo.TryRedo(undone, original,
                    online, bots, tutorial, timer, hidden,
                    out var redone, out var journal));
                Assert.IsNull(redone);
                Assert.IsEmpty(journal);
            }
        }

        [Test]
        public void RedoRefusesNullAndUnrelatedGames()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            Assert.IsFalse(GameRedo.CanRedo(null, original, false, false, false, false, false));
            Assert.IsFalse(GameRedo.CanRedo(undone, null, false, false, false, false, false));
            Assert.IsFalse(GameRedo.CanRedo(undone, undone, false, false, false, false, false));
            var other = Started();
            Assert.IsFalse(GameRedo.CanRedo(other, original, false, false, false, false, false));
        }

        [Test]
        public void AlternativeActionInvalidatesTimeline()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            Assert.IsTrue(undone.DrawFaceUpCard(0).Success);
            Assert.IsFalse(GameRedo.HasExactHistoryPrefix(undone.History, original.History));
            Assert.IsFalse(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out var restored, out var journal));
            Assert.IsNull(restored);
            Assert.IsEmpty(journal);
        }

        [Test]
        public void SameCommandChecksAllImportantPayloadFields()
        {
            var a = GameCommand.BuildRoad(1, "r1", Resource.Asphalt, 1);
            var copy = JsonUtility.FromJson<GameCommand>(JsonUtility.ToJson(a));
            Assert.IsTrue(GameRedo.SameCommand(a, copy));
            copy.road = "r2";
            Assert.IsFalse(GameRedo.SameCommand(a, copy));
            copy.road = "r1";
            copy.machinery = 0;
            Assert.IsFalse(GameRedo.SameCommand(a, copy));
            Assert.IsFalse(GameRedo.SameCommand(null, a));
            Assert.IsTrue(GameRedo.SameCommand(null, null));
        }

        [Test]
        public void ContractSelectionPayloadIsComparedInOrder()
        {
            var original = GameCommand.KeepContracts(0, new[] { "a", "b" });
            var same = GameCommand.KeepContracts(0, new[] { "a", "b" });
            var changed = GameCommand.KeepContracts(0, new[] { "b", "a" });
            Assert.IsTrue(GameRedo.SameCommand(original, same));
            Assert.IsFalse(GameRedo.SameCommand(original, changed));
            Assert.IsFalse(GameRedo.SameCommand(original, null));
        }

        [Test]
        public void ExactHistoryPrefixSurvivesReplayCreatedCommandObjects()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            Assert.IsTrue(GameRedo.HasExactHistoryPrefix(undone.History, original.History));
            Assert.IsFalse(GameRedo.HasExactHistoryPrefix(original.History, undone.History));
            Assert.IsFalse(GameRedo.HasExactHistoryPrefix(null, original.History));
        }

        [Test]
        public void RedoRefusesSnapshotOnlyLegacyState()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            var save = undone.CreateSave();
            save.start = null;
            save.history = null;
            save.version = 4;
            var legacy = Game.Load(undone.Map, save);
            Assert.IsFalse(GameRedo.CanRedo(legacy, original, false, false, false, false, false));
        }

        [Test]
        public void RedoCannotBeReusedWhenHistoryIsAlreadyRestored()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            Assert.IsTrue(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out var redone, out _));
            Assert.IsFalse(GameRedo.CanRedo(redone, original, false, false, false, false, false));
        }

        [Test]
        public void TwoUndoStepsCanBeRedoneInReverseOrder()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var oneStep = Undo(original);
            var twoSteps = Undo(oneStep);
            Assert.IsTrue(GameRedo.TryRedo(twoSteps, oneStep, false, false, false,
                false, false, out var reFirst, out _));
            Assert.AreEqual(Saved(oneStep), Saved(reFirst));
            Assert.IsTrue(GameRedo.TryRedo(reFirst, original, false, false, false,
                false, false, out var reSecond, out _));
            Assert.AreEqual(Saved(original), Saved(reSecond));
        }

        [Test]
        public void ChangedCurrentSnapshotMustNotBeOverwrittenByRedo()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            undone.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            string before = Saved(undone);

            Assert.IsFalse(undone.HasVerifiedUndoHistory());
            Assert.IsFalse(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out var restored, out var journal));
            Assert.IsNull(restored);
            Assert.IsEmpty(journal);
            Assert.AreEqual(before, Saved(undone));
        }

        [Test]
        public void ChangedOriginalSnapshotMustNotBeTrustedForRedo()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            original.Debug_SetHand(0, Resource.Concrete, Resource.Concrete);
            string before = Saved(undone);

            Assert.IsFalse(original.HasVerifiedUndoHistory());
            Assert.IsFalse(GameRedo.TryRedo(undone, original, false, false, false,
                false, false, out var restored, out var journal));
            Assert.IsNull(restored);
            Assert.IsEmpty(journal);
            Assert.AreEqual(before, Saved(undone));
        }

        [Test]
        public void RedoCannotReuseAHistoryWithDifferentExtraCommandKind()
        {
            var original = Started();
            Assert.IsTrue(original.DrawCardFromDeck().Success);
            var undone = Undo(original);
            var other = Started();
            Assert.IsTrue(other.DrawFaceUpCard(0).Success);

            Assert.IsFalse(GameRedo.CanRedo(undone, other, false, false, false, false, false),
                "Different map object must never be cross-replayed");
            Assert.IsFalse(GameRedo.SameCommand(original.History.Last(),
                other.History.Last()));
        }
    }
}
