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
                    EditorUtility.DisplayDialog("Capture not added", reason, "OK");
                else Repaint();
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
            if (summary.Regressions > 0)
                EditorGUILayout.HelpBox("Performance regression in " +
                    summary.Regressions + " matched scenarios. Worst P95 increase: " +
                    summary.WorstP95Percent.ToString("F1") + "%.",
                    MessageType.Warning);
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
        foreach (var item in suite.entries)
            if (item != null)
                EditorGUILayout.LabelField(item.city + " [" +
                    (item.quality == 0 ? "Low" : item.quality == 1 ? "Medium" : "High") +
                    "]  " + item.scenario + " — P95 " +
                    item.snapshot.P95Ms.ToString("F2") + " ms");
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
        foreach (var now in current.entries)
        {
            City3DPerformanceBaseline old = null;
            foreach (var candidate in baseline.entries)
                if (string.Equals(candidate.city, now.city, StringComparison.Ordinal) &&
                    candidate.quality == now.quality &&
                    string.Equals(candidate.scenario, now.scenario, StringComparison.Ordinal))
                {
                    old = candidate;
                    break;
                }
            if (old == null) continue;
            var result = City3DPerformanceRegression.Compare(
                old.snapshot, old.city, old.quality, old.scenario,
                now.snapshot, now.city, now.quality, now.scenario, allowedRegression);
            sb.AppendLine(CityProceduralAuditReport.CsvRow(
                now.city, now.quality.ToString(culture), now.scenario,
                old.snapshot.P95Ms.ToString("F3", culture),
                now.snapshot.P95Ms.ToString("F3", culture),
                result.P95DeltaPercent.ToString("F3", culture), result.Status.ToString()));
        }
        try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); }
        catch (Exception e) { EditorUtility.DisplayDialog("CSV export failed", e.Message, "OK"); }
    }
}
