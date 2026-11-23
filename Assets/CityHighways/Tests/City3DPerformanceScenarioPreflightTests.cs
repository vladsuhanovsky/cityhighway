using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class City3DPerformanceScenarioPreflightTests
    {
        static City3DPerformanceEnvironment Environment() =>
            new City3DPerformanceEnvironment
            {
                unityVersion = "6000.6.8f1", platform = "WindowsPlayer",
                graphicsDevice = "GPU", width = 1920, height = 1080,
                vSyncCount = 0, targetFrameRate = -1
            };

        [Test]
        public void MatchingStructuredScenarioHasNoWarnings()
        {
            var result = City3DPerformanceScenarioPreflight.Check(
                "magnate;seed=-42;camera=central;viewport=1920x1080",
                Environment());
            Assert.IsFalse(result.HasWarnings, result.Warnings);
            Assert.IsTrue(result.HasDeclaredSeed);
            Assert.AreEqual(-42, result.DeclaredSeed);
            Assert.IsTrue(result.HasDeclaredViewport);
            Assert.AreEqual(1920, result.ViewportWidth);
            Assert.AreEqual(1080, result.ViewportHeight);
        }

        [Test]
        public void ViewportMismatchIsVisibleBeforeCapture()
        {
            var result = City3DPerformanceScenarioPreflight.Check(
                "magnate;seed=42;camera=central;viewport=1280x720",
                Environment());
            Assert.IsTrue(result.HasWarnings);
            StringAssert.Contains("does not match actual", result.Warnings);
            Assert.IsTrue(result.HasDeclaredViewport);
        }

        [Test]
        public void InvalidSeedAndViewportAreReportedWithoutThrowing()
        {
            var result = City3DPerformanceScenarioPreflight.Check(
                "magnate;seed=999999999999999999;camera=A;viewport=0x1080",
                Environment());
            Assert.IsTrue(result.HasWarnings);
            Assert.IsFalse(result.HasDeclaredSeed);
            Assert.IsFalse(result.HasDeclaredViewport);
            StringAssert.Contains("32-bit", result.Warnings);
            StringAssert.Contains("positive", result.Warnings);
        }

        [Test]
        public void DuplicateKeysAndEmptyCameraAreReported()
        {
            var result = City3DPerformanceScenarioPreflight.Check(
                "seed=7;seed=8;camera=;camera=central;viewport=1920x1080",
                Environment());
            StringAssert.Contains("Duplicate seed key.", result.Warnings);
            StringAssert.Contains("Duplicate camera key.", result.Warnings);
            StringAssert.Contains("Camera label is empty.", result.Warnings);
            Assert.AreEqual(7, result.DeclaredSeed);
        }

        [Test]
        public void OlderFreeFormLabelsRemainAdvisoryNotRejected()
        {
            var result = City3DPerformanceScenarioPreflight.Check(
                "ручной сценарий", null);
            Assert.IsTrue(result.HasWarnings);
            StringAssert.Contains("No seed key", result.Warnings);
            StringAssert.Contains("No camera key", result.Warnings);
            StringAssert.Contains("No viewport key", result.Warnings);
        }

        [Test]
        public void UnknownRuntimeResolutionDoesNotProduceFalseMismatch()
        {
            var result = City3DPerformanceScenarioPreflight.Check(
                "magnate;seed=42;camera=central;viewport=1920x1080", null);
            Assert.IsFalse(result.HasWarnings, result.Warnings);
        }
    }
}
