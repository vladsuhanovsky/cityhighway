using System;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit opt-in local asset authoring. Never replaces existing character assets.</summary>
public static class ProfileAvatarPrototypeMenu
{
    [MenuItem("City Highways/Profile/Create Prototype 3D Avatars")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string folder = ProfileAvatarPrototypeAssets.ResourcesFolder;
        if (ProfileAvatarPrototypeAssets.OutputExists(folder))
        {
            EditorUtility.DisplayDialog("Prototype avatars", "Output already exists. Existing assets were not changed. Move them to a backup folder before generating a new pair.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Prototype avatars",
            "Create two static procedural preview prefabs and their materials? These are test models, not final character art. The game will use them instead of 2D for compatible outfits.", "Create", "Cancel")) return;
        try
        {
            ProfileAvatarPrototypeAssets.GeneratePair(folder);
            Debug.Log("Created Male/Female prototype prefabs. Open the profile in Play Mode to inspect them.");
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/Male.prefab");
        }
        catch (Exception ex) { Debug.LogError("Prototype generation failed: " + ex.Message); }
    }

    [MenuItem("City Highways/Profile/Validate Prototype 3D Avatars")]
    public static void Validate()
    {
        string folder = ProfileAvatarPrototypeAssets.ResourcesFolder;
        try
        {
            ProfileAvatarPrototypeAssets.ValidatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Male.prefab"), CharacterCatalog.Male);
            ProfileAvatarPrototypeAssets.ValidatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Female.prefab"), CharacterCatalog.Female);
            Debug.Log("Both prototype prefabs passed catalog, geometry and material validation. Inspect actual rendering separately in Play Mode.");
        }
        catch (Exception ex) { Debug.LogWarning("Prototype validation: " + ex.Message); }
    }
}
