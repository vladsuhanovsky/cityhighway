using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DMeshBoundsTests
    {
        static void AddQuad(MeshBuilder builder, float x, float z, float size)
        {
            builder.Quad(
                new Vector3(x, 0f, z),
                new Vector3(x, 0f, z + size),
                new Vector3(x + size, 0f, z + size),
                new Vector3(x + size, 0f, z),
                new Rect(0f, 0f, 1f, 1f));
        }

        static void AssertBounds(Bounds bounds, Vector3 min, Vector3 max)
        {
            Assert.That(bounds.min.x, Is.EqualTo(min.x).Within(0.0001f));
            Assert.That(bounds.min.y, Is.EqualTo(min.y).Within(0.0001f));
            Assert.That(bounds.min.z, Is.EqualTo(min.z).Within(0.0001f));
            Assert.That(bounds.max.x, Is.EqualTo(max.x).Within(0.0001f));
            Assert.That(bounds.max.y, Is.EqualTo(max.y).Within(0.0001f));
            Assert.That(bounds.max.z, Is.EqualTo(max.z).Within(0.0001f));
        }

        [Test]
        public void ToMeshCalculatesBoundsOfAllQuads()
        {
            var builder = new MeshBuilder();
            AddQuad(builder, -4f, 2f, 2f);
            AddQuad(builder, 5f, -3f, 3f);
            var mesh = builder.ToMesh("bounds test");
            try
            {
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);
                AssertBounds(mesh.bounds, new Vector3(-4f, 0f, -3f), new Vector3(8f, 0f, 4f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void WriteToRecalculatesBoundsAfterReplacingLargerGeometry()
        {
            var builder = new MeshBuilder();
            var mesh = new Mesh();
            try
            {
                AddQuad(builder, -8f, -8f, 16f);
                builder.WriteTo(mesh);
                AssertBounds(mesh.bounds, new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, 8f));

                builder.Clear();
                AddQuad(builder, 2f, 3f, 1f);
                builder.WriteTo(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
                AssertBounds(mesh.bounds, new Vector3(2f, 0f, 3f), new Vector3(3f, 0f, 4f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void EmptyBuilderProducesEmptyGeometryWithoutStaleBounds()
        {
            var builder = new MeshBuilder();
            var mesh = new Mesh();
            try
            {
                AddQuad(builder, 20f, 30f, 2f);
                builder.WriteTo(mesh);
                builder.Clear();
                builder.WriteTo(mesh);

                Assert.AreEqual(0, mesh.vertexCount);
                Assert.AreEqual(0, mesh.triangles.Length);
                AssertBounds(mesh.bounds, Vector3.zero, Vector3.zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
