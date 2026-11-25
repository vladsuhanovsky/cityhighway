# Road marking alignment (Magnate / Classic 3D)

Reported symptom: a white dashed line appears beside a long owned road from Южный вокзал toward Ратуша in a generated Logistics Magnate city. Exact seed, camera state and original save are unavailable, so the specific reproduction is not confirmed.

The previous implementation calculated lane marks and curbs from station positions again, but owner bands used the final BuiltRoad plan. All three now derive position, direction and heights from that plan. Road details are invalidated if the BuiltRoad instance is replaced, even with unchanged owner. Marking strips are clipped to actual piece bounds; owner bands are split at ramp/bridge boundaries. A detected bridge is treated as a bridge for visual details even if the map flag is false.

Roads are currently straight in plan view; the dash cadence remains the existing horizontal-world-distance cadence. No road graph, river generation, save format, UI overlay, materials or gameplay changes. Custom prefabs still need to obey RoadBuilder's documented surface/pivot convention.

## Manual acceptance
- Generate multiple Magnate cities; build or claim diagonal roads, including public and player-owned roads.
- Zoom in, rotate camera, tilt and inspect central dashes, colored bands, curbs and junction ends.
- Inspect a sloped road, bridge ramps and a nearby unbuilt route; the latter remains a separate UI overlay.
- Check a Classic map, reload an existing game, and undo/redo or rebuild a road. No old road-detail mesh should remain.
- If the original offset remains, capture the seed, exact road endpoints, save, and camera angle. Inspect whether the offset is a 3D Lane marks object or a 2D MapView overlay before modifying projection.

Unity Edit Mode CI is required; manual visual reproduction remains necessary.
