using System;
using System.IO;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CitySeedBatchArchiveTests
    {
        string directory;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "city-archive-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void Cleanup() => Directory.Delete(directory, true);

        static CitySeedReport Row(int seed, bool passed = true, int actual = 0, int warnings = 0)
        {
            return new CitySeedReport { requestedSeed = seed, actualSeed = actual == 0 ? seed : actual,
                generated = passed, issues = passed ? Array.Empty<string>() : new[] { "generation failed" },
                layout3D = new City3DLayoutReport { warnings = Enumerable.Range(0, warnings)
                    .Select(i => new City3DLayoutIssue { code = "river" }).ToArray() } };
        }

        [Test]
        public void ExistingFormatRoundTripsIncludingCancelledBatch()
        {
            var batch = CitySeedBatchReport.Begin(-2, 3, true);
            batch.Add(Row(-2, warnings: 2), "../outside.json");
            var restored = CitySeedBatchArchive.Read(batch.Write(directory));
            Assert.AreEqual(1, restored.Completed);
            Assert.AreEqual(1, restored.Warned);
            Assert.IsFalse(restored.Complete);
            Assert.AreEqual("../outside.json", restored.rows[0].reportPath);
        }

        [Test]
        public void EmptyCancelledBatchCanBeImported()
        {
            var batch = CitySeedBatchReport.Begin(42, 16, false);
            Assert.AreEqual(0, CitySeedBatchArchive.Read(batch.Write(directory)).Completed);
        }

        [TestCase("{}")]
        [TestCase("{")]
        [TestCase("{\"schema\":99,\"requestedCount\":1}")]
        [TestCase("{\"requestedCount\":1,\"utc\":\"2026-11-26T00:00:00.0000000Z\",\"unityVersion\":\"6000\",\"rows\":[]}")]
        public void RejectsInvalidOrUnsupportedJson(string json)
        {
            var path = Path.Combine(directory, "bad.json");
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => CitySeedBatchArchive.Read(path));
        }

        [Test]
        public void RejectsOversizedFileBeforeParsing()
        {
            var path = Path.Combine(directory, "large.json");
            using (var file = File.Create(path)) file.SetLength(CitySeedBatchArchive.MaxBytes + 1L);
            Assert.Throws<InvalidDataException>(() => CitySeedBatchArchive.Read(path));
        }

        [Test]
        public void RejectsInconsistentRowAndMode()
        {
            var batch = CitySeedBatchReport.Begin(42, 1, false);
            batch.Add(Row(42), null);
            var row = batch.rows[0];
            row.requestedSeed = 43;
            Assert.Throws<InvalidDataException>(() => CitySeedBatchArchive.Validate(batch));
            row.requestedSeed = 42;
            row.warnings = 1; row.warningCodes = new[] { "river" };
            Assert.Throws<InvalidDataException>(() => CitySeedBatchArchive.Validate(batch));
            row.warnings = 0; row.warningCodes = Array.Empty<string>(); row.passed = false;
            Assert.Throws<InvalidDataException>(() => CitySeedBatchArchive.Validate(batch));
        }

        [Test]
        public void ListsOnlyTopLevelSummaryFilesWithinLimit()
        {
            var batch = CitySeedBatchReport.Begin(0, 1, false);
            for (int i = 0; i < CitySeedBatchArchive.MaxVisible + 2; i++) batch.Write(directory);
            File.WriteAllText(Path.Combine(directory, "city-seed-42.json"), "{}");
            Directory.CreateDirectory(Path.Combine(directory, "nested"));
            batch.Write(Path.Combine(directory, "nested"));
            var listed = CitySeedBatchArchive.List(directory);
            Assert.AreEqual(CitySeedBatchArchive.MaxVisible, listed.Length);
            Assert.IsTrue(listed.All(p => Path.GetDirectoryName(p) == directory && Path.GetFileName(p).StartsWith("city-batch-")));
        }

        [Test]
        public void ComparesOnlySharedCompletedSeedsAndDoesNotResolveWarningsOnFailure()
        {
            var before = CitySeedBatchReport.Begin(1, 5, true);
            before.Add(Row(1), null);
            before.Add(Row(2, warnings: 3), null);
            before.Add(Row(3, false), null);
            before.Add(Row(4, warnings: 1), null);
            var after = CitySeedBatchReport.Begin(2, 5, true);
            after.Add(Row(2, false), null);
            after.Add(Row(3), null);
            after.Add(Row(4, actual: 90, warnings: 0), null);
            after.Add(Row(5, warnings: 2), null);
            var diff = CitySeedBatchComparison.Compare(before, after);
            Assert.AreEqual(3, diff.Shared);
            Assert.AreEqual(1, diff.OnlyBaseline);
            Assert.AreEqual(1, diff.OnlyCurrent);
            Assert.AreEqual(1, diff.NewFailures);
            Assert.AreEqual(1, diff.ResolvedFailures);
            Assert.AreEqual(1, diff.GeneratedInBoth);
            Assert.AreEqual(1, diff.ResolvedWarnings);
            Assert.AreEqual(-1, diff.WarningDelta);
            Assert.AreEqual(1, diff.ActualSeedChanged);
        }

        [Test]
        public void DifferentDiagnosticModesCannotBeCompared()
        {
            Assert.Throws<ArgumentException>(() => CitySeedBatchComparison.Compare(
                CitySeedBatchReport.Begin(1, 1, false), CitySeedBatchReport.Begin(1, 1, true)));
        }

        [Test]
        public void DisjointRangesHaveNoSharedRows()
        {
            var before = CitySeedBatchReport.Begin(1, 1, true);
            before.Add(Row(1), null);
            var after = CitySeedBatchReport.Begin(2, 1, true);
            after.Add(Row(2), null);
            var diff = CitySeedBatchComparison.Compare(before, after);
            Assert.AreEqual(0, diff.Shared);
            Assert.AreEqual(1, diff.OnlyBaseline);
            Assert.AreEqual(1, diff.OnlyCurrent);
        }
    }
}
