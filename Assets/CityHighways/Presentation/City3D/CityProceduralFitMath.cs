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

        /// <summary>
        /// Comparison metadata for an authored replacement against a generated target.
        /// Each size ratio is replacement / target; a value of 1 means matching size.
        /// </summary>
        public static bool TryCompare(Vector3 replacementSize, Vector3 targetSize,
            Vector3 replacementPosition, Vector3 targetPosition,
            out Vector3 sizeRatios, out float centerOffset)
        {
            sizeRatios = Vector3.zero;
            centerOffset = 0f;
            if (!Valid(replacementSize) || !Valid(targetSize) ||
                !FinitePosition(replacementPosition) || !FinitePosition(targetPosition)) return false;
            sizeRatios = new Vector3(
                replacementSize.x / targetSize.x,
                replacementSize.y / targetSize.y,
                replacementSize.z / targetSize.z);
            centerOffset = Vector3.Distance(replacementPosition, targetPosition);
            return FinitePosition(sizeRatios) && !float.IsInfinity(centerOffset);
        }

        static bool FinitePosition(Vector3 position) =>
            !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
            !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
            !float.IsNaN(position.z) && !float.IsInfinity(position.z);

        static bool Valid(Vector3 size)
        {
            return IsPositiveFinite(size.x) && IsPositiveFinite(size.y) && IsPositiveFinite(size.z);
        }

        static bool IsPositiveFinite(float value) =>
            value > 0.000001f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
