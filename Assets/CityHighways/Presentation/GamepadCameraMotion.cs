using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Controller camera intentions; independent of Gamepad hardware so they
    /// can be exercised by Unity Edit Mode tests without a connected device.
    /// Buttons intentionally avoid UI Navigate (D-pad and sticks).
    /// </summary>
    public readonly struct GamepadCameraMotion
    {
        public readonly float Turn;
        public readonly float Tilt;
        public readonly float Zoom;
        public readonly bool Reset;

        GamepadCameraMotion(float turn, float tilt, float zoom, bool reset)
        {
            Turn = turn;
            Tilt = tilt;
            Zoom = zoom;
            Reset = reset;
        }

        public bool HasMovement => Turn != 0f || Tilt != 0f || Zoom != 0f;

        public static GamepadCameraMotion Read(
            bool leftShoulder, bool rightShoulder,
            bool westButton, bool northButton,
            float leftTrigger, float rightTrigger,
            bool reset)
        {
            float turn = (rightShoulder ? 1f : 0f) - (leftShoulder ? 1f : 0f);
            float tilt = (northButton ? 1f : 0f) - (westButton ? 1f : 0f);
            float zoom = Mathf.Clamp01(rightTrigger) - Mathf.Clamp01(leftTrigger);
            // Ignore trigger rest noise, but preserve analogue pressure beyond it.
            if (Mathf.Abs(zoom) < 0.15f) zoom = 0f;
            return new GamepadCameraMotion(turn, tilt, zoom, reset);
        }
    }
}
