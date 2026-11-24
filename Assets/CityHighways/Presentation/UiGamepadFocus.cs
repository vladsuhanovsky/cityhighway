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
        /// A moving mouse/Steam Deck trackpad hands focus back to the pointer
        /// without requiring a click. Ignore subpixel noise and tiny drift.
        /// </summary>
        public static bool HasMouseIntent(Vector2 delta) => delta.sqrMagnitude >= 9f;

        /// <summary>
        /// Keep a valid UI focus in both gameplay HUDs, without claiming it
        /// from mouse-only players or intercepting game-rule actions.
        /// Button B uses only a registered visible Back action; mandatory
        /// decisions are never skipped by a synthetic confirm/cancel.
        /// </summary>
        public static void MaintainGameplayFocus(VisualElement root, ref bool active,
            bool suppressBack = false, VisualElement priorityScope = null)
        {
            if (root == null || root.panel == null) return;
            var pad = Gamepad.current;
            if (pad == null)
            {
                active = false;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null && HasMouseIntent(mouse.delta.ReadValue()))
                active = false;
            if (HasNavigationIntent(pad.dpad.ReadValue(),
                    pad.leftStick.ReadValue(), pad.buttonSouth.wasPressedThisFrame))
                active = true;
            if (pad.buttonEast.wasPressedThisFrame)
            {
                active = true;
                // B exits the virtual map cursor first. In that frame it
                // must not also dismiss a game modal or navigate backward.
                if (ShouldGoBack(true, suppressBack) &&
                    !GamepadTextEntry.TryExit(root))
                    Ui.GoBack(root);
            }
            if (active) EnsureFocus(ResolveScope(root, priorityScope));
        }

        /// <summary>
        /// Constrain controller focus to a visible blocking modal rather than
        /// placing it on underlying HUD buttons during a payment or handoff.
        /// </summary>
        public static VisualElement ResolveScope(VisualElement root, VisualElement preferred)
        {
            if (root == null) return null;
            return preferred != null && preferred.enabledInHierarchy &&
                IsVisible(preferred, root) ? preferred : root;
        }

        /// <summary>Find the first active focusable control in visual-tree order.</summary>
        public static VisualElement FirstEligible(VisualElement root)
        {
            if (root == null || !IsVisible(root, root)) return null;
            // Modal layers may be focusable only to receive Escape. Their
            // action buttons must take priority over the backdrop itself.
            foreach (var child in root.Children())
            {
                var found = FindIn(child, root);
                if (found != null) return found;
            }
            return IsEligible(root, root) ? root : null;
        }

        static VisualElement FindIn(VisualElement element, VisualElement root)
        {
            if (!IsVisible(element, root)) return null;

            // ScrollView itself may be focusable but has no primary action;
            // prefer its visible controls so A selects an item instead of
            // parking controller focus on the scroll container.
            if (!(element is ScrollView) && element.focusable &&
                element.enabledInHierarchy)
                return element;
            foreach (var child in element.Children())
            {
                var found = FindIn(child, root);
                if (found != null) return found;
            }
            return element.focusable && element.enabledInHierarchy ? element : null;
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
            // A hidden old card and a rebuilt visible card can briefly
            // coexist with the same focus name. Find the visible one.
            foreach (var candidate in root.Query<VisualElement>(name).ToList())
                if (IsEligible(candidate, root)) return candidate;
            return null;
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
            if (IsEligible(focused, root) &&
                (focused != root || FirstEligible(root) == root))
                return false;
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
