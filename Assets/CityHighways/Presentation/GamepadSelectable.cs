using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Enables gamepad/keyboard Submit on existing custom visual cards
    /// without synthesizing a pointer click or changing its mouse handler.
    /// </summary>
    public static class GamepadSelectable
    {
        public static bool CanSubmit(VisualElement element, IEventHandler origin) =>
            element != null && ReferenceEquals(element, origin) &&
            element.focusable && element.enabledInHierarchy;

        public static void Attach(VisualElement element, Action submit,
            Action onFocus = null, Action onBlur = null)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (submit == null) throw new ArgumentNullException(nameof(submit));
            element.focusable = true;
            var outline = new VisualElement
            {
                name = "gamepad-focus-outline",
                pickingMode = PickingMode.Ignore,
                focusable = false
            };
            Ui.Fill(outline);
            Ui.Border(outline, 2f, Theme.Gold);
            Ui.Radius(outline, 8f);
            Ui.Show(outline, false);
            element.Add(outline);

            element.RegisterCallback<FocusInEvent>(evt =>
            {
                if (evt.target != element) return;
                Ui.Show(outline, true);
                onFocus?.Invoke();
            });
            element.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (evt.target != element) return;
                Ui.Show(outline, false);
                onBlur?.Invoke();
            });
            element.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                if (!CanSubmit(element, evt.target)) return;
                evt.StopPropagation();
                submit();
            });
        }
    }
}
