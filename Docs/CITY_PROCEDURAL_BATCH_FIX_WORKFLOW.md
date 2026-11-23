# Batch replacement cleanup: grouped issues, preview and one Undo operation

The **City Highways → 3D Objects → Procedural Object Catalog** window now combines three steps for reviewing a large map without editing every replacement anchor separately.

## 1. Group issues by anchor

Capture a current 3D city catalog in Play Mode, exit Play Mode and run **Scan all replacement anchors in loaded scenes**. Enable **Group issues by anchor** to combine every matching warning for the same replacement object into one list row. Disable it to see separate issue rows, as before. Issue-type filtering and CSV export are unchanged: CSV still includes **one row for every issue**, even when the display is grouped.

## 2. Preview the corrections

Pick **Issue type** (All, Oversized, Offset or BelowGround; other categories have no safe automatic fix) and select **Preview fixes for filtered issues**.

The preview lists original and proposed Transform **position and scale** for each affected anchor. It only includes enabled, matching city/type/ID anchors with a current captured terrain height. Configuration/geometry problems are not changed automatically.

- **Offset**: align X/Z to the catalog target, preserve Y.
- **Oversized**: use the uniform dimension-fit multiplier, **shrinking only**; never enlarge a model during batch cleanup.
- **BelowGround**: raise Y by the estimated terrain penetration. When combined with a resize, the predicted lowest corner is recalculated for the proposed uniform scale.
- **All**: combine applicable corrections on each anchor in one proposal. Changes to rotation, source Prefab, ID, map data, game rules, saves and runtime code are never proposed.

The first 15 proposals are displayed, with an additional count if more exist. Preview does not mutate scene objects. It is based on the latest completed **Scan**, and it is cleared when the catalog or issue filter changes or another Scan runs.

## 3. Apply with confirmation

Click **Apply previewed fixes (single Undo group)** and confirm the dialog. Before changing any object, the tool checks that **all** previewed anchors are still loaded, enabled, and have the same IDs, city, position and scale as when previewed. If any have changed, **no changes are applied**; scan and preview again.

If all checks pass, the tool adjusts only the authored anchor Transforms, marks the affected scenes dirty, and records one Unity Undo group for the batch. A new audit scan is run afterward to refresh the warnings.

The approximations remain bounding-box based: terrain is sampled at the generated object's center; unusual pivot positions and sloped ground may require manual adjustment and Play Mode visual verification.

## Acceptance

1. Capture a fresh catalog and scan a scene with several replacement anchors.
2. Verify grouped and ungrouped views contain the same underlying warnings; check Select and Focus.
3. Filter by Offset and Preview; ensure only X/Z changes are proposed.
4. Filter by Oversized and Preview; ensure only reduction in scale is proposed.
5. Choose All to preview combined fixes, verify the counts and positions, then Apply.
6. Confirm one Undo restores all previous Transforms; Redo restores them.
7. Change an anchor between Preview and Apply; confirm the whole batch is rejected.
8. Confirm configuration problems and missing catalog objects are never automatically fixed.
9. Run Unity Edit Mode CI and inspect replacements in Play Mode.
