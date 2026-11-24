using System;
using System.Linq;
using CityHighways.Net;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SteamAchievementAccountQueueTests
    {
        [Test]
        public void StartsUnboundAndRejectsUnownedUnlocks()
        {
            var queue = new SteamAchievementAccountQueue();
            Assert.AreEqual(0UL, queue.AccountId);
            Assert.IsFalse(queue.BelongsTo(0UL));
            Assert.AreEqual(0, queue.QueueForAccount(1UL, new[] { "first_win" }));
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void BindsToCurrentAccountAndDeduplicatesQueuedIds()
        {
            var queue = new SteamAchievementAccountQueue();
            Assert.IsTrue(queue.ObserveAccount(1UL));
            Assert.AreEqual(2, queue.QueueForAccount(1UL,
                new[] { "mg_win", "first_win", "mg_win", null, "", " " }));
            Assert.AreEqual(0, queue.QueueForAccount(1UL, new[] { "mg_win" }));
            CollectionAssert.AreEqual(new[] { "first_win", "mg_win" },
                queue.PendingSnapshot());
        }

        [Test]
        public void SameSteamIdDoesNotClearPendingOrStagedWork()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(7UL);
            queue.QueueForAccount(7UL, new[] { "first_win" });
            Assert.IsTrue(queue.Stage("first_win"));
            Assert.IsFalse(queue.ObserveAccount(7UL));
            Assert.IsTrue(queue.ContainsPending("first_win"));
            Assert.IsTrue(queue.ContainsStaged("first_win"));
        }

        [Test]
        public void SwitchingSteamAccountAtomicallyErasesPendingStagedAndUnknown()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            Assert.IsTrue(queue.Stage("first_win"));
            Assert.IsTrue(queue.FirstUnknownId("unknown_api"));
            Assert.IsTrue(queue.ObserveAccount(2UL));
            Assert.AreEqual(2UL, queue.AccountId);
            Assert.Zero(queue.PendingCount);
            Assert.Zero(queue.StagedCount);
            Assert.Zero(queue.UnknownCount);
            Assert.IsFalse(queue.BelongsTo(1UL));
            Assert.AreEqual(0, queue.QueueForAccount(1UL, new[] { "first_win" }));
            Assert.IsEmpty(queue.PendingSnapshot());
        }

        [Test]
        public void NewAccountCanOnlyQueueNewIdsAfterBinding()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            queue.ObserveAccount(2UL);
            Assert.AreEqual(1, queue.QueueForAccount(2UL, new[] { "mg_win" }));
            CollectionAssert.AreEqual(new[] { "mg_win" }, queue.PendingSnapshot());
        }

        [Test]
        public void SteamLogoutClearsAllPendingStateAndRejectsZero()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(5UL);
            queue.QueueForAccount(5UL, new[] { "first_win" });
            Assert.IsTrue(queue.ObserveAccount(0UL));
            Assert.Zero(queue.AccountId);
            Assert.IsEmpty(queue.PendingSnapshot());
            Assert.AreEqual(0, queue.QueueForAccount(0UL, new[] { "mg_win" }));
        }

        [Test]
        public void StageOnlyWorksForOwnedPendingId()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            Assert.IsFalse(queue.Stage("first_win"));
            queue.QueueForAccount(1UL, new[] { "first_win" });
            Assert.IsTrue(queue.Stage("first_win"));
            Assert.IsFalse(queue.Stage("first_win"));
            Assert.IsTrue(queue.ContainsStaged("first_win"));
        }

        [Test]
        public void FailedStoreRetainsStageForRetry()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            queue.Stage("first_win");
            Assert.IsFalse(queue.CompleteStore(1UL, false));
            Assert.AreEqual(1, queue.StagedCount);
            Assert.AreEqual(1, queue.PendingCount);
        }

        [Test]
        public void SuccessfulStoreCommitsStagedButNotLaterPendingIds()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            queue.Stage("first_win");
            queue.QueueForAccount(1UL, new[] { "mg_win" });
            Assert.IsTrue(queue.CompleteStore(1UL, true));
            Assert.Zero(queue.StagedCount);
            CollectionAssert.AreEqual(new[] { "mg_win" }, queue.PendingSnapshot());
        }

        [Test]
        public void LateStoreCallbackFromOldAccountCannotCommitNewAccount()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            queue.Stage("first_win");
            queue.ObserveAccount(2UL);
            queue.QueueForAccount(2UL, new[] { "mg_win" });
            queue.Stage("mg_win");
            Assert.IsFalse(queue.CompleteStore(1UL, true));
            Assert.IsTrue(queue.ContainsStaged("mg_win"));
            Assert.IsTrue(queue.ContainsPending("mg_win"));
        }

        [Test]
        public void PersistedRemoteAchievementRemovedWhenNotAwaitingStore()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            Assert.IsTrue(queue.ForgetPersisted("first_win"));
            Assert.IsFalse(queue.ForgetPersisted("first_win"));
            Assert.Zero(queue.PendingCount);
        }

        [Test]
        public void LocalStagedAchievementMustNotBeForgottenBeforeStoreSuccess()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            queue.QueueForAccount(1UL, new[] { "first_win" });
            queue.Stage("first_win");
            Assert.IsFalse(queue.ForgetPersisted("first_win"));
            Assert.IsTrue(queue.ContainsPending("first_win"));
            Assert.IsTrue(queue.CompleteStore(1UL, true));
            Assert.IsFalse(queue.ContainsPending("first_win"));
        }

        [Test]
        public void MissingSteamApiIdsLogOncePerAccount()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(1UL);
            Assert.IsTrue(queue.FirstUnknownId("unknown"));
            Assert.IsFalse(queue.FirstUnknownId("unknown"));
            Assert.IsFalse(queue.FirstUnknownId(""));
            Assert.IsFalse(queue.FirstUnknownId(null));
            Assert.AreEqual(1, queue.UnknownCount);
            queue.ObserveAccount(2UL);
            Assert.IsTrue(queue.FirstUnknownId("unknown"));
            Assert.AreEqual(1, queue.UnknownCount);
        }

        [Test]
        public void ResetIsIdempotentAndUnbindsAccount()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(9UL);
            queue.QueueForAccount(9UL, new[] { "first_win" });
            queue.Reset();
            queue.Reset();
            Assert.AreEqual(0UL, queue.AccountId);
            Assert.AreEqual(0, queue.PendingCount);
            Assert.AreEqual(0, queue.StagedCount);
        }

        [Test]
        public void EmptyOrNullInputsLeaveValidQueueUntouched()
        {
            var queue = new SteamAchievementAccountQueue();
            queue.ObserveAccount(2UL);
            queue.QueueForAccount(2UL, new[] { "first_win" });
            Assert.AreEqual(0, queue.QueueForAccount(2UL, null));
            Assert.AreEqual(0, queue.QueueForAccount(2UL, Enumerable.Empty<string>()));
            Assert.AreEqual(0, queue.QueueForAccount(3UL, new[] { "mg_win" }));
            CollectionAssert.AreEqual(new[] { "first_win" }, queue.PendingSnapshot());
        }
    }
}
