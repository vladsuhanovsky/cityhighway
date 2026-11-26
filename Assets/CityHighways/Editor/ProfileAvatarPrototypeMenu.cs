using System;
using System.Collections.Generic;
using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit opt-in local asset authoring. Never replaces existing character assets.</summary>
public static class ProfileAvatarPrototypeMenu
{
    const string Parent = "Assets/CityHighways/Resources/CityHighways";
    const string Folder = Parent + "/Profile3D";
    const string Palette = Folder + "/PrototypeMaterials";
    const string Male = Folder + "/Male.prefab";
    const string Female = Folder + "/Female.prefab";

    [MenuItem("City Highways/Profile/Create Prototype 3D Avatars")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        // The entire output set must be free. Existing authored models are never overwritten.
        if (File.Exists(Male) || File.Exists(Female) || Directory.Exists(Palette) ||
            AssetDatabase.LoadMainAssetAtPath(Male) != null || AssetDatabase.LoadMainAssetAtPath(Female) != null ||
            AssetDatabase.IsValidFolder(Palette))
        {
            EditorUtility.DisplayDialog("Prototype avatars", "Output already exists. Existing assets were not changed. Move them to a backup folder before generating a new pair.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Prototype avatars",
            "Create two static procedural preview prefabs and their materials? These are test models, not final character art. The game will use them instead of 2D for compatible outfits.", "Create", "Cancel")) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) { Debug.LogError("No compatible avatar shader found."); return; }
        var created = new List<string>();
        var mats = new Dictionary<Color, Material>();
        var roots = new List<GameObject>();
        bool success = false;
        try
        {
            if (!AssetDatabase.IsValidFolder(Parent))
                throw new InvalidOperationException("Resources parent is missing: " + Parent);
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Parent, "Profile3D"))) throw new IOException("Cannot create output folder.");
                created.Add(Folder);
            }
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Folder, "PrototypeMaterials"))) throw new IOException("Cannot create material folder.");
            created.Add(Palette);
            Material Resolve(Color color)
            {
                if (mats.TryGetValue(color, out var existing)) return existing;
                var mat = new Material(shader) { color = color, name = "Prototype " + mats.Count };
                string path = Palette + "/Color_" + mats.Count.ToString("00") + ".mat";
                created.Add(path);
                try { AssetDatabase.CreateAsset(mat, path); }
                catch { if (!AssetDatabase.Contains(mat)) Object.DestroyImmediate(mat); throw; }
                mats.Add(color, mat);
                return mat;
            }
            foreach (var gender in new[] {CharacterCatalog.Male, CharacterCatalog.Female})
            {
                var root = ProfileAvatarPrototype.Build(gender, Resolve);
                roots.Add(root);
                string path = gender == CharacterCatalog.Male ? Male : Female;
                created.Add(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null || !prefab.GetComponent<ProfileAvatarRig>().TryValidate(out _))
                    throw new InvalidOperationException("Invalid saved prototype: " + path);
            }
            AssetDatabase.SaveAssets();
            success = true;
        }
        catch (Exception ex) { Debug.LogError("Prototype generation failed: " + ex.Message); }
        finally
        {
            foreach(var root in roots) if (root != null) Object.DestroyImmediate(root);
            if (!success)
                for(int i=created.Count-1;i>=0;i--) AssetDatabase.DeleteAsset(created[i]);
        }
        if (success)
        {
            Debug.Log("Created Male/Female prototype prefabs. Open the profile in Play Mode to inspect them.");
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(Male);
        }
    }
}
