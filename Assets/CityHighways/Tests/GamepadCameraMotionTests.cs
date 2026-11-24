using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class GamepadCameraMotionTests
    {
        [Test]
        public void ShoulderButtonsRotateInOppositeDirections()
        {
            var right = GamepadCameraMotion.Read(false, true, false, false, 0f, 0f, false);
            var left = GamepadCameraMotion.Read(true, false, false, false, 0f, 0f, false);
            Assert.AreEqual(1f, right.Turn);
            Assert.AreEqual(-1f, left.Turn);
            Assert.AreEqual(0f, right.Tilt);
            Assert.AreEqual(0f, left.Zoom);
            Assert.IsTrue(right.HasMovement);
        }

        [Test]
        public void SimultaneousShouldersAndTiltButtonsCancel()
        {
            var sample = GamepadCameraMotion.Read(true, true, true, true, 0f, 0f, false);
            Assert.AreEqual(0f, sample.Turn);
            Assert.AreEqual(0f, sample.Tilt);
            Assert.IsFalse(sample.HasMovement);
        }

        [Test]
        public void FaceButtonsTiltUpAndDownWithoutSubmitting()
        {
            var up = GamepadCameraMotion.Read(false, false, false, true, 0f, 0f, false);
            var down = GamepadCameraMotion.Read(false, false, true, false, 0f, 0f, false);
            Assert.AreEqual(1f, up.Tilt);
            Assert.AreEqual(-1f, down.Tilt);
            Assert.AreEqual(0f, up.Turn);
        }

        [Test]
        public void TriggersZoomInAndOutWithDeadZoneAndPressure()
        {
            var rest = GamepadCameraMotion.Read(false, false, false, false, 0.07f, 0.1f, false);
            Assert.AreEqual(0f, rest.Zoom);
            var zoomIn = GamepadCameraMotion.Read(false, false, false, false, 0f, 0.7f, false);
            var zoomOut = GamepadCameraMotion.Read(false, false, false, false, 1.0f, 0f, false);
            Assert.AreEqual(0.7f, zoomIn.Zoom, 1e-5);
            Assert.AreEqual(-1f, zoomOut.Zoom);
            Assert.IsTrue(zoomIn.HasMovement);
        }

        [Test]
        public void ResetIntentDoesNotInventCameraMovement()
        {
            var reset = GamepadCameraMotion.Read(false, false, false, false, 0f, 0f, true);
            Assert.IsTrue(reset.Reset);
            Assert.IsFalse(reset.HasMovement);
        }

        [Test]
        public void TriggersClampInvalidPressureToAllowedRange()
        {
            var sample = GamepadCameraMotion.Read(false, false, false, false, -1f, 2f, false);
            Assert.AreEqual(1f, sample.Zoom);
        }
    }
}
