using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Presentation-only observation session. Manual input abandons the
    /// bookmark; an explicit return or lost target restores it exactly once.</summary>
    public sealed class CharacterObservation
    {
        public const float ObservationZoom = 8f;
        public bool Active { get; private set; }
        float previousZoom;
        Vector2 previousPan;

        public static bool IsFinite(Vector3 point) =>
            !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
            !float.IsNaN(point.z) && !float.IsInfinity(point.z);

        public bool Begin(float zoom, Vector2 pan)
        {
            if (Active || float.IsNaN(zoom) || float.IsInfinity(zoom) ||
                !IsFinite(new Vector3(pan.x, pan.y, 0f))) return false;
            previousZoom = zoom;
            previousPan = pan;
            Active = true;
            return true;
        }

        public bool End(bool restore, out float zoom, out Vector2 pan)
        {
            zoom = previousZoom;
            pan = previousPan;
            if (!Active) return false;
            Active = false;
            return restore;
        }
    }
}
