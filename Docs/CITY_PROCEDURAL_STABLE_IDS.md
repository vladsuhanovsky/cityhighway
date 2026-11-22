# Stable procedural visual IDs — authored replacements

Individual building/tree replacement anchors support **two modes**:

- **Nearest (legacy)**: leave `Stable Object Id` blank; the system selects the nearest remaining procedural visual of the chosen type within `Search Radius`.
- **Exact ID**: paste `Stable Object Id`; the system targets only that ID and ignores the radius. If the ID no longer exists in the current generated city, the visual remains untouched and the prefab does not spawn.

IDs are derived deterministically from the object type (B/T), generation seed and quantized field position, **not** Unity InstanceID or list position. They are stable while the generator produces the same position/seed, including across runs and changes that do not alter the object. They are **not permanent identifiers** across edited map coordinates or major generator algorithm changes.

## Get an ID from the Unity Editor

1. Open the tracked startup scene and add **GameObject → City Highways → Procedural Object Replacement** with a valid `Map Name` and `Target` type.
2. Place the replacement anchor close to the desired procedural object and assign a `Visual Prefab`. Keep `Stable Object Id` blank for now.
3. Enter **Play Mode** and start a **3D** game using the matching map. While playing, select the replacement anchor in the Hierarchy.
4. Click **Copy nearest generated object ID** in the Inspector. The button looks for a generated object matching the city/type within `Search Radius` and copies its ID into the system clipboard. If none is found, adjust the radius or placement.
5. Exit Play Mode, reselect the anchor and **paste the ID into `Stable Object Id`**, then save the scene. These steps avoid Unity discarding Inspector changes made during Play Mode.
6. On the next 3D run, the Prefab replaces exactly that generated object or stays absent with a warning when it no longer exists.

The catalog is captured before authored replacements remove anything from the procedural plan, and cleared when the runtime 3D city is disposed. The Inspector button is disabled outside Play Mode. If an exclusion zone covers the original target, it will not exist in the catalog; remove or adjust that zone.

## Checks

- [ ] Edit Mode CI tests pass for deterministic IDs and exact matching.
- [ ] Inspector copy button is available in Play Mode and clipboard is filled for a nearby target.
- [ ] Paste ID after exiting Play Mode and verify it replaces the correct object even when changing Search Radius.
- [ ] An invalid ID does not remove another building/tree and does not instantiate a prefab.
- [ ] Legacy anchors with an empty ID still work via nearest-object selection.
- [ ] No changes to Core map, roads, saves, replays or the 2D board.
