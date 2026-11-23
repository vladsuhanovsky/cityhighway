using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Explicit, undoable editor corrections for an authored replacement anchor.
/// No automatic runtime edits and no changes to source Prefab assets.
/// </summary>
public static class CityProceduralPlacementQuickFixes
{
    public static bool AlignHorizontal(CityProceduralReplacement anchor, Vector3 targetPosition)
    {
        if (EditorApplication.isPlaying || anchor == null ||
            !Finite(targetPosition)) return false;
        var transform = anchor.transform;
        var position = CityProceduralPlacementFixMath.AlignHorizontal(transform.position, targetPosition);
        Undo.RecordObject(transform, "Align replacement anchor to procedural target");
        transform.position = position;
        MarkModified(anchor);
        return true;
    }

    public static bool RaiseAboveGround(CityProceduralReplacement anchor, float terrainY)
    {
        if (EditorApplication.isPlaying || anchor == null ||
            float.IsNaN(terrainY) || float.IsInfinity(terrainY) ||
            !CityProceduralPrefabFitting.TryWorldLowestY(anchor, out var lowestY) ||
            !CityProceduralPlacementFixMath.TryRaiseAboveGround(
                anchor.transform.position.y, lowestY, terrainY, out var correctedY)) return false;
        if (Mathf.Approximately(correctedY, anchor.transform.position.y)) return false;
        var transform = anchor.transform;
        Undo.RecordObject(transform, "Raise replacement Prefab above sampled ground");
        var position = transform.position;
        position.y = correctedY;
        transform.position = position;
        MarkModified(anchor);
        return true;
    }

    static bool Finite(Vector3 position) =>
        !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
        !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
        !float.IsNaN(position.z) && !float.IsInfinity(position.z);

    static void MarkModified(CityProceduralReplacement anchor)
    {
        EditorUtility.SetDirty(anchor.transform);
        if (anchor.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        SceneView.RepaintAll();
    }
}
