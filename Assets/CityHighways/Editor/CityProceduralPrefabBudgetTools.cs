using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in per-city automatic Prefab complexity budget.</summary>
public static class CityProceduralPrefabBudgetTools
{
    [MenuItem("GameObject/City Highways/Procedural Prefab Budget", false, 15)]
    static void AddBudget(MenuCommand command)
    {
        var go = new GameObject("Procedural Prefab Budget");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create procedural Prefab budget");
        Undo.AddComponent<CityProceduralPrefabBudget>(go);
        Selection.activeGameObject = go;
    }
}
