using System.Linq;
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
        public void NamedCardCanBeRecoveredOnlyWhenItIsVisibleAndEnabled()
        {
            var root = new VisualElement();
            var card = new VisualElement
            {
                name = "gamepad-contract-choice-0-abc",
                focusable = true
            };
            root.Add(card);
            Assert.AreSame(card, UiGamepadFocus.FindEligibleNamed(root, card.name));
            Assert.IsNull(UiGamepadFocus.FindEligibleNamed(root, "absent"));
            Assert.IsNull(UiGamepadFocus.FindEligibleNamed(root, null));

            card.style.display = DisplayStyle.None;
            Assert.IsNull(UiGamepadFocus.FindEligibleNamed(root, card.name));
            card.style.display = DisplayStyle.Flex;
            card.SetEnabled(false);
            Assert.IsNull(UiGamepadFocus.FindEligibleNamed(root, card.name));
        }

        [Test]
        public void MapCursorConsumesBackInsteadOfDismissingGameOverlay()
        {
            Assert.IsTrue(UiGamepadFocus.ShouldGoBack(true, false));
            Assert.IsFalse(UiGamepadFocus.ShouldGoBack(true, true));
            Assert.IsFalse(UiGamepadFocus.ShouldGoBack(false, true));
            Assert.IsFalse(UiGamepadFocus.ShouldGoBack(false, false));
        }

        [Test]
        public void ActiveOverlayHasFocusPriorityOverUnderlyingHud()
        {
            var root = new VisualElement();
            var toolbar = new Button();
            root.Add(toolbar);
            var overlay = new VisualElement();
            overlay.Add(new Button());
            root.Add(overlay);
            Assert.AreSame(overlay, UiGamepadFocus.ResolveScope(root, overlay));
            Assert.AreSame(overlay.Children().First(), UiGamepadFocus.FirstEligible(
                UiGamepadFocus.ResolveScope(root, overlay)));

            overlay.style.display = DisplayStyle.None;
            Assert.AreSame(root, UiGamepadFocus.ResolveScope(root, overlay));
            overlay.style.display = DisplayStyle.Flex;
            overlay.SetEnabled(false);
            Assert.AreSame(root, UiGamepadFocus.ResolveScope(root, overlay));
        }

        [Test]
        public void DetachedModalCannotClaimFocusFromGameplay()
        {
            var root = new VisualElement();
            var unrelated = new VisualElement();
            Assert.AreSame(root, UiGamepadFocus.ResolveScope(root, unrelated));
            Assert.AreSame(root, UiGamepadFocus.ResolveScope(root, null));
            Assert.IsNull(UiGamepadFocus.ResolveScope(null, unrelated));
        }

        [Test]
        public void FocusableModalBackdropPrefersNestedCloseButton()
        {
            var backdrop = new VisualElement { focusable = true };
            var dialog = new VisualElement();
            var close = new Button();
            backdrop.Add(dialog);
            dialog.Add(close);
            Assert.AreSame(close, UiGamepadFocus.FirstEligible(backdrop));
            close.SetEnabled(false);
            Assert.AreSame(backdrop, UiGamepadFocus.FirstEligible(backdrop));
        }

        [Test]
        public void RestoreCandidateMustBelongToVisibleEnabledHud()
        {
            var root = new VisualElement();
            var action = new Button();
            root.Add(action);
            Assert.AreSame(action, GamepadFocusReturn.RestorableControl(root, action));
            action.style.display = DisplayStyle.None;
            Assert.IsNull(GamepadFocusReturn.RestorableControl(root, action));
            action.style.display = DisplayStyle.Flex;
            action.SetEnabled(false);
            Assert.IsNull(GamepadFocusReturn.RestorableControl(root, action));
            Assert.IsNull(GamepadFocusReturn.RestorableControl(root, new Button()));
        }

        [Test]
        public void MapViewIsNeverUsedAsHudReturnTarget()
        {
            var root = new VisualElement();
            var map = new MapView { focusable = true };
            root.Add(map);
            Assert.IsNull(GamepadFocusReturn.RestorableControl(root, map));
        }

        [Test]
        public void FocusReturnManagerDoesNotActOnDetachedPanels()
        {
            var memory = new GamepadFocusReturn();
            var root = new VisualElement();
            var modal = new VisualElement();
            root.Add(modal);
            Assert.DoesNotThrow(() => memory.Update(root, modal, true));
            Assert.DoesNotThrow(() => memory.Update(root, root, true));
            Assert.DoesNotThrow(() => memory.Update(root, null, false));
            Assert.DoesNotThrow(() => memory.Update(null, modal, true));
        }

        [Test]
        public void EmptyOrDetachedUiDoesNotStealFocus()
        {
            Assert.IsNull(UiGamepadFocus.FirstEligible(null));
            Assert.IsNull(UiGamepadFocus.FirstEligible(new VisualElement()));
            Assert.IsFalse(UiGamepadFocus.EnsureFocus(new VisualElement()));
            Assert.IsFalse(UiGamepadFocus.FocusFirst(new VisualElement()));
        }

        [Test]
        public void MouseHandoffIgnoresSmallJitterButDetectsTrackpadMovement()
        {
            Assert.IsFalse(UiGamepadFocus.HasMouseIntent(Vector2.zero));
            Assert.IsFalse(UiGamepadFocus.HasMouseIntent(new Vector2(1f, 1f)));
            Assert.IsTrue(UiGamepadFocus.HasMouseIntent(new Vector2(3f, 0f)));
            Assert.IsTrue(UiGamepadFocus.HasMouseIntent(new Vector2(-4f, 5f)));
        }

        [Test]
        public void ScrollContainerDoesNotTakeInitialFocusFromItsVisibleAction()
        {
            var root = new VisualElement();
            var scroll = new ScrollView { focusable = true };
            var action = new Button();
            scroll.Add(action);
            root.Add(scroll);
            Assert.AreSame(action, UiGamepadFocus.FirstEligible(root));

            action.SetEnabled(false);
            Assert.AreSame(scroll, UiGamepadFocus.FirstEligible(root));
        }

        [Test]
        public void DuplicateNamedFocusPrefersVisibleRebuiltCard()
        {
            var root = new VisualElement();
            var oldCard = new Button { name = "gamepad-card" };
            var newCard = new Button { name = "gamepad-card" };
            oldCard.style.display = DisplayStyle.None;
            root.Add(oldCard);
            root.Add(newCard);
            Assert.AreSame(newCard, UiGamepadFocus.FindEligibleNamed(root, "gamepad-card"));

            newCard.SetEnabled(false);
            Assert.IsNull(UiGamepadFocus.FindEligibleNamed(root, "gamepad-card"));
        }
    }
}
