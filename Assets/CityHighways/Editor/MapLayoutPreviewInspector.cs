using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Convenient actions for a selected map preview in the Unity Inspector.</summary>
[CustomEditor(typeof(MapLayoutPreview))]
public sealed class MapLayoutPreviewInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Move child location markers on the X/Z plane in Scene View. " +
            "Export writes coordinates into the original JSON map, not the scene.",
            MessageType.Info);

        var preview = (MapLayoutPreview)target;
        using (new EditorGUI.DisabledScope(preview.SourceMap == null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Export location coordinates to JSON"))
                MapLayoutAuthoring.ExportPreview(preview);
        }
    }
}

/// <summary>Show stable map IDs when selecting markers in Scene View.</summary>
[CustomEditor(typeof(MapLocationMarker))]
public sealed class MapLocationMarkerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Move this marker on X/Z, then export from the parent Map Layout Preview.", MessageType.Info);
    }

    void OnSceneGUI()
    {
        var marker = (MapLocationMarker)target;
        Handles.color = Color.yellow;
        Handles.Label(marker.transform.position + Vector3.up * 0.12f, marker.Id);
    }
}
