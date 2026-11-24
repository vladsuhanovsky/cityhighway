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
    }
}
