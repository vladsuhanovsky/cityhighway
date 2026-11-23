# Visual size comparison — procedural target and replacement Prefab

Use the **City Highways → 3D Objects → Procedural Object Catalog** window to compare a generated building or tree with an authored replacement Prefab, before running the game.

## Steps

1. Run a 3D city in Play Mode and click **Capture live 3D catalog for Edit Mode**. Exit Play Mode.
2. Select a scene object with the **CityProceduralReplacement** component. It must have a valid **Stable Object Id**, matching **Map Name** and a **Visual Prefab** assigned.
3. Open the Catalog window and enable **Show clickable markers in Scene View** and **Compare selected Prefab bounds in Scene View**.
4. Scene View displays two wireframe volumes for the selected anchor: **blue** = saved target size/rotation/location, **orange** = combined mesh bounds of the assigned Prefab after applying the anchor's authored Transform.
5. A **yellow dotted line** joins the generated object's position to the anchor pivot; use it to spot misplaced replacements.
6. The **Prefab size comparison** section of the Catalog displays **Prefab / target** size ratios (W/H/D; 1.0x indicates matching extent) and the distance between anchor pivot and generated target position. Use the existing uniform **Fit Prefab scale** button or the Scene View gizmo to adjust your asset.

## Design boundaries

- These are **bounding volume guides**, not exact production geometry. Procedural building roofs/tree canopies and Prefab mesh surfaces are not rendered by this tool.
- The Prefab wire box includes the combined MeshFilter and SkinnedMeshRenderer local bounds, including child transforms, root transform position, rotation, scale and geometry pivot. It does not instantiate the asset.
- Numerical size ratios follow the existing fitting heuristic and use the anchor's world scale, not an exact world oriented bounding box for nonuniformly rotated/hierarchical geometry.
- The target preview uses the saved building-base elevation when available, otherwise center terrain height, otherwise flat ground. Recapture your catalog if map/generator/terrain parameters change.
- **No GameObjects are created** in the edited scene and **no gameplay logic** is modified.
- The comparison is rendered in **Edit Mode only**, and only when the current selection's map, ID, target type and Prefab match the saved catalog.
- Old snapshots without dimensions or missing Prefab geometry do not show a comparison.

## Checks

- [ ] Unity Edit Mode CI succeeds.
- [ ] Assign a building Prefab and verify both differently colored boxes in Scene View.
- [ ] Move/rotate/scale the anchor; orange box follows while blue stays at generated coordinates.
- [ ] Compare W/H/D ratios and center-distance diagnostics in the Catalog window.
- [ ] Try **Fit Prefab scale**, Undo, then repeat with a tree.
- [ ] Select an anchor for another city or remove its Prefab; comparison disappears.
