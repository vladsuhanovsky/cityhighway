using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class MenuRoadDepartureTests
    {
        [TestCase(-5f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.425f, 0.5f)]
        [TestCase(0.85f, 1f)]
        [TestCase(8f, 1f)]
        public void DepartureProgressIsClamped(float elapsed, float expected)
        {
            Assert.AreEqual(expected, MenuRoadDeparture.Progress(elapsed), 0.001f);
        }

        [Test]
        public void CarStartsInVisibleRoadLane()
        {
            var p = MenuRoadDeparture.Position(0f, 1200f, 800f);
            Assert.AreEqual(1200f * 0.61f, p.x, 0.01f);
            float top = 800f * 0.82f;
            Assert.AreEqual(top + (800f - top) * 0.72f, p.y, 0.01f);
        }

        [Test]
        public void DepartureTurnsOntoTheRamp()
        {
            var beforeTurn = MenuRoadDeparture.Position(0.55f, 1200f, 800f);
            var afterTurn = MenuRoadDeparture.Position(1f, 1200f, 800f);
            Assert.AreEqual(1200f * 0.74f, beforeTurn.x, 0.01f);
            Assert.AreEqual(1200f * 0.82f, afterTurn.x, 0.01f);
            Assert.AreEqual(800f * 0.82f - 55f, afterTurn.y, 0.01f);
            Assert.Less(afterTurn.y, beforeTurn.y);
        }

        [Test]
        public void MovementIsMonotonicTowardCity()
        {
            var previous = MenuRoadDeparture.Position(0f, 1920f, 1080f);
            for (int step = 1; step <= 100; step++)
            {
                var next = MenuRoadDeparture.Position(step / 100f, 1920f, 1080f);
                Assert.GreaterOrEqual(next.x, previous.x - 0.001f);
                Assert.LessOrEqual(next.y, previous.y + 0.001f);
                previous = next;
            }
        }

        [Test]
        public void DepartureIsResolutionIndependent()
        {
            var small = MenuRoadDeparture.Position(0.55f, 1280f, 720f);
            var wide = MenuRoadDeparture.Position(0.55f, 1920f, 1080f);
            Assert.AreEqual(small.x / 1280f, wide.x / 1920f, 0.0001f);
            Assert.AreEqual(small.y / 720f, wide.y / 1080f, 0.0001f);
        }

        [Test]
        public void PositionClampsProgressBeyondBothEnds()
        {
            var beginning = MenuRoadDeparture.Position(0f, 1200f, 800f);
            var ending = MenuRoadDeparture.Position(1f, 1200f, 800f);
            Assert.AreEqual(beginning, MenuRoadDeparture.Position(-10f, 1200f, 800f));
            Assert.AreEqual(ending, MenuRoadDeparture.Position(10f, 1200f, 800f));
        }

        [Test]
        public void PixelRoadCanRestartDepartureWithoutStaleProgress()
        {
            var road = new MenuHighwayOverlay();
            Assert.IsFalse(road.Departing);
            road.BeginDeparture();
            Assert.IsTrue(road.Departing);
            road.Tick(MenuRoadDeparture.DurationSeconds / 2f, false);
            Assert.AreEqual(0.5f, road.DepartureProgress, 0.001f);
            road.BeginDeparture();
            Assert.AreEqual(0f, road.DepartureProgress, 0.001f);
            road.Tick(MenuRoadDeparture.DurationSeconds, false);
            Assert.IsFalse(road.Departing);
            Assert.AreEqual(1f, road.DepartureProgress, 0.001f);
        }

        [Test]
        public void NonPositiveDeltaDoesNotAdvanceDeparture()
        {
            var road = new MenuHighwayOverlay();
            road.BeginDeparture();
            road.Tick(-0.5f, false);
            road.Tick(0f, false);
            Assert.AreEqual(0f, road.DepartureProgress, 0.001f);
        }
    }
}
