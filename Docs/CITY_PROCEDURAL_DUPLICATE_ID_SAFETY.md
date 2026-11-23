# Duplicate stable ID detection and safe batch previews

The procedural replacement editor now checks for conflicting authored anchors that target the **same stable generated object** in the **same city**.

## Workflow

1. In Play Mode, capture the procedural catalog for a 3D city, then exit Play Mode.
2. Open **City Highways → 3D Objects → Procedural Object Catalog** and run **Scan all replacement anchors in loaded scenes**.
3. Select **DuplicateId** in **Issue type** to see conflicts. Both (or all) active anchors sharing the same nonempty ID are reported with their shared ID. Use **Select** or **Focus** to inspect them.
4. Pick the intended replacement for that generated object and change or disable the other conflicting anchors. Re-run the scan.
5. If using **Preview fixes for filtered issues**, duplicate-ID anchors are excluded from automatic corrections; they must be resolved manually. CSV export includes the conflict category and ID.

Only enabled, active anchors belonging to the captured city are included in duplicate detection. Anchors for other cities are not conflicts, and empty IDs are handled separately by the existing MissingObjectId diagnostic.

## Extra batch apply safeguards

Before applying a preview, the editor rejects the entire batch if any proposed anchor has changed its assigned Prefab, rotation, full world matrix (including parent transform), stable ID, map name, position or scale. It also verifies that each proposed stable ID is still uniquely owned by one active replacement anchor in the loaded scenes. If any condition fails, **no Transform changes are applied**; re-run Scan and Preview.

The existing single-Undo-group operation is preserved. No runtime behavior, maps, original Prefab assets, saved games or replay data is changed.

## Acceptance

- [ ] Create two active replacements with the same ID and city; run Scan and filter by **DuplicateId**. Both must be shown.
- [ ] Set one anchor to another city or disable it; re-scan. The conflict must disappear.
- [ ] Verify duplicate anchors cannot be included in bulk quick fixes.
- [ ] Preview valid fixes; change a Prefab, rotate the anchor, or move its parent. Apply must reject the entire batch.
- [ ] Preview valid fixes; create a second active anchor with the same ID. Apply must reject the batch.
- [ ] Check Unity Edit Mode CI and manual Undo/Redo.
