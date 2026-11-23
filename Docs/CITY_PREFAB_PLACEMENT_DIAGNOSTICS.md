# Prefab placement diagnostics in the 3D object catalog

After capturing a 3D city, exiting Play Mode and selecting an authored **Procedural Object Replacement** anchor with a matching **Stable Object Id**, the catalog's **Prefab size comparison** panel now checks three common problems.

- **Oversized:** the largest Prefab-to-generated width/height/depth ratio exceeds **1.15×**.
- **Horizontal displacement:** the marker's X/Z is farther than **max(0.02 world units, 25% of the larger target footprint dimension)** from the generated object's center.
- **Possible ground intersection:** the lowest point of the Prefab's transformed mesh bounding box lies more than **0.01 world units** below the ground height captured at the generated object's center.

The panel shows contextual yellow warnings, or “No placement warnings at current thresholds” when nothing exceeds these thresholds. It does **not** automatically move/resize anything. Use the existing **Place**, **Fit Prefab scale**, Transform handles or manual adjustments to resolve warnings, and check in Play Mode.

## How to verify

1. Open **City Highways → 3D Objects → Procedural Object Catalog**, run a 3D city and choose **Capture live 3D catalog for Edit Mode**.
2. Exit Play Mode, select a replacement anchor with a matching city, object ID and valid 3D Visual Prefab.
3. View **Placement diagnostics** in the Catalog window; try scaling the anchor above 1.15× the target dimension, moving it horizontally, and lowering it into the terrain.
4. Confirm warnings disappear as the anchor is corrected, and Undo/Redo updates them.
5. Test a missing Prefab/flat mesh, stale ID, or older snapshot without captured terrain height: the tool does not invent ground-intersection results.

## Limits and safety

This is **editor-only static-bounds analysis**, not actual mesh-to-mesh intersection. The ground is sampled at a single point (the procedural object's center), so terrain slopes, hollow geometry, complex Prefab pivots, nonuniform parent scales and animation may make a warning approximate. The displacement is measured from the *anchor pivot*, not the center of the Prefab's geometry. W/H/D ratios reuse the approximate existing Prefab-fitting calculation.

The diagnostics do not touch runtime city generation, 2D board state, game rules, JSON maps, saves or replay data.
