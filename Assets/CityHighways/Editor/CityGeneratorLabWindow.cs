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
    CitySeedReport current;
    Vector2 scroll;
    string summary = "";
    readonly List<string> failures = new List<string>();

    [MenuItem("City Highways/City Generator Lab")]
    static void Open() => GetWindow<CityGeneratorLabWindow>("City Generator Lab");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Magnate topology preview and reproducible seed diagnostics. No assets, saves or live game state are modified. Timing includes diagnostic checks, not 3D scene generation.", MessageType.Info);
        seed = EditorGUILayout.IntField("Requested seed", seed);
        count = Mathf.Clamp(EditorGUILayout.IntField("Batch count (1–512)", count), 1, 512);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Generate and validate"))
            {
                current = CitySeedDiagnostics.Run(seed);
                failures.Clear();
                summary = current.Passed ? "Valid topology" : "Validation failed";
                if (!current.Passed) SaveFailure(current);
            }
            if (GUILayout.Button("Run seed range")) RunBatch();
        }
        if (!string.IsNullOrEmpty(summary)) EditorGUILayout.HelpBox(summary, MessageType.None);
        if (current == null) return;
        EditorGUILayout.LabelField("Actual seed", current.generated ? current.actualSeed.ToString() : "Unavailable");
        EditorGUILayout.LabelField("Duration", current.milliseconds + " ms");
        EditorGUILayout.LabelField("Locations / roads / hubs",
            (current.locations?.Length ?? 0) + " / " + (current.roads?.Length ?? 0) + " / " + (current.hubs?.Length ?? 0));
        if (current.generated) DrawPreview();
        if (GUILayout.Button("Export selected diagnostic JSON"))
        {
            string dir = EditorUtility.OpenFolderPanel("Export seed diagnostics", "", "");
            if (!string.IsNullOrEmpty(dir))
                try { summary = CitySeedDiagnostics.Write(current, dir); }
                catch (Exception ex) { summary = "Export failed: " + ex.Message; }
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string issue in current.issues ?? Array.Empty<string>())
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        foreach (string line in failures) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    void RunBatch()
    {
        failures.Clear();
        int completed = 0, invalid = 0;
        try
        {
            for (int i = 0; i < count; i++)
            {
                long requested = (long)seed + i;
                if (requested > int.MaxValue) break;
                if (EditorUtility.DisplayCancelableProgressBar("City Generator Lab",
                    "Seed " + requested, i / (float)count)) break;
                current = CitySeedDiagnostics.Run((int)requested);
                completed++;
                if (!current.Passed) { invalid++; SaveFailure(current); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        summary = "Completed " + completed + " / " + count + "; failed " + invalid +
            ". Failure reports: Library/CityHighways/CitySeedDiagnostics.";
    }

    void SaveFailure(CitySeedReport report)
    {
        try
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "CityHighways", "CitySeedDiagnostics"));
            string path = CitySeedDiagnostics.Write(report, dir);
            failures.Add("Seed " + report.requestedSeed + ": " + path);
        }
        catch (Exception ex) { failures.Add("Seed " + report.requestedSeed + ": could not export: " + ex.Message); }
    }

    void DrawPreview()
    {
        var area = GUILayoutUtility.GetAspectRect(1400f / 780f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(.10f, .13f, .15f));
        if (Event.current.type != EventType.Repaint || current.locations == null) return;
        var locations = current.locations.ToDictionary(p => p.id);
        var hubs = new HashSet<string>((current.hubs ?? Array.Empty<string>()).Select(h => h.Split(':')[0]));
        var publicRoads = new HashSet<string>(current.publicRoads ?? Array.Empty<string>());
        Vector3 At(float x, float y) => new Vector3(area.x + x * area.width, area.y + y * area.height, 0);
        Handles.BeginGUI();
        try
        {
            foreach (var river in current.rivers ?? Array.Empty<RiverDto>())
            {
                Handles.color = new Color(.22f, .61f, .87f);
                if (river.points.Length >= 2) Handles.DrawAAPolyLine(3f, river.points.Select(p => At(p.x, p.y)).ToArray());
            }
            foreach (var road in current.roads ?? Array.Empty<RoadDto>())
            {
                if (!locations.TryGetValue(road.from, out var a) || !locations.TryGetValue(road.to, out var b)) continue;
                Handles.color = string.Equals(road.resource, "Steel", StringComparison.OrdinalIgnoreCase) ?
                    new Color(.9f, .7f, .28f) : publicRoads.Contains(road.id) ? Color.white : Color.gray;
                Handles.DrawAAPolyLine(1.5f, At(a.x, a.y), At(b.x, b.y));
            }
        }
        finally { Handles.EndGUI(); }
        foreach (var point in current.locations)
        {
            var p = At(point.x, point.y);
            float size = hubs.Contains(point.id) ? 7 : 4;
            EditorGUI.DrawRect(new Rect(p.x - size / 2, p.y - size / 2, size, size),
                hubs.Contains(point.id) ? new Color(.95f, .4f, .3f) : Color.white);
        }
        EditorGUILayout.LabelField("Blue: rivers · gold: bridges · red: hubs · white: public roads");
    }
}
