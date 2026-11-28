#if UNITY_EDITOR
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ProfileHairTrialTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void HairChangesOnlySelectedVisualAndKeepsSharedBones(bool female)
        {
            var root = new GameObject("Local trial");
            try
            {
                string prefix = female ? "F01" : "M02";
                var ids = ProfileHairTrial.ItemIds(female);
                var renderers = ids.Select(id => Child(root, prefix + "_V11_" + id)
                    .AddComponent<MeshRenderer>()).ToArray();
                var bone = Child(root, "Head");
                var face = Child(root, prefix + "_V10_HeadFace").AddComponent<MeshRenderer>();
                var hat = Child(root, prefix + "_hardhat_BOUND_TRIAL").AddComponent<MeshRenderer>();
                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.IsTrue(ProfileHairTrial.TrySelect(root, female, ids[i], out var error), error);
                    for (int n = 0; n < ids.Length; n++)
                    {
                        Assert.AreEqual(n == i, renderers[n].enabled);
                        Assert.IsTrue(renderers[n].gameObject.activeSelf);
                    }
                    Assert.IsTrue(face.enabled);
                    Assert.IsTrue(bone.activeSelf);
                }
                ProfileHairTrial.SetHardhatVisible(root, female, false);
                Assert.IsFalse(hat.enabled);
                Assert.IsTrue(renderers.Last().enabled);
                ProfileHairTrial.SetHardhatVisible(root, female, true);
                Assert.IsTrue(hat.enabled);
                Assert.IsFalse(ProfileHairTrial.TrySelect(root, female, "hair_beard", out _));
                Assert.IsTrue(renderers.Last().enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MissingOrDuplicateModulesFailBeforeChangingAnyRenderer()
        {
            var root = new GameObject("Local trial");
            try
            {
                var first = Child(root, "M02_V11_hair_short").AddComponent<MeshRenderer>();
                var second = Child(root, "M02_V11_hair_long").AddComponent<MeshRenderer>();
                first.enabled = false;
                Assert.IsFalse(ProfileHairTrial.TrySelect(root, false, "hair_short", out _));
                Assert.IsFalse(first.enabled);
                Assert.IsTrue(second.enabled);
                Child(root, "M02_V11_hair_bun").AddComponent<MeshRenderer>();
                Child(root, "M02_V11_hair_long").AddComponent<MeshRenderer>();
                Assert.IsFalse(ProfileHairTrial.TrySelect(root, false, "hair_short", out _));
                Assert.IsFalse(first.enabled);
                Assert.IsTrue(second.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false, "M02_V11_HairTrial.glb")]
        [TestCase(true, "F01_V11_HairTrial.glb")]
        public void HairTrialPathIsLocalAndSeparate(bool female, string file)
        {
            Assert.AreEqual("Assets/CityHighwaysLicensedArt/RoadWorkerV11/" + file,
                ProfileHairTrial.AssetPath(female));
            Assert.AreNotEqual(ProfileRoadWorkerTrial.AssetPath(female, false), ProfileHairTrial.AssetPath(female));
        }

        static GameObject Child(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }
    }
}
#endif
