using System;
using System.Globalization;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DMeshAppendBenchmarkCsvTests
    {
        [Test]
        public void ExportRetainsRawRunOrderAndInvariantNumbers()
        {
            var utc = new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
            string csv = City3DMeshAppendBenchmarkCsv.Export(utc, "6000.6.8f1",
                12, 250, new[] { "Identity", "Translation" },
                new[] { new[] { 2.5d, 1.25d }, new[] { 3.75d } });
            StringAssert.Contains("scenario,run,elapsed_ms", csv);
            StringAssert.Contains("\"Identity\",1,2.5,12,250", csv);
            StringAssert.Contains("\"Identity\",2,1.25,12,250", csv);
            StringAssert.Contains("\"Translation\",1,3.75,12,250", csv);
            Assert.AreEqual(4, csv.TrimEnd().Split('\n').Length);
        }

        [Test]
        public void ExportEscapesCsvFieldsAndIsCultureIndependent()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string csv = City3DMeshAppendBenchmarkCsv.Export(
                    new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc),
                    "Unity,\"Editor\"", 12, 250,
                    new[] { "Rotate,\"Scale\"" }, new[] { new[] { 1.75d } });
                StringAssert.Contains("\"Unity,\"\"Editor\"\"\"", csv);
                StringAssert.Contains("\"Rotate,\"\"Scale\"\"\"", csv);
                StringAssert.Contains(",1,1.75,12,250", csv);
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Test]
        public void ExportRejectsMissingOrInvalidSamples()
        {
            var now = new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkCsv.Export(now, "Unity", 12, 250,
                    new[] { "Identity" }, new double[0][]));
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkCsv.Export(now, "Unity", 12, 250,
                    new[] { "Identity" }, new[] { new[] { double.NaN } }));
            Assert.Throws<ArgumentException>(() =>
                City3DMeshAppendBenchmarkCsv.Export(now, "Unity", 12, 250,
                    new[] { "Identity" }, new[] { new[] { -1d } }));
        }
    }
}
