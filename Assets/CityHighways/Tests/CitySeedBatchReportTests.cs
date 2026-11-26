using System;
using System.IO;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CitySeedBatchReportTests
    {
        static CitySeedReport Row(int seed, int actual, bool generated = true, params string[] issues) =>
            new CitySeedReport { requestedSeed = seed, actualSeed = actual, generated = generated, issues = issues, milliseconds = 12 };

        [Test]
        public void EmptyCancelledBatchHasNoCompletedSeeds()
        {
            var batch = CitySeedBatchReport.Begin(42, 16, true);
            Assert.AreEqual(0, batch.Completed);
            Assert.AreEqual(0, batch.Failed);
            Assert.IsFalse(batch.Complete);
        }

        [Test]
        public void CountsWarningsFailuresAndFallbackIndependently()
        {
            var batch = CitySeedBatchReport.Begin(-1, 3, true);
            var warned = Row(-1, 17);
            warned.layout3D = new City3DLayoutReport { warnings = new[] {
                new City3DLayoutIssue { code = "z" }, new City3DLayoutIssue { code = "a" }, new City3DLayoutIssue { code = "a" } } };
            batch.Add(warned, "/tmp/report.json");
            batch.Add(Row(0, 0, false, "failed"), null);
            batch.Add(Row(1, 1), null);
            Assert.AreEqual(3, batch.Completed);
            Assert.AreEqual(1, batch.Failed);
            Assert.AreEqual(1, batch.Warned);
            Assert.AreEqual(1, batch.Fallback);
            Assert.AreEqual(3, batch.rows[0].warnings);
            CollectionAssert.AreEqual(new[] { "a", "z" }, batch.rows[0].warningCodes);
            Assert.AreEqual("/tmp/report.json", batch.rows[0].reportPath);
            Assert.IsTrue(batch.Complete);
        }

        [Test]
        public void RejectsOutOfOrderDuplicateAndExtraRows()
        {
            var batch = CitySeedBatchReport.Begin(int.MaxValue - 1, 2, false);
            Assert.Throws<InvalidOperationException>(() => batch.Add(Row(int.MaxValue, int.MaxValue), null));
            batch.Add(Row(int.MaxValue - 1, int.MaxValue - 1), null);
            Assert.Throws<InvalidOperationException>(() => batch.Add(Row(int.MaxValue - 1, int.MaxValue - 1), null));
            batch.Add(Row(int.MaxValue, int.MaxValue), null);
            Assert.Throws<InvalidOperationException>(() => batch.Add(Row(int.MaxValue, int.MaxValue), null));
        }

        [Test]
        public void PartialBatchRoundTripsAndExportDoesNotOverwrite()
        {
            var batch = CitySeedBatchReport.Begin(7, 3, false);
            batch.Add(Row(7, 7), null);
            batch.Add(Row(8, 12, true, "diagnostic"), "report.json");
            string dir = Path.Combine(Path.GetTempPath(), "city-batch-" + Guid.NewGuid().ToString("N"));
            try
            {
                string first = batch.Write(dir), second = batch.Write(dir);
                Assert.AreNotEqual(first, second);
                var restored = JsonUtility.FromJson<CitySeedBatchReport>(File.ReadAllText(first));
                Assert.AreEqual(2, restored.Completed);
                Assert.AreEqual(1, restored.Fallback);
                Assert.AreEqual(1, restored.Failed);
                Assert.IsFalse(restored.Complete);
                Assert.AreEqual(3, restored.requestedCount);
                Assert.AreEqual("report.json", restored.rows[1].reportPath);
                CollectionAssert.AreEqual(new[] { "diagnostic" }, restored.rows[1].issues);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [TestCase(0)]
        [TestCase(513)]
        public void RejectsInvalidBatchSize(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CitySeedBatchReport.Begin(0, count, false));
        }
    }
}
