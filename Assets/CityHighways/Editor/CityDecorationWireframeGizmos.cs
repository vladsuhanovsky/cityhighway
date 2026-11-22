using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lightweight selected-anchor preview: reads mesh assets without instantiating a Prefab
/// or changing the scene hierarchy. Drawn only in the Scene View Gizmos pass.
/// </summary>
public static class CityDecorationWireframeGizmos
{
    [DrawGizmo(GizmoType.Selected)]
    static void DrawSelected(CitySceneDecoration anchor, GizmoType type)
    {
        if (anchor == null || !anchor.PreviewWireframeInScene || anchor.VisualPrefab == null)
            return;

        var prefab = anchor.VisualPrefab;
        var rootToLocal = prefab.transform.worldToLocalMatrix;
        var savedMatrix = Gizmos.matrix;
        var savedColor = Gizmos.color;
        Gizmos.color = anchor.ShowInGame
            ? new Color(0.2f, 0.95f, 0.65f, 0.85f)
            : new Color(0.65f, 0.65f, 0.65f, 0.75f);

        // The root-relative mesh transform also handles nested Prefabs and non-unit model scales.
        // A cap prevents very complex imported models from slowing down the Scene View.
        const int maxMeshes = 128;
        int drawn = 0;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Gizmos.matrix = anchor.transform.localToWorldMatrix *
                            rootToLocal * filter.transform.localToWorldMatrix;
            Gizmos.DrawWireMesh(filter.sharedMesh);
            if (++drawn >= maxMeshes) break;
        }

        if (drawn < maxMeshes)
        {
            foreach (var skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) continue;
                Gizmos.matrix = anchor.transform.localToWorldMatrix *
                                rootToLocal * skinned.transform.localToWorldMatrix;
                Gizmos.DrawWireMesh(skinned.sharedMesh);
                if (++drawn >= maxMeshes) break;
            }
        }

        Gizmos.matrix = savedMatrix;
        Gizmos.color = savedColor;
    }
}
