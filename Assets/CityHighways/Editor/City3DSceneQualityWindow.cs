using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// On-demand approximate 3D scene quality report for authored decorations and replacements.
/// Uses mesh bounds, not exact procedural geometry or runtime profiling.
/// </summary>
public sealed class City3DSceneQualityWindow : EditorWindow
{
    sealed class Visual
    {
        public Component Anchor;
        public Bounds WorldBounds;
        public int Meshes;
        public long Triangles;
    }

    string city = "Москва";
    float minimumOverlap = 0.01f;
    bool showPotentialOverlaps = true;
    bool hasResults;
    int invalidVisuals;
    int skippedVisuals;
    CitySceneQualityMetrics.Summary summary;
    readonly List<Visual> visuals = new List<Visual>();
    readonly List<string> overlaps = new List<string>();
    Vector2 scroll;
    Vector2 runtimeScroll;
    City3DGeometryAudit.Entry[] runtimeEntries = new City3DGeometryAudit.Entry[0];
    City3DGeometryAudit.Totals runtimeTotals;
    string runtimeCity;
    int runtimeBuildingCount;
    int runtimeTreeCount;
    bool runtimeCaptured;
    int runtimeLimit = 20;
    long totalTriangleBudget = 300000;
    long groupTriangleBudget = 50000;
    bool baselineCaptured;
    string baselineCity;
    City3DGeometryAudit.Totals baselineTotals;
    City3DGeometryAudit.Entry[] baselineEntries = new City3DGeometryAudit.Entry[0];
    bool frameCaptured, frameBaselineCaptured;
    int lowTargetFps = 30, mediumTargetFps = 45, highTargetFps = 60;
    string frameCity, frameBaselineCity;
    int frameQuality, frameBaselineQuality;
    City3DFrameMonitor.Snapshot frameSnapshot, frameBaseline;


