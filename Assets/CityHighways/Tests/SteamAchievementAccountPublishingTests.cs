using System;
using System.Collections.Generic;
using CityHighways.Net;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SteamAchievementAccountPublishingTests
    {
        sealed class AccountPublisher : IAccountBoundSteamAchievements
        {
            public string ActiveAccount = "steam1";
            public bool RejectNext;
            public int BoundCalls;
            public int LegacyCalls;
            public readonly List<string> Sources = new List<string>();
            public readonly List<string[]> Batches = new List<string[]>();

            public void QueueUnlockedAchievements(IReadOnlyCollection<string> ids)
            {
                LegacyCalls++;
                throw new InvalidOperationException(
                    "Account-bound publisher must not use the legacy queue.");
            }

            public bool TryQueueUnlockedAchievementsForAccount(string steamId,
                IReadOnlyCollection<string> ids)
            {
                BoundCalls++;
                if (RejectNext)
                {
                    RejectNext = false;
                    return false;
                }
                if (!string.Equals(steamId, ActiveAccount, StringComparison.Ordinal))
                    return false;
                Sources.Add(steamId);
                var snapshot = new string[ids.Count];
                ids.CopyTo(snapshot, 0);
                Batches.Add(snapshot);
                return true;
            }
        }

        static PlayerProfile Profile(string steamId, params string[] achievements)
        {
            var p = new PlayerProfile { steamId = steamId };
            p.achievements.AddRange(achievements);
            return p;
        }

        [Test]
        public void DispatchUsesAccountBoundPublisherInsteadOfLegacyMethod()
        {
            var d = new SteamAchievementDispatch();
            var publisher = new AccountPublisher();
            Assert.IsTrue(d.Reconcile(publisher,
                Profile("steam1", "first_win"), "steam1", 1f));
            Assert.Zero(publisher.LegacyCalls);
            Assert.AreEqual(1, publisher.BoundCalls);
            CollectionAssert.AreEqual(new[] { "first_win" }, publisher.Batches[0]);
            Assert.AreEqual("steam1", publisher.Sources[0]);
        }

        [Test]
        public void RejectingAccountChangeDoesNotMemoizeFailedDispatch()
        {
            var d = new SteamAchievementDispatch();
            var publisher = new AccountPublisher { ActiveAccount = "steam2" };
            var p = Profile("steam1", "first_win");
            Assert.IsFalse(d.Reconcile(publisher, p, "steam1", 1f));
            Assert.Zero(publisher.Batches.Count);
            publisher.ActiveAccount = "steam1";
            Assert.IsTrue(d.Reconcile(publisher, p, "steam1", 2f));
            Assert.AreEqual(2, publisher.BoundCalls);
            Assert.AreEqual(1, publisher.Batches.Count);
        }

        [Test]
        public void TransientAccountBoundRejectionRetriesNextTick()
        {
            var d = new SteamAchievementDispatch();
            var publisher = new AccountPublisher { RejectNext = true };
            var p = Profile("steam1", "mg_win");
            Assert.IsFalse(d.Reconcile(publisher, p, "steam1", 0f));
            Assert.IsTrue(d.Reconcile(publisher, p, "steam1", 1f));
            CollectionAssert.AreEqual(new[] { "mg_win" }, publisher.Batches[0]);
        }

        [Test]
        public void AccountBoundPublisherReceivesOnlyCurrentProfilesCanonicalIds()
        {
            var d = new SteamAchievementDispatch();
            var publisher = new AccountPublisher();
            var p = Profile("steam1", "unknown", "mg_win", "first_win", "mg_win");
            Assert.IsTrue(d.Reconcile(publisher, p, "steam1", 1f));
            CollectionAssert.AreEqual(new[] { "first_win", "mg_win" }, publisher.Batches[0]);
            Assert.IsFalse(d.Reconcile(publisher, p, "steam2", 2f));
            Assert.AreEqual(1, publisher.Batches.Count);
        }

        [Test]
        public void PublisherChangeRequeuesThroughNewAccountBoundInstance()
        {
            var d = new SteamAchievementDispatch();
            var a = new AccountPublisher();
            var b = new AccountPublisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(d.Reconcile(a, p, "steam1", 1f));
            Assert.IsTrue(d.Reconcile(b, p, "steam1", 2f));
            Assert.AreEqual(1, b.BoundCalls);
            Assert.Zero(b.LegacyCalls);
        }

        [Test]
        public void SuccessfulAccountBoundQueueStillRespectsDedupAndTimeout()
        {
            var d = new SteamAchievementDispatch();
            var publisher = new AccountPublisher();
            var p = Profile("steam1", "first_win");
            Assert.IsTrue(d.Reconcile(publisher, p, "steam1", 0f));
            Assert.IsFalse(d.Reconcile(publisher, p, "steam1", 1f));
            Assert.IsTrue(d.Reconcile(publisher, p, "steam1",
                SteamAchievementDispatch.ReconcileSeconds));
            Assert.AreEqual(2, publisher.BoundCalls);
        }
    }
}
