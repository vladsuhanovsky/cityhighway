using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only prefab thumbnail and direct placement controls for decoration anchors.</summary>
[CustomEditor(typeof(CitySceneDecoration))]
public sealed class CitySceneDecorationInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        var anchor = (CitySceneDecoration)target;
        EditorGUILayout.Space();
        if (anchor.VisualPrefab == null)
        {
            EditorGUILayout.HelpBox("Assign a visual Prefab to see a preview and spawn this decoration in 3D.", MessageType.Info);
            return;
        }

        var thumbnail = AssetPreview.GetAssetPreview(anchor.VisualPrefab) ??
                        AssetPreview.GetMiniThumbnail(anchor.VisualPrefab);
        if (thumbnail != null)
        {
            GUILayout.Label("Prefab preview", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(160f, 160f, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, thumbnail, ScaleMode.ScaleToFit);
        }

        if (GUILayout.Button("Select assigned Prefab"))
        {
            Selection.activeObject = anchor.VisualPrefab;
            EditorGUIUtility.PingObject(anchor.VisualPrefab);
        }

        EditorGUILayout.HelpBox(
            "Move, rotate and scale this anchor with Unity Transform tools. " +
            "Only the matching city shows the prefab during 3D gameplay. " +
            "The scene view displays a placement gizmo, not the full generated city.",
            MessageType.None);
    }

    void OnSceneGUI()
    {
        var anchor = (CitySceneDecoration)target;
        if (anchor.VisualPrefab == null) return;
        var transform = anchor.transform;

        Handles.color = anchor.ShowInGame
            ? new Color(0.2f, 0.9f, 0.6f, 1f)
            : new Color(0.65f, 0.65f, 0.65f, 1f);
        Handles.Label(transform.position + Vector3.up * 0.2f,
            anchor.gameObject.name + " [" + anchor.MapName + "]");
        Handles.ArrowHandleCap(0, transform.position,
            transform.rotation, HandleUtility.GetHandleSize(transform.position) * 0.6f,
            EventType.Repaint);
    }
}
