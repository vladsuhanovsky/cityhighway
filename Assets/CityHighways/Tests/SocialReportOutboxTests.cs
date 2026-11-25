using System;
using System.Collections.Generic;
using System.IO;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class SocialReportOutboxTests
    {
        string _folder;
        string _path;
        const string SteamA = "76561198000000001";
        const string SteamB = "76561198000000002";
        const long Now = 638900000000000000L;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cityhighways-outbox-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_folder, "social", "reports.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void ReportSurvivesFreshProcessAndIsOwnedByOneSteamAccount()
        {
            var first = new SocialReportOutbox(_path);
            Assert.IsTrue(first.Enqueue(SteamA, "magnate:abc", "{\"action\":\"report\"}", Now));

            var afterRestart = new SocialReportOutbox(_path);
            Assert.AreEqual(1, afterRestart.Count);
            Assert.IsNull(afterRestart.Next(SteamB, Now));
            Assert.AreEqual("magnate:abc", afterRestart.Next(SteamA, Now)?.key);
        }

        [Test]
        public void DuplicateLocalReportKeyIsIdempotentBeforeUpload()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "classic:replay-1", "{\"score\":100}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "classic:replay-1", "{\"score\":999}", Now));
            Assert.AreEqual(1, outbox.Count);
            Assert.AreEqual("{\"score\":100}", outbox.Next(SteamA, Now).requestJson);
        }

        [Test]
        public void DifferentSteamAccountsCanStoreIndependentPendingReports()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "magnate:same", "{}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamB, "magnate:same", "{}", Now));
            Assert.AreEqual(2, outbox.Count);
            Assert.AreEqual(SteamA, outbox.Next(SteamA, Now).ownerSteamId);
            Assert.AreEqual(SteamB, outbox.Next(SteamB, Now).ownerSteamId);
        }

        [Test]
        public void SuccessfulReportIsRemovedFromDiskAndNeverReplayed()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "classic:report", "{}", Now));
            var report = outbox.Next(SteamA, Now);
            Assert.IsTrue(outbox.Complete(report));
            Assert.IsNull(outbox.Next(SteamA, Now));
            Assert.AreEqual(0, new SocialReportOutbox(_path).Count);
        }

        [Test]
        public void FailedReportIsDeferredAcrossRestart()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "magnate:retry", "{}", Now));
            var report = outbox.Next(SteamA, Now);
            Assert.IsTrue(outbox.Reschedule(report, Now, 503));
            var reloaded = new SocialReportOutbox(_path);
            Assert.IsNull(reloaded.Next(SteamA, Now + 10 * TimeSpan.TicksPerSecond));
            Assert.IsNotNull(reloaded.Next(SteamA, Now + 15 * TimeSpan.TicksPerSecond));
            Assert.AreEqual(1, reloaded.Entries[0].attempts);
        }

        [Test]
        public void RetryScheduleExponentialButCapped()
        {
            Assert.AreEqual(15, SocialReportOutbox.RetryDelaySeconds(1, 503));
            Assert.AreEqual(30, SocialReportOutbox.RetryDelaySeconds(2, 503));
            Assert.AreEqual(60, SocialReportOutbox.RetryDelaySeconds(3, 503));
            Assert.AreEqual(900, SocialReportOutbox.RetryDelaySeconds(50, 503));
            Assert.AreEqual(900, SocialReportOutbox.RetryDelaySeconds(1, 429));
            Assert.AreEqual(300, SocialReportOutbox.RetryDelaySeconds(1, 404));
            Assert.AreEqual(3600, SocialReportOutbox.RetryDelaySeconds(1, 400));
        }

        [Test]
        public void DoesNotWriteOrShareInvalidAccountIdentifiers()
        {
            var outbox = new SocialReportOutbox(_path);
            foreach (var owner in new[] { "", "abcde", "7656 1198", "-123456", null })
                Assert.IsFalse(outbox.Enqueue(owner, "k", "{}", Now));
            Assert.AreEqual(0, outbox.Count);
            Assert.IsNull(outbox.Next(null, Now));
        }

        [Test]
        public void RejectsOversizedPayloadAndEmptyKeys()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsFalse(outbox.Enqueue(SteamA, "id", new string('x', SocialReportOutbox.MaxPayloadBytes + 1), Now));
            Assert.IsFalse(outbox.Enqueue(SteamA, "", "{}", Now));
            Assert.IsFalse(outbox.Enqueue(SteamA, "key", "", Now));
            Assert.AreEqual(0, outbox.Count);
        }

        [Test]
        public void OversizeDiskFileIsNeverReadIntoMemory()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using (var file = File.Create(_path))
                file.SetLength(SocialReportOutbox.MaxFileBytes + 1);
            Assert.AreEqual(0, new SocialReportOutbox(_path).Count);
        }

        [Test]
        public void PayloadBoundIsMeasuredInUtf8BytesNotCharacterCount()
        {
            var outbox = new SocialReportOutbox(_path);
            string unicode = new string('Ж', SocialReportOutbox.MaxPayloadBytes / 2 + 1);
            Assert.IsFalse(outbox.Enqueue(SteamA, "unicode", unicode, Now));
        }

        [Test]
        public void QueueHasAnExplicitBound()
        {
            var outbox = new SocialReportOutbox(_path);
            for (int i = 0; i < SocialReportOutbox.MaxEntries; i++)
                Assert.IsTrue(outbox.Enqueue(SteamA, "game-" + i, "{}", Now + i));
            Assert.AreEqual(SocialReportOutbox.MaxEntries, outbox.Count);
            Assert.IsFalse(outbox.Enqueue(SteamA, "extra", "{}", Now));
            Assert.AreEqual(SocialReportOutbox.MaxEntries,
                new SocialReportOutbox(_path).Count);
        }

        [Test]
        public void CorruptPrimaryRecoversFromPreviousBackup()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "first", "{}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "second", "{}", Now + 1));
            File.WriteAllText(_path, "not valid json");
            var restored = new SocialReportOutbox(_path);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("first", restored.Next(SteamA, Now)?.key);
        }

        [Test]
        public void RetryKeepsOriginalOwnerAndKeyAfterReload()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "game-key", "{}", Now));
            var entry = outbox.Next(SteamA, Now);
            Assert.IsTrue(outbox.Reschedule(entry, Now, 429));
            var reloaded = new SocialReportOutbox(_path);
            Assert.AreEqual("game-key", reloaded.Entries[0].key);
            Assert.AreEqual(SteamA, reloaded.Entries[0].ownerSteamId);
            Assert.IsNull(reloaded.Next(SteamB, Now + TimeSpan.TicksPerDay));
        }

        [Test]
        public void OldestEligibleReportIsSelectedBeforeNewer()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "new", "{}", Now + 100));
            Assert.IsTrue(outbox.Enqueue(SteamA, "old", "{}", Now));
            Assert.AreEqual("old", outbox.Next(SteamA, Now + 100).key);
        }

        [Test]
        public void ReportsFromSundayCannotCreditTheFollowingMonday()
        {
            long sunday = new DateTime(2026, 11, 29, 23, 59, 0, DateTimeKind.Utc).Ticks;
            long monday = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc).Ticks;
            Assert.IsFalse(SocialReportOutbox.SameUtcWeek(sunday, monday));
            Assert.IsTrue(SocialReportOutbox.SameUtcWeek(sunday,
                new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc).Ticks));
        }

        [Test]
        public void IsoWeekBoundaryAcrossNewYearDoesNotUseCalendarYear()
        {
            long december = new DateTime(2026, 12, 31, 23, 50, 0, DateTimeKind.Utc).Ticks;
            long january = new DateTime(2027, 1, 1, 0, 10, 0, DateTimeKind.Utc).Ticks;
            Assert.IsTrue(SocialReportOutbox.SameUtcWeek(december, january));
        }

        [Test]
        public void RemovingUnknownReportDoesNotChangeQueue()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "existing", "{}", Now));
            Assert.IsFalse(outbox.Complete(new SocialReportOutbox.Entry { key = "other" }));
            Assert.AreEqual(1, outbox.Count);
        }
        [Test]
        public void FailedInitialDiskWriteDoesNotLeavePhantomQueuedReport()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, "social"), "not a directory");
            var outbox = new SocialReportOutbox(_path);
            Assert.IsFalse(outbox.Enqueue(SteamA, "lost", "{}", Now));
            Assert.AreEqual(0, outbox.Count);
            Assert.IsNull(outbox.Next(SteamA, Now));
        }

        [Test]
        public void FailedDiskWritesRollBackRemovalAndRetrySchedule()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "durable", "{}", Now));
            var entry = outbox.Next(SteamA, Now);
            Assert.IsNotNull(entry);

            // A directory occupying the temporary-file name reliably blocks
            // the atomic write on Windows, macOS and Linux.
            Directory.CreateDirectory(_path + ".tmp");
            Assert.IsFalse(outbox.Reschedule(entry, Now, 503));
            Assert.AreEqual(0, entry.attempts);
            Assert.AreEqual(Now, entry.nextAttemptUtcTicks);
            Assert.AreEqual(entry, outbox.Next(SteamA, Now));

            Assert.IsFalse(outbox.Complete(entry));
            Assert.AreEqual(1, outbox.Count);
            Assert.AreEqual(entry, outbox.Next(SteamA, Now));

            Directory.Delete(_path + ".tmp");
            Assert.IsTrue(outbox.Reschedule(entry, Now, 503));
            Assert.IsNull(outbox.Next(SteamA, Now));
            Assert.IsTrue(outbox.Complete(entry));
            Assert.AreEqual(0, new SocialReportOutbox(_path).Count);
        }

        [Test]
        public void InvalidRetryClockDoesNotMutatePendingReport()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "clock", "{}", Now));
            var entry = outbox.Next(SteamA, Now);
            Assert.IsFalse(outbox.Reschedule(entry, -1, 503));
            Assert.IsFalse(outbox.Reschedule(entry, DateTime.MaxValue.Ticks + 1, 503));
            Assert.AreEqual(0, entry.attempts);
            Assert.AreEqual(Now, entry.nextAttemptUtcTicks);
        }

        [Test]
        public void RecoveringBackupNeverReplacesTheGoodBackupWithCorruptPrimary()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "first", "{}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "second", "{}", Now + 1));

            File.WriteAllText(_path, "broken primary");
            var recovered = new SocialReportOutbox(_path);
            Assert.AreEqual(1, recovered.Count);
            Assert.AreEqual("first", recovered.Entries[0].key);

            Assert.IsTrue(recovered.Enqueue(SteamA, "third", "{}", Now + 2));
            File.WriteAllText(_path, "broken again");
            var secondRecovery = new SocialReportOutbox(_path);
            Assert.AreEqual(1, secondRecovery.Count);
            Assert.AreEqual("first", secondRecovery.Entries[0].key,
                "The first repair must not replace the healthy backup with corrupt primary data.");
        }

        [Test]
        public void EnqueueRejectsOutOfRangeClockAndInvalidCurrentTime()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsFalse(outbox.Enqueue(SteamA, "future", "{}", DateTime.MaxValue.Ticks + 1));
            Assert.IsFalse(outbox.Enqueue(SteamA, "negative", "{}", -1));
            Assert.IsNull(outbox.Next(SteamA, -1));
            Assert.IsNull(outbox.Next(SteamA, DateTime.MaxValue.Ticks + 1));
            Assert.AreEqual(0, outbox.Count);
        }

        [Serializable]
        sealed class LegacyStateFixture
        {
            public List<SocialReportOutbox.Entry> entries = new List<SocialReportOutbox.Entry>();
        }

        [Test]
        public void NewSnapshotsStoreIntegrityMetadataAndSurviveRestart()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "first", "{}", Now));
            string json = File.ReadAllText(_path);
            StringAssert.Contains("\"formatVersion\":1", json);
            StringAssert.Contains("\"checksum\":", json);
            var reloaded = new SocialReportOutbox(_path);
            Assert.IsFalse(reloaded.LoadBlocked);
            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual("first", reloaded.Next(SteamA, Now).key);
        }

        [Test]
        public void ChangedButValidJsonInPrimaryFallsBackToChecksummedBackup()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "first", "{}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "second", "{}", Now + 1));

            string primary = File.ReadAllText(_path);
            StringAssert.Contains("\"key\":\"second\"", primary);
            File.WriteAllText(_path, primary.Replace("\"key\":\"second\"", "\"key\":\"forged\""));

            var restored = new SocialReportOutbox(_path);
            Assert.IsFalse(restored.LoadBlocked);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("first", restored.Next(SteamA, Now).key);
        }

        [Test]
        public void InvalidEntryInPrimaryNeverDropsValidBackupRecords()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "first", "{}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "second", "{}", Now + 1));
            string primary = File.ReadAllText(_path);
            StringAssert.Contains("\"ownerSteamId\":\"" + SteamA + "\"", primary);
            File.WriteAllText(_path,
                primary.Replace("\"ownerSteamId\":\"" + SteamA + "\"", "\"ownerSteamId\":\"invalid\""));
            var restored = new SocialReportOutbox(_path);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("first", restored.Next(SteamA, Now).key);
        }

        [Test]
        public void LegacyQueueIsAcceptedAndUpgradedToVersionedFormat()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var legacy = new LegacyStateFixture();
            legacy.entries.Add(new SocialReportOutbox.Entry
            {
                ownerSteamId = SteamA,
                key = "legacy",
                requestJson = "{\"action\":\"report\"}",
                createdUtcTicks = Now,
                nextAttemptUtcTicks = Now,
                attempts = 0
            });
            File.WriteAllText(_path, JsonUtility.ToJson(legacy));

            var migrated = new SocialReportOutbox(_path);
            Assert.IsFalse(migrated.LoadBlocked);
            Assert.AreEqual("legacy", migrated.Next(SteamA, Now).key);
            Assert.IsTrue(migrated.Enqueue(SteamA, "new", "{}", Now + 1));
            StringAssert.Contains("\"formatVersion\":1", File.ReadAllText(_path));
            var afterRestart = new SocialReportOutbox(_path);
            Assert.IsFalse(afterRestart.LoadBlocked);
            Assert.AreEqual(2, afterRestart.Count);
        }

        [Test]
        public void MissingEntriesFieldCannotTurnCorruptSnapshotIntoBlankQueue()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{}");
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.LoadBlocked);
            Assert.AreEqual(0, outbox.Count);
            Assert.IsFalse(outbox.Enqueue(SteamA, "new", "{}", Now));
            Assert.AreEqual("{}", File.ReadAllText(_path));
        }

        [Test]
        public void UnsupportedSnapshotVersionIsNotOverwritten()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "original", "{}", Now));
            string unknown = File.ReadAllText(_path).Replace("\"formatVersion\":1", "\"formatVersion\":99");
            File.WriteAllText(_path, unknown);
            var blocked = new SocialReportOutbox(_path);
            Assert.IsTrue(blocked.LoadBlocked);
            Assert.IsFalse(blocked.Enqueue(SteamA, "new", "{}", Now + 1));
            Assert.AreEqual(unknown, File.ReadAllText(_path));
        }

        [Test]
        public void BothCorruptSnapshotsArePreservedUntilManualRecovery()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "first", "{}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "second", "{}", Now + 1));
            File.WriteAllText(_path, "broken primary");
            File.WriteAllText(_path + ".bak", "broken backup");

            var blocked = new SocialReportOutbox(_path);
            Assert.IsTrue(blocked.LoadBlocked);
            Assert.IsFalse(blocked.Enqueue(SteamA, "third", "{}", Now + 2));
            Assert.AreEqual("broken primary", File.ReadAllText(_path));
            Assert.AreEqual("broken backup", File.ReadAllText(_path + ".bak"));
        }

        [Test]
        public void MalformedReportCanBeDeferredWithoutBlockingNewerValidReports()
        {
            var outbox = new SocialReportOutbox(_path);
            Assert.IsTrue(outbox.Enqueue(SteamA, "malformed", "{bad}", Now));
            Assert.IsTrue(outbox.Enqueue(SteamA, "newer", "{\"action\":\"report\"}", Now + 1));
            var malformed = outbox.Next(SteamA, Now + 1);
            Assert.AreEqual("malformed", malformed.key);
            Assert.IsTrue(outbox.Reschedule(malformed, Now + 1, 422));
            Assert.AreEqual("newer", outbox.Next(SteamA, Now + 1).key);
            var reloaded = new SocialReportOutbox(_path);
            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual("newer", reloaded.Next(SteamA, Now + 1).key);
            Assert.AreEqual("malformed",
                reloaded.Next(SteamA, Now + 3601 * TimeSpan.TicksPerSecond).key);
        }

    }
}
