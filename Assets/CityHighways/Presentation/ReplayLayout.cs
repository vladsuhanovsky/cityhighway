using UnityEngine;

namespace CityHighways.Presentation
{
    public static class ReplayLayout
    {
        public static bool Compact(float width) => width < 1400f;
        public static float SideWidth(float width) => Mathf.Clamp(width * 0.25f, 220f, 360f);
    }
}
