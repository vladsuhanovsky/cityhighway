using NUnit.Framework;
using UnityEngine;
using CityHighways.Core;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ConstructionFleetTests
    {
        [TestCase(Resource.Asphalt, "paver")]
        [TestCase(Resource.Concrete, "mixer")]
        [TestCase(Resource.Gravel, "grader")]
        [TestCase(Resource.Steel, "crane")]
        [TestCase(null, "paver")]
        public void ResourceMapsToCosmeticVehicle(Resource? resource, string expected)
        {
            Assert.AreEqual(expected, ConstructionFleetPolicy.VehicleFor(resource));
        }

        [Test]
        public void ActualLocalSeatResolvesItsOwnFleetNotActiveProfile()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name = "Alpha" };
            var b = new PlayerProfile { name = "Beta" };
            book.profiles.Add(a); book.profiles.Add(b); book.active = a.name;
            CompanyIdentity.Store(book, a, CompanyIdentity.Create(a, "Alpha Co", "", "green", "white"));
            CompanyIdentity.Store(book, b, CompanyIdentity.Create(b, "Beta Co", "", "blue", "steel"));
            FleetLivery.Store(book, b, "mixer", "reverse", true);
            var setup = new GameSetup();
            setup.players.Add(new PlayerSlot("Alpha", false));
            setup.players.Add(new PlayerSlot("Beta", false));
            var livery = ConstructionFleetPolicy.Resolve(book, setup, 1, "Beta", false, Resource.Concrete);
            Assert.IsNotNull(livery);
            Assert.AreEqual("Beta Co", livery.CompanyName);
            Assert.AreEqual("mixer", livery.Vehicle);
            Assert.AreEqual(CompanyIdentity.Paint("steel"), livery.Primary);
            Assert.IsTrue(livery.HideEmblem);
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 0, "Beta", false, Resource.Concrete));
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 2, "Beta", false, Resource.Concrete));
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 1, "Beta", true, Resource.Concrete));
            setup.players[1].isBot = true;
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 1, "Beta", false, Resource.Concrete));
            setup.players[1].isBot = false;
            setup.players[0].name = " BETA ";
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 1, "Beta", false, Resource.Concrete));
            setup.players[0].name = "Alpha";
            book.profiles.Add(new PlayerProfile { name = "beta" });
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 1, "Beta", false, Resource.Concrete));
        }

        [Test]
        public void PoolBoundsConcurrentVehiclesAndRepaintsReusedInstance()
        {
            var parent = new GameObject("Construction fleet test");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            var pool = new ConstructionFleetPool(parent.transform, material);
            try
            {
                var book = new ProfileBook();
                var a = new PlayerProfile { name = "Alpha" };
                CompanyIdentity.Store(book, a, CompanyIdentity.Create(a, "Alpha Co", "", "green", "white"));
                var value = FleetLivery.Read(book, a, "paver");
                var first = pool.Acquire(value);
                var second = pool.Acquire(value);
                Assert.IsNotNull(first); Assert.IsNotNull(second);
                Assert.AreNotSame(first, second);
                Assert.AreEqual(ConstructionFleetPool.MaxPerType, pool.ActiveCount);
                Assert.IsNull(pool.Acquire(value));
                pool.Release(first);
                pool.Release(first);
                Assert.IsFalse(first.Root.activeSelf);
                Assert.AreEqual(1, pool.ActiveCount);
                var next = pool.Acquire(FleetLivery.Preview(book, a, "paver", "reverse", false));
                Assert.AreSame(first, next);
                Assert.AreEqual(2, pool.Count);
                var block = new MaterialPropertyBlock();
                next.Root.transform.Find("Engine").GetComponent<Renderer>().GetPropertyBlock(block);
                var color = block.GetColor(Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color"));
                Assert.That(color.r, Is.EqualTo(CompanyIdentity.Paint("white").r).Within(.0001f));
                Assert.That(color.g, Is.EqualTo(CompanyIdentity.Paint("white").g).Within(.0001f));
                Assert.That(color.b, Is.EqualTo(CompanyIdentity.Paint("white").b).Within(.0001f));
                Assert.IsNotNull(pool.Acquire(FleetLivery.Read(book, a, "mixer")));
                Assert.AreEqual(3, pool.ActiveCount);
                pool.Clear();
                Assert.AreEqual(0, pool.Count);
                Assert.AreEqual(0, pool.ActiveCount);
                Assert.AreEqual(0, parent.transform.childCount);
                Assert.IsNotNull(material);
            }
            finally
            {
                pool.Clear();
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void PolicyRejectsTutorialCampaignAndMissingProfile()
        {
            var book = new ProfileBook();
            var setup = new GameSetup();
            setup.players.Add(new PlayerSlot("Alpha", false));
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 0, "Alpha", false, null));
            book.profiles.Add(new PlayerProfile { name = "Alpha" });
            setup.practice = true;
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 0, "Alpha", false, null));
            setup.practice = false; setup.tutorial = true;
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 0, "Alpha", false, null));
            setup.tutorial = false; setup.campaignMission = "test";
            Assert.IsNull(ConstructionFleetPolicy.Resolve(book, setup, 0, "Alpha", false, null));
        }
    }
}
