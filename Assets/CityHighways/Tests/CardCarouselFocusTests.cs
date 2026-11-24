using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public sealed class CardCarouselFocusTests
    {
        [Test]
        public void DirectAndNestedCardFocusResolveToSameSlot()
        {
            var strip = new VisualElement();
            var first = new VisualElement();
            var second = new VisualElement();
            var nested = new Button();
            second.Add(nested);
            strip.Add(first);
            strip.Add(second);
            Assert.AreEqual(0, CardCarousel.FocusedCardIndex(strip, first));
            Assert.AreEqual(1, CardCarousel.FocusedCardIndex(strip, second));
            Assert.AreEqual(1, CardCarousel.FocusedCardIndex(strip, nested));
        }

        [Test]
        public void UnrelatedOrMissingFocusDoesNotMoveCarousel()
        {
            var strip = new VisualElement();
            var unrelated = new Button();
            strip.Add(new VisualElement());
            Assert.AreEqual(-1, CardCarousel.FocusedCardIndex(strip, unrelated));
            Assert.AreEqual(-1, CardCarousel.FocusedCardIndex(strip, null));
            Assert.AreEqual(-1, CardCarousel.FocusedCardIndex(null, unrelated));
            Assert.AreEqual(-1, CardCarousel.FocusedCardIndex(strip, strip));
        }

        [Test]
        public void CardIndexTracksReordering()
        {
            var strip = new VisualElement();
            var one = new VisualElement();
            var two = new VisualElement();
            strip.Add(one);
            strip.Add(two);
            Assert.AreEqual(1, CardCarousel.FocusedCardIndex(strip, two));
            two.RemoveFromHierarchy();
            strip.Insert(0, two);
            Assert.AreEqual(0, CardCarousel.FocusedCardIndex(strip, two));
        }
    }
}
