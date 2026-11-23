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
