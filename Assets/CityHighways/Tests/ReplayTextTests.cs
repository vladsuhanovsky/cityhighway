using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class ReplayTextTests
    {
        [Test]
        public void MetadataAndPlayerDetailsFollowLanguageWithoutTranslatingAuthor()
        {
            string old = LocalizationService.Language;
            try
            {
                var file = new ReplayFile { source = "friend", author = "Анна", authorSeat = 0, authorScore = 12, won = true, online = true, map = "Москва" };
                LocalizationService.SetLanguage("ru");
                StringAssert.Contains("Повтор: Анна", ReplayText.Title(file));
                StringAssert.Contains("12 очк.", ReplayText.Facts(file, "date"));
                StringAssert.Contains("Карт: 2", ReplayText.PlayerDetails(2, 3, 4));
                LocalizationService.SetLanguage("en");
                StringAssert.Contains("Replay: Анна", ReplayText.Title(file));
                StringAssert.Contains("12 pts, victory", ReplayText.Facts(file, "date"));
                StringAssert.Contains("online", ReplayText.Facts(file, "date"));
                StringAssert.Contains("Cards: 2", ReplayText.PlayerDetails(2, 3, 4));
                Assert.AreEqual("Анна", file.author);
            }
            finally { LocalizationService.SetLanguage(old); }
        }

        [Test]
        public void DeferredCommandAndRawJournalFollowLiveLanguage()
        {
            string old = LocalizationService.Language;
            try
            {
                var game = TestCities.Started(TestCities.Small());
                var command = new GameCommand { kind = GameCommand.DrawDeck, player = 0 };
                var render = ReplayText.Describe(game, command);
                string line = game.Players[0].Name + " берёт карту из колоды.";
                LocalizationService.SetLanguage("ru");
                StringAssert.Contains("берёт карту из колоды", render());
                Assert.AreEqual(line, GameView.FormatJournalMessage(game, line));
                LocalizationService.SetLanguage("en");
                StringAssert.Contains("draws a card from the deck", render());
                StringAssert.Contains("draws a card from the deck", GameView.FormatJournalMessage(game, line));
                Assert.AreEqual("Unknown legacy text", GameView.FormatJournalMessage(game, "Unknown legacy text"));
            }
            finally { LocalizationService.SetLanguage(old); }
        }

        [Test]
        public void ErrorPrefixIsLocalizedButTechnicalDetailRemains()
        {
            string old = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("ru");
                StringAssert.Contains("Карта повтора не найдена", ReplayText.Error("map", "technical detail", 0));
                LocalizationService.SetLanguage("en");
                Assert.AreEqual("Replay map not found: technical detail", ReplayText.Error("map", "technical detail", 0));
                StringAssert.Contains("action 5", ReplayText.Error("command", "technical detail", 5));
            }
            finally { LocalizationService.SetLanguage(old); }
        }

        [TestCase(800f, true, 220f)]
        [TestCase(1280f, true, 320f)]
        [TestCase(1399f, true, 349.75f)]
        [TestCase(1400f, false, 350f)]
        [TestCase(1920f, false, 360f)]
        public void ReplayLayoutUsesBoundedSidebarAndCompactTransport(float width, bool compact, float side)
        {
            Assert.AreEqual(compact, ReplayLayout.Compact(width));
            Assert.AreEqual(side, ReplayLayout.SideWidth(width), 0.001f);
        }
    }
}
