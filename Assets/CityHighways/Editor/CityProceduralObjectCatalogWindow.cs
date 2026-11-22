using System;
using System.Collections.Generic;
using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Inspect generated visual IDs in a list, keep a local snapshot after Play Mode,
/// and assign a selected ID to a scene-authored replacement anchor in Edit Mode.
/// </summary>
public sealed class CityProceduralObjectCatalogWindow : EditorWindow
{
    [Serializable]
    sealed class SnapshotRow
    {
        public string id;
        public int kind;
        public float x;
        public float y;
    }

    [Serializable]
    sealed class Snapshot
    {
        public string city;
        public List<SnapshotRow> rows = new List<SnapshotRow>();
    }

    static readonly string SnapshotFile = Path.Combine("Library", "CityHighwaysProceduralCatalog.json");
    Snapshot snapshot;
    string filter = "";
    int kindFilter;
    Vector2 scroll;
    int shownLimit = 100;

    [MenuItem("City Highways/3D Objects/Procedural Object Catalog")]
    static void Open() => GetWindow<CityProceduralObjectCatalogWindow>("3D Object Catalog");

    void OnEnable()
    {
        LoadSnapshot();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;

    void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) LoadSnapshot();
        Repaint();
    }

    static Snapshot ReadLive()
    {
        var items = CityProceduralReplacementResolver.GetCatalogSnapshot();
        var city = CityProceduralReplacementResolver.CurrentCatalogMapName;
        if (items.Length == 0 || string.IsNullOrEmpty(city)) return null;

        var data = new Snapshot { city = city };
        foreach (var item in items)
            data.rows.Add(new SnapshotRow
            {
                id = item.Id,
                kind = (int)item.Kind,
                x = item.FieldPosition.x,
                y = item.FieldPosition.y
            });
        return data;
    }

    void SaveLiveSnapshot()
    {
        var live = ReadLive();
        if (live == null)
        {
            Debug.LogWarning("Start a 3D city before capturing the procedural object catalog.");
            return;
        }
        // Library is a local Unity cache, not a tracked game or map asset.
        File.WriteAllText(SnapshotFile, JsonUtility.ToJson(live));
        snapshot = live;
        Repaint();
        Debug.Log("Saved local 3D object catalog for " + live.city + " (" + live.rows.Count + " visuals).");
    }

    void LoadSnapshot()
    {
        snapshot = null;
        if (!File.Exists(SnapshotFile)) return;
        try
        {
            var loaded = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotFile));
            if (loaded != null && loaded.rows != null) snapshot = loaded;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unable to read local procedural catalog: " + e.Message);
        }
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Start a 3D game, then capture its generated-object catalog. " +
            "After exiting Play Mode, select a Procedural Object Replacement anchor and assign an ID from this list. " +
            "Snapshots are local and may become stale when maps, generation settings or quality change.",
            MessageType.Info);

        var live = EditorApplication.isPlaying ? ReadLive() : null;
        using (new EditorGUI.DisabledScope(live == null))
        {
            if (GUILayout.Button("Capture live 3D catalog for Edit Mode"))
                SaveLiveSnapshot();
        }

        var data = live ?? snapshot;
        if (data == null)
        {
            EditorGUILayout.HelpBox("No catalog yet. Open a matching 3D city in Play Mode and capture it.", MessageType.Warning);
            return;
        }

        GUILayout.Label((live != null ? "LIVE" : "SAVED") + "  " + data.city +
                        "  —  " + data.rows.Count + " visuals", EditorStyles.boldLabel);
        filter = EditorGUILayout.TextField("Find ID", filter);
        kindFilter = EditorGUILayout.Popup("Type", kindFilter, new[] { "All", "Buildings", "Trees" });
        shownLimit = EditorGUILayout.IntSlider("Maximum rows", shownLimit, 20, 300);

        var selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<CityProceduralReplacement>() : null;
        if (!EditorApplication.isPlaying && selected == null)
            EditorGUILayout.HelpBox("Select a Procedural Object Replacement in Hierarchy to enable Assign ID.", MessageType.None);

        int matching = 0, rendered = 0;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var item in data.rows)
        {
            if (kindFilter == 1 && item.kind != (int)CityProceduralReplacement.TargetKind.Building) continue;
            if (kindFilter == 2 && item.kind != (int)CityProceduralReplacement.TargetKind.Tree) continue;
            if (!string.IsNullOrEmpty(filter) && item.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            matching++;
            if (rendered++ >= shownLimit) continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(item.kind == 0 ? "B" : "T", GUILayout.Width(16));
            GUILayout.Label(item.id, GUILayout.MinWidth(150), GUILayout.ExpandWidth(true));
            GUILayout.Label("(" + item.x.ToString("F1") + ", " + item.y.ToString("F1") + ")", GUILayout.Width(112));

            if (GUILayout.Button("Copy", GUILayout.Width(43)))
                EditorGUIUtility.systemCopyBuffer = item.id;
            if (GUILayout.Button("Focus", GUILayout.Width(48)))
                FocusPosition(item);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || selected == null))
                if (GUILayout.Button("Assign", GUILayout.Width(52)))
                    AssignToAnchor(selected, data.city, item);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("Matching entries: " + matching +
                                   (matching > shownLimit ? " (displaying first " + shownLimit + ")" : ""));
    }

    static void FocusPosition(SnapshotRow entry)
    {
        var world = new Vector3(
            entry.x / CityPlanner.PixelsPerUnit - BoardLayout.WorldWidth * 0.5f,
            0f,
            BoardLayout.WorldDepth * 0.5f - entry.y / CityPlanner.PixelsPerUnit);
        var view = SceneView.lastActiveSceneView;
        if (view != null)
            view.Frame(new Bounds(world, Vector3.one * 0.6f), false);
    }

    static void AssignToAnchor(CityProceduralReplacement anchor, string city, SnapshotRow entry)
    {
        if (anchor == null || EditorApplication.isPlaying) return;
        Undo.RecordObject(anchor, "Assign procedural visual ID");
        var serialized = new SerializedObject(anchor);
        serialized.FindProperty("stableObjectId").stringValue = entry.id;
        serialized.FindProperty("mapName").stringValue = city;
        serialized.FindProperty("target").enumValueIndex = entry.kind;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(anchor);
        if (anchor.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        Selection.activeObject = anchor;
    }
}
