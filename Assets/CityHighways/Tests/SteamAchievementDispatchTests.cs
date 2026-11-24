using System;
using System.Collections.Generic;
using CityHighways.Net;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SteamAchievementDispatchTests
    {
        sealed class Publisher : ISteamAchievements
        {
            public readonly List<string[]> Sent = new List<string[]>();
            public bool ThrowNext;

            public void QueueUnlockedAchievements(IReadOnlyCollection<string> ids)
            {
                if (ThrowNext)
                {
                    ThrowNext = false;
                    throw new InvalidOperationException("Simulated Steam reconnect");
                }
                var snapshot = new string[ids.Count];
                int index = 0;
                foreach (string id in ids) snapshot[index++] = id;
                Sent.Add(snapshot);
            }
        }

        static PlayerProfile Profile(string account, params string[] ids)
        {
            var p = new PlayerProfile { steamId = account };
            p.achievements.AddRange(ids);
            return p;
        }

        [Test]
        public void PublishesOnlyOwnedCanonicalSortedIds()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "mg_win", "first_win", "garbage", "first_win", "");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 0f));
            Assert.AreEqual(1, fake.Sent.Count);
            CollectionAssert.AreEqual(new[] { "first_win", "mg_win" }, fake.Sent[0]);
        }

        [Test]
        public void DeduplicatesUntilPeriodicReconciliationIsDue()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 0f));
            Assert.IsFalse(dispatcher.Reconcile(fake, p, "steam1", 1f));
            Assert.IsFalse(dispatcher.Reconcile(fake, p, "steam1",
                SteamAchievementDispatch.ReconcileSeconds - 0.01f));
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1",
                SteamAchievementDispatch.ReconcileSeconds));
            Assert.AreEqual(2, fake.Sent.Count);
            Assert.IsFalse(dispatcher.Reconcile(fake, p, "steam1",
                SteamAchievementDispatch.ReconcileSeconds + 1f));
        }

        [Test]
        public void NewlyEarnedAchievementIsPublishedImmediately()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 10f));
            p.achievements.Add("mg_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 11f));
            CollectionAssert.AreEqual(new[] { "first_win", "mg_win" }, fake.Sent[1]);
        }

        [Test]
        public void ReplacingPublisherRequeuesUnchangedAchievements()
        {
            var dispatcher = new SteamAchievementDispatch();
            var a = new Publisher();
            var b = new Publisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(a, p, "steam1", 1f));
            Assert.IsTrue(dispatcher.Reconcile(b, p, "steam1", 2f));
            Assert.AreEqual(1, b.Sent.Count);
        }

        [Test]
        public void SwitchingSteamAccountsNeverCarriesOtherUsersUnlocks()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var a = Profile("steam1", "first_win");
            var b = Profile("steam2", "mg_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, a, "steam1", 1f));
            Assert.IsFalse(dispatcher.Reconcile(fake, a, "steam2", 2f));
            Assert.IsTrue(dispatcher.Reconcile(fake, b, "steam2", 3f));
            CollectionAssert.AreEqual(new[] { "mg_win" }, fake.Sent[1]);
            Assert.AreEqual(2, fake.Sent.Count);
        }

        [Test]
        public void DisconnectClearsCacheSoReconnectionCanRecover()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 1f));
            Assert.IsFalse(dispatcher.Reconcile(null, p, "steam1", 2f));
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 3f));
            Assert.AreEqual(2, fake.Sent.Count);
        }

        [Test]
        public void OfflineOrMissingAchievementsCanNeverPublish()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            Assert.IsFalse(dispatcher.Reconcile(fake, Profile("", "first_win"), "steam1", 0f));
            Assert.IsFalse(dispatcher.Reconcile(fake, Profile("steam1"), "steam1", 0f));
            Assert.IsFalse(dispatcher.Reconcile(fake, null, "steam1", 0f));
            Assert.IsFalse(dispatcher.Reconcile(fake, Profile("steam1", "made_up"), "steam1", 0f));
            Assert.IsEmpty(fake.Sent);
        }

        [Test]
        public void FailedQueueCallDoesNotMakeIdsLookDelivered()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher { ThrowNext = true };
            var p = Profile("steam1", "first_win");
            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Reconcile(fake, p, "steam1", 1f));
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 2f));
            Assert.AreEqual(1, fake.Sent.Count);
        }

        [Test]
        public void ExplicitResetRequeuesUnchangedIds()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 1f));
            dispatcher.Reset();
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 2f));
            Assert.AreEqual(2, fake.Sent.Count);
        }

        [Test]
        public void DisconnectionAfterAccountSwitchDoesNotRetryOldAccount()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            Assert.IsTrue(dispatcher.Reconcile(fake,
                Profile("steam1", "first_win"), "steam1", 1f));
            Assert.IsFalse(dispatcher.Reconcile(fake,
                Profile("steam1", "first_win"), "steam2", 2f));
            Assert.IsFalse(dispatcher.Reconcile(fake,
                Profile("steam1", "first_win"), "steam2", 140f));
            Assert.AreEqual(1, fake.Sent.Count);
        }

        [Test]
        public void SwitchingProfilesOfSameAccountForcesReconciliation()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var first = Profile("steam1", "first_win");
            var second = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, first, "steam1", 1f));
            Assert.IsTrue(dispatcher.Reconcile(fake, second, "steam1", 2f));
            Assert.AreEqual(2, fake.Sent.Count);
            Assert.IsFalse(dispatcher.Reconcile(fake, second, "steam1", 3f));
        }

        [Test]
        public void ReorderingOrDuplicatingUnlocksDoesNotTriggerExtraDispatch()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "first_win", "mg_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 1f));
            p.achievements.Reverse();
            p.achievements.Add("first_win");
            Assert.IsFalse(dispatcher.Reconcile(fake, p, "steam1", 2f));
            Assert.AreEqual(1, fake.Sent.Count);
        }

        [Test]
        public void ClearingLocalUnlocksStopsPublishingStaleIds()
        {
            var dispatcher = new SteamAchievementDispatch();
            var fake = new Publisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 1f));
            p.achievements.Clear();
            Assert.IsFalse(dispatcher.Reconcile(fake, p, "steam1", 2f));
            Assert.IsFalse(dispatcher.Reconcile(fake, p, "steam1", 200f));
            Assert.AreEqual(1, fake.Sent.Count);
            p.achievements.Add("mg_win");
            Assert.IsTrue(dispatcher.Reconcile(fake, p, "steam1", 201f));
            CollectionAssert.AreEqual(new[] { "mg_win" }, fake.Sent[1]);
        }
    }
}
