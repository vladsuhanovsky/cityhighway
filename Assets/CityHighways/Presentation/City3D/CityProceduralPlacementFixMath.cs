using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure suggested corrections for the editor-only replacement placement workflow.</summary>
    public static class CityProceduralPlacementFixMath
    {
        /// <summary>Aligns X/Z to target without changing authored height.</summary>
        public static Vector3 AlignHorizontal(Vector3 current, Vector3 target) =>
            new Vector3(target.x, current.y, target.z);

        /// <summary>
        /// Raises the lowest mesh-bounds corner to sampled ground; never lowers an asset.
        /// Returns false for unavailable or invalid measurements.
        /// </summary>
        public static bool TryRaiseAboveGround(float currentY, float lowestWorldY, float groundY,
            out float correctedY)
        {
            correctedY = currentY;
            if (!Finite(currentY) || !Finite(lowestWorldY) || !Finite(groundY)) return false;
            correctedY = currentY + Mathf.Max(0f, groundY - lowestWorldY);
            return Finite(correctedY);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
