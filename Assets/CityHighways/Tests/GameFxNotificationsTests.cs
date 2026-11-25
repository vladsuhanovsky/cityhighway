using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Tests
{
    public class GameFxNotificationsTests
    {
        [Test]
        public void OlderToastCannotHideReplacement()
        {
            var tweener = new Tweener();
            var layer = new VisualElement();
            var notices = new GameFxNotifications(new GameFxLifetime(tweener, layer), layer,
                new VisualElement(), new VisualElement(), () => 1280f, 60f);
            notices.Toast("first", Color.white);
            tweener.Tick(1.5f);
            notices.Toast("second", Color.red);
            tweener.Tick(1f);
            var holder = layer[0];
            Assert.AreEqual(DisplayStyle.Flex, holder.style.display.value);
            Assert.AreEqual("second", ((Label)holder[0]).text);
            tweener.Tick(1.5f);
            Assert.AreEqual(DisplayStyle.None, holder.style.display.value);
        }

        [Test]
        public void ResetInvalidatesNotificationsWithoutRemovingPersistentLayer()
        {
            var tweener = new Tweener();
            var layer = new VisualElement();
            var card = new VisualElement();
            var pill = new VisualElement();
            var notices = new GameFxNotifications(new GameFxLifetime(tweener, layer), layer, card, pill, () => 800f, 60f);
            notices.Toast("old", Color.white);
            notices.GlowEventCard(Color.red);
            notices.Flash("error", null, 0f);
            tweener.Tick(0.5f);
            notices.Reset();
            tweener.Tick(0.5f);
            Assert.IsFalse(notices.HasFlash);
            Assert.AreEqual("", notices.FlashText);
            Assert.AreEqual(1, layer.childCount);
            Assert.AreEqual(DisplayStyle.None, layer[0].style.display.value);
            Assert.AreEqual(Theme.PanelLight, card.style.backgroundColor.value);
            Assert.AreEqual(Theme.PanelLight, pill.style.backgroundColor.value);
        }

        [Test]
        public void FlashExpiryUsesLatestMessageAndExactDeadline()
        {
            var layer = new VisualElement();
            var notices = new GameFxNotifications(new GameFxLifetime(new Tweener(), layer), layer,
                new VisualElement(), new VisualElement(), () => 1280f, 60f);
            notices.Flash("first", null, 1f);
            notices.Flash("second", null, 3f);
            Assert.IsFalse(notices.ExpireFlash(4.6f));
            Assert.IsFalse(notices.ExpireFlash(6.5f));
            Assert.IsTrue(notices.HasFlash);
            Assert.IsTrue(notices.ExpireFlash(6.51f));
            Assert.IsFalse(notices.HasFlash);
            Assert.IsFalse(notices.ExpireFlash(10f));
        }

        [Test]
        public void ActiveFlashRendersUsingCurrentLanguage()
        {
            string original = LocalizationService.Language;
            try
            {
                var layer = new VisualElement();
                var notices = new GameFxNotifications(new GameFxLifetime(new Tweener(), layer), layer,
                    new VisualElement(), new VisualElement(), () => 1280f, 60f);
                LocalizationService.SetLanguage("ru");
                notices.Flash("custom error", "game.ui.092", 0f);
                string russian = notices.FlashText;
                Assert.AreEqual(LocalizationService.Get("game.ui.092") + LocalizationService.TranslateGameError("custom error"), russian);
                LocalizationService.SetLanguage("en");
                string english = notices.FlashText;
                Assert.AreEqual(LocalizationService.Get("game.ui.092") + LocalizationService.TranslateGameError("custom error"), english);
                Assert.AreNotEqual(russian, english);
            }
            finally { LocalizationService.SetLanguage(original); }
        }
    }
}
