using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DGeometryAuditTests
    {
        [Test]
        public void GroupClassifierRecognizesGeneratedAndAuthoredVisuals()
        {
            Assert.AreEqual(City3DGeometryAudit.Group.Buildings,
                City3DGeometryAudit.Classify("District Central"));
            Assert.AreEqual(City3DGeometryAudit.Group.Trees,
                City3DGeometryAudit.Classify("Trees Central#0,0"));
            Assert.AreEqual(City3DGeometryAudit.Group.Replacements,
                City3DGeometryAudit.Classify("Tower (replacement)"));
            Assert.AreEqual(City3DGeometryAudit.Group.Decorations,
                City3DGeometryAudit.Classify("Bench (authored)"));
            Assert.AreEqual(City3DGeometryAudit.Group.Other,
                City3DGeometryAudit.Classify("Roads"));
        }

        [Test]
        public void MissingRootHasNoGeometry()
        {
            var entries = City3DGeometryAudit.Capture(null, out var totals);
            Assert.AreEqual(0, entries.Length);
            Assert.AreEqual(0, totals.Meshes);
            Assert.AreEqual(0L, totals.Triangles);
        }

        [Test]
        public void CaptureCountsRealMeshGeometryAndRanksGroups()
        {
            var root = new GameObject("Test 3D Root");
            var district = new GameObject("District East");
            var tree = new GameObject("Trees North#0,0");
            var cubeMesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            };
            try
            {
                district.transform.SetParent(root.transform, false);
                tree.transform.SetParent(root.transform, false);
                var cube = district.AddComponent<MeshFilter>();
                cube.sharedMesh = cubeMesh;
                var second = tree.AddComponent<MeshFilter>();
                second.sharedMesh = cubeMesh;
                var entries = City3DGeometryAudit.Capture(root.transform, out var totals);
                Assert.AreEqual(2, entries.Length);
                Assert.AreEqual(2, totals.Meshes);
                Assert.AreEqual(1, totals.Buildings);
                Assert.AreEqual(1, totals.Trees);
                Assert.AreEqual((long)cubeMesh.vertexCount * 2, totals.Vertices);
                Assert.AreEqual(2L, totals.Triangles);
                Assert.AreEqual(City3DGeometryAudit.Group.Buildings, entries[0].Kind);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cubeMesh);
            }
        }
    }
}
