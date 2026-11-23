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
        public float width;
        public float depth;
        public float height;
        public float angle;
        public float terrainY;
        public bool hasTerrainY;
        public float buildingBaseY;
        public bool hasBuildingBaseY;
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
    bool showSceneMarkers;
    bool showApproximateShapes;
    bool alignBuildingRotation;
    bool snapToCapturedTerrain;
    bool useBuildingBaseHeight;
    string selectedObjectId;
    const float SceneMarkerReach = 2.5f;
    const int MaxSceneMarkers = 120;

    [MenuItem("City Highways/3D Objects/Procedural Object Catalog")]
    static void Open() => GetWindow<CityProceduralObjectCatalogWindow>("3D Object Catalog");

    void OnEnable()
    {
        LoadSnapshot();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        SceneView.duringSceneGui += DrawSceneMarkers;
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SceneView.duringSceneGui -= DrawSceneMarkers;
    }

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
                y = item.FieldPosition.y,
                width = item.Width,
                depth = item.Depth,
                height = item.Height,
                angle = item.Angle,
                terrainY = item.TerrainY,
                hasTerrainY = true,
                buildingBaseY = item.BuildingBaseY,
                hasBuildingBaseY = item.Kind == CityProceduralReplacement.TargetKind.Building
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
        var selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<CityProceduralReplacement>() : null;
        filter = EditorGUILayout.TextField("Find ID", filter);
        kindFilter = EditorGUILayout.Popup("Type", kindFilter, new[] { "All", "Buildings", "Trees" });
        shownLimit = EditorGUILayout.IntSlider("Maximum rows", shownLimit, 20, 300);
        showSceneMarkers = EditorGUILayout.ToggleLeft("Show clickable markers in Scene View", showSceneMarkers);
        using (new EditorGUI.DisabledScope(!showSceneMarkers))
            showApproximateShapes = EditorGUILayout.ToggleLeft("Show approximate building/tree volumes", showApproximateShapes);
        if (showSceneMarkers)
            EditorGUILayout.HelpBox(
                "After capturing a 3D catalog, Scene View shows clickable markers in Edit Mode. " +
                "Only entries within 2.5 world units of its pivot (maximum 120) are displayed. " +
                "Use Focus on a row to navigate elsewhere. Click a marker to select its ID; " +
                "then select a replacement anchor in Hierarchy and click Assign selected marker.",
                MessageType.None);
        if (showSceneMarkers && !string.IsNullOrEmpty(selectedObjectId))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(selectedObjectId, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || selected == null))
            {
                if (GUILayout.Button("Assign selected marker", GUILayout.Width(165)))
                {
                    foreach (var row in data.rows)
                        if (row.id == selectedObjectId)
                        {
                            AssignToAnchor(selected, data.city, row);
                            break;
                        }
                }
                if (GUILayout.Button("Assign + Place", GUILayout.Width(110)))
                {
                    foreach (var row in data.rows)
                        if (row.id == selectedObjectId)
                        {
                            AssignAndPlace(selected, data.city, row, alignBuildingRotation,
                                snapToCapturedTerrain, useBuildingBaseHeight);
                            break;
                        }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        alignBuildingRotation = EditorGUILayout.ToggleLeft("Match building yaw when placing anchor", alignBuildingRotation);
        snapToCapturedTerrain = EditorGUILayout.ToggleLeft("Snap Y to captured terrain height", snapToCapturedTerrain);
        using (new EditorGUI.DisabledScope(!snapToCapturedTerrain))
            useBuildingBaseHeight = EditorGUILayout.ToggleLeft("Use highest building footprint corner for Y", useBuildingBaseHeight);
        if (!EditorApplication.isPlaying && selected == null)
            EditorGUILayout.HelpBox("Select a Procedural Object Replacement in Hierarchy to enable assignment or placement.", MessageType.None);

        DrawPrefabComparison(data, selected);

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
            {
                selectedObjectId = item.id;
                FocusPosition(item);
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || selected == null))
            {
                if (GUILayout.Button("Assign", GUILayout.Width(52)))
                {
                    selectedObjectId = item.id;
                    AssignToAnchor(selected, data.city, item);
                }
                if (GUILayout.Button("Place", GUILayout.Width(48)))
                {
                    selectedObjectId = item.id;
                    AssignAndPlace(selected, data.city, item, alignBuildingRotation,
                        snapToCapturedTerrain, useBuildingBaseHeight);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("Matching entries: " + matching +
                                   (matching > shownLimit ? " (displaying first " + shownLimit + ")" : ""));
    }


    /// <summary>Compare a selected catalog volume to the assigned Prefab's static meshes.</summary>
    void DrawPrefabComparison(Snapshot data, CityProceduralReplacement anchor)
    {
        if (anchor == null) return;
        string id = !string.IsNullOrEmpty(selectedObjectId) ? selectedObjectId : anchor.StableObjectId;
        SnapshotRow match = null;
        foreach (var row in data.rows)
            if (row.id == id) { match = row; break; }
        if (match == null || match.kind != (int)anchor.Target) return;
        if (!string.Equals(data.city, anchor.MapName, StringComparison.Ordinal)) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab size comparison", EditorStyles.boldLabel);
        var targetSize = new Vector3(match.width, match.height, match.depth);
        EditorGUILayout.LabelField("Generated target (W/H/D, meters)",
            targetSize.x.ToString("F3") + " / " + targetSize.y.ToString("F3") + " / " + targetSize.z.ToString("F3"));

        if (!CityProceduralPrefabFitting.TryFit(anchor, targetSize, out var sourceSize, out var factor))
        {
            EditorGUILayout.HelpBox(
                "Assign a visual Prefab with nonzero 3D mesh bounds to compare sizes. " +
                "An empty or flat mesh cannot be fitted.",
                MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("Assigned Prefab (W/H/D, meters)",
            sourceSize.x.ToString("F3") + " / " + sourceSize.y.ToString("F3") + " / " + sourceSize.z.ToString("F3"));
        EditorGUILayout.LabelField("Uniform fit multiplier", factor.ToString("F3") + "x");

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Fit Prefab scale to generated volume (uniform)"))
                CityProceduralPrefabFitting.ApplyUniformFit(anchor, targetSize);
        }
        EditorGUILayout.HelpBox(
            "Uniform scaling preserves proportions and fits all three bounding dimensions. " +
            "It may leave empty space, and cannot fix a different pivot or model orientation.",
            MessageType.None);
    }

    static Vector3 ToScenePosition(SnapshotRow row)
    {
        return CityPlanner.ToFlatWorld(new Vector2(row.x, row.y),
            row.hasTerrainY ? row.terrainY : 0f);
    }

    void DrawSceneMarkers(SceneView scene)
    {
        // Draw from the saved local snapshot in Edit Mode; no live catalog allocations per frame.
        if (!showSceneMarkers || EditorApplication.isPlaying || scene == null || scene.camera == null) return;
        var data = snapshot;
        if (data == null) return;

        // Avoid thousands of editor handles: only show objects near the Scene View pivot.
        var near = new List<SnapshotRow>();
        float distanceLimit = SceneMarkerReach * SceneMarkerReach;
        Vector3 pivot = scene.pivot;
        foreach (var row in data.rows)
        {
            if (kindFilter == 1 && row.kind != (int)CityProceduralReplacement.TargetKind.Building) continue;
            if (kindFilter == 2 && row.kind != (int)CityProceduralReplacement.TargetKind.Tree) continue;
            if (!string.IsNullOrEmpty(filter) && row.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var point = ToScenePosition(row);
            float dx = point.x - pivot.x, dz = point.z - pivot.z;
            if (dx * dx + dz * dz <= distanceLimit) near.Add(row);
        }
        near.Sort((a, b) =>
        {
            var pa = ToScenePosition(a);
            var pb = ToScenePosition(b);
            float da = (pa.x - pivot.x) * (pa.x - pivot.x) + (pa.z - pivot.z) * (pa.z - pivot.z);
            float db = (pb.x - pivot.x) * (pb.x - pivot.x) + (pb.z - pivot.z) * (pb.z - pivot.z);
            int cmp = da.CompareTo(db);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.id, b.id);
        });

        int count = Mathf.Min(MaxSceneMarkers, near.Count);
        for (int i = 0; i < count; i++)
        {
            var row = near[i];
            var position = ToScenePosition(row);
            bool active = row.id == selectedObjectId;
            Handles.color = active ? Color.yellow :
                row.kind == (int)CityProceduralReplacement.TargetKind.Building
                    ? new Color(0.15f, 0.75f, 1f, 0.9f)
                    : new Color(0.25f, 0.95f, 0.5f, 0.9f);

            float size = Mathf.Min(0.085f, HandleUtility.GetHandleSize(position) * 0.06f);
            if (Handles.Button(position, Quaternion.identity, size, size * 1.4f, Handles.DotHandleCap))
            {
                selectedObjectId = row.id;
                Repaint();
            }
            if (showApproximateShapes)
                DrawApproximateShape(row, position, active);
            if (active)
            {
                Handles.color = Color.white;
                Handles.Label(position + Vector3.up * 0.12f, row.id);
            }
        }
        Handles.color = Color.white;
    }

    /// <summary>
    /// Editor-only simplified visual volumes. These are not production mesh geometry.
    /// Catalog snapshots created before dimensions were introduced simply draw markers.
    /// </summary>
    static void DrawApproximateShape(SnapshotRow row, Vector3 position, bool active)
    {
        if (row.width <= 0f || row.depth <= 0f || row.height <= 0f) return;
        var previous = Handles.matrix;
        var previousColor = Handles.color;
        Handles.color = active ? new Color(1f, 0.85f, 0.2f, 0.95f) :
            row.kind == (int)CityProceduralReplacement.TargetKind.Building
                ? new Color(0.2f, 0.7f, 1f, 0.6f)
                : new Color(0.25f, 0.9f, 0.45f, 0.6f);
        if (row.kind == (int)CityProceduralReplacement.TargetKind.Building)
        {
            Handles.matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, row.angle, 0f), Vector3.one);
            Handles.DrawWireCube(new Vector3(0f, row.height * 0.5f, 0f),
                new Vector3(row.width, row.height, row.depth));
        }
        else
        {
            // Trees use a coarse vertical guide, not the actual procedural canopy.
            Handles.matrix = Matrix4x4.identity;
            var center = position + Vector3.up * row.height * 0.5f;
            Handles.DrawWireDisc(center, Vector3.up, row.width * 0.5f);
            Handles.DrawLine(position, position + Vector3.up * row.height);
        }
        Handles.matrix = previous;
        Handles.color = previousColor;
    }

    static void FocusPosition(SnapshotRow entry)
    {
        var world = ToScenePosition(entry);
        var view = SceneView.lastActiveSceneView;
        if (view != null)
            view.Frame(new Bounds(world, Vector3.one * 0.6f), false);
    }

    static void AssignAndPlace(CityProceduralReplacement anchor, string city, SnapshotRow entry,
        bool alignBuildingRotation, bool useCapturedTerrainHeight, bool useBuildingBaseHeight)
    {
        if (anchor == null || EditorApplication.isPlaying) return;
        // Assign is recorded separately from placement; both operations remain undoable.
        AssignToAnchor(anchor, city, entry);
        CityProceduralAnchorPlacement.Place(anchor, new Vector2(entry.x, entry.y), entry.angle,
            alignBuildingRotation && entry.kind == (int)CityProceduralReplacement.TargetKind.Building,
            useBuildingBaseHeight && entry.hasBuildingBaseY ? entry.buildingBaseY : entry.terrainY,
            useCapturedTerrainHeight,
            useBuildingBaseHeight && entry.hasBuildingBaseY || entry.hasTerrainY);
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
