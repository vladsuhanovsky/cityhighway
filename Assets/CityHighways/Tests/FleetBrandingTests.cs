using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class FleetBrandingTests
    {
        static Material NewMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader);
            return new Material(shader);
        }

        static PlayerProfile Owner(ProfileBook book, string name, string emblem)
        {
            var owner = new PlayerProfile { name = name };
            book.profiles.Add(owner);
            owner.owned.Add(emblem);
            CompanyIdentity.Store(book, owner, CompanyIdentity.Create(owner, name, emblem, "navy", "orange"));
            return owner;
        }

        static Renderer Badge(FleetVehicleVisual model, string side) =>
            model.Root.transform.Find("Company badge " + side).GetComponent<Renderer>();

        static Texture TextureOf(Renderer renderer, Material material)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetTexture(Shader.PropertyToID(material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex"));
        }

        [Test]
        public void CacheCoversExistingEmblemsAndRejectsUnknownIds()
        {
            var cache = new FleetEmblemCache();
            try
            {
                foreach (var item in Profiles.Catalog)
                {
                    if (item.Kind != ShopKind.Emblem) continue;
                    Assert.IsTrue(FleetEmblemCache.Supports(item.Id), item.Id);
                    var texture = cache.Get(item.Id);
                    Assert.IsNotNull(texture, item.Id);
                    Assert.AreEqual(FleetEmblemCache.TextureSize, texture.width);
                    Assert.AreEqual(FleetEmblemCache.TextureSize, texture.height);
                    Assert.AreSame(texture, cache.Get(item.Id));
                }
                Assert.AreEqual(8, cache.Count);
                Assert.IsNull(cache.Get(null));
                Assert.IsNull(cache.Get("../emblem_star"));
                Assert.AreEqual(8, cache.Count);
                var old = cache.Get("emblem_gear");
                cache.Clear();
                Assert.AreEqual(0, cache.Count);
                Assert.IsTrue(old == null);
                Assert.IsNotNull(cache.Get("emblem_gear"));
                Assert.AreEqual(1, cache.Count);
            }
            finally { cache.Clear(); }
        }

        [TestCase("paver")]
        [TestCase("mixer")]
        [TestCase("grader")]
        [TestCase("crane")]
        public void BothDoorsUseOneOwnedEmblemAndHideOrRevocationRemovesIt(string vehicle)
        {
            var host = new GameObject("Fleet badge test") { layer = 12 };
            var material = NewMaterial();
            var cache = new FleetEmblemCache();
            try
            {
                var book = new ProfileBook();
                var owner = Owner(book, "Alpha", "emblem_gear");
                var model = FleetVehicleVisual.Build(FleetLivery.Read(book, owner, vehicle), material, host.transform, cache);
                Assert.IsNotNull(model);
                var left = Badge(model, "left");
                var right = Badge(model, "right");
                Assert.AreEqual("emblem_gear", model.AppliedEmblem);
                Assert.IsTrue(left.enabled);
                Assert.IsTrue(right.enabled);
                Assert.AreSame(material, left.sharedMaterial);
                Assert.AreSame(material, right.sharedMaterial);
                Assert.AreEqual(12, left.gameObject.layer);
                Assert.AreSame(cache.Get("emblem_gear"), TextureOf(left, material));
                Assert.AreSame(TextureOf(left, material), TextureOf(right, material));
                Assert.That(Vector3.Dot(-left.transform.forward, Vector3.left), Is.GreaterThan(.99f));
                Assert.That(Vector3.Dot(-right.transform.forward, Vector3.right), Is.GreaterThan(.99f));
                Assert.IsEmpty(model.Root.GetComponentsInChildren<Collider>(true));
                Assert.IsTrue(model.Apply(FleetLivery.Preview(book, owner, vehicle, "reverse", true)));
                Assert.IsFalse(left.enabled);
                Assert.IsFalse(right.enabled);
                Assert.AreEqual("", model.AppliedEmblem);
                Assert.IsNull(TextureOf(left, material));
                Assert.IsTrue(model.Apply(FleetLivery.Read(book, owner, vehicle)));
                Assert.IsTrue(left.enabled);
                owner.owned.Clear();
                Assert.IsTrue(model.Apply(FleetLivery.Read(book, owner, vehicle)));
                Assert.IsFalse(left.enabled);
                Assert.IsFalse(right.enabled);
                Assert.AreEqual("", model.AppliedEmblem);
                Assert.AreEqual(1, cache.Count);
            }
            finally
            {
                Object.DestroyImmediate(host);
                cache.Clear();
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ReusedConstructionVehicleCannotRetainPreviousOwnersEmblem()
        {
            var host = new GameObject("Fleet badge pool test");
            var material = NewMaterial();
            var pool = new ConstructionFleetPool(host.transform, material);
            try
            {
                var book = new ProfileBook();
                var alpha = Owner(book, "Alpha", "emblem_star");
                var beta = Owner(book, "Beta", "emblem_crown");
                var first = pool.Acquire(FleetLivery.Read(book, alpha, "mixer"));
                var second = pool.Acquire(FleetLivery.Read(book, beta, "mixer"));
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                var firstTexture = TextureOf(Badge(first, "left"), material);
                var secondTexture = TextureOf(Badge(second, "left"), material);
                Assert.IsNotNull(firstTexture);
                Assert.IsNotNull(secondTexture);
                Assert.AreNotSame(firstTexture, secondTexture);
                pool.Release(first);
                Assert.IsFalse(Badge(first, "left").enabled);
                Assert.AreEqual("", first.AppliedEmblem);
                var reused = pool.Acquire(FleetLivery.Read(null, null, "mixer"));
                Assert.AreSame(first, reused);
                Assert.IsFalse(Badge(reused, "left").enabled);
                Assert.AreEqual("emblem_crown", second.AppliedEmblem);
                Assert.AreSame(secondTexture, TextureOf(Badge(second, "left"), material));
                pool.Release(reused);
                reused = pool.Acquire(FleetLivery.Read(book, beta, "mixer"));
                Assert.AreSame(first, reused);
                Assert.AreEqual("emblem_crown", reused.AppliedEmblem);
                Assert.AreSame(secondTexture, TextureOf(Badge(reused, "right"), material));
                pool.Clear();
                Assert.IsTrue(firstTexture == null);
                Assert.IsTrue(secondTexture == null);
                Assert.AreEqual(0, host.transform.childCount);
                Assert.IsNotNull(material);
            }
            finally
            {
                pool.Clear();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
            }
        }
    }
}
