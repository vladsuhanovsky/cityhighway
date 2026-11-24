using System;
using System.Globalization;
using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Manual side-by-side comparison for two raw MeshBuilder.Append CSV exports.</summary>
public sealed class City3DMeshAppendCompareWindow : EditorWindow
{
    string baselinePath;
    string currentPath;
    string baselineCsv;
    string currentCsv;
    string error;
    City3DMeshAppendBenchmarkComparison.Report report;

    [MenuItem("City Highways/3D Objects/Compare MeshBuilder Append CSV")]
    static void Open() =>
        GetWindow<City3DMeshAppendCompareWindow>("Mesh Append Compare");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Compare two saved synthetic MeshBuilder.Append CSV captures. " +
            "Matching Unity versions, benchmark workloads, scenarios and run counts " +
            "are required. Results are advisory Editor CPU timings, not FPS or GPU time. " +
            "Use the same machine and Editor workload; filenames do not verify revisions.",
            MessageType.Info);

        if (GUILayout.Button("Load baseline (before) CSV"))
            LoadFile(true);
        EditorGUILayout.LabelField("Baseline", string.IsNullOrEmpty(baselinePath)
            ? "(not selected)" : Path.GetFileName(baselinePath));

        if (GUILayout.Button("Load current (after) CSV"))
            LoadFile(false);
        EditorGUILayout.LabelField("Current", string.IsNullOrEmpty(currentPath)
            ? "(not selected)" : Path.GetFileName(currentPath));

        using (new EditorGUI.DisabledScope(
            baselineCsv == null || currentCsv == null))
        {
            if (GUILayout.Button("Compare median run times"))
            {
                if (!City3DMeshAppendBenchmarkComparison.TryCompare(
                    baselineCsv, currentCsv, out report, out error))
                    report = null;
            }
        }

        if (!string.IsNullOrEmpty(error))
            EditorGUILayout.HelpBox(error, MessageType.Warning);

        if (report == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Workload", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Unity", report.UnityVersion);
        EditorGUILayout.LabelField("Source quads", report.SourceQuads.ToString());
        EditorGUILayout.LabelField("Appends per run", report.AppendsPerRun.ToString());
        foreach (var item in report.Scenarios)
        {
            string delta = (item.ChangePercent > 0d ? "+" : "") +
                item.ChangePercent.ToString("F2", CultureInfo.InvariantCulture);
            EditorGUILayout.HelpBox(
                item.Name + ": " +
                item.BaselineMedianMs.ToString("F3", CultureInfo.InvariantCulture) +
                " → " +
                item.CurrentMedianMs.ToString("F3", CultureInfo.InvariantCulture) +
                " ms (elapsed time " + delta + "%, " +
                item.Runs + " runs in each capture). " +
                "Negative change means shorter measured time.",
                MessageType.None);
        }
        EditorGUILayout.HelpBox(
            "Differences reflect synthetic Editor wall-clock samples. " +
            "They are not confidence intervals or evidence of an end-to-end FPS change. " +
            "Confirm with repeated runs and the Unity Profiler in real scenes.",
            MessageType.Info);
    }

    void LoadFile(bool baseline)
    {
        string path = EditorUtility.OpenFilePanel(
            baseline ? "Select baseline benchmark CSV" :
                "Select current benchmark CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            string contents = File.ReadAllText(path);
            if (baseline)
            {
                baselinePath = path;
                baselineCsv = contents;
            }
            else
            {
                currentPath = path;
                currentCsv = contents;
            }
            report = null;
            error = null;
        }
        catch (Exception ex)
        {
            report = null;
            error = "Cannot load benchmark CSV: " + ex.Message;
        }
    }
}
