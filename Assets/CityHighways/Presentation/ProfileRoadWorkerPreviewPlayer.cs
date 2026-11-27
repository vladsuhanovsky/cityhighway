#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CityHighways.Presentation
{
    // Editor-only preview of the accepted local fixed outfit. No wardrobe or save
    // ownership decisions, no new Resources paths, no dependency in player builds.
    public sealed class ProfileRoadWorkerPreviewPlayer
    {
        readonly GameObject model;
        readonly AnimationClip idle, walk;
        readonly Transform[] nodes;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;

        public static bool TryLoad(string gender, out GameObject asset, out AnimationClip idle,
            out AnimationClip walk, out string error)
        {
            asset = null; idle = walk = null; error = null;
            if (gender != CharacterCatalog.Male && gender != CharacterCatalog.Female)
            { error = "Unsupported trial gender."; return false; }
            var path = ProfileRoadWorkerTrial.AssetPath(gender == CharacterCatalog.Female, false);
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!ProfileRoadWorkerTrial.TryInspect(asset, out _, out error)) return false;
            return TrySelectClips(asset, AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray(),
                out idle, out walk, out error);
        }

        public static bool TrySelectClips(GameObject model, AnimationClip[] clips,
            out AnimationClip idle, out AnimationClip walk, out string error)
        {
            idle = walk = null; error = null;
            if (model == null || clips == null) { error = "Missing model or clips."; return false; }
            var i = clips.Where(c => c != null && c.name == "Idle").ToArray();
            var w = clips.Where(c => c != null && c.name == "Walk").ToArray();
            if (i.Length != 1 || w.Length != 1)
            { error = "Expected exactly one Idle and one Walk clip."; return false; }
            if (!Valid(model, i[0]) || !Valid(model, w[0]))
            { error = "Expected finite transform-only clips with bindings on this model and no events."; return false; }
            idle = i[0]; walk = w[0]; return true;
        }

        static bool Valid(GameObject model, AnimationClip clip)
        {
            if (clip.humanMotion || !(clip.length > .001f) || float.IsInfinity(clip.length) ||
                AnimationUtility.GetAnimationEvents(clip).Length != 0 ||
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0) return false;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            return bindings.Length != 0 && bindings.All(b => b.type == typeof(Transform) &&
                (string.IsNullOrEmpty(b.path) || model.transform.Find(b.path) != null));
        }

        public ProfileRoadWorkerPreviewPlayer(GameObject model, AnimationClip idle, AnimationClip walk)
        {
            if (model == null || idle == null || walk == null) throw new ArgumentNullException();
            this.model = model; this.idle = idle; this.walk = walk;
            foreach (var a in model.GetComponentsInChildren<Animator>(true))
            { a.runtimeAnimatorController = null; a.applyRootMotion = false; a.fireEvents = false; a.enabled = false; }
            foreach (var a in model.GetComponentsInChildren<Animation>(true)) a.enabled = false;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            nodes = model.GetComponentsInChildren<Transform>(true);
            positions = nodes.Select(n => n.localPosition).ToArray();
            rotations = nodes.Select(n => n.localRotation).ToArray();
            scales = nodes.Select(n => n.localScale).ToArray();
        }

        public float Rate(ProfileAvatarMotionMode mode) =>
            1f / (mode == ProfileAvatarMotionMode.Walk ? walk.length : idle.length);

        public void Sample(ProfileAvatarMotionMode mode, float phase)
        {
            if (model == null || float.IsNaN(phase) || float.IsInfinity(phase)) return;
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].localPosition = positions[i];
                nodes[i].localRotation = rotations[i];
                nodes[i].localScale = scales[i];
            }
            if (mode == ProfileAvatarMotionMode.Still) return;
            var clip = mode == ProfileAvatarMotionMode.Walk ? walk : idle;
            clip.SampleAnimation(model, Mathf.Repeat(phase, 1f) * clip.length);
        }
    }
}
#endif
