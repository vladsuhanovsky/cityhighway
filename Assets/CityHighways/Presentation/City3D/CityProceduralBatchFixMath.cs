using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure preview of opt-in batch corrections without modifying scene objects.</summary>
    public static class CityProceduralBatchFixMath
    {
        public static bool TryPropose(Vector3 position, Vector3 scale, Vector3 targetPosition,
            float uniformFactor, float lowestWorldY, float groundY,
            bool alignHorizontal, bool fitOversized, bool raiseAboveGround,
            out Vector3 newPosition, out Vector3 newScale)
        {
            newPosition = position;
            newScale = scale;
            if (!Finite(position) || !Finite(scale) || !Finite(targetPosition) ||
                !Finite(uniformFactor) || uniformFactor <= 0f) return false;
            float factor = fitOversized ? Mathf.Min(1f, uniformFactor) : 1f;
            if (alignHorizontal)
                newPosition = CityProceduralPlacementFixMath.AlignHorizontal(position, targetPosition);
            if (raiseAboveGround)
            {
                if (!Finite(lowestWorldY) || !Finite(groundY)) return false;
                float predictedLowest = newPosition.y + factor * (lowestWorldY - position.y);
                if (!CityProceduralPlacementFixMath.TryRaiseAboveGround(
                    newPosition.y, predictedLowest, groundY, out float correctedY)) return false;
                newPosition.y = correctedY;
            }
            newScale = scale * factor;
            return Finite(newPosition) && Finite(newScale);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) =>
            Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
