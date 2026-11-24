using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class GamepadHudIconTests
    {
        [Test]
        public void IconButtonsRemainNativeFocusableButtons()
        {
            var icon = new IconButton(IconButton.Glyph.Menu, "Menu", () => { });
            Assert.IsInstanceOf<Button>(icon);
            Assert.IsTrue(icon.focusable);
            Assert.IsTrue(icon.enabledInHierarchy);
        }

        [Test]
        public void MinimapCameraAndCarouselIconsCanJoinFocusNavigation()
        {
            var rotate = new IconButton(IconButton.Glyph.RotateLeft, "", () => { });
            var scroll = new IconButton(IconButton.Glyph.Right, "", () => { });
            var root = new VisualElement();
            root.Add(rotate);
            root.Add(scroll);
            Assert.AreSame(rotate, UiGamepadFocus.FirstEligible(root));
            rotate.SetEnabled(false);
            Assert.AreSame(scroll, UiGamepadFocus.FirstEligible(root));
        }
    }
}
