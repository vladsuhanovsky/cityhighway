# Individual procedural visual replacements

This first increment allows an **authored Prefab to replace one generated 3D building or tree** rather than clearing an entire zone.

## Unity workflow

1. Open `Assets/CityHighways/Scenes/CityHighways.unity`.
2. Choose **GameObject → City Highways → Procedural Object Replacement**.
3. Set the exact **Map Name**, choose **Target** (Building or Tree), assign **Visual Prefab** and configure **Search Radius** (world units; default 0.5).
4. Place the marker's Transform on the X/Z ground plane where a procedural building/tree is expected. You can use a nearby known coordinate or manually inspect the generated 3D layout in Play Mode.
5. Position/rotate/scale the marker to specify where the replacement Prefab appears. Save the scene and start the matching city with 3D enabled.

## Runtime behavior

- Once the procedural plan is generated (including the existing optional density and clearance settings), each active replacement marker attempts to match **the nearest not-yet-replaced procedural object** of its type within its horizontal search radius.
- On a match the generator removes **one** building or tree from the render plan and spawns the assigned Prefab under the generated city root. Building ground-footprint entries are removed alongside their corresponding main-city building.
- If no matching generated object exists inside the radius, **nothing is removed or spawned**, and Unity logs a warning.
- Matching is performed on each 3D city build. The selected object can change when procedural density, map layout or quality changes. It is not a persistent ID-based replacement system yet.
- Replacement markers are visual-only. CityMap, interactive locations, roads, contracts, 2D board, replay and save data are untouched.
- Use **either** a clearance zone **or** individual replacement at a given spot. A clearance zone may remove the target before replacement matching, causing the marker not to match.
- The scene's preview is an anchor wire-sphere (radius). The assigned Prefab appears only in runtime 3D.

## Verification

- [ ] Unity Edit Mode tests compile and run.
- [ ] One target building/tree is replaced, with no duplicated procedural geometry.
- [ ] A distant marker or marker on another city has no effect on generated visuals.
- [ ] Disabling the marker or clearing the Prefab restores original generation.
- [ ] Returning to the menu and starting another 3D session does not accumulate instances.
- [ ] Verify 2D gameplay, saves and replay still work.
