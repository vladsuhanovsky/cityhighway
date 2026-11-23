using System;
using System.IO;
using System.Text;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Manual, repeatable multi-city/seed performance comparison in Unity Editor.</summary>
public sealed class City3DPerformanceSuiteWindow : EditorWindow
{
    City3DPerformanceSuite baseline = new City3DPerformanceSuite();
    City3DPerformanceSuite current = new City3DPerformanceSuite();
    string scenario = "magnate;seed=42;camera=central;viewport=1920x1080";
    float allowedRegression = 10f;
    Vector2 scroll;
    int maxDiagnosticRows = 30;

    [MenuItem("City Highways/3D Objects/Performance Scenario Suite")]
    static void Open() => GetWindow<City3DPerformanceSuiteWindow>("3D Scenario Suite");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Manual benchmarks: capture each random Magnate seed using a matching " +
            "city, graphics quality, camera, viewport, machine and scene workload. " +
            "Scenario labels are user-provided, not verified by the generator. " +
            "Frame intervals include Unity Editor overhead and are not GPU timings.",
            MessageType.Info);
        var active = City3DScene.ActiveScene;
        scenario = EditorGUILayout.TextField("Scenario (seed/camera/viewport)", scenario);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying ||
            active == null || active.FrameMonitor == null))
        {
            if (GUILayout.Button("Add active 3D capture to CURRENT suite"))
            {
                var item = City3DPerformanceBaseline.Create(active.CityName,
                    active.GraphicsQuality, scenario, active.FrameMonitor.Capture());
                if (!current.TryAdd(item, out var reason))
                {
                    // Duplicate captures must not be overwritten without approval.
                    if (reason != null && reason.StartsWith("Scenario already exists:",
                        StringComparison.Ordinal) &&
                        EditorUtility.DisplayDialog("Replace captured scenario?",
                            item.city + " [" + item.quality + "] " + item.scenario +
                            "\nReplace the previous CURRENT capture with the new measurement?",
                            "Replace", "Cancel"))
                    {
                        if (!current.TryReplace(item, out var replaceReason))
                            EditorUtility.DisplayDialog("Replacement failed", replaceReason, "OK");
                    }
                    else if (reason == null || !reason.StartsWith("Scenario already exists:",
                        StringComparison.Ordinal))
                        EditorUtility.DisplayDialog("Capture not added", reason, "OK");
                }
                Repaint();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save CURRENT suite JSON..."))
            Save(current, "Current");
        if (GUILayout.Button("Load CURRENT suite JSON..."))
            Load(false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save BASELINE suite JSON..."))
            Save(baseline, "Baseline");
        if (GUILayout.Button("Load BASELINE suite JSON..."))
            Load(true);
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(!current.IsValid(out _)))
            if (GUILayout.Button("Use CURRENT as new BASELINE"))
            {
                // Deep-copy so further current suite changes cannot alter the baseline.
                City3DPerformanceSuite.TryFromJson(current.ToJson(),
                    out baseline, out _);
                current = new City3DPerformanceSuite();
            }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear CURRENT")) current = new City3DPerformanceSuite();
        if (GUILayout.Button("Clear BASELINE")) baseline = new City3DPerformanceSuite();
        EditorGUILayout.EndHorizontal();

        allowedRegression = Mathf.Max(0f, EditorGUILayout.FloatField(
            "Allowed P95 regression (%)", allowedRegression));
        var summary = City3DPerformanceSuite.Compare(baseline, current, allowedRegression);
        bool bothValid = baseline.IsValid(out _) && current.IsValid(out _);
        var diagnostics = City3DPerformanceDiagnostics.Build(
            baseline, current, allowedRegression);
        if (!bothValid)
            EditorGUILayout.HelpBox(
                "Load or capture at least one valid scenario in each suite to compare.",
                MessageType.Info);
        else
        {
            EditorGUILayout.LabelField("Matched: " + summary.Matched +
                "    Pass: " + summary.Passed +
                "    Regressions: " + summary.Regressions, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Missing from current: " + summary.Missing +
                "    Additional in current: " + summary.Additional +
                "    Rejected: " + summary.Rejected);
            EditorGUILayout.LabelField("Baseline scenario coverage: " +
                diagnostics.CoveragePercent.ToString("F1") + "%");
            if (!diagnostics.CompleteCoverage)
                EditorGUILayout.HelpBox(
                    "Incomplete scenario coverage: " + diagnostics.Missing +
                    " missing, " + diagnostics.Additional +
                    " additional, " + diagnostics.Invalid +
                    " invalid. Missing seeds or changed graphics quality are NOT a pass.",
                    MessageType.Warning);
            if (summary.Regressions > 0)
                EditorGUILayout.HelpBox("Performance regression in " +
                    summary.Regressions + " matched scenarios. Worst P95 increase: " +
                    summary.WorstP95Percent.ToString("F1") + "%.",
                    MessageType.Warning);
            maxDiagnosticRows = EditorGUILayout.IntSlider(
                "Visible diagnostic rows", maxDiagnosticRows, 5, 100);
            EditorGUILayout.LabelField("Problem scenarios first", EditorStyles.boldLabel);
            for (int i = 0; i < Mathf.Min(maxDiagnosticRows, diagnostics.Rows.Length); i++)
            {
                var issue = diagnostics.Rows[i];
                EditorGUILayout.LabelField(issue.Status + " — " + issue.City +
                    " [" + issue.Quality + "] " + issue.Scenario +
                    (issue.Status == City3DPerformanceDiagnostics.Kind.Regression
                        ? " (P95 +" + issue.DeltaPercent.ToString("F1") + "%)" : ""));
            }
        }
        using (new EditorGUI.DisabledScope(!bothValid))
            if (GUILayout.Button("Export matched scenario comparison CSV..."))
                ExportComparison();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        ShowEntries("Baseline scenarios", baseline);
        ShowEntries("Current scenarios", current);
        EditorGUILayout.EndScrollView();
    }

    static void ShowEntries(string title, City3DPerformanceSuite suite)
    {
        EditorGUILayout.LabelField(title + ": " + (suite.entries?.Count ?? 0),
            EditorStyles.boldLabel);
        if (suite.entries == null) return;
        // Render actions from a snapshot to keep collection mutation safe in IMGUI.
        var items = suite.entries.ToArray();
        foreach (var item in items)
        {
            if (item == null) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(item.city + " [" +
                (item.quality == 0 ? "Low" : item.quality == 1 ? "Medium" : "High") +
                "]  " + item.scenario + " — P95 " +
                item.snapshot.P95Ms.ToString("F2") + " ms");
            if (GUILayout.Button("Remove", GUILayout.Width(65f)) &&
                EditorUtility.DisplayDialog("Remove benchmark scenario?",
                    "Remove " + item.city + " / " + item.scenario +
                    " from " + title + "?", "Remove", "Cancel"))
            {
                suite.TryRemove(item.city, item.quality, item.scenario);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static void Save(City3DPerformanceSuite suite, string label)
    {
        if (!suite.IsValid(out var reason))
        {
            EditorUtility.DisplayDialog("Cannot save benchmark suite", reason, "OK");
            return;
        }
        var path = EditorUtility.SaveFilePanel("Save " + label + " benchmark suite", "",
            "CityHighways" + label + "PerformanceSuite.json", "json");
        if (string.IsNullOrEmpty(path)) return;
        try { File.WriteAllText(path, suite.ToJson(), new UTF8Encoding(false)); }
        catch (Exception e) { EditorUtility.DisplayDialog("Save failed", e.Message, "OK"); }
    }

    void Load(bool loadBaseline)
    {
        var path = EditorUtility.OpenFilePanel("Open benchmark suite", "", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (!City3DPerformanceSuite.TryFromJson(
                File.ReadAllText(path, Encoding.UTF8), out var loaded, out var reason))
            {
                EditorUtility.DisplayDialog("Invalid benchmark suite", reason, "OK");
                return;
            }
            if (loadBaseline) baseline = loaded;
            else current = loaded;
        }
        catch (Exception e) { EditorUtility.DisplayDialog("Load failed", e.Message, "OK"); }
    }

    void ExportComparison()
    {
        var path = EditorUtility.SaveFilePanel("Export scenario comparison CSV", "",
            "CityHighwaysScenarioComparison.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine(CityProceduralAuditReport.CsvRow(
            "City", "Quality", "Scenario", "BaselineP95Ms",
            "CurrentP95Ms", "P95ChangePercent", "Verdict"));
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var diagnostics = City3DPerformanceDiagnostics.Build(
            baseline, current, allowedRegression);
        foreach (var entry in diagnostics.Rows)
        {
            bool hasBaseline = entry.Status != City3DPerformanceDiagnostics.Kind.Additional;
            bool hasCurrent = entry.Status != City3DPerformanceDiagnostics.Kind.Missing;
            bool matched = hasBaseline && hasCurrent;
            sb.AppendLine(CityProceduralAuditReport.CsvRow(
                entry.City, entry.Quality.ToString(culture), entry.Scenario,
                hasBaseline ? entry.BaselineP95Ms.ToString("F3", culture) : "",
                hasCurrent ? entry.CurrentP95Ms.ToString("F3", culture) : "",
                matched ? entry.DeltaPercent.ToString("F3", culture) : "",
                entry.Status.ToString()));
        }
        try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); }
        catch (Exception e) { EditorUtility.DisplayDialog("CSV export failed", e.Message, "OK"); }
    }
}
