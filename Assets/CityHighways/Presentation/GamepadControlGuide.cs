using System;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Lightweight, pointer-transparent controller hints for both game modes
    /// and the main menu. Steam Deck OS keyboard is user-invoked (STEAM+X);
    /// this class never attempts to emulate Steam Input or submit text.
    /// </summary>
    public static class GamepadControlGuide
    {
        public enum Context { Menu, Game, Magnate }
        public enum ControllerLayout { Xbox, PlayStation, Nintendo }

        /// <summary>
        /// Classify the identity exposed by Unity Input System, not arbitrary
        /// Steam Input remappings. Unknown and Steam Deck virtual Xbox pads
        /// deliberately keep Xbox/Deck A/B legends.
        /// </summary>
        public static ControllerLayout DetectLayout(string manufacturer, string product)
        {
            string identity = (manufacturer ?? "") + " " + (product ?? "");
            if (identity.IndexOf("nintendo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("joy-con", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("switch pro", StringComparison.OrdinalIgnoreCase) >= 0)
                return ControllerLayout.Nintendo;

            if (identity.IndexOf("sony", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("playstation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("dualshock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("dualsense", StringComparison.OrdinalIgnoreCase) >= 0)
                return ControllerLayout.PlayStation;

            return ControllerLayout.Xbox;
        }

        public static ControllerLayout CurrentLayout()
        {
            var pad = Gamepad.current;
            return pad == null ? ControllerLayout.Xbox :
                DetectLayout(pad.description.manufacturer,
                    (pad.description.product ?? "") + " " + (pad.displayName ?? ""));
        }

        public static string SubmitLabel(ControllerLayout layout, bool english)
        {
            if (layout == ControllerLayout.PlayStation) return english ? "Cross" : "Крест";
            return layout == ControllerLayout.Nintendo ? "B" : "A";
        }

        public static string BackLabel(ControllerLayout layout, bool english)
        {
            if (layout == ControllerLayout.PlayStation) return english ? "Circle" : "Круг";
            return layout == ControllerLayout.Nintendo ? "A" : "B";
        }

        public static bool IsTextEntryElement(VisualElement element)
        {
            for (var node = element; node != null; node = node.parent)
                if (node is TextField) return true;
            return false;
        }

        public static bool TextEntryFocused(VisualElement root)
        {
            if (root == null || root.panel == null) return false;
            var focused = root.panel.focusController?.focusedElement as VisualElement;
            if (focused == null || !root.Contains(focused)) return false;
            return IsTextEntryElement(focused);
        }

        public static string Hint(Context context, bool english, bool textEntry) =>
            Hint(context, english, textEntry, ControllerLayout.Xbox);

        public static string Hint(Context context, bool english, bool textEntry,
            ControllerLayout layout)
        {
            if (textEntry)
                return english
                    ? "Text entry: type with keyboard  |  Steam Deck: STEAM + X"
                    : "Ввод текста: клавиатура  |  Steam Deck: STEAM + X";

            string submit = SubmitLabel(layout, english);
            string back = BackLabel(layout, english);

            if (context == Context.Menu)
                return english
                    ? $"D-pad / left stick: navigate   {submit}: select   {back}: back"
                    : $"Крестовина / левый стик: выбор   {submit}: открыть   {back}: назад";

            return context == Context.Magnate
                ? english
                    ? $"D-pad: HUD   {submit}: select   {back}: back   R3: map cursor   LT/RT: zoom"
                    : $"Крестовина: интерфейс   {submit}: выбрать   {back}: назад   R3: курсор   LT/RT: масштаб"
                : english
                    ? $"D-pad: HUD   {submit}: select   {back}: back   R3: map cursor   LB/RB: rotate"
                    : $"Крестовина: интерфейс   {submit}: выбрать   {back}: назад   R3: курсор   LB/RB: поворот";
        }

        public static bool ShouldDisplay(bool gamepadConnected, bool controllerActive,
            bool targetingMap) => gamepadConnected && controllerActive && !targetingMap;

        public static Label Create()
        {
            var label = Ui.Text("", 12f, Theme.Text, true);
            label.name = "gamepad-controller-help";
            label.pickingMode = PickingMode.Ignore;
            label.focusable = false;
            label.style.position = Position.Absolute;
            label.style.right = 14f;
            label.style.bottom = 8f;
            label.style.maxWidth = 580f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(label, 7f);
            Ui.Padding(label, 6f, 10f);
            Ui.Show(label, false);
            return label;
        }

        public static void Update(Label label, Context context, bool connected,
            bool active, bool targetingMap, bool english, bool textEntry,
            ControllerLayout? layout = null)
        {
            if (label == null) return;
            bool visible = ShouldDisplay(connected, active, targetingMap);
            // Recompute both device labels and language in live views, so a
            // controller hot swap does not leave the previous guide onscreen.
            string hint = visible ? Hint(context, english, textEntry,
                layout ?? CurrentLayout()) : null;
            if (label.style.display.value == (visible ? DisplayStyle.Flex : DisplayStyle.None)
                && (!visible || label.text == hint))
                return;
            if (visible) label.text = hint;
            Ui.Show(label, visible);
        }
    }
}
