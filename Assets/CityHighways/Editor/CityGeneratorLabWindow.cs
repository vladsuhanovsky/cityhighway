using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

public sealed class CityGeneratorLabWindow : EditorWindow
{
    int seed = 42, count = 64;
    bool include3D;
    CitySeedReport current;
    CitySeedBatchReport lastBatch;
    string batchExportPath;
    int batchPage;
    bool onlyProblems;
    bool archiveOpen;
    bool attemptsOpen;
    int attemptPage;
    string[] archivePaths = Array.Empty<string>();
    int archiveIndex;
    CitySeedBatchReport baseline;
    string archiveMessage;
    Vector2 windowScroll;
    Vector2 scroll;
    string summary = "";
    readonly List<string> failures = new List<string>();

    [MenuItem("City Highways/City Generator Lab")]
    static void Open() => GetWindow<CityGeneratorLabWindow>("City Generator Lab");

    void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        try { DrawContents(); }
        finally { EditorGUILayout.EndScrollView(); }
    }

    void DrawContents()
    {
        EditorGUILayout.HelpBox("Magnate topology preview and reproducible seed diagnostics. No assets, saves or live game state are modified. Timing includes diagnostic checks, not 3D scene generation.", MessageType.Info);
        seed = EditorGUILayout.IntField("Requested seed", seed);
        count = EditorGUILayout.IntField("Batch count (1–512)", count);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Quick topology (16)")) { count = 16; include3D = false; }
        if (GUILayout.Button("Standard topology (64)")) { count = 64; include3D = false; }
        if (GUILayout.Button("3D audit (16)")) { count = 16; include3D = true; }
        EditorGUILayout.EndHorizontal();
        bool validRange = CitySeedBatch.IsValid(seed, count);
        if (!validRange)
            EditorGUILayout.HelpBox("Invalid batch range: count must be 1–512 and the last seed must fit Int32.", MessageType.Warning);
        include3D = EditorGUILayout.ToggleLeft("Audit 3D footprints (slower; no meshes)", include3D);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Generate and validate"))
            {
                current = CitySeedDiagnostics.Run(seed, include3D);
                failures.Clear();
                summary = current.Passed ? "Valid topology" : "Validation failed";
                attemptPage = 0;
                if (ShouldArchive(current)) SaveFailure(current);
            }
            if (GUILayout.Button("Generate fixed tutorial city"))
            {
                current = CitySeedDiagnostics.RunTutorial(include3D);
                attemptPage = 0;
                failures.Clear();
                summary = current.Passed ? "Valid tutorial topology" : "Tutorial validation failed";
                if (ShouldArchive(current)) SaveFailure(current);
            }
            using (new EditorGUI.DisabledScope(!validRange))
                if (GUILayout.Button("Run seed range")) RunBatch();
        }
        if (!string.IsNullOrEmpty(summary)) EditorGUILayout.HelpBox(summary, MessageType.None);
        DrawArchive();
        DrawBatchSummary();
        if (current == null) return;
        EditorGUILayout.LabelField("Generation profile", current.generationProfile ?? "standard");
        EditorGUILayout.LabelField("Actual seed", current.generated ? current.actualSeed.ToString() : "Unavailable");
        EditorGUILayout.LabelField("Duration", current.milliseconds + " ms");
        EditorGUILayout.LabelField("Locations / roads / hubs",
            (current.locations?.Length ?? 0) + " / " + (current.roads?.Length ?? 0) + " / " + (current.hubs?.Length ?? 0));
        if (current.generated)
        {
            DrawPreview();
            EditorGUILayout.LabelField("Blue: rivers · gold: bridges · red: hubs · white: public roads");
            if (current.layout3D != null)
                EditorGUILayout.HelpBox("Cyan: prepared 3D river line; orange: hub footprint or warned road/junction; purple: adjusted crosswalk junction. Warnings: " + WarningCount(current) +
                    " | Crosswalks drawn: " + current.layout3D.crosswalksDrawn + "/" + current.layout3D.crosswalkCandidates +
                    " | Legacy overlap pairs: " + current.layout3D.legacyOverlapPairs, MessageType.Info);
        }
        DrawAttemptTrace();
        if (GUILayout.Button("Export selected diagnostic JSON"))
        {
            string dir = EditorUtility.OpenFolderPanel("Export seed diagnostics", "", "");
            if (!string.IsNullOrEmpty(dir))
                try { summary = CitySeedDiagnostics.Write(current, dir); }
                catch (Exception ex) { summary = "Export failed: " + ex.Message; }
        }
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(220));
        foreach (string issue in current.issues ?? Array.Empty<string>())
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        foreach (var issue in current.layout3D?.warnings ?? Array.Empty<City3DLayoutIssue>())
            EditorGUILayout.HelpBox(issue.code + " · " + issue.id + " · " + issue.measured.ToString("0.000") + " / " + issue.limit.ToString("0.000"), MessageType.Warning);
        foreach (string line in failures) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    void RunBatch()
    {
        var seeds = CitySeedBatch.Seeds(seed, count);
        failures.Clear();
        lastBatch = CitySeedBatchReport.Begin(seed, count, include3D);
        batchPage = 0;
        batchExportPath = null;
        current = null;
        attemptPage = 0;
        try
        {
            for (int i = 0; i < seeds.Length; i++)
            {
                int requested = seeds[i];
                if (EditorUtility.DisplayCancelableProgressBar("City Generator Lab",
                    "Seed " + requested, i / (float)count)) break;
                current = CitySeedDiagnostics.Run(requested, include3D);
                string path = ShouldArchive(current) ? SaveFailure(current) : null;
                lastBatch.Add(current, path);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        summary = "Completed " + lastBatch.Completed + " / " + lastBatch.requestedCount + "; failed " + lastBatch.Failed +
            "; 3D warnings on " + lastBatch.Warned + " seed(s); fallback seeds " + lastBatch.Fallback + "." +
            (!lastBatch.Complete ? " Cancelled before completion." : "");
        try { batchExportPath = lastBatch.Write(ReportDirectory()); RefreshArchive(); }
        catch (Exception ex) { summary += " Batch export failed: " + ex.Message; }
    }

    void RefreshArchive()
    {
        try
        {
            archivePaths = CitySeedBatchArchive.List(ReportDirectory());
            archiveIndex = 0;
            archiveMessage = archivePaths.Length + " recent summaries from a bounded local scan.";
        }
        catch (Exception ex) { archiveMessage = "Archive scan failed: " + ex.Message; }
    }

    void ImportBatch(string path, bool asBaseline)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var loaded = CitySeedBatchArchive.Read(path);
            if (asBaseline) baseline = loaded;
            else
            {
                lastBatch = loaded;
                batchExportPath = path;
                batchPage = 0;
                current = null;
                failures.Clear();
                summary = "Imported batch summary; reproduce a seed to preview the current generator.";
            }
            archiveMessage = "Loaded " + Path.GetFileName(path);
        }
        catch (Exception ex) { archiveMessage = "Import failed: " + ex.Message; }
    }

    void DrawArchive()
    {
        archiveOpen = EditorGUILayout.Foldout(archiveOpen, "Diagnostic archive and comparison", true);
        if (!archiveOpen) return;
        if (GUILayout.Button("Refresh local archive")) RefreshArchive();
        if (archivePaths.Length > 0)
        {
            archiveIndex = Mathf.Clamp(archiveIndex, 0, archivePaths.Length - 1);
            archiveIndex = EditorGUILayout.Popup("Local summary", archiveIndex, archivePaths.Select(Path.GetFileName).ToArray());
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("View summary")) ImportBatch(archivePaths[archiveIndex], false);
            if (GUILayout.Button("Use as baseline")) ImportBatch(archivePaths[archiveIndex], true);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Import summary JSON"))
            ImportBatch(EditorUtility.OpenFilePanel("Import batch summary", "", "json"), false);
        if (GUILayout.Button("Import baseline JSON"))
            ImportBatch(EditorUtility.OpenFilePanel("Import comparison baseline", "", "json"), true);
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(archiveMessage))
            EditorGUILayout.HelpBox(archiveMessage, MessageType.Info);
        if (baseline == null) return;
        EditorGUILayout.LabelField("Baseline", baseline.utc + " · " + baseline.Completed + "/" + baseline.requestedCount +
            (baseline.include3D ? " · 3D" : " · topology"));
        if (GUILayout.Button("Clear baseline")) { baseline = null; return; }
        if (lastBatch == null) return;
        if (baseline.include3D != lastBatch.include3D)
        {
            EditorGUILayout.HelpBox("Compare the same diagnostic mode (topology or 3D).", MessageType.Warning);
            return;
        }
        var diff = CitySeedBatchComparison.Compare(baseline, lastBatch);
        EditorGUILayout.LabelField("Shared / baseline only / current only", diff.Shared + " / " + diff.OnlyBaseline + " / " + diff.OnlyCurrent);
        if (diff.Shared == 0) { EditorGUILayout.HelpBox("No shared completed seeds.", MessageType.Info); return; }
        EditorGUILayout.LabelField("New / resolved failures", diff.NewFailures + " / " + diff.ResolvedFailures);
        EditorGUILayout.LabelField("Passed in both (geometry comparison)", diff.GeneratedInBoth.ToString());
        EditorGUILayout.LabelField("New / resolved warned seeds", diff.NewWarnings + " / " + diff.ResolvedWarnings);
        EditorGUILayout.LabelField("Warning count delta (shared seeds)", diff.WarningDelta.ToString());
        EditorGUILayout.LabelField("Changed actual seed / city counts", diff.ActualSeedChanged + " / " + diff.SizeChanged);
        EditorGUILayout.HelpBox("Counts and status only; not a topology hash or performance comparison.", MessageType.Info);
    }

    void DrawBatchSummary()
    {
        if (lastBatch == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected batch", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Diagnostic mode", lastBatch.include3D ? "3D audit" : "Topology");
        EditorGUILayout.LabelField("Completed / requested", lastBatch.Completed + " / " + lastBatch.requestedCount);
        EditorGUILayout.LabelField("Failures / warned / fallback", lastBatch.Failed + " / " + lastBatch.Warned + " / " + lastBatch.Fallback);
        if (!string.IsNullOrEmpty(batchExportPath))
            EditorGUILayout.SelectableLabel(batchExportPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        if (GUILayout.Button("Export batch summary JSON"))
        {
            string dir = EditorUtility.OpenFolderPanel("Export batch summary", "", "");
            if (!string.IsNullOrEmpty(dir))
                try { batchExportPath = lastBatch.Write(dir); }
                catch (Exception ex) { summary = "Batch export failed: " + ex.Message; }
        }

        bool filter = EditorGUILayout.ToggleLeft("Only failures, warnings or fallback seeds", onlyProblems);
        if (filter != onlyProblems) { onlyProblems = filter; batchPage = 0; }
        var rows = lastBatch.rows.Where(r => !onlyProblems || !r.passed || r.warnings > 0 ||
            (r.generated && r.requestedSeed != r.actualSeed)).ToArray();
        const int pageSize = 12;
        int pages = Math.Max(1, (rows.Length + pageSize - 1) / pageSize);
        batchPage = Mathf.Clamp(batchPage, 0, pages - 1);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(batchPage == 0))
            if (GUILayout.Button("Previous")) batchPage--;
        GUILayout.Label((batchPage + 1) + " / " + pages + " (" + rows.Length + " seeds)");
        using (new EditorGUI.DisabledScope(batchPage + 1 >= pages))
            if (GUILayout.Button("Next")) batchPage++;
        EditorGUILayout.EndHorizontal();
        foreach (var row in rows.Skip(batchPage * pageSize).Take(pageSize))
        {
            EditorGUILayout.BeginHorizontal();
            string actual = row.generated ? row.actualSeed.ToString() : "n/a";
            string status = !row.passed ? "FAIL" : row.warnings > 0 ? "WARN" : "OK";
            GUILayout.Label(row.requestedSeed + " → " + actual + " · " + status + " · " + row.warnings + " warnings · " + row.milliseconds + " ms");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Reproduce", GUILayout.Width(90)))
                {
                    seed = row.requestedSeed;
                    include3D = lastBatch.include3D;
                    current = CitySeedDiagnostics.Run(seed, include3D);
                    summary = "Reproduced requested seed " + seed + "; actual " +
                        (current.generated ? current.actualSeed.ToString() : "n/a");
                    attemptPage = 0;
                    if (ShouldArchive(current)) SaveFailure(current);
                }
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawAttemptTrace()
    {
        var attempts = current.attempts ?? Array.Empty<CityGenerationAttempt>();
        attemptsOpen = EditorGUILayout.Foldout(attemptsOpen, "Generation attempts (" + attempts.Length + ")", true);
        if (!attemptsOpen) return;
        foreach (var group in attempts.GroupBy(a => a.outcome).OrderBy(g => g.Key, StringComparer.Ordinal))
            EditorGUILayout.LabelField(group.Key, group.Count().ToString());
        const int pageSize = 8;
        int pages = Math.Max(1, (attempts.Length + pageSize - 1) / pageSize);
        attemptPage = Mathf.Clamp(attemptPage, 0, pages - 1);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(attemptPage == 0))
            if (GUILayout.Button("Previous attempts")) attemptPage--;
        GUILayout.Label((attemptPage + 1) + " / " + pages);
        using (new EditorGUI.DisabledScope(attemptPage + 1 >= pages))
            if (GUILayout.Button("Next attempts")) attemptPage++;
        EditorGUILayout.EndHorizontal();
        foreach (var a in attempts.Skip(attemptPage * pageSize).Take(pageSize))
        {
            EditorGUILayout.LabelField("#" + a.index + " · " + a.profile + " · seed " + a.seed + " · " + a.outcome,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Locations / roads / components",
                FormatCount(a.locations) + " / " + FormatCount(a.roads) + " / " + FormatCount(a.components));
            foreach (string issue in a.issues ?? Array.Empty<string>())
                EditorGUILayout.HelpBox(issue, MessageType.Info);
        }
    }

    static string FormatCount(int count) => count < 0 ? "n/a" : count.ToString();

    static bool ShouldArchive(CitySeedReport report) => !report.Passed || WarningCount(report) > 0 ||
        (report.attempts?.Length ?? 0) > 1;

    static int WarningCount(CitySeedReport report) => report.layout3D?.warnings?.Length ?? 0;

    static string ReportDirectory() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "CityHighways", "CitySeedDiagnostics"));

    string SaveFailure(CitySeedReport report)
    {
        try
        {
            string path = CitySeedDiagnostics.Write(report, ReportDirectory());
            failures.Add("Seed " + report.requestedSeed + ": " + path);
            return path;
        }
        catch (Exception ex) { failures.Add("Seed " + report.requestedSeed + ": could not export: " + ex.Message); return null; }
    }

    void DrawPreview()
    {
        var area = GUILayoutUtility.GetAspectRect(1400f / 780f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(.10f, .13f, .15f));
        if (Event.current.type != EventType.Repaint || current.locations == null) return;
        var locations = current.locations.ToDictionary(p => p.id);
        var hubs = new HashSet<string>((current.hubs ?? Array.Empty<string>()).Select(h => h.Split(':')[0]));
        var publicRoads = new HashSet<string>(current.publicRoads ?? Array.Empty<string>());
        var flagged = new HashSet<string>((current.layout3D?.warnings ?? Array.Empty<City3DLayoutIssue>()).Select(w => w.id));
        var adjusted = new HashSet<string>(current.layout3D?.crosswalkAdjustedJunctions ?? Array.Empty<string>());
        Vector3 At(float x, float y) => new Vector3(area.x + x * area.width, area.y + y * area.height, 0);
        Handles.BeginGUI();
        try
        {
            foreach (var river in current.rivers ?? Array.Empty<RiverDto>())
            {
                Handles.color = new Color(.22f, .61f, .87f);
                if (river.points.Length >= 2) Handles.DrawAAPolyLine(3f, river.points.Select(p => At(p.x, p.y)).ToArray());
            }
            if (current.layout3D != null)
            {
                Handles.color = Color.cyan;
                foreach (var river in current.layout3D.preparedRivers ?? Array.Empty<RiverDto>())
                    for (int i = 1; i < river.points.Length; i++)
                    {
                        Vector2 a = new Vector2(river.points[i - 1].x, river.points[i - 1].y);
                        Vector2 b = new Vector2(river.points[i].x, river.points[i].y);
                        if (ClipToMap(ref a, ref b)) Handles.DrawAAPolyLine(2f, At(a.x, a.y), At(b.x, b.y));
                    }
            }
            foreach (var road in current.roads ?? Array.Empty<RoadDto>())
            {
                if (!locations.TryGetValue(road.from, out var a) || !locations.TryGetValue(road.to, out var b)) continue;
                Handles.color = string.Equals(road.resource, "Steel", StringComparison.OrdinalIgnoreCase) ?
                    new Color(.9f, .7f, .28f) : publicRoads.Contains(road.id) ? Color.white : Color.gray;
                if (flagged.Contains(road.id)) Handles.color = new Color(1f, .5f, .1f);
                Handles.DrawAAPolyLine(flagged.Contains(road.id) ? 3f : 1.5f, At(a.x, a.y), At(b.x, b.y));
            }
            if (current.layout3D != null)
            {
                foreach (var hub in current.layout3D.hubFootprints ?? Array.Empty<City3DHubFootprint>())
                {
                    Handles.color = flagged.Contains(hub.id) ? new Color(1f, .5f, .1f) : new Color(.95f, .65f, .25f);
                    for (int i = 0; i < hub.corners.Length; i++)
                    {
                        var p = hub.corners[i]; var q = hub.corners[(i + 1) % hub.corners.Length];
                        Vector2 a = new Vector2(p.x, p.y), b = new Vector2(q.x, q.y);
                        if (ClipToMap(ref a, ref b)) Handles.DrawAAPolyLine(2f, At(a.x, a.y), At(b.x, b.y));
                    }
                }
            }
        }
        finally { Handles.EndGUI(); }
        foreach (var point in current.locations)
        {
            var p = At(point.x, point.y);
            float size = hubs.Contains(point.id) ? 7 : 4;
            EditorGUI.DrawRect(new Rect(p.x - size / 2, p.y - size / 2, size, size),
                flagged.Contains(point.id) ? new Color(1f, .5f, .1f) : adjusted.Contains(point.id) ? new Color(.75f, .45f, 1f) : hubs.Contains(point.id) ? new Color(.95f, .4f, .3f) : Color.white);
        }
    }

    // Clip horizon extensions and off-board footprints without bending them.
    static bool ClipToMap(ref Vector2 a, ref Vector2 b)
    {
        var start = a; var delta = b - a;
        float lo = 0f, hi = 1f;
        bool Bound(float p, float q)
        {
            if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
            float t = q / p;
            if (p < 0f) lo = Mathf.Max(lo, t); else hi = Mathf.Min(hi, t);
            return lo <= hi;
        }
        if (!Bound(-delta.x, start.x) || !Bound(delta.x, 1f - start.x) ||
            !Bound(-delta.y, start.y) || !Bound(delta.y, 1f - start.y)) return false;
        a = start + delta * lo;
        b = start + delta * hi;
        return true;
    }

}
