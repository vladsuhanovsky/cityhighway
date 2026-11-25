using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class RoadMarkingGeometryTests
    {
        static BuiltRoad Road(Vector3 a, Vector3 b, float height = 0.006f)
        {
            var road = new BuiltRoad { Start = a, End = b, Length = new Vector2(b.x - a.x, b.z - a.z).magnitude };
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Road, From = 0f, To = road.Length, FromHeight = height, ToHeight = height });
            return road;
        }

        [TestCase(6f, 3f)]
        [TestCase(-6f, 3f)]
        [TestCase(0f, -3f)]
        [TestCase(3f, 0f)]
        public void DiagonalAndAxisDashesStayCenteredOnFinalSlopedSurface(float dx, float dz)
        {
            var road = Road(new Vector3(2f, 0.3f, -4f), new Vector3(2f + dx, 0.7f, -4f + dz), 0.014f);
            var quads = RoadMarkingGeometry.LaneMarks(road).ToArray();
            Assert.Greater(quads.Length, 10);
            var along = new Vector3(dx, 0f, dz).normalized;
            for (int i = 0; i < quads.Length; i++)
            {
                var q = quads[i];
                float from = RoadMarkingGeometry.DashGap + i * (RoadMarkingGeometry.DashLength + RoadMarkingGeometry.DashGap);
                var expected = road.PointAt(from) + Vector3.up * (0.014f + RoadMarkingGeometry.MarkLift);
                Assert.That(Vector3.Distance((q.A + q.D) / 2f, expected), Is.LessThan(1e-5f));
                Assert.That(Vector3.Dot(q.B - q.A, along), Is.EqualTo(RoadMarkingGeometry.DashLength).Within(1e-5f));
                Assert.That((q.A - q.D).magnitude, Is.EqualTo(RoadMarkingGeometry.DashHalfWidth * 2f).Within(1e-5f));
                Assert.Greater(Vector3.Cross(q.B - q.A, q.C - q.A).y, 0f);
                Assert.Less(Vector3.Dot((q.B + q.C) / 2f - road.Start, along), road.Length - RoadMarkingGeometry.DashGap * 0.5f);
            }
        }

        [Test]
        public void OwnerBandsSplitAtRampAndBridgeBoundaries()
        {
            var road = Road(Vector3.zero, new Vector3(1f, 0.1f, 0f));
            road.Pieces.Clear();
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Road, From = 0f, To = 0.2f, FromHeight = 0.006f, ToHeight = 0.006f });
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Ramp, From = 0.2f, To = 0.4f, FromHeight = 0.006f, ToHeight = 0.05f });
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Bridge, From = 0.4f, To = 1f, FromHeight = 0.05f, ToHeight = 0.05f });
            foreach (float sign in new[] { -1f, 1f })
            {
                float low = sign > 0 ? 0.025f : -0.04f, high = sign > 0 ? 0.04f : -0.025f;
                var strips = RoadMarkingGeometry.Strips(road, 0f, 1f, low, high, 0.0006f).ToArray();
                Assert.AreEqual(3, strips.Length);
                for (int i = 0; i < strips.Length; i++)
                {
                    var q = strips[i];
                    var piece = road.Pieces[i];
                    Assert.That(q.A.x, Is.EqualTo(piece.From).Within(1e-6f));
                    Assert.That(q.B.x, Is.EqualTo(piece.To).Within(1e-6f));
                    Assert.That(q.A.y, Is.EqualTo(piece.From * 0.1f + piece.FromHeight + 0.0006f).Within(1e-6f));
                    Assert.That(q.B.y, Is.EqualTo(piece.To * 0.1f + piece.ToHeight + 0.0006f).Within(1e-6f));
                    Assert.Less(Mathf.Abs(q.A.z), City3DScene.RoadWidth / 2f);
                    Assert.Less(Mathf.Abs(q.D.z), City3DScene.RoadWidth / 2f);
                    Assert.Greater(Vector3.Cross(q.B - q.A, q.C - q.A).y, 0f);
                }
            }
            Assert.IsEmpty(RoadMarkingGeometry.LaneMarks(road));
        }

        [Test]
        public void ShortAndEmptyRoadsDoNotLeakMarkingsBeyondJunctions()
        {
            var shortRoad = Road(Vector3.zero, new Vector3(0.05f, 0f, 0f));
            Assert.IsEmpty(RoadMarkingGeometry.LaneMarks(shortRoad));
            shortRoad.Pieces.Clear();
            Assert.IsEmpty(RoadMarkingGeometry.Strips(shortRoad, -1f, 1f, -0.01f, 0.01f, 0.001f));
        }

        [Test]
        public void WaterDetectedBridgeDoesNotReceiveGroundLaneMarks()
        {
            var road = Road(Vector3.zero, Vector3.right);
            road.WaterSpans.Add(new WaterSpan(0.3f, 0.7f));
            Assert.IsEmpty(RoadMarkingGeometry.LaneMarks(road));
        }

        [Test]
        public void StripClipsToRoadAndDoesNotFillMissingPieces()
        {
            var road = Road(Vector3.zero, Vector3.right);
            road.Pieces.Clear();
            road.Pieces.Add(new RoadPiece { From = 0f, To = 0.2f });
            road.Pieces.Add(new RoadPiece { From = 0.6f, To = 1f });
            var quads = RoadMarkingGeometry.Strips(road, -1f, 2f, -0.01f, 0.01f, 0.001f).ToArray();
            Assert.AreEqual(2, quads.Length);
            Assert.That(quads[0].A.x, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(quads[0].B.x, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(quads[1].A.x, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(quads[1].B.x, Is.EqualTo(1f).Within(1e-6f));
        }
    }
}
