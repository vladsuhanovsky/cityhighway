using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class GamepadActionFocusTests
    {
        static VisualElement Root(out Button undo, out Button redo, out Button log)
        {
            var root = new VisualElement();
            undo = new Button { name = "gamepad-hud-undo" };
            redo = new Button { name = "gamepad-hud-redo" };
            log = new Button { name = "gamepad-hud-log" };
            root.Add(undo);
            root.Add(redo);
            root.Add(log);
            return root;
        }

        [Test]
        public void DisablingFocusedUndoPrefersEnabledRedo()
        {
            var root = Root(out var undo, out var redo, out var log);
            undo.SetEnabled(false);
            Assert.AreSame(redo, GamepadActionFocus.ReplacementAfterDisable(
                root, root, undo, undo, redo, log));
        }

        [Test]
        public void DisablingFocusedRedoPrefersEnabledUndo()
        {
            var root = Root(out var undo, out var redo, out var log);
            redo.SetEnabled(false);
            Assert.AreSame(undo, GamepadActionFocus.ReplacementAfterDisable(
                root, root, redo, undo, redo, log));
        }

        [Test]
        public void BothDisabledReturnToFocusableLogInsteadOfDeadControl()
        {
            var root = Root(out var undo, out var redo, out var log);
            undo.SetEnabled(false);
            redo.SetEnabled(false);
            Assert.AreSame(log, GamepadActionFocus.ReplacementAfterDisable(
                root, root, redo, undo, redo, log));
            Assert.AreSame(log, GamepadActionFocus.ReplacementAfterDisable(
                root, root, undo, undo, redo, log));

            log.SetEnabled(false);
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, root, undo, undo, redo, log));
        }

        [Test]
        public void ActiveModalBlocksTransferBehindIt()
        {
            var root = Root(out var undo, out var redo, out var log);
            var modal = new VisualElement();
            modal.Add(new Button());
            root.Add(modal);
            undo.SetEnabled(false);
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, modal, undo, undo, redo, log));
        }

        [Test]
        public void OtherFocusedControlAndStillEnabledActionAreUntouched()
        {
            var root = Root(out var undo, out var redo, out var log);
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, root, undo, undo, redo, log));
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, root, log, undo, redo, log));
            redo.SetEnabled(false);
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, root, log, undo, redo, log));
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                null, null, redo, undo, redo, log));
        }

        [Test]
        public void HiddenOrDetachedFallbackCannotCaptureController()
        {
            var root = Root(out var undo, out var redo, out var log);
            undo.SetEnabled(false);
            redo.SetEnabled(false);
            log.style.display = DisplayStyle.None;
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, root, undo, undo, redo, log));
            var detached = new Button();
            Assert.IsNull(GamepadActionFocus.ReplacementAfterDisable(
                root, root, redo, undo, redo, detached));
        }
    }
}
