#if UNITY_EDITOR
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ProfileRoadWorkerPreviewPlayerTests
    {
        static AnimationClip Clip(string name, string path, float length, float end)
        {
            var c = new AnimationClip { name = name, legacy = true };
            c.SetCurve(path, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, end));
            return c;
        }

        [Test]
        public void ExactNamedClipsAreRequiredAndDiagnosticWalkIsNotSelected()
        {
            var root = new GameObject("Trial");
            var idle = Clip("Idle", "", 4f, 4f);
            var walk = Clip("Walk", "", 1f, 2f);
            var source = Clip("Walk_Source", "", 1f, 3f);
            var invalid = Clip("Walk", "MissingBone", 1f, 2f);
            try
            {
                Assert.IsTrue(ProfileRoadWorkerPreviewPlayer.TrySelectClips(root, new[] {idle, walk, source},
                    out var i, out var w, out var error), error);
                Assert.AreSame(idle, i); Assert.AreSame(walk, w);
                Assert.IsFalse(ProfileRoadWorkerPreviewPlayer.TrySelectClips(root, new[] {idle, source}, out _, out _, out _));
                Assert.IsFalse(ProfileRoadWorkerPreviewPlayer.TrySelectClips(root, new[] {idle, walk, walk}, out _, out _, out _));
                Assert.IsFalse(ProfileRoadWorkerPreviewPlayer.TrySelectClips(root, new[] {idle, invalid}, out _, out _, out _));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(idle); Object.DestroyImmediate(walk);
                Object.DestroyImmediate(source); Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void BakedGeometryBoundsIgnoreInflatedImporterBoundsAndTrackMovedBone()
        {
            var root = new GameObject("Trial");
            var child = new GameObject("Trial_boot_L").transform;
            child.SetParent(root.transform, false);
            var bone = new GameObject("Bone").transform;
            bone.SetParent(root.transform, false);
            var mesh = new Mesh();
            var idle = Clip("Idle", "Bone", 1f, 1f);
            var walk = Clip("Walk", "Bone", 1f, 1f);
            try
            {
                mesh.vertices = new[] { new Vector3(0f, .1f, 0f), new Vector3(1f, .1f, 0f), new Vector3(0f, .2f, 1f) };
                mesh.triangles = new[] {0, 1, 2};
                mesh.bindposes = new[] {Matrix4x4.identity};
                var weight = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                mesh.boneWeights = new[] {weight, weight, weight};
                mesh.RecalculateBounds();
                var skin = child.gameObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.bones = new[] {bone};
                skin.rootBone = bone;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
                using var player = new ProfileRoadWorkerPreviewPlayer(root, idle, walk);
                Assert.IsTrue(player.TryGetGeometryBounds(root.transform, true, out var before));
                Assert.That(before.min.y, Is.EqualTo(.1f).Within(.0001f));
                bone.localPosition = Vector3.up * .25f;
                Assert.IsTrue(player.TryGetGeometryBounds(root.transform, true, out var after));
                Assert.That(after.min.y, Is.EqualTo(.35f).Within(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(idle); Object.DestroyImmediate(walk);
            }
        }

        [Test]
        public void SamplingRestoresRestPoseAndUsesActualClipDuration()
        {
            var root = new GameObject("Trial");
            var bone = new GameObject("Bone").transform;
            bone.SetParent(root.transform, false); bone.localPosition = Vector3.right * 5f;
            var idle = Clip("Idle", "Bone", 4f, 4f);
            var walk = Clip("Walk", "Bone", 1f, 2f);
            try
            {
                using var player = new ProfileRoadWorkerPreviewPlayer(root, idle, walk);
                Assert.AreEqual(.25f, player.Rate(ProfileAvatarMotionMode.Idle));
                Assert.AreEqual(1f, player.Rate(ProfileAvatarMotionMode.Walk));
                player.Sample(ProfileAvatarMotionMode.Idle, .5f);
                Assert.That(bone.localPosition.x, Is.EqualTo(2f).Within(.0001f));
                player.Sample(ProfileAvatarMotionMode.Walk, .5f);
                Assert.That(bone.localPosition.x, Is.EqualTo(1f).Within(.0001f));
                player.Sample(ProfileAvatarMotionMode.Still, 0f);
                Assert.AreEqual(5f, bone.localPosition.x);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(idle); Object.DestroyImmediate(walk); }
        }
    }
}
#endif
