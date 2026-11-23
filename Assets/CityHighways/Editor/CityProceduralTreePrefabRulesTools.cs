using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Author tree Prefab rules once for fixed and randomly seeded cities.</summary>
public static class CityProceduralTreePrefabRulesTools
{
    [MenuItem("GameObject/City Highways/Procedural Tree Prefab Rules", false, 13)]
    static void AddRules(MenuCommand command)
    {
        var go = new GameObject("Procedural Tree Prefab Rules");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create procedural tree Prefab rules");
        Undo.AddComponent<CityProceduralTreePrefabRules>(go);
        Selection.activeGameObject = go;
    }
}
