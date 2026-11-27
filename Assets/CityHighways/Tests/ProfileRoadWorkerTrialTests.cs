#if UNITY_EDITOR
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class ProfileRoadWorkerTrialTests
    {
        [Test]
        public void PathsRemainInsideIgnoredLocalArtFolder()
        {
            Assert.AreEqual("Assets/CityHighwaysLicensedArt/RoadWorkerV4/M02_RoadWorker_ANIMATED_V4.glb",
                ProfileRoadWorkerTrial.AssetPath(false, false));
            Assert.AreEqual("Assets/CityHighwaysLicensedArt/RoadWorkerV4/F01_RoadWorker_ANIMATED_V4.fbx",
                ProfileRoadWorkerTrial.AssetPath(true, true));
        }

        [Test]
        public void SharedSkinSetIsAcceptedButExternalOrDifferentBonesAreRejected()
        {
            var root = new GameObject("Root");
            var external = new GameObject("External");
            var mesh = new Mesh();
            try
            {
                var bone = new GameObject("Hips").transform; bone.SetParent(root.transform);
                var other = new GameObject("Other").transform; other.SetParent(root.transform);
                mesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.boneWeights = new[] { Weight(), Weight(), Weight() };
                var a = Skin(root.transform, bone, mesh);
                var b = Skin(root.transform, bone, mesh);
                Assert.IsTrue(ProfileRoadWorkerTrial.TryInspect(root, out var info, out var error), error);
                Assert.AreEqual(2, info.Skins); Assert.AreEqual(1, info.Bones);
                Assert.AreEqual(2L, info.Triangles); Assert.IsFalse(info.ValidHumanoid);
                b.bones = new[] { other }; b.rootBone = other;
                Assert.IsFalse(ProfileRoadWorkerTrial.TryInspect(root, out _, out _));
                b.bones = new[] { external.transform }; b.rootBone = external.transform;
                Assert.IsFalse(ProfileRoadWorkerTrial.TryInspect(root, out _, out _));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(external); Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MissingSkinAndPhysicsAreRejected()
        {
            var root = new GameObject("Root");
            try
            {
                Assert.IsFalse(ProfileRoadWorkerTrial.TryInspect(root, out _, out _));
                root.AddComponent<BoxCollider>();
                Assert.IsFalse(ProfileRoadWorkerTrial.TryInspect(root, out _, out var error));
                StringAssert.Contains("visual-only", error);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static BoneWeight Weight() => new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        static SkinnedMeshRenderer Skin(Transform root, Transform bone, Mesh mesh)
        {
            var go = new GameObject("Skin"); go.transform.SetParent(root);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = mesh; r.bones = new[] { bone }; r.rootBone = bone;
            return r;
        }
    }
}
#endif
