using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure, editor-facing diagnostics for comparing authored geometry with an approximate generated volume.</summary>
    public static class CityProceduralPlacementDiagnostics
    {
        public struct Result
        {
            public bool Oversized;
            public bool Offset;
            public bool BelowGround;
            public float MaximumSizeRatio;
            public float HorizontalOffset;
            public float GroundPenetration;
        }

        /// <summary>
        /// Approximate checks; groundY is the catalog's center terrain sample, not a collision test.
        /// </summary>
        public static bool TryEvaluate(Vector3 prefabSize, Vector3 targetSize,
            Vector3 prefabPivot, Vector3 targetPosition, float prefabLowestY, float groundY,
            out Result result)
        {
            result = default;
            if (!Positive(prefabSize.x) || !Positive(prefabSize.y) || !Positive(prefabSize.z) ||
                !Positive(targetSize.x) || !Positive(targetSize.y) || !Positive(targetSize.z) ||
                !Finite(prefabPivot.x) || !Finite(prefabPivot.y) || !Finite(prefabPivot.z) ||
                !Finite(targetPosition.x) || !Finite(targetPosition.y) || !Finite(targetPosition.z) ||
                !Finite(prefabLowestY) || !Finite(groundY)) return false;

            result.MaximumSizeRatio = Mathf.Max(prefabSize.x / targetSize.x,
                Mathf.Max(prefabSize.y / targetSize.y, prefabSize.z / targetSize.z));
            var dx = prefabPivot.x - targetPosition.x;
            var dz = prefabPivot.z - targetPosition.z;
            result.HorizontalOffset = Mathf.Sqrt(dx * dx + dz * dz);
            result.GroundPenetration = Mathf.Max(0f, groundY - prefabLowestY);
            if (!Finite(result.MaximumSizeRatio) || !Finite(result.HorizontalOffset) ||
                !Finite(result.GroundPenetration)) return false;

            result.Oversized = result.MaximumSizeRatio > 1.15f;
            result.Offset = result.HorizontalOffset > Mathf.Max(0.02f,
                Mathf.Max(targetSize.x, targetSize.z) * 0.25f);
            result.BelowGround = result.GroundPenetration > 0.01f;
            return true;
        }

        static bool Positive(float v) => Finite(v) && v > 0.000001f;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
