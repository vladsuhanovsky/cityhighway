using UnityEngine;
using UnityEngine.InputSystem;
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
        /// <summary>
        /// Ignore low stick drift and mouse-only sessions: the controller
        /// should claim menu focus only when the player actually navigates.
        /// </summary>
        public static bool HasNavigationIntent(Vector2 dpad, Vector2 stick, bool submit) =>
            submit || dpad.sqrMagnitude > 0.2f || stick.sqrMagnitude > 0.49f;

        public static bool ShouldGoBack(bool buttonPressed, bool mapTargetingOrExiting) =>
            buttonPressed && !mapTargetingOrExiting;

        /// <summary>
        /// Keep a valid UI focus in both gameplay HUDs, without claiming it
        /// from mouse-only players or intercepting game-rule actions.
        /// Button B uses only a registered visible Back action; mandatory
        /// decisions are never skipped by a synthetic confirm/cancel.
        /// </summary>
        public static void MaintainGameplayFocus(VisualElement root, ref bool active,
            bool suppressBack = false)
        {
            if (root == null || root.panel == null) return;
            var pad = Gamepad.current;
            if (pad == null)
            {
                active = false;
                return;
            }
            if (HasNavigationIntent(pad.dpad.ReadValue(),
                    pad.leftStick.ReadValue(), pad.buttonSouth.wasPressedThisFrame))
                active = true;
            if (pad.buttonEast.wasPressedThisFrame)
            {
                active = true;
                // B exits the virtual map cursor first. In that frame it
                // must not also dismiss a game modal or navigate backward.
                if (ShouldGoBack(true, suppressBack)) Ui.GoBack(root);
            }
            if (active) EnsureFocus(root);
        }

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

        /// <summary>Find a newly rebuilt card with a stable controller-focus name.</summary>
        public static VisualElement FindEligibleNamed(VisualElement root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var candidate = root.Q<VisualElement>(name);
            return IsEligible(candidate, root) ? candidate : null;
        }

        public static bool RestoreNamedFocus(VisualElement root, string name)
        {
            if (root == null || root.panel == null) return false;
            var candidate = FindEligibleNamed(root, name);
            if (candidate == null) return false;
            candidate.Focus();
            return true;
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
