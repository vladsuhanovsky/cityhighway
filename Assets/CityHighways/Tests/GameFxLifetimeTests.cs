using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public class GameFxLifetimeTests
    {
        [Test]
        public void ResetRemovesOnlyTemporaryVisualsAndOwnedTweens()
        {
            var tweener = new Tweener();
            var layer = new VisualElement();
            var persistent = new Label("HUD");
            layer.Add(persistent);
            var effects = new GameFxLifetime(tweener, layer);
            var card = new VisualElement();
            effects.Attach(card);
            int stale = 0, unrelated = 0;
            effects.Add(2f, _ => stale++, () => stale++);
            effects.After(1f, () => stale++);
            tweener.After(1f, () => unrelated++, true);
            Assert.AreEqual(2, tweener.BlockingCount);
            effects.Reset();
            Assert.AreEqual(1, tweener.BlockingCount);
            Assert.IsNull(card.parent);
            Assert.AreSame(layer, persistent.parent);
            tweener.Tick(3f);
            Assert.AreEqual(0, stale);
            Assert.AreEqual(1, unrelated);
            Assert.AreEqual(0, tweener.BlockingCount);
        }

        [Test]
        public void NormalCompletionRemovesVisualAndScopeIsReusableAfterReset()
        {
            var tweener = new Tweener();
            var layer = new VisualElement();
            var effects = new GameFxLifetime(tweener, layer);
            var first = new VisualElement();
            effects.Attach(first);
            effects.Add(1f, null, () => effects.Remove(first));
            tweener.Tick(1f);
            Assert.IsNull(first.parent);
            effects.Reset();
            var next = new VisualElement();
            effects.Attach(next);
            effects.Add(1f, null, () => effects.Remove(next));
            tweener.Tick(1f);
            Assert.IsNull(next.parent);
            Assert.AreEqual(0, layer.childCount);
        }

        [Test]
        public void ResetDuringUpdateSkipsCompletionAndOtherStaleCallbacks()
        {
            var tweener = new Tweener();
            var effects = new GameFxLifetime(tweener, new VisualElement());
            int stale = 0, next = 0;
            effects.Add(0f, _ =>
            {
                effects.Reset();
                effects.After(0f, () => next++);
            }, () => stale++);
            effects.After(0f, () => stale++);
            tweener.Tick(1f);
            Assert.AreEqual(0, stale);
            Assert.AreEqual(0, next);
            tweener.Tick(1f);
            Assert.AreEqual(1, next);
        }

        [Test]
        public void GlobalClearDuringTickCancelsSnapshotButKeepsNewlyQueuedWork()
        {
            var tweener = new Tweener();
            int stale = 0, next = 0;
            tweener.Add(0f, _ =>
            {
                tweener.Clear();
                tweener.After(0f, () => next++);
            }, () => stale++);
            tweener.After(0f, () => stale++);
            tweener.Tick(1f);
            Assert.AreEqual(0, stale);
            Assert.AreEqual(0, next);
            tweener.Tick(1f);
            Assert.AreEqual(1, next);
        }

        [Test]
        public void SeparateScopesDoNotCancelEachOther()
        {
            var tweener = new Tweener();
            var a = new GameFxLifetime(tweener, new VisualElement());
            var b = new GameFxLifetime(tweener, new VisualElement());
            int called = 0;
            a.After(0f, () => Assert.Fail("cancelled"));
            b.After(0f, () => called++);
            a.Reset();
            tweener.Tick(1f);
            Assert.AreEqual(1, called);
        }
    }
}
