using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>One optional city-scale avatar, reusing a validated profile prefab.
    /// No autonomous behaviour, navigation, physics or material instances.</summary>
    public sealed class CityProfileWalker
    {
        public const float Height = .014f;
        public const int MaxTransforms = 512, MaxVisibleRenderers = 48;
        public GameObject Root { get; }
        readonly ProfileAvatarMotion motion;

        CityProfileWalker(GameObject root, ProfileAvatarMotion motion)
        { Root = root; this.motion = motion; }

        public static CityProfileWalker TryCreate(ProfileAppearance appearance, Transform parent,
            Func<string, ProfileAvatarRig> load = null)
        {
            if (appearance == null || parent == null) return null;
            GameObject holder = null;
            try
            {
                string path = ProfileAvatarPreviewStage.ResourceFor(appearance.Gender);
                if (path == null) return null;
                var prefab = load != null ? load(path) : Resources.Load<GameObject>(path)?.GetComponent<ProfileAvatarRig>();
                if (prefab == null || !prefab.gameObject.activeSelf || prefab.Gender != appearance.Gender ||
                    !prefab.TryValidate(out _) ||
                    prefab.GetComponentsInChildren<Transform>(true).Length > MaxTransforms) return null;
                // Reject authored behaviour before instantiating anything in the city.
                foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour == null || !(behaviour is ProfileAvatarRig || behaviour is ProfileAvatarMotion)) return null;
                if (prefab.GetComponentsInChildren<ProfileAvatarRig>(true).Length != 1 ||
                    prefab.GetComponentsInChildren<ProfileAvatarMotion>(true).Length > 1 ||
                    prefab.GetComponentsInChildren<Camera>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<Light>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<AudioSource>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<Animator>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<Animation>(true).Length != 0) return null;

                holder = new GameObject("Local profile pedestrian") { layer = parent.gameObject.layer };
                holder.SetActive(false);
                holder.transform.SetParent(parent, false);
                var rig = Object.Instantiate(prefab, holder.transform, false);
                if (!rig.TryApply(appearance, out _)) return Fail(holder);
                foreach (var child in holder.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = parent.gameObject.layer;
                var motion = rig.GetComponent<ProfileAvatarMotion>();
                if (motion != null && !motion.IsValid()) return Fail(holder);
                if (motion != null) motion.SampleValidated(ProfileAvatarMotionMode.Still, 0f);

                holder.SetActive(true);
                bool hasBounds = false;
                Bounds bounds = default;
                int count = 0;
                foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (++count > MaxVisibleRenderers) return Fail(holder);
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                float height = bounds.size.y;
                if (!hasBounds || height < .001f || !Finite(height) ||
                    !Finite(bounds.size.x) || !Finite(bounds.size.z) ||
                    bounds.size.x > height * 2f || bounds.size.z > height * 2f) return Fail(holder);
                var bottom = holder.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                if (!Finite(bottom.x) || !Finite(bottom.y) || !Finite(bottom.z)) return Fail(holder);
                rig.transform.localPosition -= bottom;
                holder.transform.localScale = Vector3.one * (Height / height);
                holder.SetActive(false);
                return new CityProfileWalker(holder, motion);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Profile pedestrian unavailable: " + ex.Message);
                return Fail(holder);
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static CityProfileWalker Fail(GameObject root)
        {
            Remove(root);
            return null;
        }

        public void SetPose(Vector3 position, Quaternion rotation, float phase)
        {
            if (Root == null) return;
            Root.transform.SetPositionAndRotation(position, rotation);
            if (motion != null && Finite(phase)) motion.SampleValidated(ProfileAvatarMotionMode.Walk, phase);
            if (!Root.activeSelf) Root.SetActive(true);
        }

        public void Hide() { if (Root != null) Root.SetActive(false); }
        public void Release() => Remove(Root);

        static void Remove(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }
    }
}
