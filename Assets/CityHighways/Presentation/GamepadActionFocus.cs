using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Choose a safe adjacent HUD button when Undo/Redo becomes disabled
    /// under controller focus. This policy has no Input System dependency
    /// and never takes focus away from a blocking overlay or text field.
    /// </summary>
    public static class GamepadActionFocus
    {
        public static VisualElement ReplacementAfterDisable(
            VisualElement root, VisualElement activeScope,
            VisualElement previouslyFocused, VisualElement undo,
            VisualElement redo, VisualElement fallback)
        {
            if (root == null || activeScope != root || previouslyFocused == null)
                return null;

            if (ReferenceEquals(previouslyFocused, undo) &&
                !UiGamepadFocus.IsEligible(undo, root))
                return Eligible(redo, root) ?? Eligible(fallback, root);

            if (ReferenceEquals(previouslyFocused, redo) &&
                !UiGamepadFocus.IsEligible(redo, root))
                return Eligible(undo, root) ?? Eligible(fallback, root);

            return null;
        }

        static VisualElement Eligible(VisualElement element, VisualElement root) =>
            UiGamepadFocus.IsEligible(element, root) ? element : null;
    }
}
