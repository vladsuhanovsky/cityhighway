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
    string baselineRevisionLabel = "";
    string currentRevisionLabel = "";

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
                    active.GraphicsQuality, scenario, active.FrameMonitor.Capture(),
                    captureEnvironment: City3DPerformanceEnvironment.Capture());
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
        var overallStatus = City3DPerformanceSuiteVerdict.Evaluate(
            baseline.IsValid(out _), current.IsValid(out _), diagnostics);
        EditorGUILayout.HelpBox(
            "Overall performance audit: " + overallStatus + "\n" +
            City3DPerformanceSuiteVerdict.Explain(overallStatus),
            overallStatus == City3DPerformanceSuiteVerdict.Status.Pass
                ? MessageType.Info
                : overallStatus == City3DPerformanceSuiteVerdict.Status.Regression
                    ? MessageType.Error : MessageType.Warning);
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
                diagnostics.CoveragePercent.ToString("F1") + "%" +
                "    Legacy environment comparisons: " + diagnostics.LegacyMatches);
            if (!diagnostics.CompleteCoverage)
                EditorGUILayout.HelpBox(
                    "Incomplete scenario coverage: " + diagnostics.Missing +
                    " missing, " + diagnostics.Additional +
                    " additional, " + diagnostics.Invalid +
                    " invalid. Missing seeds or changed graphics quality are NOT a pass.",
                    MessageType.Warning);
            if (diagnostics.Invalid > 0)
                EditorGUILayout.HelpBox("Some matched scenarios have incompatible or " +
                    "incomplete automatic capture environments (Unity version, platform, " +
                    "GPU, resolution, VSync or frame cap). These are NOT passes. " +
                    "Inspect baseline/current capture conditions.", MessageType.Warning);
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
        EditorGUILayout.LabelField("Optional revision labels for review handoff",
            EditorStyles.boldLabel);
        baselineRevisionLabel = EditorGUILayout.TextField(
            "Baseline revision", baselineRevisionLabel);
        currentRevisionLabel = EditorGUILayout.TextField(
            "Current revision", currentRevisionLabel);
        using (new EditorGUI.DisabledScope(!bothValid))
        {
            if (GUILayout.Button("Export scenario comparison CSV..."))
                ExportComparison();
            if (GUILayout.Button("Export audit summary TXT..."))
                ExportAuditSummary();
            if (GUILayout.Button("Export review artifact JSON..."))
                ExportReviewArtifact();
        }

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
                item.snapshot.P95Ms.ToString("F2") + " ms" +
                (item.environment == null ? " [legacy metadata unknown]" :
                    " [" + item.environment.Describe() + "]"));
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

    void ExportReviewArtifact()
    {
        var path = EditorUtility.SaveFilePanel("Export performance review artifact", "",
            "CityHighwaysPerformanceReview.json", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var artifact = City3DPerformanceReviewArtifact.Build(
                baseline, current, allowedRegression,
                baselineRevisionLabel.Trim(), currentRevisionLabel.Trim());
            File.WriteAllText(path, artifact.ToJson(), new UTF8Encoding(false));
            Debug.Log("Exported performance review artifact to " + path);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Review export failed", e.Message, "OK");
        }
    }

    void ExportAuditSummary()
    {
        var path = EditorUtility.SaveFilePanel("Export performance audit summary", "",
            "CityHighwaysPerformanceAuditSummary.txt", "txt");
        if (string.IsNullOrEmpty(path)) return;
        var report = City3DPerformanceDiagnostics.Build(
            baseline, current, allowedRegression);
        var verdict = City3DPerformanceSuiteVerdict.Evaluate(
            baseline.IsValid(out _), current.IsValid(out _), report);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine("City Highways 3D Performance Scenario Audit");
        text.AppendLine("Status: " + verdict);
        text.AppendLine(City3DPerformanceSuiteVerdict.Explain(verdict));
        text.AppendLine("P95 allowed regression (%): " +
            allowedRegression.ToString("F2", culture));
        text.AppendLine("Matched: " + report.Matched + " | Passed: " +
            report.Passed + " | Regressions: " + report.Regressions);
        text.AppendLine("Missing: " + report.Missing + " | Additional: " +
            report.Additional + " | Invalid: " + report.Invalid);
        text.AppendLine("Unverified legacy comparisons: " + report.LegacyMatches);
        text.AppendLine("Coverage (%): " +
            report.CoveragePercent.ToString("F1", culture));
        text.AppendLine("Note: manually entered seeds/cameras and runtime load are not verified.");
        text.AppendLine("Note: frame intervals are not isolated GPU/CPU timings.");
        try { File.WriteAllText(path, text.ToString(), new UTF8Encoding(true)); }
        catch (Exception e) { EditorUtility.DisplayDialog("Summary export failed", e.Message, "OK"); }
    }

    void ExportComparison()
    {
        var path = EditorUtility.SaveFilePanel("Export scenario comparison CSV", "",
            "CityHighwaysScenarioComparison.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine(CityProceduralAuditReport.CsvRow(
            "City", "Quality", "Scenario", "BaselineP95Ms",
            "CurrentP95Ms", "P95ChangePercent", "Verdict",
            "BaselineEnvironment", "CurrentEnvironment"));
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var diagnostics = City3DPerformanceDiagnostics.Build(
            baseline, current, allowedRegression);
        foreach (var entry in diagnostics.Rows)
        {
            City3DPerformanceBaseline before = null, after = null;
            foreach (var capture in baseline.entries)
                if (capture.city == entry.City && capture.quality == entry.Quality &&
                    capture.scenario == entry.Scenario) { before = capture; break; }
            foreach (var capture in current.entries)
                if (capture.city == entry.City && capture.quality == entry.Quality &&
                    capture.scenario == entry.Scenario) { after = capture; break; }
            bool hasBaseline = entry.Status != City3DPerformanceDiagnostics.Kind.Additional;
            bool hasCurrent = entry.Status != City3DPerformanceDiagnostics.Kind.Missing;
            bool matched = hasBaseline && hasCurrent;
            sb.AppendLine(CityProceduralAuditReport.CsvRow(
                entry.City, entry.Quality.ToString(culture), entry.Scenario,
                hasBaseline ? entry.BaselineP95Ms.ToString("F3", culture) : "",
                hasCurrent ? entry.CurrentP95Ms.ToString("F3", culture) : "",
                matched && entry.Status != City3DPerformanceDiagnostics.Kind.Invalid
                    ? entry.DeltaPercent.ToString("F3", culture) : "",
                entry.Status.ToString(),
                before?.environment?.Describe() ?? "",
                after?.environment?.Describe() ?? ""));
        }
        try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); }
        catch (Exception e) { EditorUtility.DisplayDialog("CSV export failed", e.Message, "OK"); }
    }
}
