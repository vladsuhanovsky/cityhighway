using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public class GameFxVisualsTests
    {
        bool _oldReducedMotion;

        [SetUp]
        public void SaveMotionSetting()
        {
            _oldReducedMotion = Ui.ReducedMotion;
            Ui.ReducedMotion = false;
        }

        [TearDown]
        public void RestoreMotionSetting() => Ui.ReducedMotion = _oldReducedMotion;

        [Test]
        public void ReducedMotionFloatingTextRemainsStationaryAndStillExpires()
        {
            Ui.ReducedMotion = true;
            var tween = new Tweener();
            var layer = new VisualElement();
            var visuals = new GameFxVisuals(new GameFxLifetime(tween, layer));
            visuals.FloatAt(new Vector2(100f, 80f), "+5", Color.white);
            var text = (Label)layer[0];
            Assert.AreEqual(68f, text.style.top.value.value, 0.001f);
            tween.Tick(0.6f);
            Assert.AreEqual(68f, text.style.top.value.value, 0.001f);
            Assert.AreEqual(1f, text.style.opacity.value, 0.001f);
            Assert.AreEqual(0, tween.BlockingCount);
            tween.Tick(1f);
            Assert.AreEqual(0, layer.childCount);
        }

        [Test]
        public void FloatingTextCompletesAndRemovesOnlyItsOwnElement()
        {
            var tween = new Tweener();
            var layer = new VisualElement();
            var persistent = new Label("HUD");
            layer.Add(persistent);
            var visuals = new GameFxVisuals(new GameFxLifetime(tween, layer));
            visuals.FloatAt(new Vector2(100f, 80f), "+5", Color.white);
            Assert.AreEqual(2, layer.childCount);
            var text = (Label)layer[1];
            Assert.AreEqual("+5", text.text);
            Assert.AreEqual(PickingMode.Ignore, text.pickingMode);
            Assert.AreEqual(0, tween.BlockingCount);
            tween.Tick(0.6f);
            Assert.Less(text.style.top.value.value, 68f);
            tween.Tick(1f);
            Assert.AreEqual(1, layer.childCount);
            Assert.AreSame(layer, persistent.parent);
        }

        [Test]
        public void CoinCompletionAndSoundOccurExactlyOnce()
        {
            var tween = new Tweener();
            var layer = new VisualElement();
            var visuals = new GameFxVisuals(new GameFxLifetime(tween, layer));
            int sounds = 0, done = 0;
            visuals.FlyCoin(Vector2.zero, new Vector2(100f, 80f), 7, 0.5f, () => sounds++, () => done++);
            Assert.AreEqual(1, layer.childCount);
            Assert.AreEqual("+7", ((Label)layer[0][0]).text);
            Assert.AreEqual(0, tween.BlockingCount);
            tween.Tick(0.25f);
            Assert.AreEqual(0, sounds);
            tween.Tick(0.5f);
            Assert.AreEqual(1, sounds);
            Assert.AreEqual(0, done);
            tween.Tick(1f);
            tween.Tick(1f);
            Assert.AreEqual(1, sounds);
            Assert.AreEqual(1, done);
            Assert.AreEqual(0, layer.childCount);
        }

        [Test]
        public void ResetCancelsCoinAndFloatingTextWithoutPlayingStaleCallbacks()
        {
            var tween = new Tweener();
            var layer = new VisualElement();
            var lifetime = new GameFxLifetime(tween, layer);
            var visuals = new GameFxVisuals(lifetime);
            int callbacks = 0;
            visuals.FlyCoin(Vector2.zero, Vector2.one, 2, 1f, () => callbacks++, () => callbacks++);
            visuals.FloatAt(Vector2.zero, "-2", Color.red);
            Assert.AreEqual(2, layer.childCount);
            lifetime.Reset();
            tween.Tick(3f);
            Assert.AreEqual(0, layer.childCount);
            Assert.AreEqual(0, callbacks);
        }
    }
}
