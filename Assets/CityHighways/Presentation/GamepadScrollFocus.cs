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
        public static bool ContainsContentTarget(ScrollView scroll,
            VisualElement focused)
        {
            if (scroll == null || focused == null || focused == scroll ||
                focused == scroll.contentContainer || !scroll.Contains(focused))
                return false;

            // UI Toolkit can expose a different visual/logical parent chain
            // for ScrollView.Add() children in Edit Mode. The whole ScrollView
            // is the stable ownership boundary. Exclude its built-in scrollbars
            // so their focused controls never cause recursive scrolling.
            var vertical = scroll.verticalScroller;
            if (vertical != null &&
                (focused == vertical || vertical.Contains(focused)))
                return false;
            var horizontal = scroll.horizontalScroller;
            if (horizontal != null &&
                (focused == horizontal || horizontal.Contains(focused)))
                return false;
            return true;
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
                !ContainsContentTarget(scroll, focused) ||
                !focused.enabledInHierarchy ||
                !UiGamepadFocus.IsEligible(focused, scroll))
                return;

            scroll.ScrollTo(focused);
        }
    }
}
