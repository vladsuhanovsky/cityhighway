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
        public void SamplingRestoresRestPoseAndUsesActualClipDuration()
        {
            var root = new GameObject("Trial");
            var bone = new GameObject("Bone").transform;
            bone.SetParent(root.transform, false); bone.localPosition = Vector3.right * 5f;
            var idle = Clip("Idle", "Bone", 4f, 4f);
            var walk = Clip("Walk", "Bone", 1f, 2f);
            try
            {
                var player = new ProfileRoadWorkerPreviewPlayer(root, idle, walk);
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
