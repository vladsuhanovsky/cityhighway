using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ProfileAppearanceTests
    {
        [TestCase("m")]
        [TestCase("f")]
        public void AllCatalogItemsRoundTripThroughCosmeticSnapshot(string gender)
        {
            foreach (string slot in CharacterCatalog.Slots)
                foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                {
                    var outfit = Outfit.DefaultFor(gender).With(slot, item.id);
                    var appearance = ProfileAppearance.FromOutfit(outfit);
                    Assert.AreEqual(outfit.Serialize(), appearance.Serialized);
                    Assert.AreEqual(gender, appearance.Gender);
                    Assert.AreEqual(item.id, appearance.ToOutfit().Get(slot));
                    Assert.AreEqual(outfit.HiddenBy(slot) == null ? item.id : "", appearance.VisibleId(slot));
                }
        }

        [Test]
        public void HiddenTieRemainsEquippedButNotVisible()
        {
            var outfit = Outfit.Default.With("accessory", "acc_tie").With("top", "top_sweater");
            var snapshot = ProfileAppearance.FromOutfit(outfit);
            Assert.AreEqual("acc_tie", snapshot.ToOutfit().Get("accessory"));
            Assert.AreEqual("", snapshot.VisibleId("accessory"));
            Assert.AreEqual("acc_tie", ProfileAppearance.FromOutfit(snapshot.ToOutfit().With("top", "top_shirt")).VisibleId("accessory"));
        }

        [TestCase("body=unknown;top=../../secret;hat=unknown")]
        [TestCase("body=body_f_light;hat=hat_hardhat")]
        public void UnknownAndIncompatibleIdsNeverBecomeVisualReferences(string raw)
        {
            var look = new PlayerLook("", "", "", raw);
            var snapshot = ProfileAppearance.FromLook(look);
            foreach (var slot in CharacterCatalog.Slots)
            {
                var id = snapshot.VisibleId(slot);
                if (string.IsNullOrEmpty(id)) continue;
                var item = CharacterCatalog.Item(id);
                Assert.IsNotNull(item);
                Assert.AreEqual(slot, item.slot);
                Assert.IsTrue(item.Fits(snapshot.Gender));
            }
            Assert.AreEqual("", snapshot.VisibleId("hat"));
        }

        [Test]
        public void OversizedAppearanceFallsBackToDefault()
        {
            Assert.AreEqual(Outfit.Default.Serialize(),
                ProfileAppearance.FromSerialized(new string('x', ProfileAppearance.MaxSerializedLength + 1)).Serialized);
        }

        [TestCase("m")]
        [TestCase("f")]
        public void RigAppliesOneVariantPerVisibleSlotWithoutChangingProfile(string gender)
        {
            var owner = new GameObject("Test rig");
            try
            {
                var rig = owner.AddComponent<ProfileAvatarRig>();
                var items = CharacterCatalog.Slots.SelectMany(s => CharacterCatalog.ItemsIn(s, gender)).ToArray();
                var visuals = Configure(rig, gender, items.Select(i => i.id).ToArray());
                Assert.IsTrue(rig.TryValidate(out var error), error);
                Assert.IsEmpty(rig.MissingCatalogIds());
                var outfit = Outfit.DefaultFor(gender);
                var appearance = ProfileAppearance.FromOutfit(outfit);
                Assert.IsTrue(rig.TryApply(appearance, out error), error);
                foreach (var item in items)
                    Assert.AreEqual(appearance.VisibleId(item.slot) == item.id, visuals[item.id].activeSelf, item.id);
                var hat = CharacterCatalog.ItemsIn("hat", gender).First();
                var next = ProfileAppearance.FromOutfit(outfit.With("hat", hat.id));
                Assert.IsTrue(rig.TryApply(next, out error), error);
                Assert.IsTrue(visuals[hat.id].activeSelf);
                Assert.AreEqual("", outfit.Get("hat"));
                Assert.IsTrue(rig.TryApply(appearance, out error), error);
                Assert.IsFalse(visuals[hat.id].activeSelf);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void IncompleteRigDoesNotApplyPartialOutfit()
        {
            var owner = new GameObject("Incomplete rig");
            try
            {
                var rig = owner.AddComponent<ProfileAvatarRig>();
                var visuals = Configure(rig, "m", new[] { "body_light", "hat_hardhat" });
                visuals["body_light"].SetActive(false);
                visuals["hat_hardhat"].SetActive(true);
                Assert.IsFalse(rig.TryApply(ProfileAppearance.FromOutfit(Outfit.Default), out var error));
                StringAssert.Contains("Missing visual binding", error);
                Assert.IsFalse(visuals["body_light"].activeSelf);
                Assert.IsTrue(visuals["hat_hardhat"].activeSelf);
                Assert.Contains("hair_short", rig.MissingCatalogIds());
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase("duplicate")]
        [TestCase("nested")]
        [TestCase("wrong-gender")]
        [TestCase("external")]
        public void RigRejectsInvalidBindings(string kind)
        {
            var owner = new GameObject("Invalid rig");
            var outside = new GameObject("External");
            try
            {
                var rig = owner.AddComponent<ProfileAvatarRig>();
                var ids = kind == "wrong-gender" ? new[] { "body_f_light", "hat_hardhat" } : new[] { "body_light", "hat_hardhat" };
                var visuals = Configure(rig, "m", ids);
                var data = new SerializedObject(rig);
                var bindings = data.FindProperty("bindings");
                var second = bindings.GetArrayElementAtIndex(1);
                if (kind == "duplicate") second.FindPropertyRelative("itemId").stringValue = ids[0];
                if (kind == "external") second.FindPropertyRelative("visual").objectReferenceValue = outside;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (kind == "nested") visuals[ids[1]].transform.SetParent(visuals[ids[0]].transform);
                Assert.IsFalse(rig.TryValidate(out var error));
                Assert.IsNotEmpty(error);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(outside); }
        }

        static Dictionary<string, GameObject> Configure(ProfileAvatarRig rig, string gender, string[] ids)
        {
            var visuals = new Dictionary<string, GameObject>();
            var data = new SerializedObject(rig);
            data.FindProperty("gender").stringValue = gender;
            var entries = data.FindProperty("bindings");
            entries.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                var visual = new GameObject(ids[i]);
                visual.transform.SetParent(rig.transform, false);
                visuals.Add(ids[i], visual);
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("itemId").stringValue = ids[i];
                entry.FindPropertyRelative("visual").objectReferenceValue = visual;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return visuals;
        }

        [TestCase("m")]
        [TestCase("f")]
        public void CompatibilityAdapterUsesCanonicalSnapshotForEveryCatalogItem(string gender)
        {
            foreach (var slot in CharacterCatalog.Slots)
                foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                {
                    var outfit = Outfit.DefaultFor(gender).With(slot, item.id);
                    var canonical = ProfileAppearance.FromOutfit(outfit);
                    var adapter = ProfileCharacterAppearance.FromLook(outfit.Serialize());
                    Assert.AreEqual(canonical.Serialized, adapter.Serialized);
                    Assert.AreEqual(item.id, canonical.EquippedId(slot));
                    foreach (var s in CharacterCatalog.Slots)
                    {
                        Assert.AreEqual(canonical.EquippedId(s), adapter.Equipped(s));
                        Assert.AreEqual(canonical.VisibleId(s), adapter.Visible(s));
                    }
                }
        }

        [Test]
        public void CanonicalProfileSnapshotSeparatesTryOnAndOwnership()
        {
            var paid = CharacterCatalog.ItemsIn("hat", "m").First(i => i.price > 0);
            var profile = new PlayerProfile { outfit = Outfit.Default.With("hat", paid.id).Serialize(), wallet = 25 };
            Assert.AreEqual(paid.id, ProfileAppearance.FromSerialized(profile.outfit).EquippedId("hat"));
            Assert.AreEqual("", ProfileAppearance.FromProfile(profile).EquippedId("hat"));
            profile.owned.Add(paid.id);
            var snapshot = ProfileAppearance.FromProfile(profile);
            Assert.AreEqual(paid.id, snapshot.EquippedId("hat"));
            Assert.AreEqual(snapshot.Serialized, ProfileCharacterAppearance.FromProfile(profile).Serialized);
            profile.owned.Clear();
            Assert.AreEqual(paid.id, snapshot.EquippedId("hat"));
            Assert.AreEqual(25, profile.wallet);
            Assert.IsEmpty(profile.history);
        }

        [Test]
        public void CanonicalSnapshotHandlesNullAndUnknownSlots()
        {
            var snapshot = ProfileAppearance.FromProfile(null);
            Assert.AreEqual(Outfit.Default.Serialize(), snapshot.Serialized);
            Assert.AreEqual("", snapshot.EquippedId(null));
            Assert.AreEqual("", snapshot.VisibleId("unknown"));
            Assert.AreEqual("", snapshot.EquippedId("unknown"));
        }

        [TestCase("m", ProfileAvatarPreviewStage.MaleResource)]
        [TestCase("f", ProfileAvatarPreviewStage.FemaleResource)]
        [TestCase("unknown", null)]
        [TestCase(null, null)]
        public void RuntimePreviewUsesOnlyFixedAuthoredResources(string gender, string expected)
        {
            Assert.AreEqual(expected, ProfileAvatarPreviewStage.ResourceFor(gender));
        }

        [Test]
        public void DetachedPreviewKeepsFallbackAndDoesNotAllocateOrLoad()
        {
            var fallback = new UnityEngine.UIElements.VisualElement();
            int loads = 0;
            var stage = new ProfileAvatarPreviewStage(fallback, _ => { loads++; return null; });
            stage.Show(ProfileAppearance.FromOutfit(Outfit.Default));
            Assert.AreSame(stage, fallback.parent);
            Assert.IsFalse(stage.IsShowing3D);
            stage.SetDebugFallback(true);
            stage.SetDebugFallback(false);
            stage.Release();
            stage.Release();
            Assert.AreEqual(0, loads);
            Assert.IsFalse(stage.IsShowing3D);
            Assert.AreEqual(1f, fallback.style.opacity.value);
        }

        [TestCase("m")]
        [TestCase("f")]
        public void ProceduralPrototypeCoversAllCatalogItemsWithVisibleGeometry(string gender)
        {
            var root = ProfileAvatarPrototype.Build(gender, _ => null);
            try
            {
                var rig = root.GetComponent<ProfileAvatarRig>();
                Assert.IsTrue(rig.TryValidate(out var error), error);
                Assert.IsEmpty(rig.MissingCatalogIds());
                Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
                foreach (var slot in CharacterCatalog.Slots)
                    foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                    {
                        var variant = root.transform.Find(item.id);
                        Assert.IsNotNull(variant, item.id);
                        Assert.IsNotEmpty(variant.GetComponentsInChildren<MeshRenderer>(true), item.id);
                        var snapshot = ProfileAppearance.FromOutfit(Outfit.DefaultFor(gender).With(slot, item.id));
                        Assert.IsTrue(rig.TryApply(snapshot, out error), error);
                        foreach (var s in CharacterCatalog.Slots)
                            foreach (var other in CharacterCatalog.ItemsIn(s, gender))
                                Assert.AreEqual(snapshot.VisibleId(s) == other.id,
                                    root.transform.Find(other.id).gameObject.activeSelf, other.id);
                    }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ProceduralPrototypeRejectsUnsupportedGenderAndMissingMaterialResolver()
        {
            Assert.Throws<ArgumentException>(() => ProfileAvatarPrototype.Build("unknown", _ => null));
            Assert.Throws<ArgumentNullException>(() => ProfileAvatarPrototype.Build("m", null));
        }

        [Test]
        public void ItemCropClampsAndFallsBackForInvalidBounds()
        {
            var item = new CharacterItem { bounds = new[] { -16, 76, 272, 384 } };
            var rect = WardrobeItemPreview.Crop(item);
            Assert.AreEqual(0f, rect.xMin);
            Assert.AreEqual(0.5f, rect.xMax, 0.0001f);
            Assert.AreEqual(0.5f, rect.height, 0.0001f);
            item.bounds = new[] { 700, 0, 20, 20 };
            Assert.AreEqual(new Rect(0, 0, 1, 1), WardrobeItemPreview.Crop(item));
            Assert.AreEqual(new Rect(0, 0, 1, 1), WardrobeItemPreview.Crop(null));
        }
    }
}
