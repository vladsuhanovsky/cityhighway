using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Restores focus to an actual visible control after opening a menu page.
    /// UI Toolkit's Input System navigation handles arrows, stick and submit;
    /// this class only supplies the initial/fallback focused element.
    /// </summary>
    public static class UiGamepadFocus
    {
        /// <summary>Find the first active focusable control in visual-tree order.</summary>
        public static VisualElement FirstEligible(VisualElement root)
        {
            if (root == null) return null;
            if (IsEligible(root, root)) return root;
            foreach (var child in root.Children())
            {
                var found = FindIn(child, root);
                if (found != null) return found;
            }
            return null;
        }

        static VisualElement FindIn(VisualElement element, VisualElement root)
        {
            if (!IsVisible(element, root)) return null;
            if (element.focusable && element.enabledInHierarchy) return element;
            foreach (var child in element.Children())
            {
                var found = FindIn(child, root);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// A focused item becomes invalid when a page, section or modal is
        /// hidden or rebuilt; do not keep navigation trapped on a stale control.
        /// </summary>
        public static bool IsEligible(VisualElement element, VisualElement root) =>
            element != null && element.focusable && element.enabledInHierarchy &&
            IsVisible(element, root);

        static bool IsVisible(VisualElement element, VisualElement root)
        {
            if (element == null || root == null) return false;
            for (var node = element; node != null; node = node.parent)
            {
                if (node.style.display.value == DisplayStyle.None ||
                    node.style.visibility.value == Visibility.Hidden)
                    return false;
                if (node.panel != null &&
                    (node.resolvedStyle.display == DisplayStyle.None ||
                     node.resolvedStyle.visibility == Visibility.Hidden ||
                     node.resolvedStyle.opacity < 0.05f))
                    return false;
                if (node == root) return true;
            }
            return false;
        }

        public static bool EnsureFocus(VisualElement root)
        {
            if (root == null || root.panel == null) return false;
            var focused = root.panel.focusController.focusedElement as VisualElement;
            if (IsEligible(focused, root)) return false;
            return FocusFirst(root);
        }

        public static bool FocusFirst(VisualElement root)
        {
            if (root == null || root.panel == null) return false;
            var candidate = FirstEligible(root);
            if (candidate == null) return false;
            candidate.Focus();
            return true;
        }
    }
}
