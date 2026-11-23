# Catalog terrain height and optional Y snapping

The procedural object catalog now captures **terrain elevation at each generated building/tree center** while a 3D city is running.

## Workflow

1. Enter Play Mode, start a 3D city and open **City Highways → 3D Objects → Procedural Object Catalog**.
2. Click **Capture live 3D catalog for Edit Mode**, then exit Play Mode.
3. Select your **Procedural Object Replacement** anchor.
4. Enable **Snap Y to captured terrain height** if you want its Y position to match the sampled terrain elevation. This toggle is **off by default**.
5. Click **Place** on a row or **Assign + Place** on a selected Scene View marker. The anchor now uses that captured Y as well as the correct X/Z. Optional **Match building yaw** works independently.
6. Save the scene and check the replacement Prefab in Play Mode. Adjust Y manually if needed.

## Important limits

- Heights are sampled at the procedural object's **center**; procedural buildings on sloping terrain may actually rest on their highest footprint corner with an extra plinth. Y snapping is an approximate starting point, not guaranteed perfect placement.
- The sampled catalog position is not updated after edits to map coordinates, terrain or generation logic. **Capture again** after those changes.
- Old local `Library/CityHighwaysProceduralCatalog.json` snapshots have no `hasTerrainY` flag. Y snapping intentionally falls back to preserving the anchor's existing height for those snapshots.
- The existing **Place** commands preserve Y unless you explicitly enable the new toggle.
- Scene View markers and approximate outlines use captured terrain elevation when available.
- This is editor-only placement metadata; gameplay map, visual generation rules, saves, 2D mode and replacement matching remain unchanged.

## Acceptance

- [ ] Unity Edit Mode CI passes.
- [ ] Capture a new 3D catalog on a map with hills.
- [ ] Place a replacement with snapping off: previous Y remains.
- [ ] Enable snapping and Place again: Y changes to the captured elevation.
- [ ] Compare Prefab and generated building/tree height, manually adjust on steep slopes.
- [ ] Verify old snapshots without height data do not reset Y to zero.
