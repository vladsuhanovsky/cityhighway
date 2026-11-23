# Replacement Prefab placement at the building footprint height

The 3D Object Catalog now captures two height values for each generated **building**:

- **Terrain center Y**: terrain height at the generated building's center (previous behavior).
- **Building base Y**: the greatest terrain elevation at the building's center or any of its four footprint corners, rotated by the building's generated Y angle. This matches the height chosen by the procedural building renderer before building its plinth.

## Unity workflow

1. Open **City Highways → 3D Objects → Procedural Object Catalog**.
2. Enter Play Mode, open the 3D city and click **Capture live 3D catalog for Edit Mode**. Exit Play Mode.
3. Select the **Procedural Object Replacement** anchor in Hierarchy.
4. Enable **Snap Y to captured terrain height** and then **Use highest building footprint corner for Y**.
5. Select a building, click **Place** or **Assign + Place**, then inspect its Transform in Scene View.
6. Save the scene and verify the assigned Prefab's appearance in the 3D city.

When the second option is off, Y uses the center terrain height, exactly as before. When the first option is off, Y is preserved. Trees always use their center terrain height. **Older snapshots** without building-base data fall back to the center terrain height, and older snapshots without any height information preserve the anchor's Y.

The calculated height represents the *generated building mesh base*, not the pivot or ground contact of every imported Prefab. For Prefabs with a different pivot, adjust Y manually. Sampling uses the active runtime relief when the catalog is captured; recapture after map or generator changes. X/Z placement, IDs, visual generation, road rules and saved games are unaffected.

## Checks

- [ ] Edit Mode tests and CI pass.
- [ ] Capture a fresh catalog, verify both toggles are shown.
- [ ] Place a building with the highest-corner option off/on in a sloped area; compare Y.
- [ ] Place a tree with highest-corner option on; tree should still use center terrain Y.
- [ ] Verify old captures and old anchors work without new data.
- [ ] Check Undo for a placement change and Play Mode prefab positioning.
