using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityGenerationTraceTests
    {
        [TestCase(0)]
        [TestCase(42)]
        [TestCase(-17)]
        [TestCase(int.MaxValue)]
        public void StandardTracePreservesOriginalLayout(int seed)
        {
            var plain = CityGenerator.Generate(seed);
            var trace = new List<CityGenerationAttempt>();
            var traced = CityGenerator.Generate(seed, trace);
            AssertSameCity(plain, traced);
            Assert.That(trace.Count, Is.InRange(1, CityGenerator.StandardAttemptLimit));
            AssertTrace(trace, seed, "standard", traced);
        }

        [Test]
        public void TutorialTracePreservesFixedTutorialCity()
        {
            var plain = CityGenerator.GenerateTutorial();
            var trace = new List<CityGenerationAttempt>();
            var traced = CityGenerator.GenerateTutorial(trace);
            AssertSameCity(plain, traced);
            Assert.AreEqual("Учебный город", traced.Map.Name);
            Assert.That(trace.Count, Is.InRange(1, CityGenerator.TutorialAttemptLimit));
            AssertTrace(trace, CityGenerator.TutorialSeed, "tutorial", traced);
        }

        [Test]
        public void ReusedTraceIsResetAndDeterministic()
        {
            var trace = new List<CityGenerationAttempt>();
            CityGenerator.Generate(42, trace);
            var before = trace.Select(a => (a.seed, a.index, a.profile, a.outcome, a.locations, a.roads, a.components,
                string.Join("|", a.issues))).ToArray();
            CityGenerator.Generate(-17, trace);
            CityGenerator.Generate(42, trace);
            CollectionAssert.AreEqual(before, trace.Select(a => (a.seed, a.index, a.profile, a.outcome,
                a.locations, a.roads, a.components, string.Join("|", a.issues))));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DiagnosticJsonRetainsAttemptTrace(bool tutorial)
        {
            var report = tutorial ? CitySeedDiagnostics.RunTutorial() : CitySeedDiagnostics.Run(42);
            Assert.IsTrue(report.generated, string.Join("; ", report.issues));
            Assert.IsNotEmpty(report.attempts);
            Assert.AreEqual("accepted", report.attempts.Last().outcome);
            var restored = JsonUtility.FromJson<CitySeedReport>(JsonUtility.ToJson(report));
            Assert.AreEqual(tutorial ? "tutorial" : "standard", restored.generationProfile);
            Assert.AreEqual(report.requestedSeed, restored.requestedSeed);
            CollectionAssert.AreEqual(report.attempts.Select(a => (a.seed, a.outcome)),
                restored.attempts.Select(a => (a.seed, a.outcome)));
            Assert.AreEqual(report.actualSeed, restored.attempts.Last().seed);
        }

        static void AssertTrace(List<CityGenerationAttempt> trace, int seed, string profile, GeneratedCity city)
        {
            for (int i = 0; i < trace.Count; i++)
            {
                var a = trace[i];
                Assert.AreEqual(i + 1, a.index);
                Assert.AreEqual(unchecked(seed + i * CityGenerator.AttemptSeedStride), a.seed);
                Assert.AreEqual(profile, a.profile);
                Assert.IsNotEmpty(a.outcome);
                Assert.AreNotEqual("started", a.outcome);
                Assert.IsNotNull(a.issues);
                if (i != trace.Count - 1)
                {
                    Assert.AreNotEqual("accepted", a.outcome);
                    Assert.GreaterOrEqual(a.locations, 0);
                    if (a.outcome == "disconnected-graph") Assert.Greater(a.components, 1);
                    if (a.outcome == "water-validation" || a.outcome == "map-validation") Assert.IsNotEmpty(a.issues);
                }
            }
            var last = trace.Last();
            Assert.AreEqual("accepted", last.outcome);
            Assert.AreEqual(city.Seed, last.seed);
            Assert.AreEqual(city.Map.Locations.Count, last.locations);
            Assert.AreEqual(city.Map.Roads.Count, last.roads);
            Assert.AreEqual(1, last.components);
        }

        static void AssertSameCity(GeneratedCity a, GeneratedCity b)
        {
            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreEqual(a.Map.Name, b.Map.Name);
            CollectionAssert.AreEqual(a.Map.Locations.Select(p => (p.Id, p.Name, p.DistrictId, p.X, p.Y)),
                b.Map.Locations.Select(p => (p.Id, p.Name, p.DistrictId, p.X, p.Y)));
            CollectionAssert.AreEqual(a.Map.Roads.Select(r => (r.Id, r.A, r.B, r.Length, r.Resource)),
                b.Map.Roads.Select(r => (r.Id, r.A, r.B, r.Length, r.Resource)));
            CollectionAssert.AreEqual(a.Map.Rivers.SelectMany(r => r.Select(p => (p.X, p.Y))),
                b.Map.Rivers.SelectMany(r => r.Select(p => (p.X, p.Y))));
            CollectionAssert.AreEqual(a.Hubs.Select(h => (h.LocationId, h.Kind)), b.Hubs.Select(h => (h.LocationId, h.Kind)));
            CollectionAssert.AreEquivalent(a.PublicRoads, b.PublicRoads);
        }
    }
}
