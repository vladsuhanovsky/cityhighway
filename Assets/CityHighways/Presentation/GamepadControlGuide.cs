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

        public static string Hint(Context context, bool english, bool textEntry)
        {
            if (textEntry)
                return english
                    ? "Text entry: type with keyboard  |  Steam Deck: STEAM + X"
                    : "Ввод текста: клавиатура  |  Steam Deck: STEAM + X";

            if (context == Context.Menu)
                return english
                    ? "D-pad / left stick: navigate   A: select   B: back"
                    : "Крестовина / левый стик: выбор   A: открыть   B: назад";

            return context == Context.Magnate
                ? english
                    ? "D-pad: HUD   A: select   B: back   R3: map cursor   LT/RT: zoom"
                    : "Крестовина: интерфейс   A: выбрать   B: назад   R3: курсор   LT/RT: масштаб"
                : english
                    ? "D-pad: HUD   A: select   B: back   R3: map cursor   LB/RB: rotate"
                    : "Крестовина: интерфейс   A: выбрать   B: назад   R3: курсор   LB/RB: поворот";
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
            bool active, bool targetingMap, bool english, bool textEntry)
        {
            if (label == null) return;
            bool visible = ShouldDisplay(connected, active, targetingMap);
            if (label.style.display.value == (visible ? DisplayStyle.Flex : DisplayStyle.None)
                && (!visible || label.text == Hint(context, english, textEntry)))
                return;
            if (visible) label.text = Hint(context, english, textEntry);
            Ui.Show(label, visible);
        }
    }
}
