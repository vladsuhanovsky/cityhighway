using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    public enum ProfileAvatarMotionMode { Still, Idle, Walk }
    public enum ProfileAvatarJoint { Head, LeftArm, RightArm, LeftLeg, RightLeg }

    /// <summary>Optional rigid-part articulation for procedural test avatars.
    /// No Update loop, Animator controller, physics, root motion or game RNG.
    /// Callers sample a cosmetic phase explicitly; the original rig remains independent.</summary>
    public sealed class ProfileAvatarMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Joint
        {
            public ProfileAvatarJoint kind;
            public Transform pivot;
            public Quaternion rest = Quaternion.identity;
        }

        [SerializeField] Joint[] joints = Array.Empty<Joint>();

#if UNITY_EDITOR
        internal void ConfigurePrototype(Joint[] value) { joints = value; }
#endif

        public bool IsValid()
        {
            if (joints == null || joints.Length == 0) return false;
            var seen = new HashSet<Transform>();
            var kinds = new HashSet<ProfileAvatarJoint>();
            foreach (var joint in joints)
            {
                if (joint == null || joint.pivot == null || joint.pivot == transform ||
                    !joint.pivot.IsChildOf(transform) || !seen.Add(joint.pivot) ||
                    !Enum.IsDefined(typeof(ProfileAvatarJoint), joint.kind)) return false;
                kinds.Add(joint.kind);
            }
            return kinds.Count == 5;
        }

        public bool TrySample(ProfileAvatarMotionMode mode, float phase)
        {
            if (!IsValid() || float.IsNaN(phase) || float.IsInfinity(phase) ||
                !Enum.IsDefined(typeof(ProfileAvatarMotionMode), mode)) return false;
            SampleValidated(mode, phase);
            return true;
        }

        // Caller validates the authored instance once before using this hot path.
        internal void SampleValidated(ProfileAvatarMotionMode mode, float phase)
        {
            float wave = Mathf.Sin(Mathf.Repeat(phase, 1f) * Mathf.PI * 2f);
            foreach (var joint in joints)
            {
                float pitch = 0f, yaw = 0f;
                if (mode == ProfileAvatarMotionMode.Idle)
                {
                    if (joint.kind == ProfileAvatarJoint.Head) yaw = wave * 4f;
                    else if (joint.kind == ProfileAvatarJoint.LeftArm || joint.kind == ProfileAvatarJoint.RightArm)
                        pitch = wave * 3f;
                }
                else if (mode == ProfileAvatarMotionMode.Walk)
                {
                    switch (joint.kind)
                    {
                        case ProfileAvatarJoint.LeftLeg: pitch = wave * 24f; break;
                        case ProfileAvatarJoint.RightLeg: pitch = -wave * 24f; break;
                        case ProfileAvatarJoint.LeftArm: pitch = -wave * 18f; break;
                        case ProfileAvatarJoint.RightArm: pitch = wave * 18f; break;
                        case ProfileAvatarJoint.Head: yaw = wave * 2f; break;
                    }
                }
                if (joint.pivot != null)
                    joint.pivot.localRotation = joint.rest * Quaternion.Euler(pitch, yaw, 0f);
            }
        }
    }
}
