using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>A bounded loop between two adjacent existing parallel hub-pad walks.
    /// This is not a navmesh or an inter-district path.</summary>
    public sealed class ProfilePadRoute
    {
        public const float Speed = .006f;
        readonly Vector3[] points;
        readonly float[] lengths;
        public float Length { get; }

        ProfilePadRoute(Vector3[] points)
        {
            this.points = points;
            lengths = new float[points.Length];
            float total = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                lengths[i] = Vector3.Distance(points[i], points[(i+1)%points.Length]);
                total += lengths[i];
            }
            Length = total;
        }

        public static ProfilePadRoute TryCreate(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (!CharacterObservation.IsFinite(a) || !CharacterObservation.IsFinite(b) ||
                !CharacterObservation.IsFinite(c) || !CharacterObservation.IsFinite(d)) return null;
            Vector3 lane = b-a, offset = c-a;
            float width = offset.magnitude;
            if (lane.magnitude < .03f || lane.magnitude > .12f || width < .004f || width > .010f ||
                Mathf.Abs(lane.y) > .0001f || Mathf.Abs(offset.y) > .0001f ||
                Mathf.Abs(Vector3.Dot(lane.normalized, offset.normalized)) > .05f ||
                ((d-b)-offset).magnitude > .0002f) return null;
            return new ProfilePadRoute(new[] { a,b,d,c });
        }

        public bool TrySample(float distance, out Vector3 position, out Quaternion heading)
        {
            position = default; heading = Quaternion.identity;
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return false;
            float remaining = Mathf.Repeat(distance, Length);
            for (int i = 0; i < points.Length; i++)
            {
                if (remaining >= lengths[i] && i != points.Length-1)
                { remaining -= lengths[i]; continue; }
                Vector3 next = points[(i+1)%points.Length];
                position = Vector3.Lerp(points[i], next, remaining / lengths[i]);
                heading = Quaternion.LookRotation(next-points[i], Vector3.up);
                return true;
            }
            return false;
        }
    }
}
