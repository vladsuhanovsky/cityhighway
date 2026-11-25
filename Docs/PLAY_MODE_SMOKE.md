# Play Mode smoke coverage

Two runtime coroutine tests: actual Classic bot actions across frames then JSON snapshot/load/replay equivalence, and UI Toolkit MapView attachment, map replacement and transient state cleanup across frames. The latter creates an isolated UIDocument; it does not start the full application, use a player's saves, exercise real controller/Steam or certify rendering performance.
Use Unity Test Runner > PlayMode > CityHighways.PlayModeTests. CI uses the separate Unity Play Mode workflow when available. Keep the existing Edit Mode suite; don't treat these two tests as end-to-end coverage.
Next add actual standalone boot/menu/quick game/return, save/reload with temporary storage, Magnate UI, campaign checkpoint, 2D/3D and controller input; isolate profile/settings/network side effects before automating them.

## Batch 3

ClockOnlyRenderRetainsHudControlsAcrossFrames executes the same clock renderer called by GameView on an attached UIDocument, checks the countdown, warning color, hidden-clock status and identity of a sibling action button. This is not a full live GameView session or controller-focus acceptance test. The existing two tests remain unchanged.

## CI license serialization

PR #134 and PR #136 encountered Unity Personal activation failures while Edit Mode and Play Mode were dispatched concurrently; this is an infrastructure failure before NUnit execution, not evidence of a failing test. Both Unity workflows now share the repository-wide `cityhighways-unity-license` concurrency group with `queue: max` and `cancel-in-progress: false`. GitHub Actions serializes the workflows and queues up to 100 pending runs, preserving both independent check names. Non-Unity checks remain parallel. This mitigates concurrent seat requests within this repository; it does not guarantee that the license server is reachable or that external Unity sessions release seats. If activation still fails, review Unity account entitlement and GameCI logs; retry failed checks without masking failures. See https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency.
