using System;
using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>
    /// Distinguishes rivers already sampled by the Logistics Magnate
    /// generator from sparse, manually authored rivers. All consumers
    /// (water safety, 3D water meshes and hub placement) must respect the
    /// same dense centerline instead of applying a second Catmull-Rom pass.
    /// This helper deliberately has no UnityEngine dependency.
    /// </summary>
    public static class CityRiverPolyline
    {
        // Logical map pixels: 1400 x 780 correspond to 14 x 7.8 m in 3D.
        public const float MapWidth = 1400f;
        public const float MapHeight = 780f;
        public const float DenseMaxStepPixels = 45f;
        public const int DenseMinimumPoints = 20;

        public static bool IsAlreadySmoothed(IReadOnlyList<MapPoint> river)
        {
            if (river == null || river.Count < DenseMinimumPoints) return false;
            float maxStep2 = DenseMaxStepPixels * DenseMaxStepPixels;
            bool hasLength = false;
            for (int i = 1; i < river.Count; i++)
            {
                var a = river[i - 1];
                var b = river[i];
                if (float.IsNaN(a.X) || float.IsNaN(a.Y) ||
                    float.IsNaN(b.X) || float.IsNaN(b.Y) ||
                    float.IsInfinity(a.X) || float.IsInfinity(a.Y) ||
                    float.IsInfinity(b.X) || float.IsInfinity(b.Y))
                    return false;
                float dx = (a.X - b.X) * MapWidth;
                float dy = (a.Y - b.Y) * MapHeight;
                float squared = dx * dx + dy * dy;
                if (squared > maxStep2) return false;
                if (squared > 0.0001f) hasLength = true;
            }
            return hasLength;
        }
    }
}
