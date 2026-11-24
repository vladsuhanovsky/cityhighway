using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class GamepadScrollFocusTests
    {
        [Test]
        public void NestedControlWithinScrollContentCanBeRevealed()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            var row = new VisualElement();
            var card = new Button();
            scroll.Add(row);
            row.Add(card);
            Assert.IsTrue(GamepadScrollFocus.ContainsContentTarget(
                scroll.contentContainer, card));
            Assert.IsTrue(GamepadScrollFocus.ContainsContentTarget(
                scroll.contentContainer, row));
        }

        [Test]
        public void UnrelatedAndDetachedControlsAreNeverAutoScrolled()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            var outside = new Button();
            var child = new Button();
            scroll.Add(child);
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll.contentContainer, outside));
            child.RemoveFromHierarchy();
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll.contentContainer, child));
        }

        [Test]
        public void ContentContainerItselfIsNotTreatedAsCard()
        {
            var scroll = new ScrollView();
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll.contentContainer, scroll.contentContainer));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(null, scroll));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll.contentContainer, null));
        }

        [Test]
        public void HorizontalAndVerticalScrollViewsAcceptFocusListeners()
        {
            var horizontal = new ScrollView(ScrollViewMode.Horizontal);
            var vertical = new ScrollView(ScrollViewMode.Vertical);
            Assert.DoesNotThrow(() => GamepadScrollFocus.Register(horizontal));
            Assert.DoesNotThrow(() => GamepadScrollFocus.Register(vertical));
            Assert.DoesNotThrow(() => GamepadScrollFocus.Register(null));
        }
    }
}
