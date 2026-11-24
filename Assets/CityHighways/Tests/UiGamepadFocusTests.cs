using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class UiGamepadFocusTests
    {
        [Test]
        public void FirstEligibleSkipsHiddenPagesAndStartsOnVisibleButton()
        {
            var root = new VisualElement();
            var hiddenPage = new VisualElement();
            var hiddenButton = new Button();
            hiddenPage.Add(hiddenButton);
            hiddenPage.style.display = DisplayStyle.None;
            root.Add(hiddenPage);

            var visiblePage = new VisualElement();
            var first = new Button();
            var second = new Button();
            visiblePage.Add(first);
            visiblePage.Add(second);
            root.Add(visiblePage);

            Assert.AreSame(first, UiGamepadFocus.FirstEligible(root));
            Assert.IsFalse(UiGamepadFocus.IsEligible(hiddenButton, root));
            Assert.IsTrue(UiGamepadFocus.IsEligible(second, root));
        }

        [Test]
        public void NestedHiddenAndDisabledContainersCannotReceiveFocus()
        {
            var root = new VisualElement();
            var invisible = new VisualElement();
            invisible.style.visibility = Visibility.Hidden;
            var hiddenChild = new Button();
            invisible.Add(hiddenChild);
            root.Add(invisible);

            var disabled = new VisualElement();
            var disabledChild = new Button();
            disabled.Add(disabledChild);
            disabled.SetEnabled(false);
            root.Add(disabled);

            var available = new Button();
            root.Add(available);

            Assert.IsFalse(UiGamepadFocus.IsEligible(hiddenChild, root));
            Assert.IsFalse(UiGamepadFocus.IsEligible(disabledChild, root));
            Assert.AreSame(available, UiGamepadFocus.FirstEligible(root));
        }

        [Test]
        public void FocusIgnoresDecorativeAndUnrelatedElements()
        {
            var root = new VisualElement();
            root.Add(new Label("Heading"));
            var decor = new VisualElement { focusable = false };
            root.Add(decor);
            var button = new Button();
            root.Add(button);
            var unrelated = new Button();

            Assert.IsFalse(UiGamepadFocus.IsEligible(decor, root));
            Assert.IsFalse(UiGamepadFocus.IsEligible(unrelated, root));
            Assert.AreSame(button, UiGamepadFocus.FirstEligible(root));
        }

        [Test]
        public void FocusWakesOnlyForMeaningfulControllerInput()
        {
            Assert.IsFalse(UiGamepadFocus.HasNavigationIntent(
                Vector2.zero, Vector2.zero, false));
            Assert.IsFalse(UiGamepadFocus.HasNavigationIntent(
                Vector2.zero, new Vector2(0.05f, -0.02f), false));
            Assert.IsTrue(UiGamepadFocus.HasNavigationIntent(
                Vector2.up, Vector2.zero, false));
            Assert.IsTrue(UiGamepadFocus.HasNavigationIntent(
                Vector2.zero, new Vector2(0.9f, 0f), false));
            Assert.IsTrue(UiGamepadFocus.HasNavigationIntent(
                Vector2.zero, Vector2.zero, true));
        }

        [Test]
        public void BackButtonsAreRecognizedInRussianAndEnglish()
        {
            Assert.IsTrue(Ui.IsBackLabel("← Назад"));
            Assert.IsTrue(Ui.IsBackLabel("← Назад к настройкам"));
            Assert.IsTrue(Ui.IsBackLabel("← Back"));
            Assert.IsTrue(Ui.IsBackLabel("← Back to menu"));
            Assert.IsFalse(Ui.IsBackLabel("← Continue"));
            Assert.IsFalse(Ui.IsBackLabel("Назад без стрелки"));
            Assert.IsFalse(Ui.IsBackLabel(""));
            Assert.IsFalse(Ui.IsBackLabel(null));
        }

        [Test]
        public void EmptyOrDetachedUiDoesNotStealFocus()
        {
            Assert.IsNull(UiGamepadFocus.FirstEligible(null));
            Assert.IsNull(UiGamepadFocus.FirstEligible(new VisualElement()));
            Assert.IsFalse(UiGamepadFocus.EnsureFocus(new VisualElement()));
            Assert.IsFalse(UiGamepadFocus.FocusFirst(new VisualElement()));
        }
    }
}
