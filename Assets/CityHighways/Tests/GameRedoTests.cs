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

        [Test]
        public void JsonUtilityRoundTripPreservesSemanticCommandEquality()
        {
            foreach (var original in new[]
            {
                GameCommand.DrawCardFromDeck(0),
                GameCommand.DrawFaceUpCard(1, 2),
                GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 1),
                GameCommand.BuildRoadFree(1, "r2"),
                GameCommand.UpgradeRoad(0, "r3", Resource.Concrete, 0),
                GameCommand.KeepContracts(0, new string[0]),
                GameCommand.KeepContracts(1, new[] { "c1", "c2" })
            })
            {
                var copy = JsonUtility.FromJson<GameCommand>(JsonUtility.ToJson(original));
                Assert.IsTrue(GameRedo.SameCommand(original, copy),
                    "JSON round-trip should keep command semantics: " + original +
                    "\\nBefore: " + DescribeCommand(original) +
                    "\\nAfter: " + DescribeCommand(copy));
            }
        }

        static string DescribeCommand(GameCommand c)
        {
            if (c == null) return "<null>";
            return "kind=" + (c.kind ?? "<null>") +
                " player=" + c.player + " slot=" + c.slot +
                " road=" + (c.road ?? "<null>") +
                " resource=" + c.resource + " machinery=" + c.machinery +
                " contracts=" + (c.contracts == null
                    ? "<null>" : string.Join(",", c.contracts)) +
                " index=" + c.index + " to=" + c.to +
                " give=" + c.giveResource + "/" + c.giveCount +
                " get=" + c.getResource + "/" + c.getCount +
                " accept=" + c.accept +
                " giveInfluence=" + c.giveInfluence +
                " getInfluence=" + c.getInfluence +
                " givePass=" + c.givePass + " getPass=" + c.getPass +
                " option=" + c.option + " bid=" + c.bid;
        }

        [Test]
        public void OptionalEmptyContractListMatchesMissingButNeverNonEmptyList()
        {
            var missing = GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 1);
            var empty = GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 1);
            empty.contracts = new string[0];
            Assert.IsTrue(GameRedo.SameCommand(missing, empty));
            Assert.IsTrue(GameRedo.SameCommand(empty, missing));

            empty.contracts = new[] { "c1" };
            Assert.IsFalse(GameRedo.SameCommand(missing, empty));
            Assert.IsFalse(GameRedo.SameCommand(empty, missing));
            var distinct = GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 1);
            distinct.contracts = new[] { "c2" };
            Assert.IsFalse(GameRedo.SameCommand(empty, distinct));
        }

        [Test]
        public void OptionalRoadStringNullAndEmptyAreEquivalentOnlyWhenAbsent()
        {
            var fromDeck = GameCommand.DrawCardFromDeck(0);
            var roundTripped = GameCommand.DrawCardFromDeck(0);
            roundTripped.road = "";
            Assert.IsTrue(GameRedo.SameCommand(fromDeck, roundTripped));
            Assert.IsTrue(GameRedo.SameCommand(roundTripped, fromDeck));

            roundTripped.road = "r1";
            Assert.IsFalse(GameRedo.SameCommand(fromDeck, roundTripped));
            Assert.IsFalse(GameRedo.SameCommand(roundTripped, fromDeck));

            var road = GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 1);
            var otherRoad = GameCommand.BuildRoad(0, "R1", Resource.Asphalt, 1);
            Assert.IsFalse(GameRedo.SameCommand(road, otherRoad),
                "Nonempty road IDs must stay case-sensitive.");
        }
    }
}
