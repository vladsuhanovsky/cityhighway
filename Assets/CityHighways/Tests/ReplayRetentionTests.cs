using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ReplayRetentionTests
    {
        static Replays.Entry Entry(string id, string date, int score,
            string map = "Maps/demo", string author = "A", string mode = "classic") =>
            new Replays.Entry
            {
                id = id, date = date, authorScore = score,
                mapPath = map, author = author, mode = mode
            };

        [Test]
        public void BelowQuotaPreservesEntriesAndTheirOrder()
        {
            var input = new[] { Entry("a", "2026-01", 1), Entry("b", "2026-02", 5) };
            CollectionAssert.AreEqual(new[] { "a", "b" },
                ReplayRetention.Select(input, 4).Select(x => x.id));
        }

        [Test]
        public void ABestScoreIsProtectedAgainstNewerLowerScores()
        {
            var input = new[] {
                Entry("best", "2024-01", 99),
                Entry("low", "2026-01", 2),
                Entry("new", "2026-02", 3)
            };
            CollectionAssert.AreEquivalent(new[] { "best", "new" },
                ReplayRetention.Select(input, 2).Select(x => x.id));
        }

        [Test]
        public void MoreDistinctBestScoreGroupsThanQuotaRetainsNewestGroups()
        {
            var input = Enumerable.Range(1, 8)
                .Select(n => Entry("id" + n, "2026-" + n.ToString("D2"), n,
                    author: "author" + n)).ToArray();
            CollectionAssert.AreEquivalent(new[] { "id6", "id7", "id8" },
                ReplayRetention.Select(input, 3).Select(e => e.id));
        }

        [Test]
        public void AuthorIsComparedCaseInsensitivelyWithinSameMapAndMode()
        {
            var input = new[] {
                Entry("a", "2026-01", 50, author: "Alice"),
                Entry("b", "2026-02", 10, author: "ALICE"),
                Entry("c", "2026-03", 20, author: "alice")
            };
            CollectionAssert.AreEquivalent(new[] { "a", "c" },
                ReplayRetention.Select(input, 2).Select(e => e.id));
        }

        [Test]
        public void SameAuthorOnDifferentMapAndModeGetsIndependentProtection()
        {
            var input = new[] {
                Entry("map1", "2025-01", 80),
                Entry("map2", "2025-02", 90, map: "Maps/other"),
                Entry("coop", "2025-03", 70, mode: "coop"),
                Entry("new", "2026-01", 1)
            };
            CollectionAssert.AreEquivalent(new[] { "map1", "map2", "coop" },
                ReplayRetention.Select(input, 3).Select(e => e.id));
        }

        [Test]
        public void DuplicateIdsKeepNewestMetadataOnce()
        {
            var input = new[] { Entry("same", "2025-01", 2),
                Entry("same", "2026-02", 3), Entry("other", "2026-03", 1) };
            var result = ReplayRetention.Select(input, 5);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("2026-02", result.Single(e => e.id == "same").date);
        }

        [Test]
        public void NullEntriesAndUnsafeIdsAreExcluded()
        {
            var result = ReplayRetention.Select(new Replays.Entry[] {
                null, Entry("../escape", "2026-01", 500), Entry("valid", "2026-02", 2)
            }, 5);
            Assert.AreEqual("valid", result.Single().id);
        }

        [Test]
        public void InvalidQuotaOrNullInputGivesEmptyResult()
        {
            Assert.IsEmpty(ReplayRetention.Select(null, 60));
            Assert.IsEmpty(ReplayRetention.Select(new[] { Entry("a", "2026-01", 1) }, 0));
            Assert.IsEmpty(ReplayRetention.Select(new[] { Entry("a", "2026-01", 1) }, -1));
        }

        [Test]
        public void RemovalPlanNeverContainsUnsafeIds()
        {
            var before = new[] {
                Entry("first", "2026-01", 1),
                Entry("second", "2026-02", 2),
                Entry("../escape", "2026-03", 3)
            };
            CollectionAssert.AreEquivalent(new[] { "first" },
                ReplayRetention.Removed(before, new[] { before[1] }));
        }

        [Test]
        public void RemovalPlanDeduplicatesIds()
        {
            var before = new[] { Entry("old", "2024-01", 1), Entry("old", "2024-02", 2) };
            CollectionAssert.AreEqual(new[] { "old" },
                ReplayRetention.Removed(before, new Replays.Entry[0]));
        }

        [Test]
        public void StableTieBreakUsesOrdinalReplayId()
        {
            var input = new[] {
                Entry("z", "2026-01", 50, author: "Z"),
                Entry("a", "2026-01", 50, author: "A")
            };
            CollectionAssert.AreEqual(new[] { "a" },
                ReplayRetention.Select(input, 1).Select(e => e.id));
        }

        [Test]
        public void NullDateAndMetadataDoNotThrow()
        {
            var input = new[] {
                Entry("a", null, 1, map: null, author: null, mode: null),
                Entry("b", "2026-01", 2, map: null, author: null, mode: null)
            };
            Assert.AreEqual(1, ReplayRetention.Select(input, 1).Count);
        }

        [Test]
        public void QuotaNeverExceedsSixtyForManyUniqueAuthors()
        {
            var input = Enumerable.Range(0, 180)
                .Select(n => Entry("r" + n, "2026-" + n.ToString("D3"), n,
                    author: "author" + n)).ToList();
            Assert.AreEqual(60, ReplayRetention.Select(input, 60).Count);
        }

        [Test]
        public void NewlyStoredLowScoreMayBeRejectedByFullProtectedQuota()
        {
            var input = new[] {
                Entry("best-a", "2026-01", 100, author: "A"),
                Entry("best-b", "2026-02", 100, author: "B"),
                Entry("old-import", "2024-01", 1, author: "C")
            };
            var kept = ReplayRetention.Select(input, 2);
            Assert.IsFalse(kept.Any(e => e.id == "old-import"));
        }
    }
}
