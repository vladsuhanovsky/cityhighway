using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class GhostBannerLocalizationTests
    {
        [TestCase(true, "▶ Replay", "Challenge", "Beat your record")]
        [TestCase(false, "▶ Повтор", "Бросить вызов", "Побить свой рекорд")]
        public void ButtonsFollowLanguageAndSource(bool english, string watch, string challenge, string own)
        {
            Assert.AreEqual(watch, GhostBannerText.Watch(english));
            Assert.AreEqual(challenge, GhostBannerText.Raid(false, english));
            Assert.AreEqual(own, GhostBannerText.Raid(true, english));
        }

        [TestCase(true, "Other records: Bob 95. Your record: 80.")]
        [TestCase(false, "Ещё рекорды: Bob 95. Ваш рекорд: 80.")]
        public void RecordsKeepPlayerNamesAndScores(bool english, string expected) =>
            Assert.AreEqual(expected, GhostBannerText.Records(new[] { "Bob 95" }, 80, english));

        [Test]
        public void EmptyRecordListHasLocalizedExplanation()
        {
            StringAssert.StartsWith("Raid", GhostBannerText.Records(new string[0], null, true));
            StringAssert.StartsWith("Рейд", GhostBannerText.Records(new string[0], null, false));
        }

        [TestCase("self", "Your personal best")]
        [TestCase("friend", "Your friend Alice")]
        [TestCase("club", "Club member Alice")]
        [TestCase("local", "Alice (on this computer)")]
        public void ChallengeUsesEnglishSourceAndLocalizedCity(string source, string prefix)
        {
            string old = LocalizationService.Language;
            try
            {
                var ghost = new Ghost { Source = source, Name = "Alice", Score = 95, MapName = "Париж", Mode = "coop" };
                LocalizationService.SetLanguage("en");
                StringAssert.StartsWith(prefix, ghost.Challenge);
                StringAssert.Contains("Paris", ghost.Challenge);
                StringAssert.Contains("in co-op", ghost.Challenge);
                StringAssert.Contains("95", ghost.Challenge);
                LocalizationService.SetLanguage("ru");
                StringAssert.Contains("Париж", ghost.Challenge);
                StringAssert.Contains("в кооперативе", ghost.Challenge);
            }
            finally { LocalizationService.SetLanguage(old); }
        }
    }
}
