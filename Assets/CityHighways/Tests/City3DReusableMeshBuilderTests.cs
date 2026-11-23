using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DReusableMeshBuilderTests
    {
        static readonly Rect Uv = new Rect(0f, 0f, 1f, 1f);

        static void OneQuad(MeshBuilder builder, float offset)
        {
            builder.QuadWithNormal(
                new Vector3(offset, 0f, 0f),
                new Vector3(offset, 1f, 0f),
                new Vector3(offset + 1f, 1f, 0f),
                new Vector3(offset + 1f, 0f, 0f), Uv, Vector3.forward);
        }

        [Test]
        public void ReservedBuilderProducesExpectedGeometry()
        {
            var builder = new MeshBuilder(3);
            OneQuad(builder, 0f);
            OneQuad(builder, 2f);
            var mesh = builder.ToMesh("reserved");
            try
            {
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);
                Assert.AreEqual(8, mesh.normals.Length);
                Assert.AreEqual(8, mesh.uv.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ReuseAndClearWritesOnlyNewGeometry()
        {
            var builder = new MeshBuilder(2);
            var mesh = new Mesh();
            try
            {
                OneQuad(builder, 0f);
                OneQuad(builder, 2f);
                builder.WriteTo(mesh);
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);

                builder.Clear();
                OneQuad(builder, 5f);
                builder.WriteTo(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
                Assert.That(mesh.vertices[0].x, Is.EqualTo(5f));

                builder.Clear();
                builder.WriteTo(mesh);
                Assert.AreEqual(0, mesh.vertexCount);
                Assert.AreEqual(0, mesh.triangles.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void DefaultBuilderIsStillSupported()
        {
            var builder = new MeshBuilder();
            OneQuad(builder, 0f);
            var mesh = builder.ToMesh("default");
            try
            {
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void RepeatedRebuildDoesNotAccumulateVertices()
        {
            var builder = new MeshBuilder(1);
            var mesh = new Mesh();
            try
            {
                for (int i = 0; i < 30; i++)
                {
                    builder.Clear();
                    OneQuad(builder, i);
                    builder.WriteTo(mesh);
                    Assert.AreEqual(4, mesh.vertexCount);
                    Assert.AreEqual(6, mesh.triangles.Length);
                    Assert.That(mesh.vertices[0].x, Is.EqualTo((float)i));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
