using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class GamepadControlGuideTests
    {
        [Test]
        public void ConnectedGamepadShowsHelpOnlyAfterControllerInteraction()
        {
            Assert.IsFalse(GamepadControlGuide.ShouldDisplay(false, false, false));
            Assert.IsFalse(GamepadControlGuide.ShouldDisplay(true, false, false));
            Assert.IsTrue(GamepadControlGuide.ShouldDisplay(true, true, false));
            Assert.IsFalse(GamepadControlGuide.ShouldDisplay(true, true, true));
        }

        [Test]
        public void GameAndMagnateHintsExplainR3AndAreLocalized()
        {
            foreach (var context in new[]
            {
                GamepadControlGuide.Context.Game,
                GamepadControlGuide.Context.Magnate
            })
            {
                StringAssert.Contains("R3", GamepadControlGuide.Hint(context, true, false));
                StringAssert.Contains("R3", GamepadControlGuide.Hint(context, false, false));
                StringAssert.Contains("курсор", GamepadControlGuide.Hint(context, false, false));
                StringAssert.Contains("map cursor", GamepadControlGuide.Hint(context, true, false));
            }
        }

        [Test]
        public void MainMenuHintDescribesNavigationAndBack()
        {
            var english = GamepadControlGuide.Hint(
                GamepadControlGuide.Context.Menu, true, false);
            var russian = GamepadControlGuide.Hint(
                GamepadControlGuide.Context.Menu, false, false);
            StringAssert.Contains("D-pad", english);
            StringAssert.Contains("back", english);
            StringAssert.Contains("Крестовина", russian);
            StringAssert.Contains("назад", russian);
        }

        [Test]
        public void TextEntryHintExplicitlyUsesSteamDeckKeyboardShortcut()
        {
            foreach (var lang in new[] { false, true })
            {
                var text = GamepadControlGuide.Hint(
                    GamepadControlGuide.Context.Menu, lang, true);
                StringAssert.Contains("STEAM + X", text);
                StringAssert.DoesNotContain("R3", text);
            }
        }

        [Test]
        public void TextFieldAndItsNestedInputAreDetectedWithoutPanel()
        {
            var root = new VisualElement();
            var field = new TextField();
            var input = field.Q(className: TextField.inputUssClassName);
            root.Add(field);
            Assert.IsTrue(GamepadControlGuide.IsTextEntryElement(field));
            if (input != null)
                Assert.IsTrue(GamepadControlGuide.IsTextEntryElement(input));
            Assert.IsFalse(GamepadControlGuide.IsTextEntryElement(root));
            Assert.IsFalse(GamepadControlGuide.IsTextEntryElement(new Button()));
            Assert.IsFalse(GamepadControlGuide.IsTextEntryElement(null));
            Assert.IsFalse(GamepadControlGuide.TextEntryFocused(root));
        }

        [Test]
        public void HelpIsNotFocusableOrPointerPicking()
        {
            var hint = GamepadControlGuide.Create();
            Assert.IsFalse(hint.focusable);
            Assert.AreEqual(PickingMode.Ignore, hint.pickingMode);
            Assert.AreEqual(DisplayStyle.None, hint.style.display.value);
            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, false, true, false);
            Assert.AreEqual(DisplayStyle.Flex, hint.style.display.value);
            StringAssert.Contains("D-pad", hint.text);
            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, true, true, false);
            Assert.AreEqual(DisplayStyle.None, hint.style.display.value);
            Assert.DoesNotThrow(() => GamepadControlGuide.Update(null,
                GamepadControlGuide.Context.Menu, true, true, false, true, false));
        }

        [Test]
        public void LanguageSwitchUpdatesGuideText()
        {
            var hint = GamepadControlGuide.Create();
            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, false, true, false);
            var english = hint.text;
            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, false, false, false);
            Assert.AreNotEqual(english, hint.text);
            StringAssert.Contains("Крестовина", hint.text);
        }

        [Test]
        public void DeviceIdentityMapsKnownControllersAndDefaultsUnknownToXbox()
        {
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.PlayStation,
                GamepadControlGuide.DetectLayout("Sony", "DualSense Wireless Controller"));
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.PlayStation,
                GamepadControlGuide.DetectLayout("", "DualShock 4"));
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.Nintendo,
                GamepadControlGuide.DetectLayout("Nintendo", "Switch Pro Controller"));
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.Nintendo,
                GamepadControlGuide.DetectLayout("", "Joy-Con Left"));
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.Xbox,
                GamepadControlGuide.DetectLayout("Microsoft", "Xbox Wireless Controller"));
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.Xbox,
                GamepadControlGuide.DetectLayout("Valve", "Steam Deck Controller"));
            Assert.AreEqual(GamepadControlGuide.ControllerLayout.Xbox,
                GamepadControlGuide.DetectLayout(null, null));
        }

        [Test]
        public void ControllerFaceButtonsReflectActualSouthAndEastBindings()
        {
            Assert.AreEqual("Cross", GamepadControlGuide.SubmitLabel(
                GamepadControlGuide.ControllerLayout.PlayStation, true));
            Assert.AreEqual("Круг", GamepadControlGuide.BackLabel(
                GamepadControlGuide.ControllerLayout.PlayStation, false));
            Assert.AreEqual("B", GamepadControlGuide.SubmitLabel(
                GamepadControlGuide.ControllerLayout.Nintendo, true));
            Assert.AreEqual("A", GamepadControlGuide.BackLabel(
                GamepadControlGuide.ControllerLayout.Nintendo, true));
            Assert.AreEqual("A", GamepadControlGuide.SubmitLabel(
                GamepadControlGuide.ControllerLayout.Xbox, true));
            Assert.AreEqual("B", GamepadControlGuide.BackLabel(
                GamepadControlGuide.ControllerLayout.Xbox, true));
        }

        [Test]
        public void EveryContextUsesDeviceCorrectLabelsInBothLanguages()
        {
            foreach (var context in new[]
            {
                GamepadControlGuide.Context.Menu,
                GamepadControlGuide.Context.Game,
                GamepadControlGuide.Context.Magnate
            })
            {
                StringAssert.Contains("Cross: select",
                    GamepadControlGuide.Hint(context, true, false,
                        GamepadControlGuide.ControllerLayout.PlayStation));
                StringAssert.Contains("Circle: back",
                    GamepadControlGuide.Hint(context, true, false,
                        GamepadControlGuide.ControllerLayout.PlayStation));
                StringAssert.Contains(context == GamepadControlGuide.Context.Menu
                        ? "B: открыть" : "B: выбрать",
                    GamepadControlGuide.Hint(context, false, false,
                        GamepadControlGuide.ControllerLayout.Nintendo));
                StringAssert.Contains("A: назад",
                    GamepadControlGuide.Hint(context, false, false,
                        GamepadControlGuide.ControllerLayout.Nintendo));
            }
        }

        [Test]
        public void ChangingDeviceLayoutRefreshesVisibleControllerHelp()
        {
            var hint = GamepadControlGuide.Create();
            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, false, true, false,
                GamepadControlGuide.ControllerLayout.Xbox);
            StringAssert.Contains("A: select", hint.text);

            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, false, true, false,
                GamepadControlGuide.ControllerLayout.Nintendo);
            StringAssert.Contains("B: select", hint.text);
            StringAssert.Contains("A: back", hint.text);

            GamepadControlGuide.Update(hint, GamepadControlGuide.Context.Menu,
                true, true, false, false, false,
                GamepadControlGuide.ControllerLayout.PlayStation);
            StringAssert.Contains("Крест: открыть", hint.text);
            StringAssert.Contains("Круг: назад", hint.text);
        }

        [Test]
        public void CameraButtonLabelsMatchDetectedDeviceLayout()
        {
            var ps = GamepadControlGuide.ControllerLayout.PlayStation;
            var switchPad = GamepadControlGuide.ControllerLayout.Nintendo;
            var xbox = GamepadControlGuide.ControllerLayout.Xbox;

            Assert.AreEqual("L1/R1", GamepadControlGuide.ShoulderLabel(ps));
            Assert.AreEqual("L/R", GamepadControlGuide.ShoulderLabel(switchPad));
            Assert.AreEqual("LB/RB", GamepadControlGuide.ShoulderLabel(xbox));

            Assert.AreEqual("L2/R2", GamepadControlGuide.TriggerLabel(ps));
            Assert.AreEqual("ZL/ZR", GamepadControlGuide.TriggerLabel(switchPad));
            Assert.AreEqual("LT/RT", GamepadControlGuide.TriggerLabel(xbox));

            StringAssert.Contains("L1/R1: rotate",
                GamepadControlGuide.Hint(GamepadControlGuide.Context.Game,
                    true, false, ps));
            StringAssert.Contains("ZL/ZR: zoom",
                GamepadControlGuide.Hint(GamepadControlGuide.Context.Magnate,
                    true, false, switchPad));
        }

        [Test]
        public void TextEntryShortcutDoesNotDependOnControllerIdentity()
        {
            foreach (var layout in new[]
            {
                GamepadControlGuide.ControllerLayout.Xbox,
                GamepadControlGuide.ControllerLayout.PlayStation,
                GamepadControlGuide.ControllerLayout.Nintendo
            })
            {
                foreach (var english in new[] { false, true })
                {
                    var message = GamepadControlGuide.Hint(
                        GamepadControlGuide.Context.Menu, english, true, layout);
                    StringAssert.Contains("STEAM + X", message);
                    StringAssert.DoesNotContain("Cross:", message);
                    StringAssert.DoesNotContain("Circle:", message);
                }
            }
        }
    }
}
