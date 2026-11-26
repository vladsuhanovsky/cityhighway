using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

public static class ProfileProfessionalAvatarMenu
{
    [MenuItem("City Highways/Profile/Validate Professional Avatars")]
    public static void Validate()
    {
        foreach (var gender in new[] { CharacterCatalog.Male, CharacterCatalog.Female })
        {
            string path = "Assets/CityHighways/Resources/" + ProfileAvatarPreviewStage.HumanoidResourceFor(gender) + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("Professional avatar not installed: " + path); continue; }
            var art = prefab.GetComponent<ProfileHumanoidArt>();
            if (art == null) { Debug.LogWarning("Missing ProfileHumanoidArt: " + path); continue; }
            if (!art.TryValidate(out var error)) Debug.LogWarning(path + ": " + error);
            else Debug.Log("Professional avatar contract valid: " + path + ". Visual and animation acceptance still required.");
        }
    }
}
