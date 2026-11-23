using System;
using System.Collections.Generic;
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
        runtimeLimit = EditorGUILayout.IntSlider("Top geometry groups", runtimeLimit, 5, 100);
        int count = Mathf.Min(runtimeLimit, runtimeEntries.Length);
        runtimeScroll = EditorGUILayout.BeginScrollView(runtimeScroll, GUILayout.MaxHeight(270f));
        for (int i = 0; i < count; i++)
        {
            var entry = runtimeEntries[i];
            EditorGUILayout.LabelField((i + 1) + ". [" + entry.Kind + "] " + entry.Name +
                " — " + entry.Triangles + " triangles, " +
                entry.Vertices + " vertices, " + entry.MeshCount + " meshes");
        }
        EditorGUILayout.EndScrollView();
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
