using System;
using System.IO;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class CitySeedDiagnosticsTests
    {
        [TestCase(0, 16)]
        [TestCase(16, 16)]
        [TestCase(-16, 16)]
        [TestCase(2026, 16)]
        public void SeedRangeProducesValidCities(int first, int count)
        {
            var failures = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                var report = CitySeedDiagnostics.Run(first + i);
                if (!report.Passed)
                {
                    var path = CitySeedDiagnostics.Write(report, Path.GetFullPath(Path.Combine(Application.dataPath, "..", "artifacts", "city-seed-diagnostics")));
                    failures.Add("seed " + report.requestedSeed + ": " + string.Join("; ", report.issues) + " | " + path);
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        public void SameSeedHasSameLayout(int seed)
        {
            var first = CitySeedDiagnostics.Run(seed);
            var second = CitySeedDiagnostics.Run(seed);
            Assert.IsTrue(first.Passed, string.Join("; ", first.issues));
            Assert.IsTrue(second.Passed, string.Join("; ", second.issues));
            Assert.AreEqual(CitySeedDiagnostics.LayoutJson(first), CitySeedDiagnostics.LayoutJson(second));
        }

        [Test]
        public void DiagnosticFileRoundTripsAndDoesNotOverwrite()
        {
            var report = new CitySeedReport { requestedSeed = -3, actualSeed = 5, generated = true,
                cityName = "../city", issues = new[] { "test" } };
            string dir = Path.Combine(Path.GetTempPath(), "cityseed-" + Guid.NewGuid().ToString("N"));
            try
            {
                string a = CitySeedDiagnostics.Write(report, dir);
                string b = CitySeedDiagnostics.Write(report, dir);
                Assert.AreNotEqual(a, b);
                Assert.AreEqual(dir, Path.GetDirectoryName(a));
                var restored = JsonUtility.FromJson<CitySeedReport>(File.ReadAllText(a));
                Assert.AreEqual(-3, restored.requestedSeed);
                Assert.AreEqual(5, restored.actualSeed);
                Assert.AreEqual("test", restored.issues[0]);
                Assert.IsFalse(restored.Passed);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
