using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Add a manual clearance marker without changing gameplay or map JSON.</summary>
public static class CityGenerationExclusionTools
{
    [MenuItem("GameObject/City Highways/Procedural Clearance Zone", false, 12)]
    static void AddZone(MenuCommand command)
    {
        var go = new GameObject("Procedural Clearance Zone");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Procedural Clearance Zone");
        Undo.AddComponent<CityGenerationExclusionZone>(go);
        Selection.activeGameObject = go;
    }
}
