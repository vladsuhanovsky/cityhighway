using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;

namespace CityHighways.Tests
{
    public sealed class CompanyIdentityTests
    {
        [Test]
        public void LegacyBookDefaultsAndUnknownCosmeticsAreSafe()
        {
            var book = JsonUtility.FromJson<ProfileBook>("{\"profiles\":[],\"active\":\"\"}");
            var profile = new PlayerProfile { name = "Test" };
            var defaults = CompanyIdentity.Read(book, profile);
            Assert.AreEqual("", defaults.Name);
            Assert.AreEqual("", defaults.Emblem);
            Assert.AreEqual("navy", defaults.Primary);
            Assert.AreEqual("orange", defaults.Secondary);
            var invalid = CompanyIdentity.Create(profile, "  A\nB  ", "title_foreman", "../../x", "");
            Assert.AreEqual("AB", invalid.Name);
            Assert.AreEqual("", invalid.Emblem);
            Assert.AreEqual("navy", invalid.Primary);
            Assert.AreEqual("orange", invalid.Secondary);
        }

        [Test]
        public void NameIsBoundedWithoutSplittingSurrogatePairs()
        {
            Assert.AreEqual(new string('x', 31), CompanyIdentity.NormalizeName(new string('x', 31) + "\U0001F680"));
            Assert.AreEqual(new string('x', 30) + "\U0001F680", CompanyIdentity.NormalizeName(new string('x', 30) + "\U0001F680z"));
            Assert.AreEqual("AB", CompanyIdentity.NormalizeName("A\u202EB"));
        }

        [Test]
        public void OnlyOwnedEmblemsAreAvailableAndRevocationIsRevalidated()
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Test", wallet = 25 };
            Assert.AreEqual("", CompanyIdentity.Create(profile, "Roads", "emblem_gear", "red", "steel").Emblem);
            profile.owned.Add("emblem_gear");
            var value = CompanyIdentity.Create(profile, "Roads", "emblem_gear", "red", "steel");
            CompanyIdentity.Store(book, profile, value);
            Assert.AreEqual("emblem_gear", CompanyIdentity.Read(book, profile).Emblem);
            profile.owned.Clear();
            Assert.AreEqual("", CompanyIdentity.Read(book, profile).Emblem);
            Assert.AreEqual(25, profile.wallet);
            Assert.AreEqual("", profile.emblem);
        }

        [Test]
        public void LocalBookRoundTripKeepsProfilesSeparateAndCloudPayloadUnchanged()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name = "Alpha" };
            var b = new PlayerProfile { name = "Beta" };
            book.profiles.Add(a); book.profiles.Add(b);
            string before = JsonUtility.ToJson(a);
            CompanyIdentity.Store(book, a, CompanyIdentity.Create(a, "Alpha Roads", "", "green", "white"));
            CompanyIdentity.Store(book, b, CompanyIdentity.Create(b, "Beta Roads", "", "blue", "steel"));
            var loaded = JsonUtility.FromJson<ProfileBook>(JsonUtility.ToJson(book));
            Assert.AreEqual("Alpha Roads", CompanyIdentity.Read(loaded, loaded.profiles[0]).Name);
            Assert.AreEqual("Beta Roads", CompanyIdentity.Read(loaded, loaded.profiles[1]).Name);
            Assert.AreEqual(before, JsonUtility.ToJson(a));
            Assert.AreEqual(before, JsonUtility.ToJson(loaded.profiles[0]));
        }

        [Test]
        public void RenameAndLocalSteamAdoptionPreserveCompanyAndDeletionClearsIt()
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Old" };
            CompanyIdentity.Store(book, profile, CompanyIdentity.Create(profile, "Road Works", "", "green", "white"));
            string oldKey = CompanyIdentity.OwnerKey(profile);
            profile.name = "New";
            CompanyIdentity.Move(book, oldKey, CompanyIdentity.OwnerKey(profile));
            Assert.AreEqual("Road Works", CompanyIdentity.Read(book, profile).Name);
            oldKey = CompanyIdentity.OwnerKey(profile);
            profile.steamId = "123";
            CompanyIdentity.Move(book, oldKey, CompanyIdentity.OwnerKey(profile));
            profile.name = "Different Steam persona";
            Assert.AreEqual("Road Works", CompanyIdentity.Read(book, profile).Name);
            CompanyIdentity.Remove(book, CompanyIdentity.OwnerKey(profile));
            Assert.AreEqual("", CompanyIdentity.Read(book, profile).Name);
        }

        [Test]
        public void ExistingDestinationIsNotOverwrittenDuringMigration()
        {
            var book = new ProfileBook();
            var local = new PlayerProfile { name = "Local" };
            var steam = new PlayerProfile { name = "Steam", steamId = "123" };
            CompanyIdentity.Store(book, local, CompanyIdentity.Create(local, "Local Co", "", "green", "white"));
            CompanyIdentity.Store(book, steam, CompanyIdentity.Create(steam, "Steam Co", "", "blue", "steel"));
            CompanyIdentity.Move(book, CompanyIdentity.OwnerKey(local), CompanyIdentity.OwnerKey(steam));
            Assert.AreEqual("Steam Co", CompanyIdentity.Read(book, steam).Name);
            Assert.AreEqual("", CompanyIdentity.Read(book, local).Name);
        }
    }
}
