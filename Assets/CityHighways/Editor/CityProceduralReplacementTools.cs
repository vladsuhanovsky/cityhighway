using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Create a marker for replacing exactly one procedural building or tree.</summary>
public static class CityProceduralReplacementTools
{
    [MenuItem("GameObject/City Highways/Procedural Object Replacement", false, 13)]
    static void CreateReplacement(MenuCommand command)
    {
        var go = new GameObject("Procedural Object Replacement");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Procedural Object Replacement");
        Undo.AddComponent<CityProceduralReplacement>(go);
        Selection.activeGameObject = go;
    }
}
