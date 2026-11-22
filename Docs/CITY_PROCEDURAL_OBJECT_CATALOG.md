# Procedural Object Catalog window

The window lists deterministic generated 3D building and tree IDs for an actual running 3D city. It makes assigning **Stable Object Id** less error-prone than copying an ID from an individual replacement Inspector.

## Workflow

1. Open `Assets/CityHighways/Scenes/CityHighways.unity` and add a **Procedural Object Replacement** anchor from **GameObject → City Highways**. Assign its visual Prefab.
2. Open **City Highways → 3D Objects → Procedural Object Catalog**.
3. Enter **Play Mode**, start the desired city with 3D graphics and click **Capture live 3D catalog for Edit Mode** in the window.
4. Exit Play Mode; the captured list remains available from the local `Library/CityHighwaysProceduralCatalog.json` cache (which is **not** committed into Git).
5. Select your replacement anchor in Hierarchy. Search or filter the window by object ID and type. Click **Focus** to center Scene View on the *approximate flat-ground position* of the generated object; the actual visual exists only while the game is running.
6. Click **Assign** on the chosen row. The editor sets **Stable Object Id**, **Map Name** and **Target** on the selected anchor, with Undo support. Move the anchor where you want the replacement Prefab, then save the scene.
7. Run the matching map in 3D to verify that the intended procedural object disappears and the authored Prefab appears.

The **Copy** action also copies an object ID to clipboard for manual use.

## Caveats

- This is a **list and Scene View focus tool**, not a selectable mesh overlay or a live city view in Edit Mode.
- The snapshot represents the exact map and generator settings used at capture. After moving locations, modifying density/clearance zones, switching graphics quality or changing generator code, capture a new snapshot to avoid stale IDs.
- Only one local snapshot is retained at a time. The window displays the active live catalog in Play Mode; the last saved snapshot otherwise.
- **Assign is intentionally disabled in Play Mode**, because Unity would discard scene edits upon exiting Play Mode.
- Only active procedural buildings and trees in the 3D render plan are catalogued, not roads, hubs, decals or decorative Props.
- The list limits visible rows for responsiveness. Use **Find ID** and **Type** to narrow results.

## Verification

- [ ] Edit Mode CI compiles and tests pass.
- [ ] Live 3D game shows a nonempty catalog; capture it.
- [ ] Leave Play Mode: saved rows remain visible in Edit Mode.
- [ ] Select a replacement anchor and assign the ID to it; save the scene and verify it persists.
- [ ] Check Copy, Focus, filters and Undo.
- [ ] Confirm a newly captured catalog for another city overwrites the old local snapshot, without touching gameplay files.
