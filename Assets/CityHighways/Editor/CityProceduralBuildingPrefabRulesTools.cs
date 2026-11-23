using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in scene authoring entry point for seed-independent building Prefab rules.</summary>
public static class CityProceduralBuildingPrefabRulesTools
{
    [MenuItem("GameObject/City Highways/Procedural Building Prefab Rules", false, 12)]
    static void AddRules(MenuCommand command)
    {
        var go = new GameObject("Procedural Building Prefab Rules");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create procedural building Prefab rules");
        Undo.AddComponent<CityProceduralBuildingPrefabRules>(go);
        Selection.activeGameObject = go;
    }
}
