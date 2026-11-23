using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only read of static mesh geometry from the assigned visual Prefab.
/// Never instantiates assets or changes the prefab itself.
/// </summary>
public static class CityProceduralPrefabFitting
{
    static void EncapsulateMesh(ref Bounds result, ref bool hasBounds, Bounds meshBounds, Matrix4x4 matrix)
    {
        var c = meshBounds.center;
        var e = meshBounds.extents;
        for (int i = 0; i < 8; i++)
        {
            var point = matrix.MultiplyPoint3x4(c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z));
            if (!hasBounds) { result = new Bounds(point, Vector3.zero); hasBounds = true; }
            else result.Encapsulate(point);
        }
    }

    public static bool TryLocalBounds(GameObject prefab, out Bounds bounds)
    {
        bounds = default;
        if (prefab == null) return false;
        bool found = false;
        var rootInverse = prefab.transform.worldToLocalMatrix;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            EncapsulateMesh(ref bounds, ref found, filter.sharedMesh.bounds,
                rootInverse * filter.transform.localToWorldMatrix);
        }
        foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null) continue;
            EncapsulateMesh(ref bounds, ref found, skin.localBounds,
                rootInverse * skin.transform.localToWorldMatrix);
        }
        return found && bounds.size.x > 0.000001f && bounds.size.y > 0.000001f &&
            bounds.size.z > 0.000001f;
    }

    /// <summary>
    /// Gets the actual lowest world Y of the eight combined local-bounds corners.
    /// This is a coarse bounds test, not a per-triangle terrain collision.
    /// </summary>
    public static bool TryWorldLowestY(CityProceduralReplacement anchor, out float lowestY)
    {
        lowestY = 0f;
        if (anchor == null || !TryLocalBounds(anchor.VisualPrefab, out var local)) return false;
        var matrix = anchor.transform.localToWorldMatrix;
        float min = float.PositiveInfinity;
        var c = local.center;
        var e = local.extents;
        for (int i = 0; i < 8; i++)
        {
            var point = matrix.MultiplyPoint3x4(c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z));
            min = Mathf.Min(min, point.y);
        }
        if (float.IsNaN(min) || float.IsInfinity(min)) return false;
        lowestY = min;
        return true;
    }

    public static bool TryFit(CityProceduralReplacement anchor, Vector3 targetSize,
        out Vector3 sourceSize, out float factor)
    {
        sourceSize = Vector3.zero;
        factor = 1f;
        if (anchor == null || !TryLocalBounds(anchor.VisualPrefab, out var local)) return false;
        sourceSize = Vector3.Scale(local.size, new Vector3(
            Mathf.Abs(anchor.transform.lossyScale.x), Mathf.Abs(anchor.transform.lossyScale.y),
            Mathf.Abs(anchor.transform.lossyScale.z)));
        return CityProceduralFitMath.TryUniformFit(local.size, anchor.transform.lossyScale, targetSize, out factor);
    }

    public static bool ApplyUniformFit(CityProceduralReplacement anchor, Vector3 targetSize)
    {
        if (EditorApplication.isPlaying || anchor == null ||
            !TryFit(anchor, targetSize, out _, out var factor)) return false;
        Undo.RecordObject(anchor.transform, "Fit replacement Prefab to procedural volume");
        anchor.transform.localScale *= factor;
        EditorUtility.SetDirty(anchor.transform);
        if (anchor.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        SceneView.RepaintAll();
        return true;
    }
}
