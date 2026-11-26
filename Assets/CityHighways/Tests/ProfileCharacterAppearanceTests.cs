using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ProfileCharacterAppearanceTests
    {
        [TestCase("m")]
        [TestCase("f")]
        public void DefaultLooksRetainAllRequiredSlots(string gender)
        {
            var look = ProfileCharacterAppearance.FromLook(Outfit.DefaultFor(gender).Serialize());
            Assert.AreEqual(gender, look.Gender);
            foreach (var slot in CharacterCatalog.Slots.Where(CharacterCatalog.IsRequired))
                Assert.IsNotEmpty(look.Equipped(slot), slot);
        }

        [Test]
        public void HiddenAccessoryRemainsEquippedWithoutBeingVisible()
        {
            var look = ProfileCharacterAppearance.FromLook("body=body_light;top=top_sweater;accessory=acc_tie");
            Assert.AreEqual("acc_tie", look.Equipped("accessory"));
            Assert.AreEqual("", look.Visible("accessory"));
            Assert.AreEqual(look.Serialized, ProfileCharacterAppearance.FromLook(look.Serialized).Serialized);
        }

        [Test]
        public void UntrustedLookIsBoundedAndUsesCatalogIdsOnly()
        {
            Assert.AreEqual(Outfit.Default.Serialize(), ProfileCharacterAppearance.FromLook(new string('x', 4097)).Serialized);
            var look = ProfileCharacterAppearance.FromLook("body=body_f_dark;hat=../../test;top=top_vest;shoes=hat_cap");
            Assert.AreEqual("f", look.Gender);
            Assert.AreEqual("dark", look.Tone);
            Assert.AreEqual("", look.Equipped("hat"));
            Assert.AreEqual(CharacterCatalog.DefaultFor("top", "f"), look.Equipped("top"));
            Assert.AreEqual(CharacterCatalog.DefaultFor("shoes", "f"), look.Equipped("shoes"));
        }

        [Test]
        public void ProfileSnapshotDoesNotSpendOrModifyInventoryAndSeparatesTryOn()
        {
            var item = CharacterCatalog.ItemsIn("hat", "m").First(i => i.price > 0);
            var profile = new PlayerProfile { outfit = Outfit.Default.With("hat", item.id).Serialize(), wallet = 10 };
            string before = profile.outfit;
            Assert.AreEqual(item.id, ProfileCharacterAppearance.FromLook(before).Equipped("hat"));
            Assert.AreEqual("", ProfileCharacterAppearance.FromProfile(profile).Equipped("hat"));
            profile.owned.Add(item.id);
            var snapshot = ProfileCharacterAppearance.FromProfile(profile);
            Assert.AreEqual(item.id, snapshot.Equipped("hat"));
            profile.owned.Clear();
            Assert.AreEqual(item.id, snapshot.Equipped("hat"));
            Assert.AreEqual(before, profile.outfit);
            Assert.AreEqual(10, profile.wallet);
            Assert.IsEmpty(profile.history);
        }

        [TestCase("m")]
        [TestCase("f")]
        public void AuthoredBindingsResolveAndAttachToCorrectSockets(string gender)
        {
            var library = ScriptableObject.CreateInstance<ProfileCharacterLibrary>();
            var root = new GameObject("Rig");
            var piece = new GameObject("Item");
            GameObject assembled = null;
            try
            {
                var rig = MakeRig(root, gender);
                if (gender == "m") library.male = rig; else library.female = rig;
                var look = ProfileCharacterAppearance.FromLook(Outfit.DefaultFor(gender).Serialize());
                library.items = CharacterCatalog.Slots.Where(slot => !string.IsNullOrEmpty(look.Visible(slot)))
                    .Select(slot => new ProfileCharacterLibrary.ItemBinding { itemId = look.Visible(slot),
                        socket = ProfileCharacterLibrary.SocketFor(slot), prefab = piece }).ToArray();
                Assert.IsTrue(library.TryResolve(look, out var found, out var bindings));
                Assert.AreSame(rig, found);
                Assert.AreEqual(library.items.Length, bindings.Length);
                assembled = library.InstantiateLook(look);
                Assert.IsNotNull(assembled);
                var instance = assembled.GetComponent<ProfileCharacterRig>();
                Assert.AreNotSame(rig, instance);
                foreach (var group in bindings.GroupBy(b => b.socket))
                    Assert.AreEqual(group.Count(), instance.Socket(group.Key).childCount);
                foreach (var group in bindings.GroupBy(b => b.socket))
                    Assert.AreEqual(0, rig.Socket(group.Key).childCount);
            }
            finally
            {
                if (assembled != null) Object.DestroyImmediate(assembled);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(piece);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void MissingDuplicateOrIncorrectBindingsFailWithoutPartialAssembly()
        {
            var library = ScriptableObject.CreateInstance<ProfileCharacterLibrary>();
            var root = new GameObject("Rig");
            var piece = new GameObject("Item");
            try
            {
                library.male = MakeRig(root, "m");
                var look = ProfileCharacterAppearance.FromLook("");
                Assert.IsNull(library.InstantiateLook(look));
                library.items = CharacterCatalog.Slots.Where(s => !string.IsNullOrEmpty(look.Visible(s)))
                    .Select(s => new ProfileCharacterLibrary.ItemBinding { itemId = look.Visible(s),
                        socket = ProfileCharacterLibrary.SocketFor(s), prefab = piece }).ToArray();
                var complete = library.items;
                library.items = complete.Concat(new[] { complete[0] }).ToArray();
                Assert.IsFalse(library.TryResolve(look, out _, out _));
                Assert.Contains("duplicate:" + complete[0].itemId, library.Audit());
                library.items = complete;
                complete[0].socket = ProfileCharacterSocket.Hand;
                Assert.IsFalse(library.TryResolve(look, out _, out _));
                Assert.Contains("socket:" + complete[0].itemId, library.Audit());
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(piece);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void RigRejectsMissingDuplicateAndForeignAnchors()
        {
            var root = new GameObject("Rig");
            var foreign = new GameObject("Foreign");
            try
            {
                var rig = MakeRig(root, "m");
                Assert.IsTrue(rig.IsValid());
                var hand = rig.hand;
                rig.hand = rig.head;
                Assert.IsFalse(rig.IsValid());
                rig.hand = foreign.transform;
                Assert.IsFalse(rig.IsValid());
                rig.hand = null;
                Assert.IsFalse(rig.IsValid());
                rig.hand = hand;
                Assert.IsTrue(rig.IsValid());
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(foreign); }
        }

        static ProfileCharacterRig MakeRig(GameObject root, string gender)
        {
            var rig = root.AddComponent<ProfileCharacterRig>();
            rig.gender = gender;
            Transform Anchor(string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(root.transform, false);
                return child.transform;
            }
            rig.body = Anchor("Body"); rig.head = Anchor("Head"); rig.chest = Anchor("Chest");
            rig.hips = Anchor("Hips"); rig.feet = Anchor("Feet"); rig.hand = Anchor("Hand");
            return rig;
        }
    }
}
