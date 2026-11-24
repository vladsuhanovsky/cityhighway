using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Retains the controller's last focused HUD control while a blocking
    /// window is open, then restores it if still visible and enabled.
    /// Each gameplay view owns an independent instance.
    /// </summary>
    public sealed class GamepadFocusReturn
    {
        VisualElement _previousScope;
        VisualElement _lastHudControl;

        /// <summary>
        /// A newly started or loaded session must not restore a button
        /// remembered from the previous game's visual tree.
        /// </summary>
        public void Reset()
        {
            _previousScope = null;
            _lastHudControl = null;
        }

        public static VisualElement RestorableControl(
            VisualElement root, VisualElement previouslyFocused)
        {
            return previouslyFocused != null &&
                !(previouslyFocused is MapView) &&
                UiGamepadFocus.IsEligible(previouslyFocused, root)
                    ? previouslyFocused : null;
        }

        /// <summary>
        /// A HUD item may be rebuilt while a modal owns the controller. If
        /// its former element is detached, recover only an eligible control
        /// with the same stable gamepad name from this session's root.
        /// </summary>
        public static VisualElement RestoreCandidate(
            VisualElement root, VisualElement previouslyFocused)
        {
            if (root == null || previouslyFocused == null ||
                previouslyFocused is MapView) return null;

            var existing = RestorableControl(root, previouslyFocused);
            if (existing != null) return existing;

            return UiGamepadFocus.IsStableControllerName(previouslyFocused.name)
                ? UiGamepadFocus.FindEligibleNamed(root, previouslyFocused.name)
                : null;
        }

        public void Update(VisualElement root, VisualElement preferredScope,
            bool controllerActive)
        {
            if (root == null || root.panel == null) return;
            VisualElement scope = UiGamepadFocus.ResolveScope(root, preferredScope);
            if (!controllerActive)
            {
                _previousScope = scope;
                _lastHudControl = null;
                return;
            }

            var focused = root.panel.focusController?.focusedElement as VisualElement;
            if (scope == root)
            {
                if (_previousScope != null && _previousScope != root)
                {
                    var restore = RestoreCandidate(root, _lastHudControl);
                    if (restore != null)
                    {
                        restore.Focus();
                        focused = restore;
                    }
                }
                var valid = RestorableControl(root, focused);
                if (valid != null) _lastHudControl = valid;
            }
            else if (_previousScope == root)
            {
                // A modal may take focus immediately as it opens. Keep the
                // last HUD focus recorded on an earlier gameplay frame.
                if (_lastHudControl == null)
                    _lastHudControl = RestorableControl(root, focused);
            }
            _previousScope = scope;
        }
    }
}
