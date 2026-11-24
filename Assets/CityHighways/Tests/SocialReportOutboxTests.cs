using System;
using System.IO;
using CityHighways.Presentation;
using NUnit.Framework;

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

    }
}
