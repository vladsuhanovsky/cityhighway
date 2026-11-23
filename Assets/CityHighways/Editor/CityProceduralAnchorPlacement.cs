using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only placement of an authored replacement on catalog X/Z coordinates.
/// Keeps the artist-adjusted vertical offset and scale; does not modify map or runtime data.
/// </summary>
public static class CityProceduralAnchorPlacement
{
    public static Vector3 WorldPosition(Vector2 fieldPosition, float currentY)
    {
        return CityPlanner.ToFlatWorld(fieldPosition, currentY);
    }

    public static float ResolveHeight(float currentY, float capturedTerrainY,
        bool useCapturedTerrainHeight, bool hasCapturedTerrainHeight)
    {
        return useCapturedTerrainHeight && hasCapturedTerrainHeight ? capturedTerrainY : currentY;
    }

    public static void Place(CityProceduralReplacement anchor, Vector2 fieldPosition,
        float generatedAngle, bool applyBuildingRotation, float capturedTerrainY = 0f,
        bool useCapturedTerrainHeight = false, bool hasCapturedTerrainHeight = false)
    {
        if (anchor == null || EditorApplication.isPlaying) return;
        var transform = anchor.transform;
        Undo.RecordObject(transform, "Place procedural replacement anchor");
        transform.position = WorldPosition(fieldPosition,
            ResolveHeight(transform.position.y, capturedTerrainY,
                useCapturedTerrainHeight, hasCapturedTerrainHeight));
        if (applyBuildingRotation)
        {
            var angles = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(angles.x, generatedAngle, angles.z);
        }
        EditorUtility.SetDirty(transform);
        if (anchor.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        SceneView.RepaintAll();
    }
}
