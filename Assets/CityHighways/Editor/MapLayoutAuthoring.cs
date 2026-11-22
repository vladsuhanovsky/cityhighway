using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CityHighways.Core;
using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene View layout authoring for existing JSON maps. Only location x/y fields are modified on export;
/// game rules, road length/resources, contracts and other JSON fields are never regenerated.
/// </summary>
public static class MapLayoutAuthoring
{
    const string DefaultMap = "Assets/CityHighways/Resources/CityHighways/Maps/moscow.json";
    const float WorldWidth = 14f;
    const float WorldDepth = 7.8f;

    [MenuItem("City Highways/Map Layout/1. Import Moscow markers")]
    static void ImportMoscow()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultMap);
        if (asset == null)
        {
            Debug.LogError("City Highways: map JSON not found: " + DefaultMap);
            return;
        }
        ImportMap(asset);
    }

    static void ImportMap(TextAsset asset)
    {
        CityMapDto dto;
        try { dto = JsonUtility.FromJson<CityMapDto>(asset.text); }
        catch (Exception e) { Debug.LogError("Cannot parse map: " + e); return; }
        if (dto == null || dto.locations == null || dto.roads == null)
        {
            Debug.LogError("Map JSON has no locations or roads.");
            return;
        }
        // Never delete a hand-edited preview on repeated import.
        if (UnityEngine.Object.FindObjectsByType<MapLayoutPreview>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0)
        {
            Debug.LogWarning("A map preview already exists. Delete or save it manually before importing another.");
            return;
        }

        var root = new GameObject("Map Layout Preview - " + asset.name);
        Undo.RegisterCreatedObjectUndo(root, "Import map preview");
        var preview = root.AddComponent<MapLayoutPreview>();
        preview.Configure(asset, dto.roads.Select(r => new MapLayoutPreview.RoadLink { from = r.from, to = r.to }).ToList());
        foreach (var location in dto.locations)
        {
            if (string.IsNullOrEmpty(location.id)) continue;
            var marker = new GameObject(location.id + " - " + location.name);
            Undo.RegisterCreatedObjectUndo(marker, "Import location");
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3((location.x - 0.5f) * WorldWidth, 0.05f,
                (0.5f - location.y) * WorldDepth);
            marker.AddComponent<MapLocationMarker>().SetId(location.id);
        }
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Imported " + dto.locations.Length + " map markers. Move child objects in Scene View and use Export.");
    }

    [MenuItem("City Highways/Map Layout/2. Export selected preview to JSON")]
    static void ExportSelected()
    {
        var preview = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<MapLayoutPreview>() : null;
        if (preview == null || preview.SourceMap == null)
        {
            Debug.LogError("Select a Map Layout Preview or one of its location markers.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(preview.SourceMap);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/CityHighways/Resources/CityHighways/Maps/", StringComparison.Ordinal)
            || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("Export only supports the project's tracked map JSON assets.");
            return;
        }

        var markers = preview.GetComponentsInChildren<MapLocationMarker>(true);
        var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (var marker in markers)
        {
            if (string.IsNullOrEmpty(marker.Id) || positions.ContainsKey(marker.Id))
            {
                Debug.LogError("Map marker IDs must be non-empty and unique.");
                return;
            }
            Vector3 pos = preview.transform.InverseTransformPoint(marker.transform.position);
            float x = pos.x / WorldWidth + 0.5f;
            float y = 0.5f - pos.z / WorldDepth;
            if (float.IsNaN(x) || float.IsNaN(y) || x < 0f || x > 1f || y < 0f || y > 1f)
            {
                Debug.LogError("Marker outside normalized map bounds: " + marker.Id);
                return;
            }
            positions.Add(marker.Id, new Vector2(x, y));
        }

        string original = File.ReadAllText(path, Encoding.UTF8);
        string updated;
        try { updated = ReplaceLocationCoordinates(original, positions); }
        catch (Exception e) { Debug.LogError("Map export aborted: " + e.Message); return; }

        if (!EditorUtility.DisplayDialog("Export map coordinates",
                "Update " + positions.Count + " locations in " + path + "?\nOther JSON fields will be preserved. Review the Git diff before committing.",
                "Export", "Cancel")) return;
        File.WriteAllText(path, updated, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log("Map coordinates exported: " + path + ". Review the Git diff.");
    }

    static string ReplaceLocationCoordinates(string json, Dictionary<string, Vector2> positions)
    {
        var start = Regex.Match(json, @"""locations""\s*:\s*\[");
        if (!start.Success) throw new FormatException("Missing locations array.");
        int open = start.Index + start.Length - 1;
        int depth = 0, close = -1;
        bool inString = false, escaped = false;
        for (int i = open; i < json.Length; ++i)
        {
            char c = json[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == (char)92) escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '[') ++depth;
            else if (c == ']' && --depth == 0) { close = i; break; }
        }
        if (close < 0) throw new FormatException("Unclosed locations array.");

        string array = json.Substring(open + 1, close - open - 1);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        string changed = Regex.Replace(array, @"\{[^{}]*\}", match =>
        {
            var dto = JsonUtility.FromJson<LocationDto>(match.Value);
            if (dto == null || string.IsNullOrEmpty(dto.id) || !seen.Add(dto.id))
                throw new FormatException("Invalid or duplicate location ID.");
            if (!positions.TryGetValue(dto.id, out var point))
                throw new FormatException("Missing marker for location " + dto.id);
            string block = SetNumber(match.Value, "x", point.x);
            block = SetNumber(block, "y", point.y);
            ++count;
            return block;
        });
        if (count != positions.Count || count == 0)
            throw new FormatException("Marker count differs from source map.");
        return json.Substring(0, open + 1) + changed + json.Substring(close);
    }

    static string SetNumber(string block, string field, float value)
    {
        string pattern = @"(" + @"""" + Regex.Escape(field) + @"""\s*:\s*)(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)";
        int matches = 0;
        string updated = Regex.Replace(block, pattern, match =>
        {
            ++matches;
            return match.Groups[1].Value + value.ToString("0.######", CultureInfo.InvariantCulture);
        });
        if (matches != 1) throw new FormatException("Missing/duplicate " + field + " coordinate.");
        return updated;
    }
}
