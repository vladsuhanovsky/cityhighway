using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// On controller/keyboard focus changes, reveal the newly focused
    /// control inside a vertical or horizontal ScrollView. Pointer scrolling,
    /// kinetic dragging and normal wheel handling are unchanged.
    /// </summary>
    public static class GamepadScrollFocus
    {
        public static bool ContainsContentTarget(VisualElement content,
            VisualElement focused)
        {
            if (content == null || focused == null || content == focused)
                return false;
            for (var node = focused.parent; node != null; node = node.parent)
                if (node == content) return true;
            return false;
        }

        public static void Register(ScrollView scroll)
        {
            if (scroll == null) return;
            scroll.RegisterCallback<FocusInEvent>(RevealFocused,
                TrickleDown.TrickleDown);
        }

        static void RevealFocused(FocusInEvent evt)
        {
            var scroll = evt.currentTarget as ScrollView;
            var focused = evt.target as VisualElement;
            if (scroll == null || scroll.panel == null ||
                !ContainsContentTarget(scroll.contentContainer, focused) ||
                !focused.enabledInHierarchy ||
                !UiGamepadFocus.IsEligible(focused, scroll))
                return;

            scroll.ScrollTo(focused);
        }
    }
}
