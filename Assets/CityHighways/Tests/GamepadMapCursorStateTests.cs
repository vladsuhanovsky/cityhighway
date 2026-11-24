using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class GamepadMapCursorStateTests
    {
        static readonly Rect Area = new Rect(0f, 0f, 800f, 480f);

        [Test]
        public void ActivatingCentersCursorAndDeactivatingStopsMovement()
        {
            var cursor = new GamepadMapCursorState();
            Assert.IsFalse(cursor.Active);
            cursor.Activate(Area);
            Assert.IsTrue(cursor.Active);
            Assert.AreEqual(new Vector2(400f, 240f), cursor.Position);
            cursor.Deactivate();
            Assert.IsFalse(cursor.Move(Vector2.right, 0.05f, Area));
            Assert.AreEqual(new Vector2(400f, 240f), cursor.Position);
        }

        [Test]
        public void RightStickUsesUiToolkitDownwardYAndClampsDeltaTime()
        {
            var cursor = new GamepadMapCursorState();
            cursor.Activate(Area);
            Assert.IsTrue(cursor.Move(Vector2.up, 1f, Area));
            Assert.AreEqual(240f - 620f * 0.05f, cursor.Position.y, 1e-4f);
            Assert.IsTrue(cursor.Move(Vector2.right, 0.02f, Area));
            Assert.AreEqual(400f + 620f * 0.02f, cursor.Position.x, 1e-4f);
        }

        [Test]
        public void StickDeadZonePreventsControllerDrift()
        {
            Assert.IsFalse(GamepadMapCursorState.HasMovement(Vector2.zero));
            Assert.IsFalse(GamepadMapCursorState.HasMovement(new Vector2(.10f, -.10f)));
            Assert.IsTrue(GamepadMapCursorState.HasMovement(new Vector2(.4f, 0f)));
            var cursor = new GamepadMapCursorState();
            cursor.Activate(Area);
            Assert.IsFalse(cursor.Move(new Vector2(.10f, -.10f), .05f, Area));
        }

        [Test]
        public void CursorCannotEscapeSmallOrLargeMapBounds()
        {
            var cursor = new GamepadMapCursorState();
            cursor.Activate(Area);
            for (int i = 0; i < 100; i++)
                cursor.Move(Vector2.right, .05f, Area);
            Assert.AreEqual(Area.xMax - 14f, cursor.Position.x, 1e-4f);
            Assert.IsFalse(cursor.Move(Vector2.right, .05f, Area));
            var tiny = new Rect(10f, 20f, 12f, 8f);
            Assert.AreEqual(tiny.center, GamepadMapCursorState.Clamp(
                new Vector2(900f, -100f), tiny));
        }

        [Test]
        public void DiagonalMovementIsNormalizedAndInvalidDeltaDoesNotMove()
        {
            var cursor = new GamepadMapCursorState();
            cursor.Activate(Area);
            Assert.IsFalse(cursor.Move(Vector2.right, 0f, Area));
            Assert.IsFalse(cursor.Move(Vector2.right, -1f, Area));
            var origin = cursor.Position;
            Assert.IsTrue(cursor.Move(new Vector2(1f, 1f), .04f, Area));
            Assert.AreEqual(620f * .04f, Vector2.Distance(origin, cursor.Position), 1e-3f);
        }

        [Test]
        public void ResizingViewportConstrictsActiveCursorWithoutReactivatingIt()
        {
            var cursor = new GamepadMapCursorState();
            cursor.Activate(Area);
            for (int i = 0; i < 80; i++)
                cursor.Move(Vector2.right, .05f, Area);
            cursor.Constrain(new Rect(0f, 0f, 220f, 120f));
            Assert.AreEqual(206f, cursor.Position.x, 1e-4f);
            Assert.AreEqual(106f, cursor.Position.y, 1e-4f);
            cursor.Deactivate();
            var before = cursor.Position;
            cursor.Constrain(new Rect(0f, 0f, 10f, 10f));
            Assert.AreEqual(before, cursor.Position);
        }

        [Test]
        public void MovementIsIndependentOfUnityPanelAndUsesLocalOffsetBounds()
        {
            var bounds = new Rect(90f, 40f, 230f, 100f);
            var cursor = new GamepadMapCursorState();
            cursor.Activate(bounds);
            Assert.AreEqual(bounds.center, cursor.Position);
            for (int i = 0; i < 40; i++)
                cursor.Move(-Vector2.one, .05f, bounds);
            Assert.AreEqual(bounds.xMin + 14f, cursor.Position.x, 1e-4f);
            Assert.AreEqual(bounds.yMax - 14f, cursor.Position.y, 1e-4f);
        }
    }
}
