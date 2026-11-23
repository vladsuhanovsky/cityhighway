using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class CityProceduralAuditReportTests
    {
        [Test]
        public void CategoryFilterMatchesAllOrExactCategory()
        {
            Assert.IsTrue(CityProceduralAuditReport.Matches(
                CityProceduralAuditReport.Category.All, CityProceduralAuditReport.Category.Offset));
            Assert.IsTrue(CityProceduralAuditReport.Matches(
                CityProceduralAuditReport.Category.Geometry, CityProceduralAuditReport.Category.Geometry));
            Assert.IsFalse(CityProceduralAuditReport.Matches(
                CityProceduralAuditReport.Category.Oversized, CityProceduralAuditReport.Category.BelowGround));
        }

        [Test]
        public void DuplicateIdsDetectEveryRepeatedNonEmptyStableId()
        {
            var duplicates = CityProceduralAuditReport.DuplicateIds(new[]
            {
                "B:1:10:20", "T:9:30:40", "B:1:10:20", "", "",
                "T:9:30:40", "B:7:50:60"
            });
            Assert.AreEqual(2, duplicates.Count);
            Assert.IsTrue(duplicates.Contains("B:1:10:20"));
            Assert.IsTrue(duplicates.Contains("T:9:30:40"));
            Assert.IsFalse(duplicates.Contains(""));
            Assert.IsFalse(duplicates.Contains("B:7:50:60"));
        }

        [Test]
        public void DuplicateFilterKeepsOtherAuditCategoriesSeparate()
        {
            Assert.IsTrue(CityProceduralAuditReport.Matches(
                CityProceduralAuditReport.Category.DuplicateId,
                CityProceduralAuditReport.Category.DuplicateId));
            Assert.IsFalse(CityProceduralAuditReport.Matches(
                CityProceduralAuditReport.Category.DuplicateId,
                CityProceduralAuditReport.Category.Configuration));
        }

        [Test]
        public void NullAndUniqueIdsProduceNoConflict()
        {
            Assert.AreEqual(0, CityProceduralAuditReport.DuplicateIds(null).Count);
            Assert.AreEqual(0, CityProceduralAuditReport.DuplicateIds(
                new[] { "B:1:1:1", "T:1:1:1" }).Count);
        }

        [Test]
        public void CsvQuotesCommasNewlinesAndDoubleQuotes()
        {
            Assert.AreEqual("\"one,two\",\"say \"\"hi\"\"\",\"line1\nline2\"",
                CityProceduralAuditReport.CsvRow("one,two", "say \"hi\"", "line1\nline2"));
        }

        [Test]
        public void CsvNeutralizesSpreadsheetFormulaPrefixes()
        {
            Assert.AreEqual("\"'=HYPERLINK(1)\"",
                CityProceduralAuditReport.EscapeCell("=HYPERLINK(1)"));
            Assert.AreEqual("\"'  +42\"", CityProceduralAuditReport.EscapeCell("  +42"));
            Assert.AreEqual("\"'-3\"", CityProceduralAuditReport.EscapeCell("-3"));
            Assert.AreEqual("\"'@user\"", CityProceduralAuditReport.EscapeCell("@user"));
            Assert.AreEqual("\"plain\"", CityProceduralAuditReport.EscapeCell("plain"));
        }

        [Test]
        public void CsvEmptyAndNullCellsRemainValid()
        {
            Assert.AreEqual("\"\",\"\"", CityProceduralAuditReport.CsvRow(null, ""));
        }
    }
}
