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
                    var restore = RestorableControl(root, _lastHudControl);
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
