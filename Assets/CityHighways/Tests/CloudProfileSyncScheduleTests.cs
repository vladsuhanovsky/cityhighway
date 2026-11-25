using System;
using System.IO;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class CloudProfileSyncScheduleTests
    {
        [Test]
        public void TicketTimeoutRejectsLateCallbackEvenWithinSameAccount()
        {
            var schedule = new CloudProfileSyncSchedule();
            schedule.Reset(0);
            Assert.IsTrue(schedule.TryBegin(1, out int first));
            Assert.IsFalse(schedule.TicketExpired(15.99f));
            Assert.IsTrue(schedule.TicketExpired(16));
            Assert.IsTrue(schedule.Fail(first, 16));
            Assert.IsFalse(schedule.BeginHttp(first));
            Assert.IsFalse(schedule.TryBegin(30, out _));
            Assert.IsTrue(schedule.TryBegin(31, out int next));
            Assert.IsFalse(schedule.IsCurrent(first));
            Assert.IsFalse(schedule.Complete(first, 31));
            Assert.IsTrue(schedule.IsCurrent(next));
        }

        [Test]
        public void HttpPhaseIsNotExpiredByTicketDeadline()
        {
            var schedule = new CloudProfileSyncSchedule();
            Assert.IsTrue(schedule.TryBegin(0, out int token));
            Assert.IsTrue(schedule.BeginHttp(token));
            Assert.IsFalse(schedule.BeginHttp(token));
            Assert.IsFalse(schedule.TicketExpired(30));
            Assert.IsFalse(schedule.TryBegin(30, out _));
            Assert.IsTrue(schedule.Complete(token, 30));
            Assert.IsFalse(schedule.Due(329));
            Assert.IsTrue(schedule.Due(330));
        }

        [Test]
        public void FailuresBackOffAndDirtySavesCannotBypassRetry()
        {
            var schedule = new CloudProfileSyncSchedule();
            float now = 0;
            foreach (float delay in new[] { 15f, 30f, 60f, 120f, 240f, 300f, 300f })
            {
                Assert.IsTrue(schedule.TryBegin(now, out int token));
                Assert.IsTrue(schedule.Fail(token, now));
                schedule.MarkDirty(now, 1);
                Assert.IsFalse(schedule.Due(now + delay - 0.01f));
                Assert.IsFalse(schedule.TryBegin(now + delay - 0.01f, out _));
                now += delay;
                Assert.IsTrue(schedule.Due(now));
            }
            Assert.IsTrue(schedule.TryBegin(now, out int success));
            Assert.IsTrue(schedule.Complete(success, now));
            Assert.IsTrue(schedule.TryBegin(now, out int retry));
            schedule.Fail(retry, now);
            Assert.IsTrue(schedule.Due(now + 15));
        }

        [Test]
        public void ChangesDuringFlightAreNotLost()
        {
            var schedule = new CloudProfileSyncSchedule();
            Assert.IsTrue(schedule.TryBegin(0, out int token));
            schedule.MarkDirty(2);
            schedule.MarkDirty(3);
            schedule.Complete(token, 4);
            Assert.IsFalse(schedule.Due(9.99f));
            Assert.IsTrue(schedule.Due(10));
        }

        [Test]
        public void AdminReceiptSchedulesPromptFollowUp()
        {
            var schedule = new CloudProfileSyncSchedule();
            schedule.TryBegin(0, out int token);
            schedule.BeginHttp(token);
            schedule.MarkDirty(5, 1);
            schedule.Complete(token, 5);
            Assert.IsFalse(schedule.Due(5.99f));
            Assert.IsTrue(schedule.Due(6));
        }

        [Test]
        public void AccountResetFencesOldResponsesAndClearsBackoff()
        {
            var schedule = new CloudProfileSyncSchedule();
            schedule.TryBegin(0, out int old);
            schedule.Fail(old, 0);
            schedule.Reset(1);
            Assert.IsTrue(schedule.Due(2));
            schedule.TryBegin(2, out int current);
            Assert.IsFalse(schedule.Fail(old, 2));
            Assert.IsTrue(schedule.IsCurrent(current));
        }

        [Test]
        public void AtomicProfileRoundTripRetainsProgressAndReceipts()
        {
            string folder = Path.Combine(Path.GetTempPath(), "cityhighways-profile-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "profiles.json");
            try
            {
                var book = new ProfileBook();
                var profile = new PlayerProfile { name = "Анна", wallet = 100 };
                book.profiles.Add(profile);
                ReplayStorageIO.WriteAtomic(path, JsonUtility.ToJson(book));
                profile.wallet = 120;
                profile.appliedActions.Add(42);
                ReplayStorageIO.WriteAtomic(path, JsonUtility.ToJson(book));
                var restored = JsonUtility.FromJson<ProfileBook>(File.ReadAllText(path));
                Assert.AreEqual("Анна", restored.profiles[0].name);
                Assert.AreEqual(120, restored.profiles[0].wallet);
                CollectionAssert.AreEqual(new long[] { 42 }, restored.profiles[0].appliedActions);
                Assert.AreEqual(1, Directory.GetFiles(folder).Length);
                // Strict encoding rejects corrupt UTF-16 before replacing the old file.
                Assert.Throws<System.Text.EncoderFallbackException>(() => ReplayStorageIO.WriteAtomic(path, "\ud800"));
                Assert.AreEqual(120, JsonUtility.FromJson<ProfileBook>(File.ReadAllText(path)).profiles[0].wallet);
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
