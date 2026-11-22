using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Quick authoring entry point for non-authoritative 3D decoration anchors.</summary>
public static class CitySceneDecorationTools
{
    [MenuItem("GameObject/City Highways/Decoration Anchor", false, 10)]
    static void AddAnchor(MenuCommand command)
    {
        var go = new GameObject("City Decoration");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create City Decoration Anchor");
        Undo.AddComponent<CitySceneDecoration>(go);
        Selection.activeGameObject = go;
    }
}
