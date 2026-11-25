using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ReplayIndexTransactionTests
    {
        static Replays.Entry Entry(string id, string date, int score, string author = "A") =>
            new Replays.Entry { id = id, date = date, author = author, mode = "classic",
                mapPath = "Maps/demo", authorScore = score };

        [Test]
        public void FailedIndexCommitNeverDeletesExistingReplayFiles()
        {
            var before = new List<Replays.Entry> {
                Entry("best", "2025", 100), Entry("old", "2024", 1), Entry("new", "2026", 2)
            };
            var transaction = new ReplayIndexTransaction(before, 2);
            int deletions = 0;
            Assert.IsFalse(transaction.Commit(_ => false, ids => deletions += ids.Count));
            Assert.AreEqual(0, deletions);
            Assert.AreEqual(3, before.Count);
        }

        [Test]
        public void SuccessfulIndexCommitDeletesOnlyAfterPersistence()
        {
            var transaction = new ReplayIndexTransaction(new[] {
                Entry("best", "2025", 100), Entry("old", "2024", 1), Entry("new", "2026", 2)
            }, 2);
            bool persisted = false;
            var deleted = new List<string>();
            Assert.IsTrue(transaction.Commit(entries =>
            {
                Assert.AreEqual(2, entries.Count);
                Assert.IsEmpty(deleted);
                persisted = true;
                return true;
            }, ids =>
            {
                Assert.IsTrue(persisted);
                deleted.AddRange(ids);
            }));
            CollectionAssert.AreEqual(new[] { "old" }, deleted);
        }

        [Test]
        public void EmptyRemovalPlanStillCommitsNewIndex()
        {
            var tx = new ReplayIndexTransaction(new[] { Entry("a", "2026", 1) }, 60);
            int writes = 0;
            Assert.IsTrue(tx.Commit(entries =>
            {
                writes++;
                Assert.AreEqual(1, entries.Count);
                return true;
            }, ids => Assert.IsEmpty(ids)));
            Assert.AreEqual(1, writes);
        }

        [Test]
        public void DoesNotMutateOriginalEntriesOnSuccess()
        {
            var before = new List<Replays.Entry> {
                Entry("old", "2024", 1), Entry("new", "2026", 100)
            };
            var tx = new ReplayIndexTransaction(before, 1);
            Assert.IsTrue(tx.Commit(_ => true, _ => { }));
            Assert.AreEqual(2, before.Count);
            CollectionAssert.AreEqual(new[] { "new" }, tx.Entries.Select(e => e.id));
        }

        [Test]
        public void RejectedNewImportIsNotConsideredPersisted()
        {
            var tx = new ReplayIndexTransaction(new[] {
                Entry("a", "2026-01", 100, "A"),
                Entry("b", "2026-02", 100, "B"),
                Entry("import", "2023-01", 1, "C")
            }, 2);
            Assert.IsFalse(tx.Contains("import"));
            CollectionAssert.AreEqual(new[] { "import" }, tx.RemovedIds);
        }

        [Test]
        public void MissingPersistCallbackIsRejected()
        {
            var tx = new ReplayIndexTransaction(new Replays.Entry[0], 60);
            Assert.Throws<ArgumentNullException>(() => tx.Commit(null, _ => { }));
        }

        [Test]
        public void NullRemovalCallbackIsAllowed()
        {
            var tx = new ReplayIndexTransaction(new[] { Entry("a", "2026", 1) }, 60);
            Assert.IsTrue(tx.Commit(_ => true, null));
        }

        [Test]
        public void FailedCommitCanBeRetriedWithoutLosingRemovalPlan()
        {
            var tx = new ReplayIndexTransaction(new[] {
                Entry("best", "2025", 100), Entry("low", "2026", 1)
            }, 1);
            int writes = 0, deleted = 0;
            Assert.IsFalse(tx.Commit(_ => { writes++; return false; }, ids => deleted += ids.Count));
            Assert.IsTrue(tx.Commit(_ => { writes++; return true; }, ids => deleted += ids.Count));
            Assert.AreEqual(2, writes);
            Assert.AreEqual(1, deleted);
        }

        [Test]
        public void NullInputBehavesLikeEmptyIndex()
        {
            var tx = new ReplayIndexTransaction(null, 60);
            Assert.IsEmpty(tx.Entries);
            Assert.IsEmpty(tx.RemovedIds);
            Assert.IsFalse(tx.Contains(null));
        }

        [Test]
        public void NeverDeleteUnsupportedOrTraversalIds()
        {
            var tx = new ReplayIndexTransaction(new[] {
                Entry("../unsafe", "2025", 500), Entry("safe", "2026", 20)
            }, 1);
            Assert.IsFalse(tx.Contains("../unsafe"));
            Assert.IsEmpty(tx.RemovedIds);
        }

        [Test]
        public void EveryCommittedIndexRemainsWithinQuota()
        {
            var prior = Enumerable.Range(0, 120)
                .Select(i => Entry("p" + i, "2026-" + i.ToString("D3"), i,
                    author: "a" + i)).ToList();
            var tx = new ReplayIndexTransaction(prior, 60);
            Assert.AreEqual(60, tx.Entries.Count);
            Assert.AreEqual(60, tx.RemovedIds.Count);
            Assert.IsTrue(tx.Commit(rows => rows.Count <= 60, null));
        }
    }
}
