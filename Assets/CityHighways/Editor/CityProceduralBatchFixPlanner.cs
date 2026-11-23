using System;
using System.Collections.Generic;
using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Previews and atomically applies opt-in, undoable repairs to authored anchors.
/// Never writes source Prefabs, scenes or runtime assets without explicit confirmation.
/// </summary>
public static class CityProceduralBatchFixPlanner
{
    public sealed class Proposal
    {
        public CityProceduralReplacement Anchor;
        public string MapName;
        public string ObjectId;
        public Vector3 BeforePosition;
        public Vector3 BeforeScale;
        public Vector3 AfterPosition;
        public Vector3 AfterScale;
    }

    public static bool TryPlan(CityProceduralReplacement anchor, Vector3 targetPosition,
        Vector3 targetSize, float groundY, bool fixOffset, bool fixOversize, bool fixGround,
        out Proposal proposal)
    {
        proposal = null;
        if (anchor == null || EditorApplication.isPlaying || anchor.VisualPrefab == null ||
            !anchor.gameObject.scene.IsValid()) return false;

        var position = anchor.transform.position;
        var scale = anchor.transform.localScale;
        float factor = 1f;

        if (fixOversize)
        {
            if (!CityProceduralPrefabFitting.TryFit(anchor, targetSize, out _, out factor))
                return false;
            // Batch action only shrinks oversized objects; never enlarges smaller ones.
            factor = Mathf.Min(1f, factor);
        }

        if (fixOffset)
            position = CityProceduralPlacementFixMath.AlignHorizontal(position, targetPosition);

        if (fixGround)
        {
            if (float.IsNaN(groundY) || float.IsInfinity(groundY) ||
                !CityProceduralPrefabFitting.TryWorldLowestY(anchor, out float lowest))
                return false;
            // Uniform root scale changes each bound point's offset from the pivot by factor.
            float predictedLowest = position.y + factor * (lowest - anchor.transform.position.y);
            if (!CityProceduralPlacementFixMath.TryRaiseAboveGround(
                position.y, predictedLowest, groundY, out float newY)) return false;
            position.y = newY;
        }

        var newScale = scale * factor;
        if ((position - anchor.transform.position).sqrMagnitude < 0.0000000001f &&
            (newScale - scale).sqrMagnitude < 0.0000000001f) return false;

        proposal = new Proposal
        {
            Anchor = anchor,
            MapName = anchor.MapName,
            ObjectId = anchor.StableObjectId,
            BeforePosition = anchor.transform.position,
            BeforeScale = scale,
            AfterPosition = position,
            AfterScale = newScale
        };
        return true;
    }

    public static int Apply(IReadOnlyList<Proposal> proposals)
    {
        if (EditorApplication.isPlaying || proposals == null || proposals.Count == 0) return 0;
        // Reject the entire batch when any source has changed since preview.
        foreach (var proposal in proposals)
        {
            if (proposal == null || proposal.Anchor == null ||
                !proposal.Anchor.gameObject.scene.IsValid() ||
                !proposal.Anchor.gameObject.scene.isLoaded ||
                !proposal.Anchor.isActiveAndEnabled ||
                !string.Equals(proposal.Anchor.MapName, proposal.MapName, StringComparison.Ordinal) ||
                !string.Equals(proposal.Anchor.StableObjectId, proposal.ObjectId, StringComparison.Ordinal) ||
                proposal.Anchor.transform.position != proposal.BeforePosition ||
                proposal.Anchor.transform.localScale != proposal.BeforeScale)
                return 0;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply procedural replacement batch fixes");
        var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();
        foreach (var proposal in proposals)
        {
            var transform = proposal.Anchor.transform;
            Undo.RecordObject(transform, "Apply procedural replacement batch fixes");
            transform.position = proposal.AfterPosition;
            transform.localScale = proposal.AfterScale;
            EditorUtility.SetDirty(transform);
            dirtyScenes.Add(proposal.Anchor.gameObject.scene);
        }
        foreach (var scene in dirtyScenes)
            EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        SceneView.RepaintAll();
        return proposals.Count;
    }
}
