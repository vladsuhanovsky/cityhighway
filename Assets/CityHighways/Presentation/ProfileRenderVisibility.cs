using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Shared visibility check for optional offscreen rendering. A hidden ancestor
    /// must suspend rendering even when the child still has its own display set to Flex.</summary>
    public static class ProfileRenderVisibility
    {
        public static bool IsVisible(VisualElement element)
        {
            if (element == null || element.panel == null) return false;
            for (var el = element; el != null; el = el.parent)
                if (el.resolvedStyle.display == DisplayStyle.None ||
                    el.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        // The observation control hides itself when unavailable; test its parent instead.
        public static bool IsParentVisible(VisualElement element) => IsVisible(element?.parent);
    }
}
