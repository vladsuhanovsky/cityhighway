# Play Mode smoke coverage

Two runtime coroutine tests: actual Classic bot actions across frames then JSON snapshot/load/replay equivalence, and UI Toolkit MapView attachment, map replacement and transient state cleanup across frames. The latter creates an isolated UIDocument; it does not start the full application, use a player's saves, exercise real controller/Steam or certify rendering performance.
Use Unity Test Runner > PlayMode > CityHighways.PlayModeTests. CI uses the separate Unity Play Mode workflow when available. Keep the existing Edit Mode suite; don't treat these two tests as end-to-end coverage.
Next add actual standalone boot/menu/quick game/return, save/reload with temporary storage, Magnate UI, campaign checkpoint, 2D/3D and controller input; isolate profile/settings/network side effects before automating them.

## Batch 3

ClockOnlyRenderRetainsHudControlsAcrossFrames executes the same clock renderer called by GameView on an attached UIDocument, checks the countdown, warning color, hidden-clock status and identity of a sibling action button. This is not a full live GameView session or controller-focus acceptance test. The existing two tests remain unchanged.
