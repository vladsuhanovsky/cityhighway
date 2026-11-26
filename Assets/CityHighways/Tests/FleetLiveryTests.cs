using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;

namespace CityHighways.Tests
{
    public sealed class FleetLiveryTests
    {
        [Test]
        public void AllFourVehiclesHaveSafeDefaultsAndUnknownIdsAreRejected()
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Test" };
            CollectionAssert.AreEqual(new[] { "paver", "mixer", "grader", "crane" }, FleetLivery.VehicleIds);
            foreach (var id in FleetLivery.VehicleIds)
            {
                var value = FleetLivery.Read(book, profile, id);
                Assert.AreEqual(id, value.Vehicle);
                Assert.AreEqual("company", value.Scheme);
                Assert.AreEqual(CompanyIdentity.Paint("navy"), value.Primary);
                Assert.AreEqual(CompanyIdentity.Paint("orange"), value.Secondary);
                Assert.AreEqual("", value.Emblem);
            }
            Assert.IsNull(FleetLivery.Read(book, profile, "../../unknown"));
            Assert.IsFalse(FleetLivery.Store(book, profile, null, "company", false));
            Assert.IsEmpty(book.companies);
        }

        [TestCase("company", "green", "white")]
        [TestCase("reverse", "white", "green")]
        [TestCase("solid", "green", "green")]
        [TestCase("unknown", "green", "white")]
        public void PaintSchemesUseOnlyCompanyPalette(string scheme, string primary, string secondary)
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Test" };
            CompanyIdentity.Store(book, profile, CompanyIdentity.Create(profile, "Roadworks", "", "green", "white"));
            var livery = FleetLivery.Preview(book, profile, "paver", scheme, false);
            Assert.AreEqual(CompanyIdentity.Paint(primary), livery.Primary);
            Assert.AreEqual(CompanyIdentity.Paint(secondary), livery.Secondary);
            Assert.AreEqual("Roadworks", livery.CompanyName);
            Assert.AreEqual(scheme == "unknown" ? "company" : scheme, livery.Scheme);
        }

        [Test]
        public void FleetRoundTripIsolatedByVehicleAndProfileAndDoesNotMutatePlayer()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name = "Alpha", wallet = 25 };
            var b = new PlayerProfile { name = "Beta" };
            book.profiles.Add(a); book.profiles.Add(b);
            string before = JsonUtility.ToJson(a);
            Assert.IsTrue(FleetLivery.Store(book, a, "paver", "reverse", true));
            Assert.IsTrue(FleetLivery.Store(book, a, "crane", "solid", false));
            Assert.IsTrue(FleetLivery.Store(book, b, "mixer", "solid", true));
            var loaded = JsonUtility.FromJson<ProfileBook>(JsonUtility.ToJson(book));
            Assert.AreEqual("reverse", FleetLivery.Read(loaded, loaded.profiles[0], "paver").Scheme);
            Assert.IsTrue(FleetLivery.Read(loaded, loaded.profiles[0], "paver").HideEmblem);
            Assert.AreEqual("solid", FleetLivery.Read(loaded, loaded.profiles[0], "crane").Scheme);
            Assert.AreEqual("company", FleetLivery.Read(loaded, loaded.profiles[0], "mixer").Scheme);
            Assert.AreEqual("solid", FleetLivery.Read(loaded, loaded.profiles[1], "mixer").Scheme);
            Assert.AreEqual(before, JsonUtility.ToJson(a));
            Assert.AreEqual(before, JsonUtility.ToJson(loaded.profiles[0]));
        }

        [Test]
        public void CompanyChangesPreserveFleetSchemesAndRevokedEmblemsStayHidden()
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Test" };
            profile.owned.Add("emblem_gear");
            CompanyIdentity.Store(book, profile, CompanyIdentity.Create(profile, "Roads", "emblem_gear", "navy", "orange"));
            FleetLivery.Store(book, profile, "crane", "reverse", false);
            FleetLivery.Store(book, profile, "mixer", "company", true);
            CompanyIdentity.Store(book, profile, CompanyIdentity.Create(profile, "New Roads", "emblem_gear", "green", "white"));
            var value = FleetLivery.Read(book, profile, "crane");
            Assert.AreEqual("reverse", value.Scheme);
            Assert.AreEqual("New Roads", value.CompanyName);
            Assert.AreEqual(CompanyIdentity.Paint("white"), value.Primary);
            Assert.AreEqual("emblem_gear", value.Emblem);
            Assert.AreEqual("", FleetLivery.Read(book, profile, "mixer").Emblem);
            profile.owned.Clear();
            Assert.AreEqual("", FleetLivery.Read(book, profile, "crane").Emblem);
        }

        [Test]
        public void FleetStoreIsBoundedAndDoesNotMutatePreviousRecord()
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Test" };
            FleetLivery.Store(book, profile, "paver", "reverse", false);
            var prior = book.companies[0];
            FleetLivery.Store(book, profile, "paver", "solid", true);
            Assert.AreEqual("reverse", prior.fleet[0].scheme);
            foreach (var id in FleetLivery.VehicleIds)
                FleetLivery.Store(book, profile, id, "reverse", true);
            Assert.AreEqual(4, book.companies[0].fleet.Length);
            foreach (var id in FleetLivery.VehicleIds)
                FleetLivery.Store(book, profile, id, "company", false);
            Assert.IsEmpty(book.companies[0].fleet);
        }

        [Test]
        public void FleetFollowsExistingCompanyMigrationAndRemoval()
        {
            var book = new ProfileBook();
            var profile = new PlayerProfile { name = "Old" };
            FleetLivery.Store(book, profile, "grader", "solid", true);
            string key = CompanyIdentity.OwnerKey(profile);
            profile.name = "New";
            CompanyIdentity.Move(book, key, CompanyIdentity.OwnerKey(profile));
            Assert.AreEqual("solid", FleetLivery.Read(book, profile, "grader").Scheme);
            CompanyIdentity.Remove(book, CompanyIdentity.OwnerKey(profile));
            Assert.AreEqual("company", FleetLivery.Read(book, profile, "grader").Scheme);
        }
    }
}
