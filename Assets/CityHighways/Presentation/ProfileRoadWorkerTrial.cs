#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Editor-only inspection of a fixed, local Road Worker outfit.
    /// This is deliberately not the complete production wardrobe contract.</summary>
    public static class ProfileRoadWorkerTrial
    {
        public const string Folder = "Assets/CityHighwaysLicensedArt/RoadWorkerV4";
        public static string AssetPath(bool female, bool fbx) =>
            Folder + "/" + (female ? "F01" : "M02") + "_RoadWorker_ANIMATED_V4." + (fbx ? "fbx" : "glb");

        public struct Inspection
        {
            public int Skins, Bones;
            public long Triangles;
            public bool ValidHumanoid;
        }

        public static bool TryInspect(GameObject root, out Inspection result, out string error)
        {
            result = default; error = null;
            if (root == null || !root.activeSelf) { error = "Missing or inactive model root."; return false; }
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                root.GetComponentsInChildren<Camera>(true).Length != 0 ||
                root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<AudioSource>(true).Length != 0 ||
                root.GetComponentsInChildren<ParticleSystem>(true).Length != 0 ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 ||
                root.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            { error = "Trial requires a visual-only model, without vendor behaviours or physics."; return false; }
            var animators = root.GetComponentsInChildren<Animator>(true);
            if (animators.Length > 1) { error = "Multiple Animators."; return false; }
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) { error = "No skinned meshes."; return false; }
            HashSet<Transform> shared = null;
            long triangles = 0;
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                var bones = skin.bones;
                if (mesh == null || bones.Length == 0 || mesh.bindposes.Length != bones.Length ||
                    (skin.rootBone != null && !Inside(root.transform, skin.rootBone)))
                { error = "Missing mesh/bind poses or external root bone: " + skin.name; return false; }
                var set = new HashSet<Transform>();
                foreach (var bone in bones)
                    if (bone == null || !Inside(root.transform, bone) || !set.Add(bone))
                    { error = "Missing, duplicate or external bone: " + skin.name; return false; }
                if (shared == null) shared = set;
                else if (!shared.SetEquals(set))
                { error = "The outfit does not share one bone set: " + skin.name; return false; }
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (mesh.GetTopology(i) != MeshTopology.Triangles)
                    { error = "Non-triangle mesh: " + skin.name; return false; }
                    triangles += mesh.GetIndexCount(i) / 3;
                }
            }
            result = new Inspection {
                Skins = skins.Length, Bones = shared.Count, Triangles = triangles,
                ValidHumanoid = animators.Length == 1 && animators[0].avatar != null &&
                    animators[0].avatar.isValid && animators[0].avatar.isHuman
            };
            return true;
        }

        static bool Inside(Transform root, Transform t) => t == root || t.IsChildOf(root);
    }
}
#endif
