using System;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class RedoHistoryTests
    {
        static Game NewGame() => TestCities.Started(TestCities.Small());

        [Test]
        public void StartsEmptyAndNeverResurrectsUnrecordedAction()
        {
            var history = new RedoHistory();
            Assert.AreEqual(0, history.Count);
            Assert.IsNull(history.Next);
            Assert.IsFalse(history.Consume(NewGame()));
            Assert.IsFalse(history.Consume(null));
        }

        [Test]
        public void RecordsUndoInReverseRedoOrder()
        {
            var history = new RedoHistory();
            var earlier = NewGame();
            var later = NewGame();
            history.Push(later);
            history.Push(earlier);
            Assert.AreSame(earlier, history.Next);
            Assert.IsTrue(history.Consume(earlier));
            Assert.AreSame(later, history.Next);
            Assert.IsTrue(history.Consume(later));
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void CapacityRemovesOldestStatesInsteadOfNewest()
        {
            var history = new RedoHistory(2);
            var old = NewGame();
            var middle = NewGame();
            var newest = NewGame();
            history.Push(old);
            history.Push(middle);
            history.Push(newest);
            Assert.AreEqual(2, history.Count);
            Assert.AreSame(newest, history.Next);
            Assert.IsTrue(history.Consume(newest));
            Assert.AreSame(middle, history.Next);
            Assert.IsTrue(history.Consume(middle));
            Assert.IsFalse(history.Consume(old));
        }

        [Test]
        public void WrongExpectedCandidateCannotConsumeTopOfStack()
        {
            var history = new RedoHistory();
            var candidate = NewGame();
            history.Push(candidate);
            Assert.IsFalse(history.Consume(NewGame()));
            Assert.IsFalse(history.Consume(null));
            Assert.AreEqual(1, history.Count);
            Assert.AreSame(candidate, history.Next);
        }

        [Test]
        public void ANewGameActionDropsAllPendingRedos()
        {
            var history = new RedoHistory();
            history.Push(NewGame());
            history.Push(NewGame());
            history.Clear();
            Assert.AreEqual(0, history.Count);
            Assert.IsNull(history.Next);
        }

        [Test]
        public void InvalidCapacityOrNullStateCannotPolluteTimeline()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedoHistory(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedoHistory(-2));
            var history = new RedoHistory();
            Assert.Throws<ArgumentNullException>(() => history.Push(null));
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void DefaultUndoDepthIsEight()
        {
            Assert.AreEqual(8, RedoHistory.DefaultCapacity);
            Assert.AreEqual(RedoHistory.DefaultCapacity, new RedoHistory().Capacity);
        }

        [Test]
        public void PushAfterPartialRedoCreatesExpectedNewTop()
        {
            var history = new RedoHistory();
            var original = NewGame();
            var second = NewGame();
            var newUndo = NewGame();
            history.Push(original);
            history.Push(second);
            Assert.IsTrue(history.Consume(second));
            history.Push(newUndo);
            Assert.AreSame(newUndo, history.Next);
            Assert.IsTrue(history.Consume(newUndo));
            Assert.AreSame(original, history.Next);
        }
    }
}
