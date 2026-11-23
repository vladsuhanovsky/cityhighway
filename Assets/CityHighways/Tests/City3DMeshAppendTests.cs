using System;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class City3DMeshAppendTests
    {
        static void AddQuad(MeshBuilder builder, float x)
        {
            builder.Quad(
                new Vector3(x, 0f, 0f),
                new Vector3(x, 0f, 1f),
                new Vector3(x + 1f, 0f, 1f),
                new Vector3(x + 1f, 0f, 0f),
                new Rect(0f, 0f, 1f, 1f));
        }

        static int Capacity<T>(MeshBuilder builder, string field)
        {
            var info = typeof(MeshBuilder).GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(info, field);
            return ((System.Collections.Generic.List<T>)info.GetValue(builder)).Capacity;
        }

        [Test]
        public void RepeatedSmallAppendsUseSpareCapacityInsteadOfReallocatingEveryTime()
        {
            var destination = new MeshBuilder();
            var source = new MeshBuilder();
            AddQuad(source, 0f);

            destination.Append(source, Matrix4x4.identity);
            int first = Capacity<Vector3>(destination, "_vertices");
            destination.Append(source, Matrix4x4.identity);
            int second = Capacity<Vector3>(destination, "_vertices");
            Assert.GreaterOrEqual(second, 8);
            Assert.Greater(second, first);

            // Third append grows to 16 vertices; fourth fits without another allocation.
            destination.Append(source, Matrix4x4.identity);
            int third = Capacity<Vector3>(destination, "_vertices");
            Assert.GreaterOrEqual(third, 12);
            destination.Append(source, Matrix4x4.identity);
            Assert.AreEqual(third, Capacity<Vector3>(destination, "_vertices"));
            Assert.AreEqual(third, Capacity<Vector3>(destination, "_normals"));
            Assert.AreEqual(third, Capacity<Vector2>(destination, "_uvs"));
            Assert.GreaterOrEqual(Capacity<int>(destination, "_triangles"), 24);
            Assert.AreEqual(16, destination.VertexCount);
        }

        [Test]
        public void LargeAppendStillReservesEnoughForItsEntireGeometry()
        {
            var destination = new MeshBuilder();
            var source = new MeshBuilder();
            for (int i = 0; i < 128; i++) AddQuad(source, i * 2f);
            destination.Append(source, Matrix4x4.identity);
            Assert.AreEqual(512, destination.VertexCount);
            Assert.GreaterOrEqual(Capacity<Vector3>(destination, "_vertices"), 512);
            Assert.GreaterOrEqual(Capacity<int>(destination, "_triangles"), 768);
        }

        [Test]
        public void AppendCombinesMeshesWithTransformedVerticesAndShiftedIndices()
        {
            var destination = new MeshBuilder();
            var source = new MeshBuilder();
            AddQuad(destination, 0f);
            AddQuad(source, 3f);

            destination.Append(source, Matrix4x4.Translate(new Vector3(5f, 2f, -2f)));
            var mesh = destination.ToMesh("append");
            try
            {
                Assert.AreEqual(8, mesh.vertexCount);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 },
                    mesh.triangles);
                Assert.AreEqual(new Vector3(8f, 2f, -2f), mesh.vertices[4]);
                Assert.AreEqual(new Vector3(9f, 2f, -1f), mesh.vertices[6]);
                Assert.AreEqual(8, mesh.normals.Length);
                Assert.AreEqual(8, mesh.uv.Length);
                Assert.AreEqual(4, source.VertexCount);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void AppendEmptySourceLeavesDestinationUnchanged()
        {
            var destination = new MeshBuilder();
            AddQuad(destination, 1f);
            destination.Append(new MeshBuilder(), Matrix4x4.identity);
            var mesh = destination.ToMesh("empty append");
            try
            {
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void AppendSelfFailsWithoutChangingGeometry()
        {
            var builder = new MeshBuilder();
            AddQuad(builder, 0f);
            Assert.Throws<ArgumentException>(() => builder.Append(builder, Matrix4x4.identity));
            Assert.AreEqual(4, builder.VertexCount);
        }

        [Test]
        public void AppendNullFailsWithoutChangingGeometry()
        {
            var builder = new MeshBuilder();
            AddQuad(builder, 0f);
            Assert.Throws<ArgumentNullException>(() => builder.Append(null, Matrix4x4.identity));
            Assert.AreEqual(4, builder.VertexCount);
        }

        [Test]
        public void RepeatedAppendsRemainValidAfterClear()
        {
            var destination = new MeshBuilder();
            var source = new MeshBuilder();
            AddQuad(source, 2f);
            for (int i = 0; i < 10; i++)
                destination.Append(source, Matrix4x4.Translate(new Vector3(i, 0f, 0f)));
            Assert.AreEqual(40, destination.VertexCount);

            destination.Clear();
            destination.Append(source, Matrix4x4.identity);
            var mesh = destination.ToMesh("reused append");
            try
            {
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
