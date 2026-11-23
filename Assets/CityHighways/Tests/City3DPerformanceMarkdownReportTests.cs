using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceMarkdownReportTests
    {
        static City3DPerformanceReviewArtifact Review(params City3DPerformanceReviewArtifact.Scenario[] scenarios)
        {
            return new City3DPerformanceReviewArtifact
            {
                version = City3DPerformanceReviewArtifact.CurrentVersion,
                overallStatus = "Incomplete", generatedUtc = "2026-11-23T17:00:00Z",
                baselineRevision = "abc123", currentRevision = "def456",
                allowedP95RegressionPercent = 10f,
                matched = 1, passed = 1, missing = 1, additional = 1,
                coveragePercent = 50f, scenarios = scenarios
            };
        }

        [Test]
        public void EscapesUserProvidedScenarioAndRevisionLabels()
        {
            var report = Review(new City3DPerformanceReviewArtifact.Scenario
            {
                city = "Москва", quality = 1,
                scenario = "seed=42|camera=<A>\nnext",
                status = "Pass", baselineP95Ms = 20f,
                currentP95Ms = 18f, p95DeltaPercent = -10f,
                hasBaseline = true, hasCurrent = true, hasComparableDelta = true
            });
            report.baselineRevision = "before|hotfix";
            var markdown = City3DPerformanceMarkdownReport.Render(report);
            StringAssert.Contains("Москва", markdown);
            StringAssert.Contains("seed=42\\|camera=&lt;A&gt; next", markdown);
            StringAssert.Contains("before\\|hotfix", markdown);
            StringAssert.Contains("20.00 | 18.00 | -10.00%", markdown);
        }

        [Test]
        public void MissingAndAdditionalMeasurementsUsePlaceholderNotFalseZero()
        {
            var markdown = City3DPerformanceMarkdownReport.Render(Review(
                new City3DPerformanceReviewArtifact.Scenario
                {
                    city = "Magnate", quality = 0, scenario = "seed=7",
                    status = "Missing", baselineP95Ms = 20f, hasBaseline = true
                },
                new City3DPerformanceReviewArtifact.Scenario
                {
                    city = "Magnate", quality = 2, scenario = "seed=42",
                    status = "Additional", currentP95Ms = 16f, hasCurrent = true
                }));
            StringAssert.Contains("20.00 | — | — |", markdown);
            StringAssert.Contains("— | 16.00 | — |", markdown);
        }

        [Test]
        public void MarkdownContainsStatusCoverageAndMeasurementCaveat()
        {
            var markdown = City3DPerformanceMarkdownReport.Render(Review(
                new City3DPerformanceReviewArtifact.Scenario
                {
                    city = "Magnate", scenario = "seed=7", status = "Pass",
                    hasBaseline = true, hasCurrent = true, hasComparableDelta = true
                }));
            StringAssert.Contains("**Verdict:** Incomplete", markdown);
            StringAssert.Contains("**Coverage:** 50.0%", markdown);
            StringAssert.Contains("**Missing:** 1", markdown);
            StringAssert.Contains("not CPU/GPU profiler timings", markdown);
            StringAssert.Contains("abc123", markdown);
        }

        [Test]
        public void MissingOrUnsupportedArtifactFailsBeforeRendering()
        {
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceMarkdownReport.Render(null));
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceMarkdownReport.Render(
                    new City3DPerformanceReviewArtifact { version = 9,
                        scenarios = new City3DPerformanceReviewArtifact.Scenario[1] }));
            Assert.Throws<ArgumentException>(() =>
                City3DPerformanceMarkdownReport.Render(
                    new City3DPerformanceReviewArtifact {
                        version = City3DPerformanceReviewArtifact.CurrentVersion,
                        scenarios = new City3DPerformanceReviewArtifact.Scenario[0] }));
        }
    }
}
