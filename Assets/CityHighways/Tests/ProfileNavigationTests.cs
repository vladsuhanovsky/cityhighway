using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ProfileNavigationTests
    {
        [TestCase(ProfileSection.Overview, ProfileSection.Overview)]
        [TestCase(ProfileSection.Wardrobe, ProfileSection.Wardrobe)]
        [TestCase(ProfileSection.Collection, ProfileSection.Collection)]
        [TestCase(ProfileSection.Achievements, ProfileSection.Achievements)]
        [TestCase(ProfileSection.Mastery, ProfileSection.Mastery)]
        [TestCase(ProfileSection.Wallet, ProfileSection.Overview)]
        [TestCase(ProfileSection.Style, ProfileSection.Overview)]
        public void DetailPagesKeepTheirPrimarySectionSelected(ProfileSection section, ProfileSection primary)
        {
            Assert.AreEqual(primary, ProfileNavigation.Primary(section));
        }
    }
}
