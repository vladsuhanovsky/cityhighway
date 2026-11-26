using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    bool showPrefabComparison;
    bool alignBuildingRotation;
    bool snapToCapturedTerrain;
    bool useBuildingBaseHeight;
    string selectedObjectId;
    sealed class AuditIssue
    {
        public CityProceduralReplacement anchor;
        public string problem;
        public CityProceduralAuditReport.Category category;
    }
    readonly List<AuditIssue> auditIssues = new List<AuditIssue>();
    bool auditWasRun;
    int auditReady;
    int auditSkipped;
    int auditCount;
    Vector2 auditScroll;
    CityProceduralAuditReport.Category auditCategory;
    bool groupIssuesByAnchor = true;
    readonly List<CityProceduralBatchFixPlanner.Proposal> batchPreview =
        new List<CityProceduralBatchFixPlanner.Proposal>();
    bool batchPreviewReady;
    const int MaxAuditRows = 150;
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
        auditWasRun = false;
        auditIssues.Clear();
        batchPreview.Clear();
        batchPreviewReady = false;
        Repaint();
        Debug.Log("Saved local 3D object catalog for " + live.city + " (" + live.rows.Count + " visuals).");
    }

    void LoadSnapshot()
    {
        snapshot = null;
        auditWasRun = false;
        auditIssues.Clear();
        batchPreview.Clear();
        batchPreviewReady = false;
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
        {
            showApproximateShapes = EditorGUILayout.ToggleLeft("Show approximate building/tree volumes", showApproximateShapes);
            showPrefabComparison = EditorGUILayout.ToggleLeft("Compare selected Prefab bounds in Scene View", showPrefabComparison);
        }
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
        DrawBatchAudit(data, live != null);

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


    /// <summary>Batch scan of authored anchors in loaded scenes against the saved catalog.</summary>
    void RunBatchAudit(Snapshot data)
    {
        auditIssues.Clear();
        batchPreview.Clear();
        batchPreviewReady = false;
        auditWasRun = true;
        auditReady = 0;
        auditSkipped = 0;
        auditCount = 0;

        var byId = new Dictionary<string, SnapshotRow>(StringComparer.Ordinal);
        foreach (var row in data.rows)
            if (!string.IsNullOrEmpty(row.id) && !byId.ContainsKey(row.id))
                byId.Add(row.id, row);

        var anchors = UnityEngine.Object.FindObjectsByType<CityProceduralReplacement>(
            FindObjectsInactive.Include);
        Array.Sort(anchors, (a, b) => string.CompareOrdinal(
            a.gameObject.name, b.gameObject.name));
        var currentCityIds = new List<string>();
        foreach (var candidate in anchors)
            if (candidate != null && candidate.gameObject.scene.IsValid() &&
                candidate.gameObject.scene.isLoaded && candidate.isActiveAndEnabled &&
                string.Equals(candidate.MapName, data.city, StringComparison.Ordinal))
                currentCityIds.Add(candidate.StableObjectId);
        var duplicateIds = CityProceduralAuditReport.DuplicateIds(currentCityIds);

        foreach (var anchor in anchors)
        {
            if (anchor == null || !anchor.gameObject.scene.IsValid() ||
                !anchor.gameObject.scene.isLoaded || !anchor.isActiveAndEnabled)
            {
                auditSkipped++;
                continue;
            }

            auditCount++;
            if (string.Equals(anchor.MapName, data.city, StringComparison.Ordinal) &&
                duplicateIds.Contains(anchor.StableObjectId))
            {
                auditIssues.Add(new AuditIssue { anchor = anchor,
                    problem = "Duplicate stable ID: " + anchor.StableObjectId +
                              " (multiple active anchors target the same object)",
                    category = CityProceduralAuditReport.Category.DuplicateId });
                continue;
            }
            SnapshotRow entry = null;
            if (!string.IsNullOrEmpty(anchor.StableObjectId))
                byId.TryGetValue(anchor.StableObjectId, out entry);
            var targetSize = entry != null
                ? new Vector3(entry.width, entry.height, entry.depth) : Vector3.zero;
            Vector3 sourceSize = Vector3.zero;
            bool geometryValid = entry != null && CityProceduralPrefabFitting.TryFit(
                anchor, targetSize, out sourceSize, out _);

            var status = CityProceduralBatchAuditRules.Classify(
                data.city, anchor.MapName, anchor.StableObjectId, anchor.VisualPrefab != null,
                entry != null, entry != null && entry.kind == (int)anchor.Target,
                geometryValid);
            if (status == CityProceduralBatchAuditRules.Status.SkippedOtherCity)
            {
                auditSkipped++;
                continue;
            }
            if (status != CityProceduralBatchAuditRules.Status.Ready)
            {
                auditIssues.Add(new AuditIssue { anchor = anchor, problem = status.ToString(),
                    category = status == CityProceduralBatchAuditRules.Status.InvalidGeometry
                        ? CityProceduralAuditReport.Category.Geometry
                        : CityProceduralAuditReport.Category.Configuration });
                continue;
            }

            if (!entry.hasTerrainY)
            {
                auditIssues.Add(new AuditIssue { anchor = anchor,
                    problem = "Missing captured terrain height (recapture catalog)",
                    category = CityProceduralAuditReport.Category.MissingTerrain });
                continue;
            }

            if (!CityProceduralPrefabFitting.TryWorldLowestY(anchor, out var lowestY) ||
                !CityProceduralPlacementDiagnostics.TryEvaluate(
                    sourceSize, targetSize, anchor.transform.position, TargetPosition(entry),
                    lowestY, entry.terrainY, out var diagnostics))
            {
                auditIssues.Add(new AuditIssue { anchor = anchor, problem = "Invalid measurements",
                    category = CityProceduralAuditReport.Category.Geometry });
                continue;
            }
            bool hasWarnings = false;
            if (diagnostics.Oversized)
            {
                hasWarnings = true;
                auditIssues.Add(new AuditIssue { anchor = anchor,
                    problem = "Oversized: " + diagnostics.MaximumSizeRatio.ToString("F2") + "x",
                    category = CityProceduralAuditReport.Category.Oversized });
            }
            if (diagnostics.Offset)
            {
                hasWarnings = true;
                auditIssues.Add(new AuditIssue { anchor = anchor,
                    problem = "Horizontal offset: " + diagnostics.HorizontalOffset.ToString("F3"),
                    category = CityProceduralAuditReport.Category.Offset });
            }
            if (diagnostics.BelowGround)
            {
                hasWarnings = true;
                auditIssues.Add(new AuditIssue { anchor = anchor,
                    problem = "Possible ground intersection: " +
                        diagnostics.GroundPenetration.ToString("F3"),
                    category = CityProceduralAuditReport.Category.BelowGround });
            }
            if (!hasWarnings) auditReady++;
        }
    }

    void DrawBatchAudit(Snapshot data, bool liveCatalog)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch replacement diagnostics", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || liveCatalog))
        {
            if (GUILayout.Button("Scan all replacement anchors in loaded scenes"))
                RunBatchAudit(data);
        }
        if (liveCatalog)
            EditorGUILayout.HelpBox("Exit Play Mode to scan scene-authored anchors.", MessageType.None);
        if (!auditWasRun) return;

        EditorGUILayout.LabelField("Scanned: " + auditCount + "    Clean: " + auditReady +
            "    Skipped (inactive/other city): " + auditSkipped +
            "    Issues: " + auditIssues.Count);
        EditorGUILayout.HelpBox(
            "Snapshot-based results. Scan again after editing transforms, Prefabs or the scene. " +
            "Each warning is listed independently; a single anchor can appear several times.",
            MessageType.None);

        var nextCategory = (CityProceduralAuditReport.Category)EditorGUILayout.EnumPopup(
            "Issue type", auditCategory);
        if (nextCategory != auditCategory)
        {
            auditCategory = nextCategory;
            batchPreview.Clear();
            batchPreviewReady = false;
        }
        groupIssuesByAnchor = EditorGUILayout.ToggleLeft("Group issues by anchor", groupIssuesByAnchor);
        var filtered = new List<AuditIssue>();
        foreach (var issue in auditIssues)
            if (CityProceduralAuditReport.Matches(auditCategory, issue.category))
                filtered.Add(issue);
        EditorGUILayout.LabelField("Matching issues: " + filtered.Count);
        DrawBatchFixPreview(data, filtered);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || filtered.Count == 0))
        {
            if (GUILayout.Button("Export filtered issues as CSV..."))
                ExportBatchAuditCsv(data.city, filtered);
        }

        var display = new List<AuditIssue>();
        if (groupIssuesByAnchor)
        {
            var grouped = new Dictionary<CityProceduralReplacement, AuditIssue>();
            foreach (var issue in filtered)
            {
                if (issue.anchor == null) continue;
                if (grouped.TryGetValue(issue.anchor, out var existing))
                    existing.problem += "; " + issue.problem;
                else
                    grouped.Add(issue.anchor, new AuditIssue
                    {
                        anchor = issue.anchor, category = issue.category, problem = issue.problem
                    });
            }
            display.AddRange(grouped.Values);
            display.Sort((a, b) => string.CompareOrdinal(a.anchor.name, b.anchor.name));
        }
        else display.AddRange(filtered);

        int shown = Mathf.Min(MaxAuditRows, display.Count);
        auditScroll = EditorGUILayout.BeginScrollView(auditScroll, GUILayout.MaxHeight(210f));
        for (int i = 0; i < shown; i++)
        {
            var issue = display[i];
            if (issue.anchor == null) continue;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(issue.anchor.name + " — " + issue.problem, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Select", GUILayout.Width(55)))
                Selection.activeGameObject = issue.anchor.gameObject;
            if (GUILayout.Button("Focus", GUILayout.Width(50)))
            {
                Selection.activeGameObject = issue.anchor.gameObject;
                var view = SceneView.lastActiveSceneView;
                if (view != null)
                    view.Frame(new Bounds(issue.anchor.transform.position, Vector3.one * 0.6f), false);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (display.Count > shown)
            EditorGUILayout.LabelField("Displaying first " + shown +
                " objects/issues; export CSV or filter the report for all details.");
    }

    void CreateBatchFixPreview(Snapshot data, List<AuditIssue> filtered)
    {
        batchPreview.Clear();
        batchPreviewReady = true;

        var catalog = new Dictionary<string, SnapshotRow>(StringComparer.Ordinal);
        foreach (var row in data.rows)
            if (!string.IsNullOrEmpty(row.id) && !catalog.ContainsKey(row.id))
                catalog.Add(row.id, row);

        var unsafeAnchors = new HashSet<CityProceduralReplacement>();
        foreach (var issue in auditIssues)
            if (issue.anchor != null && issue.category == CityProceduralAuditReport.Category.DuplicateId)
                unsafeAnchors.Add(issue.anchor);

        var flags = new Dictionary<CityProceduralReplacement, int>();
        foreach (var issue in filtered)
        {
            if (issue.anchor == null) continue;
            int bit = issue.category == CityProceduralAuditReport.Category.Offset ? 1
                : issue.category == CityProceduralAuditReport.Category.Oversized ? 2
                : issue.category == CityProceduralAuditReport.Category.BelowGround ? 4 : 0;
            if (bit == 0) continue; // Configuration and missing measurements need manual repair.
            if (!flags.ContainsKey(issue.anchor)) flags[issue.anchor] = 0;
            flags[issue.anchor] |= bit;
        }

        foreach (var pair in flags)
        {
            var anchor = pair.Key;
            if (anchor == null || unsafeAnchors.Contains(anchor) ||
                !anchor.isActiveAndEnabled ||
                !string.Equals(anchor.MapName, data.city, StringComparison.Ordinal) ||
                !catalog.TryGetValue(anchor.StableObjectId, out var entry) ||
                entry.kind != (int)anchor.Target || !entry.hasTerrainY) continue;
            if (CityProceduralBatchFixPlanner.TryPlan(anchor, TargetPosition(entry),
                new Vector3(entry.width, entry.height, entry.depth), entry.terrainY,
                (pair.Value & 1) != 0, (pair.Value & 2) != 0, (pair.Value & 4) != 0,
                out var proposal))
                batchPreview.Add(proposal);
        }
        batchPreview.Sort((a, b) => string.CompareOrdinal(a.Anchor.name, b.Anchor.name));
    }

    void DrawBatchFixPreview(Snapshot data, List<AuditIssue> filtered)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch quick fixes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Preview only the selected issue category. Only Offset, Oversized and BelowGround " +
            "can be corrected automatically. Other issues require manual changes. " +
            "Nothing is modified until Apply is confirmed.", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || filtered.Count == 0))
        {
            if (GUILayout.Button("Preview fixes for filtered issues"))
                CreateBatchFixPreview(data, filtered);
        }
        if (!batchPreviewReady) return;
        EditorGUILayout.LabelField("Proposed anchor changes: " + batchPreview.Count);
        int visible = Mathf.Min(15, batchPreview.Count);
        for (int i = 0; i < visible; i++)
        {
            var item = batchPreview[i];
            if (item.Anchor == null) continue;
            EditorGUILayout.LabelField(item.Anchor.name + ": position " +
                item.BeforePosition.ToString("F3") + " → " + item.AfterPosition.ToString("F3") +
                "; scale " + item.BeforeScale.ToString("F3") + " → " +
                item.AfterScale.ToString("F3"));
        }
        if (batchPreview.Count > visible)
            EditorGUILayout.LabelField("... plus " + (batchPreview.Count - visible) + " objects");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || batchPreview.Count == 0))
        {
            if (GUILayout.Button("Apply previewed fixes (single Undo group)"))
            {
                if (EditorUtility.DisplayDialog("Apply batch Prefab fixes?",
                    "Apply " + batchPreview.Count + " previewed Transform corrections? " +
                    "The operation supports Undo and never modifies original Prefab assets.",
                    "Apply", "Cancel"))
                {
                    int applied = CityProceduralBatchFixPlanner.Apply(batchPreview);
                    if (applied == 0)
                        EditorUtility.DisplayDialog("Nothing applied",
                            "An anchor changed since preview or is no longer valid. " +
                            "Run Scan and Preview again.", "OK");
                    batchPreview.Clear();
                    batchPreviewReady = false;
                    if (applied > 0) RunBatchAudit(data);
                }
            }
        }
    }

    static void ExportBatchAuditCsv(string city, List<AuditIssue> filtered)
    {
        var path = EditorUtility.SaveFilePanel(
            "Export procedural replacement audit", "", "CityHighwaysReplacementAudit.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var csv = new StringBuilder();
        csv.AppendLine(CityProceduralAuditReport.CsvRow(
            "City", "Scene", "Anchor", "TargetKind", "StableObjectId",
            "IssueCategory", "Description", "WorldX", "WorldY", "WorldZ"));
        foreach (var issue in filtered)
        {
            if (issue.anchor == null) continue;
            var anchor = issue.anchor;
            var position = anchor.transform.position;
            csv.AppendLine(CityProceduralAuditReport.CsvRow(
                city, anchor.gameObject.scene.path, anchor.name, anchor.Target.ToString(),
                anchor.StableObjectId, issue.category.ToString(), issue.problem,
                position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                position.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }
        try
        {
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
            Debug.Log("Exported procedural replacement audit to " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Could not export procedural replacement audit: " + e.Message);
            EditorUtility.DisplayDialog("CSV export failed", e.Message, "OK");
        }
    }

    /// <summary>Compare a selected catalog volume to the assigned Prefab's static meshes.</summary>
    void DrawPrefabComparison(Snapshot data, CityProceduralReplacement anchor)
    {
        if (anchor == null) return;
        string id = !string.IsNullOrEmpty(anchor.StableObjectId) ? anchor.StableObjectId : selectedObjectId;
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
        var targetWorld = TargetPosition(match);
        if (CityProceduralFitMath.TryCompare(sourceSize, targetSize,
            anchor.transform.position, targetWorld, out var ratios, out var offset))
        {
            EditorGUILayout.LabelField("Prefab / target (W/H/D)",
                ratios.x.ToString("F2") + "x / " + ratios.y.ToString("F2") + "x / " + ratios.z.ToString("F2") + "x");
            EditorGUILayout.LabelField("Anchor offset from generated center",
                offset.ToString("F3") + " world units");
        }

        if (match.hasTerrainY &&
            CityProceduralPrefabFitting.TryWorldLowestY(anchor, out var lowestY) &&
            CityProceduralPlacementDiagnostics.TryEvaluate(
                sourceSize, targetSize, anchor.transform.position, targetWorld,
                lowestY, match.terrainY, out var diagnostics))
        {
            EditorGUILayout.LabelField("Placement diagnostics", EditorStyles.boldLabel);
            if (!diagnostics.Oversized && !diagnostics.Offset && !diagnostics.BelowGround)
                EditorGUILayout.HelpBox("No placement warnings at current thresholds.", MessageType.Info);
            if (diagnostics.Oversized)
            {
                EditorGUILayout.HelpBox("Oversized: largest dimension is " +
                    diagnostics.MaximumSizeRatio.ToString("F2") +
                    "x the generated target (warning threshold 1.15x).", MessageType.Warning);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("Fix oversized Prefab: uniform fit"))
                        CityProceduralPrefabFitting.ApplyUniformFit(anchor, targetSize);
            }
            if (diagnostics.Offset)
            {
                EditorGUILayout.HelpBox("Horizontal displacement: " +
                    diagnostics.HorizontalOffset.ToString("F3") +
                    " world units from generated center.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("Fix horizontal offset: align X/Z"))
                        CityProceduralPlacementQuickFixes.AlignHorizontal(anchor, targetWorld);
            }
            if (diagnostics.BelowGround)
            {
                EditorGUILayout.HelpBox("Possible ground intersection: lower Prefab bounds are " +
                    diagnostics.GroundPenetration.ToString("F3") +
                    " world units below sampled ground at target center.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("Fix possible ground intersection: raise Y"))
                        CityProceduralPlacementQuickFixes.RaiseAboveGround(anchor, match.terrainY);
            }
        }
        else if (!match.hasTerrainY)
            EditorGUILayout.HelpBox(
                "Placement diagnostics need terrain data. Capture a fresh 3D catalog in Play Mode.",
                MessageType.Info);

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
        if (showPrefabComparison)
            DrawSelectedPrefabComparison(data);
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

    static Vector3 TargetPosition(SnapshotRow entry)
    {
        float height = entry.hasBuildingBaseY
            ? entry.buildingBaseY
            : entry.hasTerrainY ? entry.terrainY : 0f;
        return CityPlanner.ToFlatWorld(new Vector2(entry.x, entry.y), height);
    }

    /// <summary>
    /// Compare two oriented bounding boxes in Edit Mode without creating GameObjects.
    /// Target is the catalog's simplified volume; Prefab is its aggregated local mesh bounds.
    /// </summary>
    void DrawSelectedPrefabComparison(Snapshot data)
    {
        var selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<CityProceduralReplacement>() : null;
        if (selected == null || selected.VisualPrefab == null ||
            string.IsNullOrEmpty(selected.StableObjectId) ||
            !string.Equals(selected.MapName, data.city, StringComparison.Ordinal)) return;

        SnapshotRow entry = null;
        foreach (var row in data.rows)
            if (row.id == selected.StableObjectId && row.kind == (int)selected.Target)
            {
                entry = row;
                break;
            }
        if (entry == null || entry.width <= 0f || entry.depth <= 0f || entry.height <= 0f ||
            !CityProceduralPrefabFitting.TryLocalBounds(selected.VisualPrefab, out var prefabBounds)) return;

        var oldMatrix = Handles.matrix;
        var oldColor = Handles.color;

        var targetPosition = TargetPosition(entry);
        Handles.matrix = Matrix4x4.TRS(targetPosition,
            Quaternion.Euler(0f, entry.angle, 0f), Vector3.one);
        Handles.color = new Color(0.15f, 0.8f, 1f, 0.95f);
        Handles.DrawWireCube(new Vector3(0f, entry.height * 0.5f, 0f),
            new Vector3(entry.width, entry.height, entry.depth));

        // Preserve mesh offsets, internal pivot and author-set TRS for preview.
        Handles.matrix = selected.transform.localToWorldMatrix;
        Handles.color = new Color(1f, 0.55f, 0.12f, 0.95f);
        Handles.DrawWireCube(prefabBounds.center, prefabBounds.size);

        Handles.matrix = Matrix4x4.identity;
        Handles.color = new Color(1f, 0.95f, 0.2f, 0.85f);
        Handles.DrawDottedLine(targetPosition, selected.transform.position, 3f);

        Handles.matrix = oldMatrix;
        Handles.color = oldColor;
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
