# Fresh frame-sampling window for 3D performance audits

The Unity **City Highways → 3D Objects → Performance Scenario Suite** window now exposes a measured frame count and a fresh-sampling control.

## Manual workflow

1. Enter Play Mode and load the target 3D scene.
2. Set up the required Magnate seed, camera position, graphics quality, viewport, and workload manually. Labels alone do **not** verify those conditions.
3. Click **Restart sampling (clear + warm up)**. This clears the active monitor's ring buffer, then ignores frames for a **2-second warm-up** using real time.
4. Wait until warm-up ends and at least **60 accepted frame intervals** have accumulated. The UI shows the live frame count (up to 600 recent frames).
5. Click **Add active 3D capture to CURRENT suite**. The existing scenario and baseline compatibility warnings still apply.
6. Repeat this process after changing camera, scene conditions, or quality, to avoid mixing measurements from the previous setup.

## Behavior and limitations

- The new action clears only the live frame-monitor samples. It **does not** alter already captured BASELINE/CURRENT entries, JSON, the renderer, or game saves.
- The UI disables saving before 60 samples or during the warm-up. Invalid frame intervals are ignored by the monitor.
- The monitor keeps the most recent 600 valid intervals. The reset does not guarantee thermal stability, same machine load, equivalent procedural seeds, or isolated GPU/CPU performance.
- Existing API `ResetSamples()` remains available, while `BeginFreshWindow()` explicitly resets samples and starts a new warm-up.
- Sampling and calculations remain synchronous; no background work or external telemetry is introduced.

## Verification

- [ ] Unity Edit Mode CI passes the extended `City3DFrameMonitorTests`.
- [ ] Fresh-window reset drops old live samples while preserving previously computed snapshots.
- [ ] Sample readiness activates at 60 valid intervals and never grows beyond ring capacity.
- [ ] In Play Mode, **Restart sampling** starts a new warm-up and capture becomes available only afterwards.
- [ ] Previously saved scenarios and archived benchmark reports remain unchanged.
