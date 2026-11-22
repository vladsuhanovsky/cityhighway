using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Add an optional per-city visual density configuration to the current scene.</summary>
public static class CityGenerationTuningTools
{
    [MenuItem("GameObject/City Highways/City Generation Tuning", false, 11)]
    static void AddTuning(MenuCommand command)
    {
        var go = new GameObject("City Generation Tuning");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create City Generation Tuning");
        Undo.AddComponent<CityGenerationTuning>(go);
        Selection.activeGameObject = go;
    }
}
