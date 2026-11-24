using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Pure, testable virtual pointer state for both 2D and 3D map views.
    /// Positions are in MapView local UI Toolkit pixels (Y increases down).
    /// </summary>
    public struct GamepadMapCursorState
    {
        const float Deadzone = 0.22f;
        const float Margin = 14f;

        public bool Active { get; private set; }
        public Vector2 Position { get; private set; }

        public void Activate(Rect area)
        {
            Active = true;
            Position = Clamp(area.center, area);
        }

        public void Deactivate() => Active = false;

        /// <summary>Keep the cursor reachable after changing window size or UI scale.</summary>
        public void Constrain(Rect area)
        {
            if (Active) Position = Clamp(Position, area);
        }

        public bool Move(Vector2 stick, float deltaTime, Rect area, float speed = 620f)
        {
            if (!Active || deltaTime <= 0f || speed <= 0f || !HasMovement(stick))
                return false;
            // Right-stick up is positive; UI Toolkit local Y grows downward.
            Vector2 normalized = Vector2.ClampMagnitude(stick, 1f);
            Vector2 next = Clamp(Position +
                new Vector2(normalized.x, -normalized.y) *
                speed * Mathf.Min(deltaTime, 0.05f), area);
            if ((next - Position).sqrMagnitude < 0.0001f) return false;
            Position = next;
            return true;
        }

        public static bool HasMovement(Vector2 stick) =>
            stick.sqrMagnitude > Deadzone * Deadzone;

        public static Vector2 Clamp(Vector2 position, Rect bounds)
        {
            float xMargin = Mathf.Min(Margin, Mathf.Max(0f, bounds.width * 0.5f));
            float yMargin = Mathf.Min(Margin, Mathf.Max(0f, bounds.height * 0.5f));
            return new Vector2(
                Mathf.Clamp(position.x, bounds.xMin + xMargin, bounds.xMax - xMargin),
                Mathf.Clamp(position.y, bounds.yMin + yMargin, bounds.yMax - yMargin));
        }
    }
}
