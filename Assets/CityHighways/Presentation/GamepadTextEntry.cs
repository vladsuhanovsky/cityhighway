using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// A/Submit focuses a native UI Toolkit TextField as before. While
    /// editing, B or Escape leaves the field without navigating out of the
    /// entire menu. The user can bring up the Steam Deck OS keyboard using
    /// STEAM+X; we do not emulate platform-specific keyboard APIs.
    /// </summary>
    public static class GamepadTextEntry
    {
        /// <summary>Highlight the actual inner input surface, not its container.</summary>
        public static void InstallFocusBorder(TextField field,
            VisualElement inputSurface, Color normalBorder)
        {
            if (field == null || inputSurface == null) return;
            field.RegisterCallback<FocusInEvent>(_ =>
                Ui.Border(inputSurface, 2f, Theme.Accent));
            field.RegisterCallback<FocusOutEvent>(_ =>
                Ui.Border(inputSurface, 1f, normalBorder));
        }

        public static TextField EnclosingField(VisualElement element)
        {
            for (var node = element; node != null; node = node.parent)
                if (node is TextField field) return field;
            return null;
        }

        /// <summary>
        /// Prefer an adjacent button from the same settings/player row.
        /// This prevents the next focus-repair tick from reopening text entry.
        /// </summary>
        public static Button ExitTarget(VisualElement scope, TextField field)
        {
            if (scope == null || field == null || !scope.Contains(field))
                return null;
            for (var ancestor = field.parent; ancestor != null; ancestor = ancestor.parent)
            {
                foreach (var sibling in ancestor.Children())
                {
                    // Never jump into the field's own nested input controls.
                    if (sibling == field || sibling.Contains(field)) continue;
                    if (sibling is Button button &&
                        UiGamepadFocus.IsEligible(button, scope))
                        return button;
                    // Player/profile rows also wrap buttons in styling
                    // containers, not just direct siblings of the TextField.
                    foreach (var nested in sibling.Query<Button>().ToList())
                        if (UiGamepadFocus.IsEligible(nested, scope))
                            return nested;
                }
                if (ancestor == scope) break;
            }
            return null;
        }

        public static bool TryExit(VisualElement root)
        {
            if (root == null || root.panel == null) return false;
            var focused = root.panel.focusController?.focusedElement as VisualElement;
            if (focused == null || !root.Contains(focused)) return false;
            var field = EnclosingField(focused);
            if (field == null) return false;
            var button = ExitTarget(root, field);
            field.Blur();
            if (button != null) button.Focus();
            return true;
        }
    }
}
