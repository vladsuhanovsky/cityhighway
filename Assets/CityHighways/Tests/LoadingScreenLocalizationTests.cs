using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class LoadingScreenLocalizationTests
    {
        [TestCase("Соревнование", true, "Competitive")]
        [TestCase("Рейд на рекорд", true, "Record raid")]
        [TestCase("Рейд на рекорд", false, "Рейд на рекорд")]
        [TestCase("Custom title", true, "Custom title")]
        public void CompetitionTitlePreservesLanguageAndCustomTitle(string title, bool english, string expected)
        {
            Assert.AreEqual(expected, LoadingScreen.CompetitionTitle(title, english));
        }
    }
}
