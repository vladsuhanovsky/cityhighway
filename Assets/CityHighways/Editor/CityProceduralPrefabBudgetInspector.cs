using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Inspect optional global procedural Prefab budgets and configured art costs.</summary>
[CustomEditor(typeof(CityProceduralPrefabBudget))]
public sealed class CityProceduralPrefabBudgetInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        var triangleLimit = serializedObject.FindProperty("maxTrianglesPerPrefab").intValue;
        var meshLimit = serializedObject.FindProperty("maxMeshesPerPrefab").intValue;
        EditorGUILayout.HelpBox(
            "Budget is shared across automatic building, tree and prop replacements in " +
            "each generated 3D city. Low/Medium/High control the maximum number of " +
            "automatic Prefab instances. No Budget component = legacy unlimited behavior. " +
            "Manual stable-ID replacements are not subject to these optional limits.",
            MessageType.Info);
        int checkedPrefabs = 0;
        int violations = 0;
        foreach (var rules in Object.FindObjectsByType<CityProceduralBuildingPrefabRules>(
            FindObjectsInactive.Exclude))
            foreach (var rule in rules.Rules)
                if (rule != null && rule.visualPrefab != null)
                    Check(rule.visualPrefab, triangleLimit, meshLimit,
                        ref checkedPrefabs, ref violations);
        foreach (var rules in Object.FindObjectsByType<CityProceduralTreePrefabRules>(
            FindObjectsInactive.Exclude))
            foreach (var rule in rules.Rules)
                if (rule != null && rule.visualPrefab != null)
                    Check(rule.visualPrefab, triangleLimit, meshLimit,
                        ref checkedPrefabs, ref violations);
        foreach (var rules in Object.FindObjectsByType<CityProceduralPropPrefabRules>(
            FindObjectsInactive.Exclude))
            foreach (var rule in rules.Rules)
                if (rule != null && rule.visualPrefab != null)
                    Check(rule.visualPrefab, triangleLimit, meshLimit,
                        ref checkedPrefabs, ref violations);
        EditorGUILayout.LabelField("Configured rule models checked: " + checkedPrefabs +
            "    Over individual budget: " + violations);
        EditorGUILayout.HelpBox(
            "Triangles count triangle-topology indices / 3. Geometry and instance limits " +
            "are conservative authoring controls, not measured FPS or draw-call limits.",
            MessageType.None);
    }

    static void Check(GameObject prefab, int trianglesLimit, int meshLimit,
        ref int checkedPrefabs, ref int violations)
    {
        checkedPrefabs++;
        CityProceduralPrefabBudget.CountGeometry(prefab, out int meshes, out long triangles);
        if ((trianglesLimit > 0 && triangles > trianglesLimit) ||
            (meshLimit > 0 && meshes > meshLimit))
        {
            violations++;
            EditorGUILayout.HelpBox(prefab.name + ": " + triangles +
                " triangles, " + meshes + " meshes — exceeds per-Prefab budget.",
                MessageType.Warning);
        }
    }
}
