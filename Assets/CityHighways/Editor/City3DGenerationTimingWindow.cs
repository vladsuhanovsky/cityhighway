using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only timing breakdown for the latest active generated 3D city.</summary>
public sealed class City3DGenerationTimingWindow : EditorWindow
{
    Vector2 scroll;

    [MenuItem("City Highways/3D Objects/Generation Stage Timings")]
    static void Open() => GetWindow<City3DGenerationTimingWindow>("3D Generation Timings");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Wall-clock timing of the real City3DScene.Build stages. Includes Unity and " +
            "Editor overhead; not isolated CPU Profiler, GPU time or gameplay FPS. " +
            "Measurements are captured once when the city is created.",
            MessageType.Info);

        var scene = City3DScene.ActiveScene;
        var report = scene != null ? scene.GenerationTiming : null;
        if (report == null || report.Stages.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No completed 3D city generation is active. Enter Play Mode and " +
                "start a 3D game, then reopen this window.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("City", report.City);
        EditorGUILayout.LabelField("Quality", report.Quality.ToString());
        EditorGUILayout.LabelField("Captured UTC", report.StartedUtc.ToString(
            "O", CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Build total",
            report.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build order", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var stage in report.Stages)
        {
            double percentage = report.TotalMilliseconds > 0d
                ? 100d * stage.Milliseconds / report.TotalMilliseconds : 0d;
            EditorGUILayout.LabelField(stage.Name,
                stage.Milliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                " ms (" + percentage.ToString("F1", CultureInfo.InvariantCulture) + "%)");
        }
        EditorGUILayout.EndScrollView();

        var slowest = report.Stages.OrderByDescending(x => x.Milliseconds).First();
        EditorGUILayout.HelpBox(
            "Longest measured stage: " + slowest.Name + " (" +
            slowest.Milliseconds.ToString("F3", CultureInfo.InvariantCulture) +
            " ms). Validate with Unity Profiler across repeat scene loads.",
            MessageType.Info);

        var hotspots = scene.HotspotTiming;
        if (hotspots != null && hotspots.Samples.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hotspot details (wall-clock)", EditorStyles.boldLabel);
            foreach (var sample in hotspots.Samples.OrderByDescending(x => x.Milliseconds))
                EditorGUILayout.LabelField(sample.Name,
                    sample.Milliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms");
            EditorGUILayout.HelpBox(
                "Some hotspot measurements are nested inside other hotspots. " +
                "Do not add them together or interpret them as FPS or GPU time.",
                MessageType.Info);
            if (GUILayout.Button("Save detailed hotspot timings CSV"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Save 3D hotspot timing CSV",
                    "", "city3d-generation-hotspots.csv", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.WriteAllText(path, hotspots.ExportCsv(Application.unityVersion),
                            new UTF8Encoding(false));
                        Debug.Log("City3D detailed hotspot timings saved: " + path);
                    }
                    catch (Exception ex)
                    {
                        EditorUtility.DisplayDialog("Cannot save hotspot timings",
                            ex.Message, "OK");
                    }
                }
            }
        }

        if (GUILayout.Button("Save stage timings CSV"))
        {
            string path = EditorUtility.SaveFilePanel("Save 3D generation timing CSV",
                "", "city3d-generation-stages.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    File.WriteAllText(path, report.ExportCsv(Application.unityVersion),
                        new UTF8Encoding(false));
                    Debug.Log("City3D generation timing CSV saved: " + path);
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("Cannot save timings",
                        ex.Message, "OK");
                }
            }
        }
    }
}
