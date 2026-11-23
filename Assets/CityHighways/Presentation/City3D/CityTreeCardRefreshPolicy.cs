using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Small pure LOD policy: only refresh billboards that can currently be drawn.</summary>
    public static class CityTreeCardRefreshPolicy
    {
        public static bool ShouldRefresh(bool cardsVisible, float appliedTilt,
            float appliedYaw, float cameraTilt, float cameraYaw)
        {
            if (!cardsVisible) return false;
            if (float.IsNaN(cameraTilt) || float.IsInfinity(cameraTilt) ||
                float.IsNaN(cameraYaw) || float.IsInfinity(cameraYaw)) return false;
            if (float.IsNaN(appliedTilt) || float.IsNaN(appliedYaw)) return true;
            return Mathf.Abs(cameraTilt - appliedTilt) > 3f ||
                   Mathf.Abs(Mathf.DeltaAngle(cameraYaw, appliedYaw)) > 4f;
        }
    }
}
