# 3D runtime frame intervals — ordinary cities and Logistic Magnate

## Scope
This stage adds a lightweight read-only frame-interval sampler to shared `City3DScene` in ordinary and randomly generated Logistic Magnate maps. It measures Unity's `Time.unscaledDeltaTime` frame intervals, not isolated CPU/GPU rendering time.

## Workflow
1. Start a game in 3D Play Mode and open **City Highways → 3D Objects → Scene Quality Audit**.
2. Allow the initial two-second warmup, then click **Capture recent frame statistics**. The monitor holds the most recent 600 valid frame intervals.
3. Review frame sample count, mean frame interval, P95, P99, maximum, estimated FPS and approximate 1%-low FPS.
4. Save a baseline, change quality or reopen the same city/seed, collect a fresh sample, and capture again. The window shows changes in FPS and frame intervals.
5. Export a CSV with current and optional baseline rows. CSV is UTF-8 BOM with invariant decimal formatting.
6. Use **Reset frame sampling window** to discard samples from an earlier viewing condition.

## Measurement limitations
- The sampler starts only after procedural city construction, then skips a two-second warmup. Map build time is not measured.
- The report uses wall-clock frame intervals including Editor overhead, VSync, target FPS, input and other game workloads. These are **not GPU timings, render-thread CPU timings or draw-call counts**.
- Estimated FPS = 1000 divided by mean frame ms. Approximate 1%-low FPS = 1000 divided by P99 frame ms, which is a proxy, not a full tail-average FPS.
- Different city names trigger a comparison warning. Random cities with the same name but different seeds are also not directly comparable unless seed, view, camera movement and settings are held constant.
- Use Unity Profiler, Frame Debugger and standalone Development Builds for reliable performance diagnosis.
- No changes to logical city generation, game rules, saving, rendering quality or camera input.

## Checks
- [ ] Unity Edit Mode CI with `City3DFrameMonitorTests` passes.
- [ ] Start a fixed 3D city and capture at least 60 samples after warmup.
- [ ] Capture the same Logistic Magnate seed at Low, Medium and High with the same camera and viewport.
- [ ] Save a baseline and confirm frame interval and FPS deltas are signed and informative.
- [ ] Reset collection and confirm sample count restarts.
- [ ] Export CSV and check city names, decimal points, columns and baseline row.
- [ ] Confirm 2D mode does not spawn a new 3D monitor.