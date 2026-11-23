# Approximate 3D volumes in the procedural Scene View catalog

This increment adds a **size-only preview** for procedural buildings and trees to the existing **City Highways → 3D Objects → Procedural Object Catalog** window.

## How to use

1. Open the Unity startup scene and the **Procedural Object Catalog** window.
2. Enter Play Mode with 3D enabled, then click **Capture live 3D catalog for Edit Mode**.
3. Exit Play Mode. Enable **Show clickable markers in Scene View** and then **Show approximate building/tree volumes**.
4. Use **Focus** on an object row, or navigate near the area with Scene View.
5. Buildings show a rotated box representing the planned width, depth, height and angle. Trees show a simple vertical line and horizontal width circle.
6. Click a marker, select a **Procedural Object Replacement** anchor and click **Assign selected marker**. Place the replacement Prefab with its Transform and check in Play Mode.

## Important limitations

- These are **approximate guides**, not exact meshes, roofs, canopies, textures, colliders or shadows. The building box follows the procedural footprint and height, not architectural details or uneven terrain.
- The tree height/width is a coarse guide based on the procedural tree's Size field; the actual mesh may differ.
- Previews use the flattened world X/Z projection, without terrain elevation. Actual runtime placement can be higher on hills.
- The overlay is Edit Mode only and does **not** instantiate extra GameObjects or modify generated city logic.
- Only the same nearby subset of at most **120** markers is drawn, so preview remains bounded.
- A snapshot captured before this change has no dimensions; capture a new live catalog to see volumes.
- If you change map coordinates, generation density, quality or exclusions, capture a fresh snapshot.
- Gameplay rules, saves, 2D mode, road generation and JSON maps are unchanged.

## Checks

- [ ] Unity Edit Mode CI compiles and tests succeed.
- [ ] Capture a fresh city, exit Play Mode, enable both toggles and focus a building.
- [ ] Verify the building box rotates and has plausible dimensions.
- [ ] Verify trees show a line and circular width guide.
- [ ] Disable approximate volume toggle: clickable dots remain.
- [ ] Assign a replacement ID and verify saved scene and Play Mode.
