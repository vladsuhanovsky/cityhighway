# Shared 3D tree-card refresh optimization and Logistic Magnate coverage

## What changed
The common `City3DScene.UpdateTrees` implementation now keeps the orientation of each tree-card chunk separately and rewrites billboard geometry only if that chunk is showing cards and camera tilt or yaw changes beyond the previous thresholds (3° / 4°). Chunks showing full 3D trees and closed districts do not rebuild invisible card geometry every time the camera moves. The camera's horizontal direction is calculated once per update instead of per chunk.

The existing quality-dependent switch thresholds (125/100/80 screen pixels per world unit), hysteresis (+10), full 3D tree meshes, authored replacements and gameplay behavior stay unchanged. Initial card meshes are still created once when the city is constructed; as a chunk switches from full trees to cards it refreshes its orientation before being rendered.

## Logistic Magnate compatibility
`MagnateView.Setup3D` calls the same `City3DScene.TryCreate(_game.Map, quality, hubs)` as the ordinary mode. This optimization is deliberately in the common renderer rather than a fixed-city scene setup.

`Magnate3DCompatibilityTests` builds 3D plans for generated Magnate maps with seeds 1, 42 and 2026 at each quality density 0.55, 0.8 and 1.0, and also checks the tutorial map. Tests check that there are buildings and trees and that the generated stable building/tree IDs do not collide. This tests planner compatibility but does **not** benchmark GPUs or exercise a full rendered Play Mode session.

## How to validate manually
1. Run an ordinary 3D city, pan/rotate/zoom across close and distant forest. Check for no flicker, missing trees or broken orientation.
2. Start multiple random Logistic Magnate maps at low, medium and high 3D quality and repeat the same checks.
3. In Unity Profiler compare per-frame tree card mesh updates / CPU time before and after changes; any performance gain depends on camera motion and visible chunk count.
4. Confirm Unity Edit Mode CI passes.

No Prefab changes, map save-format changes, rules changes or per-city hand-authored configuration are required.