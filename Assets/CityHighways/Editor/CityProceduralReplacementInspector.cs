using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Copy a generated object's stable ID while previewing a 3D city in Play Mode.</summary>
[CustomEditor(typeof(CityProceduralReplacement))]
public sealed class CityProceduralReplacementInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        var anchor = (CityProceduralReplacement)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Leave Stable Object Id empty for the legacy nearest-object radius search. " +
            "When filled, the ID is matched exactly and Search Radius is ignored. " +
            "There is no automatic fallback if the ID is missing.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Copy nearest generated object ID"))
            {
                Vector3 world = anchor.transform.position;
                var field = new Vector2(
                    (world.x / BoardLayout.WorldWidth + 0.5f) * CityPlanner.FieldW,
                    (0.5f - world.z / BoardLayout.WorldDepth) * CityPlanner.FieldH);
                if (CityProceduralReplacementResolver.TryFindNearestId(
                    anchor.MapName, anchor.Target, field,
                    anchor.SearchRadius * CityPlanner.PixelsPerUnit, out var id))
                {
                    EditorGUIUtility.systemCopyBuffer = id;
                    Debug.Log("Copied procedural visual ID: " + id +
                        ". Exit Play Mode, select the anchor again and paste into Stable Object Id.");
                }
                else
                {
                    Debug.LogWarning("No generated object for this map/type inside Search Radius. " +
                        "Start a 3D city for the matching map and increase the radius if needed.");
                }
            }
        }
        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox(
                "To capture an ID: enter Play Mode with 3D enabled, select this anchor, " +
                "click Copy nearest generated object ID, exit Play Mode and paste the copied ID.",
                MessageType.None);
    }
}
