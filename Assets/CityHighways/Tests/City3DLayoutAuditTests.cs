using System;
using System.IO;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DLayoutAuditTests
    {
        [Test]
        public void SegmentCrossingsAndCollinearOverlapHaveZeroDistance()
        {
            Assert.AreEqual(0f, City3DLayoutAudit.SegmentDistance(Vector2.zero, Vector2.right,
                new Vector2(.5f, -1f), new Vector2(.5f, 1f)), 1e-6f);
            Assert.AreEqual(0f, City3DLayoutAudit.SegmentDistance(Vector2.zero, Vector2.right,
                new Vector2(.5f, 0f), new Vector2(2f, 0f)), 1e-6f);
        }

        [Test]
        public void ParallelAndDegenerateSegmentsHaveCorrectDistance()
        {
            Assert.AreEqual(2f, City3DLayoutAudit.SegmentDistance(Vector2.zero, Vector2.right,
                new Vector2(0f, 2f), new Vector2(1f, 2f)), 1e-6f);
            Assert.AreEqual(1f, City3DLayoutAudit.SegmentDistance(Vector2.up, Vector2.up,
                Vector2.zero, Vector2.right), 1e-6f);
        }

        [Test]
        public void FootprintDetectsCrossingAndInteriorSegments()
        {
            var corners = new[] { new Vector2(-1f,-1f), new Vector2(1f,-1f),
                new Vector2(1f,1f), new Vector2(-1f,1f) };
            Assert.AreEqual(0f, City3DLayoutAudit.PolygonSegmentDistance(corners, new Vector2(-2f,0f), new Vector2(2f,0f)), 1e-6f);
            Assert.AreEqual(0f, City3DLayoutAudit.PolygonSegmentDistance(corners, Vector2.zero, Vector2.right), 1e-6f);
            Assert.AreEqual(.2f, City3DLayoutAudit.PolygonSegmentDistance(corners, new Vector2(-2f,1.2f), new Vector2(2f,1.2f)), 1e-6f);
        }

        [TestCase(8f, true)]
        [TestCase(90f, false)]
        [TestCase(180f, false)]
        public void CrosswalkEnvelopesDetectAcuteRisk(float angle, bool expected)
        {
            var other = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Assert.AreEqual(expected, City3DLayoutAudit.CrosswalkEnvelopesOverlap(Vector2.right, other,
                City3DScene.RoadWidth / 2f, City3DScene.CrosswalkNear, City3DScene.CrosswalkFar));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(2026)]
        public void GeneratedSeedProducesReproducible3DFootprintDiagnostics(int seed)
        {
            var report = CitySeedDiagnostics.Run(seed, true);
            Assert.IsTrue(report.Passed, string.Join("; ", report.issues));
            Assert.AreEqual(2, report.schema);
            Assert.IsNotNull(report.layout3D);
            Assert.AreEqual(report.hubs.Length, report.layout3D.hubFootprints.Length);
            Assert.IsFalse(report.layout3D.warnings.Any(w => w.code == "crosswalk-envelope-overlap"));
            Assert.Greater(report.layout3D.crosswalksDrawn, 0);
            Assert.Greater(report.layout3D.legacyOverlapPairs, 0);
            Assert.Greater(report.layout3D.crosswalksSuppressed, 0);
            Assert.AreEqual(report.layout3D.crosswalkCandidates, report.layout3D.crosswalksDrawn + report.layout3D.crosswalksSuppressed);
            Assert.IsNotEmpty(report.layout3D.preparedRivers);
            foreach (var hub in report.layout3D.hubFootprints)
                Assert.AreEqual(4, hub.corners.Length);
            var next = CitySeedDiagnostics.Run(seed, true);
            Assert.AreEqual(JsonUtility.ToJson(report.layout3D), JsonUtility.ToJson(next.layout3D));
            // Warnings are baseline evidence, not an established generator rejection rule.
            // Preserve every sampled report (including clean ones) as CI evidence.
            CitySeedDiagnostics.Write(report, Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "artifacts", "city-3d-layout")));
        }

        [Test]
        public void DefaultTopologyAuditDoesNotRun3DPlanning()
        {
            var report = CitySeedDiagnostics.Run(42);
            Assert.IsTrue(report.Passed);
            Assert.AreEqual(1, report.schema);
            Assert.IsNull(report.layout3D);
        }
    }
}
