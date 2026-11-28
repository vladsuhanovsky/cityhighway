using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CityHighways.Presentation
{
    /// <summary>Local authored asset contract. The skeleton is shared by the modular
    /// variants; vendor scripts/controllers are not required for this preview.</summary>
    public sealed class ProfileHumanoidArt : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] AnimationClip idle;
        [SerializeField] AnimationClip walk;

        public Animator Animator => animator;
        public AnimationClip Idle => idle;
        public AnimationClip Walk => walk;

        public bool TryValidate(out string error)
        {
            error = null;
            if (animator == null || (animator.transform != transform && !animator.transform.IsChildOf(transform)) ||
                animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            { error = "Expected a valid Humanoid Animator inside this prefab."; return false; }
            if (GetComponentsInChildren<Animator>(true).Length != 1)
            { error = "Expected exactly one Animator."; return false; }
            if (!ValidClip(idle) || !ValidClip(walk))
            { error = "Expected non-legacy Humanoid idle and walk clips."; return false; }
            var rig = GetComponent<ProfileAvatarRig>();
            if (rig == null || !rig.TryValidate(out _) || rig.MissingCatalogIds().Length != 0 ||
                !rig.IsStableSkeletonTransform(animator.transform))
            { error = "Expected complete wardrobe bindings outside the shared skeleton."; return false; }
            var skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0)
            { error = "Missing skinned geometry."; return false; }
            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null || skin.rootBone == null ||
                    !InsideSkeleton(skin.rootBone) || !rig.IsStableSkeletonTransform(skin.rootBone) || skin.bones.Length == 0)
                { error = "Missing mesh or shared skeleton: " + skin.name; return false; }
                foreach (var bone in skin.bones)
                    if (bone == null || !InsideSkeleton(bone) || !rig.IsStableSkeletonTransform(bone))
                    { error = "Bone outside shared Animator hierarchy: " + skin.name; return false; }
            }
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour == null || !(behaviour is ProfileAvatarRig || behaviour is ProfileHumanoidArt ||
                    (behaviour is ProfileBodyCoverage && behaviour.transform == transform)))
                { error = "Remove vendor runtime behaviours from the authored preview prefab."; return false; }
            if (GetComponentsInChildren<Camera>(true).Length != 0 ||
                GetComponentsInChildren<Light>(true).Length != 0 ||
                GetComponentsInChildren<AudioSource>(true).Length != 0 ||
                GetComponentsInChildren<ParticleSystem>(true).Length != 0 ||
                GetComponentsInChildren<Collider>(true).Length != 0 ||
                GetComponentsInChildren<Rigidbody>(true).Length != 0)
            { error = "Expected a visual-only prefab without cameras, lights or physics."; return false; }
            return true;
        }

        bool InsideSkeleton(Transform bone) => bone == animator.transform || bone.IsChildOf(animator.transform);
        static bool ValidClip(AnimationClip clip) => clip != null && !clip.legacy &&
            clip.humanMotion && clip.length > .001f;
    }

    /// <summary>Preview-owned, manually sampled graph. No controller, root motion,
    /// animation events or autonomous update. Dispose before destroying the clone.</summary>
    internal sealed class ProfileHumanoidPreviewPlayer : System.IDisposable
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idle, walk;
        readonly float idleLength, walkLength;

        public ProfileHumanoidPreviewPlayer(ProfileHumanoidArt art)
        {
            if (art == null) throw new System.ArgumentNullException(nameof(art));
            if (!art.TryValidate(out var error)) throw new System.ArgumentException(error);
            var animator = art.Animator;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            idleLength = art.Idle.length;
            walkLength = art.Walk.length;
            try
            {
                graph = PlayableGraph.Create("Profile humanoid preview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph,2);
                idle = AnimationClipPlayable.Create(graph,art.Idle);
                walk = AnimationClipPlayable.Create(graph,art.Walk);
                idle.SetApplyFootIK(false); walk.SetApplyFootIK(false);
                idle.SetApplyPlayableIK(false); walk.SetApplyPlayableIK(false);
                graph.Connect(idle,0,mixer,0); graph.Connect(walk,0,mixer,1);
                var output = AnimationPlayableOutput.Create(graph,"Avatar",animator);
                output.SetSourcePlayable(mixer);
                graph.Play();
                Sample(ProfileAvatarMotionMode.Still,0f);
            }
            catch { Dispose(); throw; }
        }

        public void Sample(ProfileAvatarMotionMode mode,float phase)
        {
            if (!graph.IsValid() || float.IsNaN(phase) || float.IsInfinity(phase)) return;
            bool walking = mode == ProfileAvatarMotionMode.Walk;
            float t = mode == ProfileAvatarMotionMode.Still ? 0f : Mathf.Repeat(phase,1f);
            mixer.SetInputWeight(0,walking?0f:1f);
            mixer.SetInputWeight(1,walking?1f:0f);
            idle.SetTime(t*idleLength);
            walk.SetTime(t*walkLength);
            graph.Evaluate(0f);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
