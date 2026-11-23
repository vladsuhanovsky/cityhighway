# Precise anchor placement from the 3D object catalog

The **City Highways → 3D Objects → Procedural Object Catalog** window can assign a stable ID **and place a replacement anchor** at the selected generated building/tree's map position.

## Workflow

1. Capture a fresh 3D catalog during Play Mode, then exit Play Mode.
2. Select the **Procedural Object Replacement** anchor in the Hierarchy.
3. In the catalog use a row's **Place** button or click a Scene View dot, then **Assign + Place**.
4. The anchor's `Stable Object Id`, `Map Name` and `Target` are set. Its Transform X/Z are snapped to the generated object's center in the captured catalog.
5. Optionally enable **Match building yaw when placing anchor** before placing to align its Y rotation with the planned building orientation. This option does not affect trees.
6. Manually adjust height (Y), rotation, and scale for the visual Prefab, save the Unity scene, and verify in Play Mode.

## Exact scope

- Placement preserves the anchor's **existing Y position and local scale**, and by default its rotation. It does **not** sample the active terrain height, so on hills you may need to adjust Y manually.
- With rotation alignment enabled, only the Y Euler angle is replaced by the catalog building angle; X/Z Euler angles are preserved.
- The catalog captures exact generated **field coordinates**, but it may become stale when source map, density, quality or exclusion zones change. Capture again after such edits.
- The marker's position is the visual Prefab spawn transform. It does not move the procedural building's generated coordinates or alter roads, maps, saves, or gameplay.
- Assign and placement actions use Undo, mark the scene dirty and are available only in Edit Mode.
- The original **Assign** action remains available when you want to set an ID without moving the anchor.

## Acceptance

- [ ] Unity Edit Mode CI runs cleanly.
- [ ] Use **Place** on a catalog row and verify X/Z snapping, unchanged Y and scale.
- [ ] Check the **Match building yaw** toggle rotates buildings but does not rotate tree replacements.
- [ ] Undo the placement and assignment, then redo; save and reload the scene.
- [ ] Run the 3D city and verify the selected object is replaced by the authored Prefab.
