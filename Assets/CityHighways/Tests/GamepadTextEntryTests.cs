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
    }
}
