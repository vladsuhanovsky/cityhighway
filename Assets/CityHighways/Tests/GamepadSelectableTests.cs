using System;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class GamepadSelectableTests
    {
        [Test]
        public void AttachingMakesCustomCardFocusableAndAddsNonPickingOutline()
        {
            var card = new VisualElement();
            Assert.IsFalse(card.focusable);
            GamepadSelectable.Attach(card, () => { });
            Assert.IsTrue(card.focusable);
            var outline = card.Q<VisualElement>("gamepad-focus-outline");
            Assert.IsNotNull(outline);
            Assert.AreEqual(PickingMode.Ignore, outline.pickingMode);
            Assert.AreEqual(DisplayStyle.None, outline.style.display.value);
        }

        [Test]
        public void SubmitOnlyAcceptsEnabledFocusedCardAsEventOrigin()
        {
            var card = new VisualElement();
            var child = new Button();
            card.Add(child);
            GamepadSelectable.Attach(card, () => { });
            Assert.IsTrue(GamepadSelectable.CanSubmit(card, card));
            Assert.IsFalse(GamepadSelectable.CanSubmit(card, child));
            card.SetEnabled(false);
            Assert.IsFalse(GamepadSelectable.CanSubmit(card, card));
            card.SetEnabled(true);
            card.focusable = false;
            Assert.IsFalse(GamepadSelectable.CanSubmit(card, card));
        }

        [Test]
        public void NestedDisabledParentCannotActivateCard()
        {
            var root = new VisualElement();
            var card = new VisualElement();
            root.Add(card);
            GamepadSelectable.Attach(card, () => { });
            root.SetEnabled(false);
            Assert.IsFalse(GamepadSelectable.CanSubmit(card, card));
        }

        [Test]
        public void InvalidBindingsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                GamepadSelectable.Attach(null, () => { }));
            Assert.Throws<ArgumentNullException>(() =>
                GamepadSelectable.Attach(new VisualElement(), null));
            Assert.IsFalse(GamepadSelectable.CanSubmit(null, null));
        }
    }
}
