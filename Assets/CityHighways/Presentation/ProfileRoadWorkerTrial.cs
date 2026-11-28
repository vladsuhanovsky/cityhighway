#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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

        public static string ModularAssetPath(bool female, bool restoredBase) =>
            "Assets/CityHighwaysLicensedArt/RoadWorkerV9/" + (female ? "F01" : "M02") +
            (restoredBase ? "_V9_RestoredBase.glb" : "_V9_Workwear.glb");

        // Match authored node names, not arbitrary substrings or bone names.
        public static string ModularGroup(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName) ||
                !(nodeName.StartsWith("M02_", System.StringComparison.Ordinal) ||
                  nodeName.StartsWith("F01_", System.StringComparison.Ordinal))) return null;
            switch (nodeName.Substring(4))
            {
                case "V9_Base_HeadHair": return "Head / baked hair";
                case "V9_Base_TorsoShirt": return "Torso / baked shirt";
                case "V9_Base_UpperArms": return "Upper arms";
                case "V9_Base_Forearms": return "Forearms";
                case "V9_Base_Hands": return "Hands";
                case "V9_Base_PelvisShorts": return "Pelvis / baked shorts";
                case "V9_Base_Thighs": return "Thighs";
                case "V9_Base_Calves": return "Calves";
                case "V9_Base_Feet": return "Feet";
                case "hardhat_BOUND_TRIAL": return "Hardhat";
                case "vest_BOUND_TRIAL": return "Vest";
                case "trousers_BOUND_TRIAL": return "Trousers";
                case "V9_Shoes":
                case "boot_L_BOUND_TRIAL":
                case "boot_R_BOUND_TRIAL": return "Boots";
                case "radio_BOUND_TRIAL": return "Radio";
                default: return null;
            }
        }

        // Fast container preflight only; the glTF importer must still validate JSON and buffers.
        public static bool TryCheckGlbFile(string path, out string error)
        {
            error = null;
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 20 || reader.ReadUInt32() != 0x46546C67u ||
                        reader.ReadUInt32() != 2u || reader.ReadUInt32() != stream.Length)
                    { error = "Invalid or incomplete GLB 2.0 container: " + path; return false; }
                    uint jsonLength = reader.ReadUInt32();
                    if (reader.ReadUInt32() != 0x4E4F534Au || jsonLength == 0 ||
                        jsonLength % 4 != 0 || jsonLength > stream.Length - 20)
                    { error = "Invalid GLB JSON chunk: " + path; return false; }
                    return true;
                }
            }
            catch (System.Exception ex) { error = "Cannot read GLB: " + ex.Message; return false; }
        }

        public struct Inspection
        {
            public int Skins, Bones;
            public long Triangles;
            public bool ValidHumanoid;
        }

        public static bool TryInspect(GameObject root, out Inspection result, out string error)
        {
            result = default; error = null;
            if (root == null) { error = "Missing model root."; return false; }
            if (!root.activeSelf) { error = "Imported model root is inactive."; return false; }
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
