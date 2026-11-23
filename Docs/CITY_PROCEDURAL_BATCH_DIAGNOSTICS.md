# Batch diagnostics for 3D procedural replacement anchors

Open **City Highways → 3D Objects → Procedural Object Catalog**. Capture a live 3D city catalog in Play Mode, then exit Play Mode.

Click **Scan all replacement anchors in loaded scenes** under **Batch replacement diagnostics**. The scan examines enabled/active `CityProceduralReplacement` components in loaded Unity scenes. Anchors for other city maps and disabled/inactive anchors are skipped. No scene assets or Prefabs are changed.

The summary displays how many anchors were scanned, how many have no warnings, how many were skipped, and how many issues were found. A single anchor can have several warnings. Up to 150 issue rows are displayed, each with **Select** (highlight the anchor in Hierarchy) and **Focus** (also center Scene View around its Transform) actions.

Issues include:
- Missing stable object ID, missing assigned Prefab, missing entry in the captured catalog, or mismatching target kind.
- Invalid/missing 3D mesh geometry.
- A snapshot without terrain height (capture a fresh 3D catalog).
- The existing single-anchor diagnostics: oversized bounding dimensions (greater than 1.15×), horizontal displacement, and possible ground intersection below sampled terrain at the object's center.

The scan is a **snapshot**; re-run it after changing scene objects or Prefabs, applying a quick fix, or capturing a newer catalog. Current map and saved catalog must match. Results use the existing approximate bounding-box logic, not exact collision detection, so manually check any correction in Play Mode.

## Acceptance checks

1. Capture a fresh catalog for one city, exit Play Mode, and run the batch scan.
2. Select an anchor with a correct ID and Prefab and verify it is counted as clean when within tolerance.
3. Create a temporary anchor with no stable ID or Prefab and verify the relevant issue appears.
4. Move, scale and lower a valid replacement to trigger different warnings; use Select and Focus.
5. Disable an anchor or set it to a different city and verify it is skipped.
6. Correct a problem, run the scan again, and verify the count changes.
7. Confirm Unity Edit Mode CI passes. No runtime generation, saves, rules or map assets should change.
