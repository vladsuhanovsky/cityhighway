using System;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SteamAchievementSyncTests
    {
        [Test]
        public void OnlyCurrentSteamAccountCanPublishItsOwnAchievements()
        {
            var profile = new PlayerProfile { steamId = "76561198000000001" };
            profile.achievements.Add("first_win");
            CollectionAssert.AreEqual(new[] { "first_win" },
                SteamAchievementSync.EligibleIds(profile, profile.steamId));

            Assert.IsEmpty(SteamAchievementSync.EligibleIds(profile, "76561198000000002"));
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile,
                "76561198000000002"));
        }

        [Test]
        public void LocalOfflineProfileNeverPublishes()
        {
            var profile = new PlayerProfile();
            profile.achievements.Add("first_win");
            Assert.IsEmpty(SteamAchievementSync.EligibleIds(profile, "76561198000000001"));
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile,
                "76561198000000001"));
        }

        [Test]
        public void UnknownIdsCannotReachSteamApi()
        {
            var profile = new PlayerProfile { steamId = "76561198000000001" };
            profile.achievements.Add("first_contract");
            profile.achievements.Add("made_up");
            profile.achievements.Add("first_win");
            profile.achievements.Add("");
            profile.achievements.Add(null);
            CollectionAssert.AreEqual(new[] { "first_contract", "first_win" },
                SteamAchievementSync.EligibleIds(profile, profile.steamId));
        }

        [Test]
        public void DuplicateUnlocksAreSortedAndPublishedOnce()
        {
            var profile = new PlayerProfile { steamId = "account" };
            profile.achievements.Add("mg_win");
            profile.achievements.Add("first_win");
            profile.achievements.Add("mg_win");
            profile.achievements.Add("first_win");
            CollectionAssert.AreEqual(new[] { "first_win", "mg_win" },
                SteamAchievementSync.EligibleIds(profile, "account"));
        }

        [Test]
        public void NullAndIncompleteProfilesFailClosed()
        {
            Assert.IsEmpty(SteamAchievementSync.EligibleIds(null, "steam"));
            Assert.IsEmpty(SteamAchievementSync.EligibleIds(new PlayerProfile(), null));
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(null, "steam"));
            var profile = new PlayerProfile { steamId = "steam", achievements = null };
            Assert.IsEmpty(SteamAchievementSync.EligibleIds(profile, "steam"));
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, "steam"));
            Assert.DoesNotThrow(SteamAchievementSync.Reset);
        }

        [Test]
        public void EveryPublishedIdComesFromCanonicalCatalog()
        {
            var profile = new PlayerProfile { steamId = "steam" };
            profile.achievements.AddRange(Achievements.All.Select(a => a.Id));
            var ids = SteamAchievementSync.EligibleIds(profile, "steam");
            Assert.AreEqual(Achievements.All.Count, ids.Count);
            Assert.IsTrue(ids.All(id =>
                Achievements.All.Any(a => a.Id == id)));
        }

        [Test]
        public void AccountIdentityComparisonIsExact()
        {
            var profile = new PlayerProfile { steamId = "123" };
            profile.achievements.Add("first_win");
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, "123 "));
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, "0123"));
            Assert.IsTrue(SteamAchievementSync.ShouldPublish(profile, "123"));
        }

        [Test]
        public void BlankSteamIdentityIsNotAuthenticated()
        {
            var profile = new PlayerProfile { steamId = " " };
            profile.achievements.Add("first_win");
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, " "));
            Assert.IsEmpty(SteamAchievementSync.EligibleIds(profile, " "));

            profile.steamId = "account";
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, "\t"));
            Assert.IsEmpty(SteamAchievementSync.EligibleIds(profile, "\t"));
        }

        [Test]
        public void AccountIdentityNeverAcceptsTrimmedNearMatches()
        {
            var profile = new PlayerProfile { steamId = "account" };
            profile.achievements.Add("first_win");
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, " account"));
            Assert.IsFalse(SteamAchievementSync.ShouldPublish(profile, "account "));
            CollectionAssert.AreEqual(new[] { "first_win" },
                SteamAchievementSync.EligibleIds(profile, "account"));
        }
    }
}
