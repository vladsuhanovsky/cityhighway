#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Editor-only, non-overwriting persistence of the procedural preview pair.
    /// The caller selects an existing Assets parent. No profile or gameplay state is modified.</summary>
    public static class ProfileAvatarPrototypeAssets
    {
        public const string ResourcesFolder = "Assets/CityHighways/Resources/CityHighways/Profile3D";

        public static bool OutputExists(string folder)
        {
            foreach (string path in new[] { folder + "/Male.prefab", folder + "/Female.prefab", folder + "/PrototypeMaterials" })
                if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") ||
                    AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path)) return true;
            return false;
        }

        public static void GeneratePair(string folder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate prototypes outside Play Mode.");
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                folder.Contains("..") || folder.Contains("\\") || folder.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("Expected an Assets folder path.", nameof(folder));
            int split = folder.LastIndexOf('/');
            string parent = folder.Substring(0, split);
            string name = folder.Substring(split + 1);
            if (!AssetDatabase.IsValidFolder(parent))
                throw new InvalidOperationException("Missing parent: " + parent);
            if (OutputExists(folder)) throw new InvalidOperationException("Prototype outputs already exist.");
            string palette = folder + "/PrototypeMaterials";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible avatar shader found.");
            var created = new List<string>();
            var mats = new Dictionary<Color, Material>();
            var roots = new List<GameObject>();
            bool success = false;
            try
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    if (File.Exists(folder) || Directory.Exists(folder) || File.Exists(folder + ".meta"))
                        throw new IOException("Output folder is occupied: " + folder);
                    CreateFolder(parent, name, folder, created);
                }
                CreateFolder(folder, "PrototypeMaterials", palette, created);
                Material Resolve(Color color)
                {
                    if (mats.TryGetValue(color, out var existing)) return existing;
                    var mat = new Material(shader) { color = color, name = "Prototype " + mats.Count };
                    string path = palette + "/Color_" + mats.Count.ToString("00") + ".mat";
                    created.Add(path);
                    try { AssetDatabase.CreateAsset(mat, path); }
                    catch { if (!AssetDatabase.Contains(mat)) Object.DestroyImmediate(mat); throw; }
                    mats.Add(color, mat);
                    return mat;
                }
                foreach (string gender in new[] { CharacterCatalog.Male, CharacterCatalog.Female })
                {
                    var model = ProfileAvatarPrototype.Build(gender, Resolve);
                    roots.Add(model);
                    string path = folder + (gender == CharacterCatalog.Male ? "/Male.prefab" : "/Female.prefab");
                    created.Add(path);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(model, path);
                    ValidatePrefab(prefab, gender);
                }
                AssetDatabase.SaveAssets();
                success = true;
            }
            finally
            {
                foreach (var model in roots) if (model != null) Object.DestroyImmediate(model);
                if (!success)
                    for (int i = created.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(created[i]);
            }
        }

        static void CreateFolder(string parent, string name, string expected, List<string> created)
        {
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid)) throw new IOException("Cannot create " + expected);
            string actual = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(actual)) created.Add(actual);
            if (actual != expected) throw new IOException("Unexpected folder path: " + actual);
        }

        public static void ValidatePrefab(GameObject prefab, string gender)
        {
            if (prefab == null || !prefab.activeSelf) throw new InvalidOperationException("Missing or inactive avatar prefab.");
            var rig = prefab.GetComponent<ProfileAvatarRig>();
            if (rig == null || rig.Gender != gender) throw new InvalidOperationException("Missing or mismatched avatar rig.");
            if (!rig.TryValidate(out var error)) throw new InvalidOperationException(error);
            if (rig.MissingCatalogIds().Length != 0) throw new InvalidOperationException("Missing catalog variants.");
            var motion = prefab.GetComponent<ProfileAvatarMotion>();
            if (motion != null && !motion.IsValid()) throw new InvalidOperationException("Invalid optional articulation.");
            foreach (string slot in CharacterCatalog.Slots)
                foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                {
                    var variant = prefab.transform.Find(item.id);
                    if (variant == null) throw new InvalidOperationException("Missing variant: " + item.id);
                    var renderers = variant.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("Missing geometry: " + item.id);
                    foreach (var renderer in renderers)
                    {
                        var mesh = renderer.GetComponent<MeshFilter>();
                        if (mesh == null || mesh.sharedMesh == null || renderer.sharedMaterial == null ||
                            !AssetDatabase.Contains(renderer.sharedMaterial))
                            throw new InvalidOperationException("Missing mesh or persistent material: " + item.id);
                    }
                }
            if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Unexpected prototype collider.");
        }
    }
}
#endif
