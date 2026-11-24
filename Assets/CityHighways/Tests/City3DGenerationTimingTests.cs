using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DGenerationTimingTests
    {
        [Test]
        public void AllGenerationStageLabelsHaveMatchingProfilerMarkers()
        {
            var type = typeof(City3DScene).Assembly.GetType(
                "CityHighways.Presentation.City3DGenerationMarkers");
            Assert.IsNotNull(type);
            var fields = type.GetFields(BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            var names = fields.Where(x => x.IsLiteral &&
                x.FieldType == typeof(string)).ToArray();
            var markers = fields.Where(x => x.FieldType ==
                typeof(ProfilerMarker)).ToArray();
            Assert.AreEqual(16, names.Length);
            Assert.AreEqual(16, markers.Length);

            var labels = names.Select(x => (string)x.GetRawConstantValue()).ToArray();
            Assert.AreEqual(16, labels.Distinct().Count());
            foreach (var name in names)
            {
                Assert.IsTrue(name.Name.EndsWith("Name", StringComparison.Ordinal));
                string markerField = name.Name.Substring(0, name.Name.Length - 4);
                Assert.IsTrue(markers.Any(x => x.Name == markerField),
                    "Missing profiler marker for " + name.Name);
            }
        }

        [Test]
        public void GenerationProfilerMarkersCanOpenAndCloseInEditMode()
        {
            var type = typeof(City3DScene).Assembly.GetType(
                "CityHighways.Presentation.City3DGenerationMarkers");
            Assert.IsNotNull(type);
            var fields = type.GetFields(BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields.Where(x => x.FieldType ==
                typeof(ProfilerMarker)))
            {
                var marker = (ProfilerMarker)field.GetValue(null);
                using (marker.Auto())
                {
                    Assert.IsNotNull(field.Name);
                }
            }
        }

        [Test]
        public void CheckpointsMeasureConsecutiveStagesAndWholeBuild()
        {
            long second = Stopwatch.Frequency;
            var report = new City3DGenerationTiming("Test City", 2, second);
            report.Mark("Planning", 2 * second);
            report.Mark("Buildings", 4 * second);
            Assert.AreEqual(2, report.Stages.Count);
            Assert.AreEqual("Planning", report.Stages[0].Name);
            Assert.AreEqual(1000d, report.Stages[0].Milliseconds, 0.00001d);
            Assert.AreEqual("Buildings", report.Stages[1].Name);
            Assert.AreEqual(2000d, report.Stages[1].Milliseconds, 0.00001d);
            Assert.AreEqual(3000d, report.TotalMilliseconds, 0.00001d);
        }

        [Test]
        public void CsvContainsStageTimesAndProportions()
        {
            long second = Stopwatch.Frequency;
            var report = new City3DGenerationTiming("Test City", 1, 0);
            report.Mark("Planning", second);
            report.Mark("Ground", 4 * second);
            string csv = report.ExportCsv("6000.6.8f1");
            StringAssert.Contains("started_utc,unity_version,city,quality,stage,elapsed_ms,share_percent", csv);
            StringAssert.Contains("\"6000.6.8f1\",\"Test City\",1,\"Planning\",1000,25", csv);
            StringAssert.Contains("\"6000.6.8f1\",\"Test City\",1,\"Ground\",3000,75", csv);
            Assert.AreEqual(3, csv.TrimEnd().Split('\n').Length);
        }

        [Test]
        public void CsvEscapesCityAndStageNames()
        {
            var report = new City3DGenerationTiming("City, \"River\"", 0, 0);
            report.Mark("Buildings,\nsmall \"roof\"", Stopwatch.Frequency);
            string csv = report.ExportCsv("6000.6.8f1");
            StringAssert.Contains("\"City, \"\"River\"\"\"", csv);
            StringAssert.Contains("\"Buildings,\nsmall \"\"roof\"\"\"", csv);
        }

        [Test]
        public void CsvUsesInvariantCultureForDecimalDurations()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var report = new City3DGenerationTiming("City", 1, 0);
                report.Mark("First", Stopwatch.Frequency);
                report.Mark("Second", 3 * Stopwatch.Frequency);
                string csv = report.ExportCsv("6000.6.8f1");
                StringAssert.Contains(",1000,33.333", csv);
                StringAssert.Contains(",2000,66.666", csv);
                Assert.IsFalse(csv.Contains(",33,333"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void ZeroDurationStageProducesFinitePercentages()
        {
            var report = new City3DGenerationTiming("City", 0, 0);
            report.Mark("Planning", 0);
            report.Mark("Ground", 0);
            Assert.AreEqual(0d, report.TotalMilliseconds);
            string csv = report.ExportCsv("6000.6.8f1");
            StringAssert.Contains("\"Planning\",0,0", csv);
            StringAssert.Contains("\"Ground\",0,0", csv);
            Assert.IsFalse(csv.Contains("NaN"));
        }

        [Test]
        public void CsvExportDoesNotResetOrReorderRecordedStages()
        {
            long second = Stopwatch.Frequency;
            var report = new City3DGenerationTiming("City", 1, 0);
            report.Mark("Relief", second);
            string first = report.ExportCsv("6000.6.8f1");
            report.Mark("Districts", 3 * second);
            string secondExport = report.ExportCsv("6000.6.8f1");

            Assert.AreEqual(1, first.Split('\n').Length - 2);
            Assert.AreEqual(2, secondExport.Split('\n').Length - 2);
            Assert.Less(secondExport.IndexOf("\"Relief\""),
                secondExport.IndexOf("\"Districts\""));
            Assert.AreEqual(3000d, report.TotalMilliseconds, 0.00001d);
            Assert.AreEqual(2, report.Stages.Count);
        }

        [Test]
        public void InvalidStageNamesAndBackwardsTimestampsAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new City3DGenerationTiming("", 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new City3DGenerationTiming("City", 0, -1));
            var report = new City3DGenerationTiming("City", 0, 20);
            Assert.Throws<ArgumentException>(() => report.Mark("", 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => report.Mark("Planning", 19));
            report.Mark("Planning", 30);
            Assert.Throws<ArgumentException>(() => report.Mark("Planning", 40));
            Assert.AreEqual(1, report.Stages.Count);
        }

        [Test]
        public void CsvNeedsRecordedStagesAndUnityVersion()
        {
            var report = new City3DGenerationTiming("City", 2, 0);
            Assert.Throws<ArgumentException>(() => report.ExportCsv(""));
            Assert.Throws<InvalidOperationException>(() => report.ExportCsv("6000.6.8f1"));
        }
    }
}
