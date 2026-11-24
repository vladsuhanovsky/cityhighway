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
                scroll, card));
            Assert.IsTrue(GamepadScrollFocus.ContainsContentTarget(
                scroll, row));
        }

        [Test]
        public void UnrelatedAndDetachedControlsAreNeverAutoScrolled()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            var outside = new Button();
            var child = new Button();
            scroll.Add(child);
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, outside));
            child.RemoveFromHierarchy();
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, child));
        }

        [Test]
        public void ContentContainerItselfIsNotTreatedAsCard()
        {
            var scroll = new ScrollView();
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, scroll.contentContainer));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(null, scroll));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, null));
        }

        [Test]
        public void ScrollbarControlsAndScrollRootAreNotContentTargets()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(scroll, scroll));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, scroll.contentContainer));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, scroll.verticalScroller));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, scroll.verticalScroller.slider));
            Assert.IsFalse(GamepadScrollFocus.ContainsContentTarget(
                scroll, scroll.horizontalScroller));
        }

        [Test]
        public void ExplicitContentContainerChildCanBeRevealed()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            var card = new Button();
            scroll.contentContainer.Add(card);
            Assert.IsTrue(GamepadScrollFocus.ContainsContentTarget(scroll, card));
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
