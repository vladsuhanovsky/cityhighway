# Quick fixes for procedural replacement placement warnings

The **City Highways → 3D Objects → Procedural Object Catalog** window offers three optional, editor-only corrections below the matching placement warnings.

- **Fix oversized Prefab: uniform fit** — runs the existing aspect-preserving scale fit. Only localScale is changed; it cannot compensate for a mismatched pivot or unusual rotations.
- **Fix horizontal offset: align X/Z** — sets the replacement anchor's X/Z to the captured generated object's center, preserving its current Y, rotation and scale.
- **Fix possible ground intersection: raise Y** — computes the lowest world-space corner of the model's combined mesh bounds and raises the anchor Y just enough to meet the captured center terrain elevation. It never lowers a model and does not change X/Z or scale.

## Unity workflow

1. In Play Mode capture a fresh 3D city catalog and exit Play Mode.
2. Select a scene-authored **Procedural Object Replacement** with **Stable Object Id** and an assigned valid 3D **Visual Prefab**.
3. Inspect **Placement diagnostics** in the catalog. For any warning, use the corresponding **Fix** button.
4. Inspect the blue procedural and orange Prefab outlines in Scene View. Undo/Redo individual adjustments and save the scene.
5. Verify the replacement visually in Play Mode, especially when the target stands on a slope.

All fixes require a valid selected anchor and matching city/type/ID. The ground-level fix additionally requires newly captured terrain height. No correction is applied automatically.

**Important:** the terrain height comes from one sampled point at the generated object's center; this is an approximate guide, not a terrain collision solver. The Prefab's combined bounding box might contain invisible/empty volume. The correction may still need manual adjustment on steep slopes or with unusual pivots. The source Prefab asset, procedural generator, maps, rules, saved games and replay records are untouched.

## Verification

- [ ] Unity CI and Edit Mode tests succeed.
- [ ] Oversize → Fit adjusts scale with Undo.
- [ ] Horizontal offset → Align corrects X/Z while preserving Y.
- [ ] Ground warning → Raise corrects the model's lowest bounding corner, never moves it down.
- [ ] A stale/no-height snapshot does not offer ground correction.
- [ ] Recheck appearance on sloping terrain in Play Mode.
