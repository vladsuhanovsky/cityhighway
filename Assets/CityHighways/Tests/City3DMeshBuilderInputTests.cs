using System;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DMeshBuilderInputTests
    {
        static MeshBuilder SingleQuad()
        {
            var builder = new MeshBuilder();
            builder.Quad(
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(1f, 0f, 0f),
                new Rect(0f, 0f, 1f, 1f));
            return builder;
        }

        [Test]
        public void NullWriteToDestinationFailsBeforeMutatingBuilder()
        {
            var builder = SingleQuad();
            Assert.Throws<ArgumentNullException>(() => builder.WriteTo(null));
            var mesh = builder.ToMesh("after rejected null");
            try
            {
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void NullDrapeHeightCallbackFailsWithoutChangingVertices()
        {
            var builder = SingleQuad();
            Assert.Throws<ArgumentNullException>(() => builder.Drape(null));
            var mesh = builder.ToMesh("after rejected drape");
            try
            {
                Assert.AreEqual(4, mesh.vertexCount);
                foreach (var vertex in mesh.vertices)
                    Assert.That(vertex.y, Is.EqualTo(0f).Within(0.00001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ValidDrapeStillAppliesHeightPerVertex()
        {
            var builder = SingleQuad();
            builder.Drape((x, z) => 2f + x + z);
            var mesh = builder.ToMesh("draped quad");
            try
            {
                Assert.AreEqual(4, mesh.vertexCount);
                foreach (var vertex in mesh.vertices)
                    Assert.That(vertex.y, Is.EqualTo(2f + vertex.x + vertex.z).Within(0.00001f));
                Assert.AreEqual(6, mesh.triangles.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void WriteToReplacesGeometryAndPreservesExpectedIndexFormat()
        {
            var builder = SingleQuad();
            var mesh = new Mesh();
            try
            {
                builder.WriteTo(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, mesh.indexFormat);

                builder.Clear();
                builder.WriteTo(mesh);
                Assert.AreEqual(0, mesh.vertexCount);
                Assert.AreEqual(0, mesh.triangles.Length);

                builder = SingleQuad();
                builder.WriteTo(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, mesh.indexFormat);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
