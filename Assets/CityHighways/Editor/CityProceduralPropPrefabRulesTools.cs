using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Create a seed-independent rule set for procedural decoration Prefabs.</summary>
public static class CityProceduralPropPrefabRulesTools
{
    [MenuItem("GameObject/City Highways/Procedural Prop Prefab Rules", false, 14)]
    static void AddRules(MenuCommand command)
    {
        var go = new GameObject("Procedural Prop Prefab Rules");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create procedural prop Prefab rules");
        Undo.AddComponent<CityProceduralPropPrefabRules>(go);
        Selection.activeGameObject = go;
    }
}
