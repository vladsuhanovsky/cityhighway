using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class CityProfileWalkerTests
    {
        [TestCase("m")]
        [TestCase("f")]
        public void ProceduralAvatarFitsCityScaleAndPreservesSource(string gender)
        {
            var parent = new GameObject("City pedestrian test") { layer = 12 };
            var prefab = ProfileAvatarPrototype.Build(gender, _ => null);
            try
            {
                var rig = prefab.GetComponent<ProfileAvatarRig>();
                var outfit = Outfit.DefaultFor(gender);
                var appearance = ProfileAppearance.FromOutfit(outfit);
                var walker = CityProfileWalker.TryCreate(appearance, parent.transform, path =>
                {
                    Assert.AreEqual(ProfileAvatarPreviewStage.ResourceFor(gender), path);
                    return rig;
                });
                Assert.IsNotNull(walker);
                Assert.IsFalse(walker.Root.activeSelf);
                var position = new Vector3(1f, .2f, 3f);
                walker.SetPose(position, Quaternion.identity, .25f);
                Assert.IsTrue(walker.Root.activeSelf);
                Assert.AreEqual(position, walker.Root.transform.position);
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in walker.Root.GetComponentsInChildren<Renderer>())
                {
                    Assert.AreEqual(12, renderer.gameObject.layer);
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Assert.IsTrue(hasBounds);
                Assert.That(bounds.size.y, Is.InRange(.010f, .018f));
                Assert.That(bounds.min.y, Is.InRange(position.y - .003f, position.y + .003f));
                Assert.AreEqual(Vector3.one, prefab.transform.localScale);
                Assert.AreEqual(Vector3.zero, prefab.transform.position);
                Assert.IsTrue(prefab.activeSelf);
                walker.Hide();
                Assert.IsFalse(walker.Root.activeSelf);
                walker.SetPose(position, Quaternion.Euler(0,90,0), .5f);
                Assert.IsTrue(walker.Root.activeSelf);
                walker.Release();
                Assert.AreEqual(0, parent.transform.childCount);
                Assert.IsTrue(prefab != null);
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void MissingOrUnsafePrefabKeepsOrdinaryPedestrianFallback()
        {
            var parent = new GameObject("City pedestrian fallback test");
            var prefab = ProfileAvatarPrototype.Build("m", _ => null);
            try
            {
                var appearance = ProfileAppearance.FromOutfit(Outfit.DefaultFor("m"));
                var rig = prefab.GetComponent<ProfileAvatarRig>();
                Assert.IsNull(CityProfileWalker.TryCreate(appearance, parent.transform, _ => null));
                Assert.IsNull(CityProfileWalker.TryCreate(null, parent.transform, _ => rig));
                Assert.IsNull(CityProfileWalker.TryCreate(ProfileAppearance.FromOutfit(Outfit.DefaultFor("f")), parent.transform, _ => rig));
                var camera = prefab.AddComponent<Camera>();
                camera.enabled = false;
                Assert.IsNull(CityProfileWalker.TryCreate(appearance, parent.transform, _ => rig));
                Object.DestroyImmediate(camera);
                var collider = prefab.AddComponent<BoxCollider>();
                Assert.IsNull(CityProfileWalker.TryCreate(appearance, parent.transform, _ => rig));
                Object.DestroyImmediate(collider);
                Assert.AreEqual(0, parent.transform.childCount);
                Assert.IsTrue(prefab.activeSelf);
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void WalkerUsesCommittedOwnedAppearanceOfVerifiedSeatOnly()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name = "Alpha", outfit = Outfit.DefaultFor("m").Serialize() };
            var b = new PlayerProfile { name = "Beta", outfit = Outfit.DefaultFor("f").Serialize() };
            book.profiles.Add(a); book.profiles.Add(b); book.active = a.name;
            var setup = new GameSetup();
            setup.players.Add(new PlayerSlot("Alpha", false));
            setup.players.Add(new PlayerSlot("Beta", false));
            var appearance = ConstructionFleetPolicy.ResolveWalker(book, setup, 1, "Beta", false, false);
            Assert.IsNotNull(appearance);
            Assert.AreEqual(ProfileAppearance.FromProfile(b).Serialized, appearance.Serialized);
            Assert.AreEqual("f", appearance.Gender);
            Assert.IsNull(ConstructionFleetPolicy.ResolveWalker(book, setup, 1, "Beta", true, false));
            Assert.IsNull(ConstructionFleetPolicy.ResolveWalker(book, setup, 1, "Beta", false, true));
            Assert.IsNull(ConstructionFleetPolicy.ResolveWalker(book, setup, 0, "Beta", false, false));
            setup.magnateTutorialMap = true;
            Assert.IsNull(ConstructionFleetPolicy.ResolveWalker(book, setup, 1, "Beta", false, false));
        }
    }
}
