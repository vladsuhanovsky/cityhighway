# Procedural object vs replacement Prefab: dimension comparison and fit

The **City Highways → 3D Objects → Procedural Object Catalog** window now compares the generated object's approximate dimensions with the **Visual Prefab** assigned to a selected **Procedural Object Replacement** anchor.

## Workflow

1. Open the 3D catalog in Unity, capture a fresh city in Play Mode, and exit Play Mode.
2. Select the **Procedural Object Replacement** anchor in Hierarchy. Assign a visual Prefab with a valid 3D mesh.
3. Select the matching generated object in the catalog (or use the anchor's existing **Stable Object Id**). The selected object must match the anchor's city and target kind.
4. Under **Prefab size comparison**, compare the source model's calculated width/height/depth against the target object's planned dimensions.
5. Click **Fit Prefab scale to generated volume (uniform)** to scale the anchor's Transform proportionally. Undo is supported. Save your scene and verify in 3D.

## What is measured

- The tool reads all MeshFilter.sharedMesh bounds and SkinnedMeshRenderer.localBounds in the assigned Prefab hierarchy, including child transforms and inactive children. It never instantiates the asset or changes the Prefab source.
- The measured source size takes the current anchor's world scale into account. The generated target uses the catalog's approximate width, height and depth.
- Uniform fit uses the **smallest ratio** over width, height and depth, so the new mesh fits into the target volume in all three dimensions while preserving aspect ratio.
- Only the anchor's **localScale** is multiplied; position, rotation, Prefab source and procedural stable IDs remain unchanged.
- When there is no 3D geometry, a flat mesh, a missing assigned Prefab or invalid dimensions, the fit action is unavailable.
- The feature is editor-only: no automatic runtime rescaling of player assets or change to the procedural generator.

## Limitations

- This is a **bounding-box** comparison, not exact mesh-to-mesh fitting. It does not align building roofs or tree canopies and may leave empty space.
- Prefab pivot placement, internal rotations and terrain slope still need manual inspection. In particular, fitting a model with a different pivot does not ensure that its feet meet the ground.
- The approximation assumes the Prefab and target object's local axes are aligned; mismatched yaw or a rotated imported model may require manual adjustment.
- Snapshot dimensions depend on current quality/density/map generation; capture again after changing these.
- Skinned Mesh Renderer bounds can be pose-dependent; visual results must be checked in Play Mode.
- A parent with unusual rotation or nonuniform scale can make bounds comparison approximate.

## Acceptance

- [ ] Unity CI compiles and Edit Mode tests pass.
- [ ] Select a generated building and an anchor with a valid 3D Prefab; compare W/H/D.
- [ ] Fit it, then Undo/Redo the change.
- [ ] Repeat with a tree replacement.
- [ ] Confirm a missing/flat Prefab does not change the scale.
- [ ] Verify runtime replacement uses the authored Transform without changing saves or gameplay.
