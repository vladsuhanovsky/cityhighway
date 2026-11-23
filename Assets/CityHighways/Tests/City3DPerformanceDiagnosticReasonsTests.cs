using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceDiagnosticReasonsTests
    {
        static City3DPerformanceEnvironment Env(string gpu = "GPU A",
            int width = 1920, int vSync = 0)
        {
            return new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1",
                platform = "WindowsPlayer",
                graphicsDevice = gpu,
                width = width, height = 1080,
                vSyncCount = vSync, targetFrameRate = -1
            };
        }

        static City3DPerformanceBaseline Capture(string scenario,
            City3DPerformanceEnvironment environment, float p95 = 20f) =>
            City3DPerformanceBaseline.Create("Magnate", 1, scenario,
                new City3DFrameMonitor.Snapshot
                {
                    Frames = 120, AverageMs = p95 * 0.8f,
                    P95Ms = p95, P99Ms = p95 + 2f, MaxMs = p95 + 5f
                }, captureEnvironment: environment);

        static City3DPerformanceSuite Suite(params City3DPerformanceBaseline[] captures)
        {
            var suite = new City3DPerformanceSuite();
            foreach (var capture in captures)
                Assert.IsTrue(suite.TryAdd(capture, out _));
            return suite;
        }

        [Test]
        public void HardwareMismatchReportsGpuAndViewportReasons()
        {
            var baseline = Suite(Capture("seed=7", Env()),
                Capture("seed=42", Env()));
            var current = Suite(Capture("seed=7", Env(gpu: "GPU B")),
                Capture("seed=42", Env(width: 1280)));
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            Assert.AreEqual(2, report.Invalid);
            var gpuRow = System.Array.Find(report.Rows, row => row.Scenario == "seed=7");
            var viewportRow = System.Array.Find(report.Rows,
                row => row.Scenario == "seed=42");
            Assert.AreEqual("Graphics device differs.", gpuRow.Reason);
            Assert.AreEqual("Viewport resolution differs.", viewportRow.Reason);
        }

        [Test]
        public void MissingEnvironmentDoesNotClaimHardwareMatch()
        {
            var report = City3DPerformanceDiagnostics.Build(
                Suite(Capture("seed=7", null)), Suite(Capture("seed=7", Env())), 10f);
            Assert.AreEqual(1, report.Invalid);
            Assert.AreEqual("Environment metadata absent on one side.", report.Rows[0].Reason);
        }

        [Test]
        public void MissingAdditionalAndRegressionExplainTheirStatus()
        {
            var baseline = Suite(Capture("seed=7", Env(), 20f),
                Capture("seed=42", Env(), 20f));
            var current = Suite(Capture("seed=7", Env(), 25f),
                Capture("seed=99", Env(), 18f));
            var report = City3DPerformanceDiagnostics.Build(baseline, current, 10f);
            Assert.AreEqual(City3DPerformanceDiagnostics.Kind.Regression, report.Rows[0].Status);
            StringAssert.Contains("P95 increase", report.Rows[0].Reason);
            Assert.AreEqual(City3DPerformanceDiagnostics.Kind.Missing, report.Rows[1].Status);
            StringAssert.Contains("No current capture", report.Rows[1].Reason);
            Assert.AreEqual(City3DPerformanceDiagnostics.Kind.Additional, report.Rows[2].Status);
            StringAssert.Contains("No baseline capture", report.Rows[2].Reason);
        }

        [Test]
        public void ReviewJsonAndMarkdownKeepDiagnosticReasonEscaped()
        {
            var baseline = Suite(Capture("seed=7|camera=<A>", Env()));
            var current = Suite(Capture("seed=7|camera=<A>", Env(vSync: 1)));
            var artifact = City3DPerformanceReviewArtifact.Build(
                baseline, current, 10f, "old", "new");
            Assert.AreEqual("VSync setting differs.", artifact.scenarios[0].reason);
            StringAssert.Contains("VSync setting differs.", artifact.ToJson());
            var markdown = City3DPerformanceMarkdownReport.Render(artifact);
            StringAssert.Contains("seed=7\\|camera=&lt;A&gt;", markdown);
            StringAssert.Contains("VSync setting differs.", markdown);
        }
    }
}
