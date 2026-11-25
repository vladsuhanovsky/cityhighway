using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opens the shipped ScriptableObject in Inspector, without searching
/// hierarchy objects or changing any scene/prefab.
/// </summary>
public static class MainMenuStyleEditorMenu
{
    public const string AssetPath =
        "Assets/CityHighways/Resources/CityHighways/UI/MainMenuStyle.asset";

    [MenuItem("City Highways/UI/Edit Main Menu Style")]
    public static void Open()
    {
        var style = AssetDatabase.LoadAssetAtPath<MainMenuStyle>(AssetPath);
        if (style == null)
        {
            // A recovery path for projects that deleted the asset.
            string folder = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            style = ScriptableObject.CreateInstance<MainMenuStyle>();
            AssetDatabase.CreateAsset(style, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = style;
        EditorGUIUtility.PingObject(style);
    }
}

[CustomEditor(typeof(MainMenuStyle))]
public sealed class MainMenuStyleInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Edit main-menu sizes and colors here. Values update the next " +
            "time the menu is constructed (restart Play Mode or reopen the menu). " +
            "No scene or C# edits required.", MessageType.Info);
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Tips: width applies to all 6 buttons. Gap Inside connects " +
            "each pair; Gap Between separates the 3 groups.",
            EditorStyles.wordWrappedLabel);
    }
}