    [MenuItem("City Highways/3D Objects/Scene Quality Audit")]
    static void Open() => GetWindow<City3DSceneQualityWindow>("Scene Quality");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "On-demand approximation of artist-authored Prefabs in loaded scenes. " +
            "Generated buildings, road meshes, draw calls, GPU time and actual collisions are NOT measured.",
            MessageType.Info);
        city = EditorGUILayout.TextField("City map name", city);
        minimumOverlap = EditorGUILayout.Slider("Minimum overlap (world units)", minimumOverlap, 0f, 0.15f);
        showPotentialOverlaps = EditorGUILayout.ToggleLeft("List potential bounds overlaps", showPotentialOverlaps);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || string.IsNullOrEmpty(city)))
        {
            if (GUILayout.Button("Analyze authored 3D visuals"))
                Analyze();
        }
        DrawRuntimeGeometry();
        DrawFrameStatistics();
        if (!hasResults) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visuals: " + summary.Visuals +
            "    Mesh instances: " + summary.Meshes +
            "    Estimated triangles: " + summary.Triangles, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Area: " + summary.FootprintArea.ToString("F2") +
            " sq. world units    Density: " + summary.AreaDensity.ToString("F2") +
            " visuals/sq. unit");
        EditorGUILayout.LabelField("Potential bounds overlaps: " + summary.PossibleOverlaps +
            "    Invalid/missing meshes: " + invalidVisuals +
            "    Skipped (inactive/other city): " + skippedVisuals);
        EditorGUILayout.HelpBox(
            "Density is authored prefab anchors per 14 × 7.8 world-unit city footprint. " +
            "Triangle count is a static mesh estimate, NOT an FPS or draw-call measurement. " +
            "Results are snapshots: run Analyze again after edits.",
            MessageType.None);
        if (!showPotentialOverlaps) return;
        EditorGUILayout.LabelField("Possible intersections (first " + overlaps.Count + " pairs)",
            EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(240f));
        foreach (var issue in overlaps)
            EditorGUILayout.LabelField(issue);
        EditorGUILayout.EndScrollView();
    }

    void DrawRuntimeGeometry()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generated 3D mesh audit (Play Mode)", EditorStyles.boldLabel);
        var active = City3DScene.ActiveScene;
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying ||
            active == null || active.GeneratedRoot == null))
        {
            if (GUILayout.Button("Capture actual generated mesh geometry"))
            {
                runtimeEntries = City3DGeometryAudit.Capture(active.GeneratedRoot, out runtimeTotals);
                runtimeCity = active.CityName;
                runtimeBuildingCount = active.BuildingCount;
                runtimeTreeCount = active.TreeCount;
                runtimeCaptured = true;
            }
        }
        if (!runtimeCaptured)
        {
            EditorGUILayout.HelpBox(
                "Start a 3D city in Play Mode and click Capture to analyze actual generated mesh batches. " +
                "This is not a benchmark or per-object profiling.", MessageType.Info);
            return;
        }
        GUILayout.Label("Captured: " + runtimeCity + " (snapshot; recapture after changes)",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Generated buildings: " + runtimeBuildingCount +
            "    Trees: " + runtimeTreeCount +
            "    District mesh groups: " + runtimeTotals.Buildings +
            "    Tree mesh groups: " + runtimeTotals.Trees);
        EditorGUILayout.LabelField("All generated-root mesh groups: " + runtimeEntries.Length +
            "    Mesh instances: " + runtimeTotals.Meshes);
        EditorGUILayout.LabelField("Total mesh vertices: " + runtimeTotals.Vertices +
            "    Triangle indices / 3: " + runtimeTotals.Triangles);
        EditorGUILayout.LabelField("District buildings: " + runtimeTotals.BuildingTriangles +
            "    Tree chunks: " + runtimeTotals.TreeTriangles + " triangles");
        EditorGUILayout.LabelField("Authored replacements/decor: " + runtimeTotals.AuthoredTriangles +
            "    Terrain/roads/other: " + runtimeTotals.OtherTriangles + " triangles");
        EditorGUILayout.HelpBox(
            "Real mesh counts from the generated 3D root, including roads, terrain, authored and " +
            "replacement visuals. The ranking is by combined mesh group (district/chunk/other), " +
            "not by individual procedural building or tree. Triangle counts include meshes in " +
            "inactive child GameObjects and reflect the capture instant. " +
            "This is NOT FPS, draw calls or visible-triangle cost.",
            MessageType.None);
        DrawGeometryBudgetsAndComparison();
        runtimeLimit = EditorGUILayout.IntSlider("Top geometry groups", runtimeLimit, 5, 100);
        int count = Mathf.Min(runtimeLimit, runtimeEntries.Length);
        runtimeScroll = EditorGUILayout.BeginScrollView(runtimeScroll, GUILayout.MaxHeight(270f));
        for (int i = 0; i < count; i++)
        {
            var entry = runtimeEntries[i];
            string warning = City3DGeometryComparison.ExceedsBudget(
                entry.Triangles, groupTriangleBudget) ? " [OVER BUDGET]" : "";
            EditorGUILayout.LabelField((i + 1) + ". [" + entry.Kind + "] " + entry.Name +
                " — " + entry.Triangles + " triangles, " +
                entry.Vertices + " vertices, " + entry.MeshCount + " meshes" + warning);
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawFrameStatistics()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3D frame intervals (Play Mode)", EditorStyles.boldLabel);
        var active = City3DScene.ActiveScene;
        var monitor = active != null ? active.FrameMonitor : null;
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying ||
            monitor == null))
        {
            if (GUILayout.Button("Capture recent frame statistics"))
            {
                frameSnapshot = monitor.Capture();
                frameCity = active.CityName;
                frameQuality = active.GraphicsQuality;
                frameCaptured = true;
            }
            if (GUILayout.Button("Reset frame sampling window"))
            {
                monitor.ResetSamples();
                frameCaptured = false;
            }
        }
        if (!frameCaptured)
        {
            EditorGUILayout.HelpBox(
                "Start a 3D city in Play Mode, let it settle, then capture recent frames. " +
                "Sampling starts after a 2-second warmup, uses up to 600 recent frame intervals.",
                MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("Snapshot: " + frameCity +
            "    Quality: " + QualityName(frameQuality) +
            "    Samples: " + frameSnapshot.Frames, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Advisory frame targets (not guaranteed hardware performance)", EditorStyles.boldLabel);
        lowTargetFps = Mathf.Max(0, EditorGUILayout.IntField("Low target FPS", lowTargetFps));
        mediumTargetFps = Mathf.Max(0, EditorGUILayout.IntField("Medium target FPS", mediumTargetFps));
        highTargetFps = Mathf.Max(0, EditorGUILayout.IntField("High target FPS", highTargetFps));
        var frameTarget = City3DFrameBudget.TargetForQuality(frameQuality,
            lowTargetFps, mediumTargetFps, highTargetFps);
        var frameResult = City3DFrameBudget.Evaluate(frameSnapshot, frameTarget);
        if (frameResult == City3DFrameBudget.Result.InsufficientSamples)
            EditorGUILayout.HelpBox("At least 60 valid frames are needed for a budget result.",
                MessageType.Info);
        else if (frameResult == City3DFrameBudget.Result.OverBudget)
            EditorGUILayout.HelpBox("P95 frame time " + frameSnapshot.P95Ms.ToString("F2") +
                " ms exceeds the " + City3DFrameBudget.LimitMs(frameTarget).ToString("F2") +
                " ms limit for target " + frameTarget + " FPS.", MessageType.Warning);
        else if (frameResult == City3DFrameBudget.Result.Pass)
            EditorGUILayout.LabelField("P95 budget status: PASS (" + frameTarget + " FPS target)");
        else
            EditorGUILayout.LabelField("P95 budget status: Disabled (target 0)");
        if (frameSnapshot.Frames > 0)
        {
            EditorGUILayout.LabelField("Estimated FPS: " +
                frameSnapshot.EstimatedFps.ToString("F1") +
                "    Approx. 1% low: " + frameSnapshot.OnePercentLowFps.ToString("F1"));
            EditorGUILayout.LabelField("Average frame: " +
                frameSnapshot.AverageMs.ToString("F2") +
                " ms    P95: " + frameSnapshot.P95Ms.ToString("F2") +
                " ms    P99: " + frameSnapshot.P99Ms.ToString("F2") +
                " ms    Max: " + frameSnapshot.MaxMs.ToString("F2") + " ms");
        }
        EditorGUILayout.HelpBox(
            "FPS is estimated from Time.unscaledDeltaTime wall-clock frame intervals, " +
            "not isolated rendering work. It includes Editor/Play Mode overhead, " +
            "VSync and frame caps. P95/P99 are frame-time percentiles. " +
            "This is NOT CPU main-thread time, render-thread time or GPU time. " +
            "Use a standalone Development Build and Unity Profiler for reliable profiling.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save frame snapshot as baseline"))
        {
            frameBaseline = frameSnapshot;
            frameBaselineCity = frameCity;
            frameBaselineQuality = frameQuality;
            frameBaselineCaptured = true;
        }
        if (GUILayout.Button("Export frame statistics CSV..."))
            ExportFrameStatisticsCsv();
        EditorGUILayout.EndHorizontal();

        if (!frameBaselineCaptured) return;
        EditorGUILayout.LabelField("Baseline: " + frameBaselineCity +
            " [" + QualityName(frameBaselineQuality) + "] → " +
            frameCity + " [" + QualityName(frameQuality) + "]");
        if (!string.Equals(frameBaselineCity, frameCity, StringComparison.Ordinal))
            EditorGUILayout.HelpBox("Different cities (and potentially different seeds): " +
                "FPS comparison is not controlled.", MessageType.Warning);
        EditorGUILayout.LabelField("Δ average frame: " +
            (frameSnapshot.AverageMs - frameBaseline.AverageMs).ToString("+0.00;-0.00;0.00") +
            " ms    Δ P95: " +
            (frameSnapshot.P95Ms - frameBaseline.P95Ms).ToString("+0.00;-0.00;0.00") +
            " ms    Δ FPS: " +
            (frameSnapshot.EstimatedFps - frameBaseline.EstimatedFps).ToString("+0.0;-0.0;0.0"));
    }

    static string QualityName(int quality) =>
        quality == 0 ? "Low" : quality == 1 ? "Medium" : "High";

    void ExportFrameStatisticsCsv()
    {
        var path = EditorUtility.SaveFilePanel("Export 3D frame statistics", "",
            "CityHighwaysFrameAudit.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine(CityProceduralAuditReport.CsvRow(
            "Capture", "City", "Quality", "Frames", "EstimatedFPS",
            "AverageFrameMs", "P95FrameMs", "P99FrameMs", "MaxFrameMs",
            "Approx1PercentLowFPS", "TargetFPS", "P95BudgetStatus"));
        AppendFrameCsv(sb, "Current", frameCity, frameQuality, frameSnapshot);
        if (frameBaselineCaptured)
            AppendFrameCsv(sb, "Baseline", frameBaselineCity,
                frameBaselineQuality, frameBaseline);
        try
        {
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            Debug.Log("Exported 3D frame statistics to " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Could not export frame statistics: " + e.Message);
            EditorUtility.DisplayDialog("Frame CSV export failed", e.Message, "OK");
        }
    }

    void AppendFrameCsv(StringBuilder sb, string label, string map,
        int quality, City3DFrameMonitor.Snapshot measurement)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        sb.AppendLine(CityProceduralAuditReport.CsvRow(
            label, map, QualityName(quality),
            measurement.Frames.ToString(culture),
            measurement.EstimatedFps.ToString("F3", culture),
            measurement.AverageMs.ToString("F3", culture),
            measurement.P95Ms.ToString("F3", culture),
            measurement.P99Ms.ToString("F3", culture),
            measurement.MaxMs.ToString("F3", culture),
            measurement.OnePercentLowFps.ToString("F3", culture),
            City3DFrameBudget.TargetForQuality(quality,
                lowTargetFps, mediumTargetFps, highTargetFps).ToString(culture),
            City3DFrameBudget.Evaluate(measurement,
                City3DFrameBudget.TargetForQuality(quality,
                    lowTargetFps, mediumTargetFps, highTargetFps)).ToString()));
    }

    void DrawGeometryBudgetsAndComparison()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Geometry budgets and comparison", EditorStyles.boldLabel);
        totalTriangleBudget = Math.Max(0L, EditorGUILayout.LongField(
            "Total triangle budget", totalTriangleBudget));
        groupTriangleBudget = Math.Max(0L, EditorGUILayout.LongField(
            "Per-group triangle budget", groupTriangleBudget));
        if (City3DGeometryComparison.ExceedsBudget(runtimeTotals.Triangles, totalTriangleBudget))
            EditorGUILayout.HelpBox("Current mesh snapshot exceeds the total triangle budget: " +
                runtimeTotals.Triangles + " > " + totalTriangleBudget, MessageType.Warning);
        int exceeding = 0;
        foreach (var entry in runtimeEntries)
            if (City3DGeometryComparison.ExceedsBudget(entry.Triangles, groupTriangleBudget))
                exceeding++;
        if (exceeding > 0)
            EditorGUILayout.HelpBox(exceeding +
                " mesh groups exceed the per-group triangle budget.", MessageType.Warning);
        EditorGUILayout.HelpBox("A budget of 0 disables its warning. These are manually chosen " +
            "geometry thresholds, not calibrated FPS or hardware targets.", MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save current capture as baseline"))
        {
            baselineEntries = (City3DGeometryAudit.Entry[])runtimeEntries.Clone();
            baselineTotals = runtimeTotals;
            baselineCity = runtimeCity;
            baselineCaptured = true;
        }
        using (new EditorGUI.DisabledScope(!runtimeCaptured))
        {
            if (GUILayout.Button("Export current geometry CSV..."))
                ExportGeometryCsv();
        }
        EditorGUILayout.EndHorizontal();
        if (!baselineCaptured)
        {
            EditorGUILayout.HelpBox("Save a baseline, then recapture after changing the " +
                "quality setting or city to compare triangle totals.", MessageType.Info);
            return;
        }
        var delta = City3DGeometryComparison.Compare(baselineTotals, runtimeTotals);
        EditorGUILayout.LabelField("Baseline: " + baselineCity + " → Current: " + runtimeCity);
        if (!string.Equals(baselineCity, runtimeCity, StringComparison.Ordinal))
            EditorGUILayout.HelpBox("Different cities: deltas are descriptive, not a " +
                "like-for-like quality comparison.", MessageType.Warning);
        EditorGUILayout.LabelField("Δ triangles: " + delta.Triangles +
            "    Δ vertices: " + delta.Vertices + "    Δ meshes: " + delta.Meshes);
        EditorGUILayout.LabelField("Δ buildings: " + delta.BuildingTriangles +
            "    Δ trees: " + delta.TreeTriangles +
            "    Δ authored: " + delta.AuthoredTriangles +
            "    Δ other: " + delta.OtherTriangles);
        if (runtimeEntries.Length > 0)
        {
            var heaviest = runtimeEntries[0];
            EditorGUILayout.LabelField("Top current group vs baseline: " + heaviest.Name +
                " (Δ " + City3DGeometryComparison.DeltaForGroup(
                    baselineEntries, runtimeEntries, heaviest.Name) + " triangles)");
        }
    }

    void ExportGeometryCsv()
    {
        var path = EditorUtility.SaveFilePanel("Export 3D geometry snapshot", "",
            "CityHighwaysGeometryAudit.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine(CityProceduralAuditReport.CsvRow(
            "City", "Group", "Category", "Meshes", "Vertices", "Triangles",
            "GroupBudget", "OverBudget", "BaselineDeltaTriangles"));
        foreach (var entry in runtimeEntries)
        {
            sb.AppendLine(CityProceduralAuditReport.CsvRow(
                runtimeCity, entry.Name, entry.Kind.ToString(),
                entry.MeshCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry.Vertices.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry.Triangles.ToString(System.Globalization.CultureInfo.InvariantCulture),
                groupTriangleBudget.ToString(System.Globalization.CultureInfo.InvariantCulture),
                City3DGeometryComparison.ExceedsBudget(entry.Triangles, groupTriangleBudget) ?
                    "YES" : "NO",
                baselineCaptured && string.Equals(baselineCity, runtimeCity, StringComparison.Ordinal) ?
                    City3DGeometryComparison.DeltaForGroup(baselineEntries,
                        runtimeEntries, entry.Name).ToString(
                        System.Globalization.CultureInfo.InvariantCulture) : ""));
        }
        try
        {
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            Debug.Log("Exported 3D geometry snapshot to " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Could not export geometry snapshot: " + e.Message);
            EditorUtility.DisplayDialog("CSV export failed", e.Message, "OK");
        }
    }

    static bool TryWorldBounds(Component anchor, GameObject prefab, out Bounds world)
    {
        world = default;
        if (!CityProceduralPrefabFitting.TryLocalBounds(prefab, out var local)) return false;
        bool hasPoint = false;
        var matrix = anchor.transform.localToWorldMatrix;
        var center = local.center;
        var extents = local.extents;
        for (int i = 0; i < 8; i++)
        {
            var point = matrix.MultiplyPoint3x4(center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z));
            if (!hasPoint) { world = new Bounds(point, Vector3.zero); hasPoint = true; }
            else world.Encapsulate(point);
        }
        return hasPoint && world.size.x > 0f && world.size.y > 0f && world.size.z > 0f;
    }

    static void MeshStatistics(GameObject prefab, out int meshes, out long triangles)
    {
        meshes = 0;
        triangles = 0;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null)
                AddMesh(filter.sharedMesh, ref meshes, ref triangles);
        foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skin.sharedMesh != null)
                AddMesh(skin.sharedMesh, ref meshes, ref triangles);
    }

    static void AddMesh(Mesh mesh, ref int meshes, ref long triangles)
    {
        meshes++;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            if (mesh.GetTopology(submesh) == MeshTopology.Triangles)
                triangles += (long)mesh.GetIndexCount(submesh) / 3L;
    }

    void Analyze()
    {
        visuals.Clear();
        overlaps.Clear();
        invalidVisuals = 0;
        skippedVisuals = 0;
        var authored = UnityEngine.Object.FindObjectsByType<CitySceneDecoration>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var anchor in authored)
        {
            if (anchor == null || !anchor.gameObject.scene.IsValid() ||
                !anchor.gameObject.scene.isLoaded || !anchor.isActiveAndEnabled ||
                !anchor.gameObject.activeInHierarchy ||
                !anchor.ShowInGame || !string.Equals(anchor.MapName, city, StringComparison.Ordinal))
            {
                skippedVisuals++;
                continue;
            }
            AddVisual(anchor, anchor.VisualPrefab);
        }
        var replacements = UnityEngine.Object.FindObjectsByType<CityProceduralReplacement>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var anchor in replacements)
        {
            if (anchor == null || !anchor.gameObject.scene.IsValid() ||
                !anchor.gameObject.scene.isLoaded || !anchor.isActiveAndEnabled ||
                !anchor.gameObject.activeInHierarchy ||
                !anchor.AppliesTo(city))
            {
                skippedVisuals++;
                continue;
            }
            AddVisual(anchor, anchor.VisualPrefab);
        }
        var records = new List<CitySceneQualityMetrics.Record>();
        foreach (var visual in visuals)
            records.Add(new CitySceneQualityMetrics.Record
            {
                WorldBounds = visual.WorldBounds,
                Meshes = visual.Meshes,
                Triangles = visual.Triangles
            });
        hasResults = CitySceneQualityMetrics.TryAnalyze(records,
            CityPlanner.FieldW / CityPlanner.PixelsPerUnit,
            CityPlanner.FieldH / CityPlanner.PixelsPerUnit,
            minimumOverlap, out summary);

        // Show only a limited list; full count is computed by the sweep-line summary.
        if (!hasResults) { Debug.LogWarning("Could not calculate scene quality metrics."); return; }
        const int maxPairs = 60;
        for (int i = 0; i < visuals.Count && overlaps.Count < maxPairs; i++)
            for (int j = i + 1; j < visuals.Count && overlaps.Count < maxPairs; j++)
                if (CitySceneQualityMetrics.PotentialIntersection(
                    visuals[i].WorldBounds, visuals[j].WorldBounds, minimumOverlap))
                    overlaps.Add(visuals[i].Anchor.name + " / " + visuals[j].Anchor.name);
        Repaint();
    }

    void AddVisual(Component anchor, GameObject prefab)
    {
        if (prefab == null || !TryWorldBounds(anchor, prefab, out var bounds))
        {
            invalidVisuals++;
            return;
        }
        MeshStatistics(prefab, out var meshes, out var triangles);
        visuals.Add(new Visual
        {
            Anchor = anchor, WorldBounds = bounds, Meshes = meshes, Triangles = triangles
        });
    }
}
