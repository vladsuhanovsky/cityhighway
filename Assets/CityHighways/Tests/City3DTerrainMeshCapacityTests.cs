using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityHighways.Tests
{
    public sealed class City3DTerrainMeshCapacityTests
    {
        static void Grid(MeshBuilder builder, int width, int height)
        {
            var uv = new Rect(0f, 0f, 1f, 1f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                builder.Quad(new Vector3(x, 0, y),
                    new Vector3(x, 0, y + 1),
                    new Vector3(x + 1, 0, y + 1),
                    new Vector3(x + 1, 0, y), uv);
        }

        [Test]
        public void ExcessiveQuadReservationThrowsBeforeIntegerOverflow()
        {
            // Six triangle indices per quad must fit in a signed 32-bit List count.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MeshBuilder(int.MaxValue / 6 + 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MeshBuilder(int.MaxValue));
        }

        [Test]
        public void NegativeQuadReservationRetainsEmptyBuilderBehavior()
        {
            var builder = new MeshBuilder(-1);
            Assert.AreEqual(0, builder.VertexCount);
            Grid(builder, 2, 1);
            var mesh = builder.ToMesh("negative reservation");
            try
            {
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ReservedGridProducesExactlyExpectedGeometry()
        {
            const int width = 12, height = 7;
            var builder = new MeshBuilder(width * height);
            Grid(builder, width, height);
            var mesh = builder.ToMesh("capacity grid");
            try
            {
                Assert.AreEqual(width * height * 4, builder.VertexCount);
                Assert.AreEqual(width * height * 4, mesh.vertexCount);
                Assert.AreEqual(width * height * 6, mesh.triangles.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void UnderestimatedReservationCanGrowWithoutTruncatingSubdivisions()
        {
            var builder = new MeshBuilder(2);
            Grid(builder, 9, 9);
            var mesh = builder.ToMesh("expanded terrain");
            try
            {
                Assert.AreEqual(9 * 9 * 4, mesh.vertexCount);
                Assert.AreEqual(9 * 9 * 6, mesh.triangles.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void LargeTerrainMeshUses32BitIndices()
        {
            // The actual main-ground grid is 240 x 136 (130,560 vertices).
            var builder = new MeshBuilder(240 * 136);
            Grid(builder, 240, 136);
            var mesh = builder.ToMesh("large terrain");
            try
            {
                Assert.AreEqual(240 * 136 * 4, mesh.vertexCount);
                Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat);
                Assert.AreEqual(240 * 136 * 6, mesh.triangles.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ReusedMeshSwitchesFrom16To32AndBackTo16BitIndices()
        {
            var builder = new MeshBuilder();
            var mesh = new Mesh();
            try
            {
                Grid(builder, 3, 2);
                builder.WriteTo(mesh);
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat);
                Assert.AreEqual(24, mesh.vertexCount);
                Assert.AreEqual(36, mesh.triangles.Length);

                builder.Clear();
                Grid(builder, 130, 127); // 66,040 vertices: exceeds 16-bit limit.
                builder.WriteTo(mesh);
                Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat);
                Assert.AreEqual(130 * 127 * 4, mesh.vertexCount);
                Assert.AreEqual(130 * 127 * 6, mesh.triangles.Length);

                builder.Clear();
                Grid(builder, 2, 1);
                builder.WriteTo(mesh);
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat);
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);
                Assert.That(mesh.bounds.min.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(mesh.bounds.max.x, Is.EqualTo(2f).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ReusedMeshCanClearLarge32BitGeometryAndBuildSmallAgain()
        {
            var builder = new MeshBuilder(130 * 127);
            var mesh = new Mesh();
            try
            {
                Grid(builder, 130, 127);
                builder.WriteTo(mesh);
                Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat);

                builder.Clear();
                builder.WriteTo(mesh);
                Assert.AreEqual(0, mesh.vertexCount);
                Assert.AreEqual(0, mesh.triangles.Length);
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat);

                Grid(builder, 1, 1);
                builder.WriteTo(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ReusingReservedTerrainBuilderDoesNotKeepOldQuads()
        {
            var builder = new MeshBuilder(24);
            var mesh = new Mesh();
            try
            {
                Grid(builder, 6, 4);
                builder.WriteTo(mesh);
                Assert.AreEqual(24 * 4, mesh.vertexCount);
                builder.Clear();
                Grid(builder, 2, 2);
                builder.WriteTo(mesh);
                Assert.AreEqual(4 * 4, mesh.vertexCount);
                Assert.AreEqual(4 * 6, mesh.triangles.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
