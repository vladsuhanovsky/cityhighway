using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class GamepadTextEntryTests
    {
        [Test]
        public void NativeInputDescendantResolvesEnclosingTextField()
        {
            var field = new TextField();
            var input = field.Q(className: TextField.inputUssClassName);
            Assert.AreSame(field, GamepadTextEntry.EnclosingField(field));
            if (input != null)
                Assert.AreSame(field, GamepadTextEntry.EnclosingField(input));
            Assert.IsNull(GamepadTextEntry.EnclosingField(new Button()));
            Assert.IsNull(GamepadTextEntry.EnclosingField(null));
        }

        [Test]
        public void ExitFocusPrefersButtonInSamePlayerRow()
        {
            var root = new VisualElement();
            var row = new VisualElement();
            var field = new TextField();
            var type = new Button();
            var other = new Button();
            root.Add(row);
            row.Add(field);
            row.Add(type);
            root.Add(other);
            Assert.AreSame(type, GamepadTextEntry.ExitTarget(root, field));
            type.SetEnabled(false);
            Assert.AreSame(other, GamepadTextEntry.ExitTarget(root, field));
        }

        [Test]
        public void ExitFocusDoesNotSelectHiddenOrDetachedButtons()
        {
            var root = new VisualElement();
            var row = new VisualElement();
            var field = new TextField();
            var hidden = new Button();
            var visible = new Button();
            row.Add(field);
            row.Add(hidden);
            row.Add(visible);
            root.Add(row);
            hidden.style.display = DisplayStyle.None;
            Assert.AreSame(visible, GamepadTextEntry.ExitTarget(root, field));
            visible.RemoveFromHierarchy();
            Assert.IsNull(GamepadTextEntry.ExitTarget(root, field));
        }

        [Test]
        public void FocusBorderCanBeAttachedToNativeTextFieldWithoutPanel()
        {
            var field = new TextField();
            var input = field.Q(className: TextField.inputUssClassName) ?? field;
            Assert.DoesNotThrow(() =>
                GamepadTextEntry.InstallFocusBorder(field, input, Theme.TextDim));
            Assert.DoesNotThrow(() =>
                GamepadTextEntry.InstallFocusBorder(null, input, Theme.Line));
            Assert.DoesNotThrow(() =>
                GamepadTextEntry.InstallFocusBorder(field, null, Theme.Line));
        }

        [Test]
        public void DetachedFieldsCannotAffectUnrelatedMenus()
        {
            var root = new VisualElement();
            var field = new TextField();
            var unrelated = new VisualElement();
            unrelated.Add(field);
            Assert.IsNull(GamepadTextEntry.ExitTarget(root, field));
            Assert.IsNull(GamepadTextEntry.ExitTarget(root, null));
            Assert.IsNull(GamepadTextEntry.ExitTarget(null, field));
            Assert.IsFalse(GamepadTextEntry.TryExit(root));
            Assert.IsFalse(GamepadTextEntry.TryExit(null));
        }

        [Test]
        public void ExitFocusFindsEnabledButtonInsideSiblingWrapper()
        {
            var root = new VisualElement();
            var row = new VisualElement();
            var inputWrapper = new VisualElement();
            var actionWrapper = new VisualElement();
            var field = new TextField();
            var action = new Button();
            root.Add(row);
            row.Add(inputWrapper);
            row.Add(actionWrapper);
            inputWrapper.Add(field);
            actionWrapper.Add(action);

            Assert.AreSame(action, GamepadTextEntry.ExitTarget(root, field));
            action.SetEnabled(false);
            Assert.IsNull(GamepadTextEntry.ExitTarget(root, field));
        }

        [Test]
        public void ExitFocusNeverTargetsButtonsInsideTheTextFieldBranch()
        {
            var root = new VisualElement();
            var row = new VisualElement();
            var wrapper = new VisualElement();
            var field = new TextField();
            var inner = new Button();
            var outside = new Button();
            root.Add(row);
            row.Add(wrapper);
            wrapper.Add(field);
            field.Add(inner);
            row.Add(outside);

            // An internal focusable control must never keep the user inside
            // the field when B/Escape explicitly ends editing.
            Assert.AreSame(outside, GamepadTextEntry.ExitTarget(root, field));
            outside.SetEnabled(false);
            Assert.IsNull(GamepadTextEntry.ExitTarget(root, field));
        }
    }
}
