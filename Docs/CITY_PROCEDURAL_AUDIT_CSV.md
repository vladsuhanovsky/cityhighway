# Batch audit filters and CSV export

From **City Highways → 3D Objects → Procedural Object Catalog**, capture a live 3D city catalog in Play Mode, then exit Play Mode. Run **Scan all replacement anchors in loaded scenes**.

## Filtering

Use **Issue type** to show one class of warning:
- **All** (default): every problem.
- **Configuration**: missing stable ID, missing Prefab, nonexistent catalog ID, or wrong object kind.
- **Geometry**: missing/invalid mesh bounds or invalid measurements.
- **Oversized**: Prefab dimensions exceed the existing threshold.
- **Offset**: anchor horizontal position exceeds tolerance.
- **BelowGround**: Prefab bounds potentially intersect sampled ground.
- **MissingTerrain**: saved catalog lacks terrain elevation.

The total issue count remains independent from the filter. **Matching issues** and the issue list update for the selected category, while **Select** and **Focus** still navigate to the scene anchor. At most 150 matching issues are shown in the UI; CSV export is **not** restricted to those first 150.

## Export

Click **Export filtered issues as CSV...**, choose a file path, and save. The file contains only the currently filtered issues from the last completed scan, with the columns:

`City, Scene, Anchor, TargetKind, StableObjectId, IssueCategory, Description, WorldX, WorldY, WorldZ`

The export writes UTF-8 with BOM for spreadsheet interoperability. CSV values are quoted and internal quotes escaped. Text beginning with a spreadsheet formula prefix (=, +, -, @) including after whitespace is prefixed with an apostrophe to prevent formula execution on import.

World coordinates use invariant numeric formatting. No repository files or Unity scene assets are modified by exporting. The report reflects the **last scan**, not a live background check: scan again after changing the scene, map, replacements or catalog. Canceling the save dialog produces no file.

## Manual acceptance

1. Start a 3D city and capture its catalog; exit Play Mode and run Scan.
2. Select **Configuration** and confirm invalid IDs/Prefabs appear while unrelated warnings disappear.
3. Select **Oversized** or **Offset** and verify only the corresponding warnings display.
4. Export CSV, open it in a spreadsheet, check Cyrillic object names, commas/newlines/quotes and numeric columns.
5. Verify more than 150 issues can be exported despite the UI display cap.
6. Cancel the dialog, re-run Scan after a correction, and confirm counts update.
7. Verify Edit Mode NUnit tests and CI.
