using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure dimension matching for editor-authored visual replacements.</summary>
    public static class CityProceduralFitMath
    {
        /// <summary>
        /// Uniform multiplier that fits source model extents inside target volume.
        /// Keeps aspect ratio; does not compensate for different pivots or orientation.
        /// </summary>
        public static bool TryUniformFit(Vector3 sourceLocalSize, Vector3 currentWorldScale,
            Vector3 targetSize, out float multiplier)
        {
            multiplier = 1f;
            if (!Valid(sourceLocalSize) || !Valid(targetSize)) return false;
            Vector3 scaled = Vector3.Scale(sourceLocalSize, new Vector3(
                Mathf.Abs(currentWorldScale.x), Mathf.Abs(currentWorldScale.y), Mathf.Abs(currentWorldScale.z)));
            if (!Valid(scaled)) return false;
            float factor = Mathf.Min(targetSize.x / scaled.x,
                Mathf.Min(targetSize.y / scaled.y, targetSize.z / scaled.z));
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f) return false;
            multiplier = factor;
            return true;
        }

        static bool Valid(Vector3 size)
        {
            return IsPositiveFinite(size.x) && IsPositiveFinite(size.y) && IsPositiveFinite(size.z);
        }

        static bool IsPositiveFinite(float value) =>
            value > 0.000001f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
