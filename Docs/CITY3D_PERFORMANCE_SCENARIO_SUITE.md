# Multiple 3D performance scenarios and random Magnate seeds

The 3D Performance Scenario Suite window extends the previous single-baseline audit to manually captured collections of independent scenarios. This allows performance regression sampling across multiple Magnate map seeds without pretending that different maps are the same workload.

## Workflow

1. Open **City Highways → 3D Objects → Performance Scenario Suite**.
2. Launch a 3D city and keep the view stable. The scene's existing sampler waits two seconds after load; collect **at least 60** frame intervals before clicking capture.
3. Set the **Scenario (seed/camera/viewport)** label before each capture, e.g. `magnate;seed=7;camera=A;viewport=1920x1080;machine=X`. The label is entered manually: the window **does not** automatically read the generator seed or validate the physical camera state.
4. Click **Add active 3D capture to CURRENT suite**. Repeat with seed 42 and any other desired seeds and quality levels. Duplicate (city name, quality, scenario) combinations are rejected; a capture is never silently overwritten.
5. Save the resulting suite to a JSON file. Select **Use CURRENT as new BASELINE** to deep-copy the suite and clear current captures, or load a previously saved baseline from JSON after restarting Unity.
6. Repeat each scenario on the new version of the game with the same seed, graphics quality, machine, viewport, camera and workload. Capture into CURRENT, then compare.
7. The report counts **matched / passed / regressed / missing / additional / rejected** scenarios. Regressions use the existing strict P95 threshold. Export **matched scenario comparison CSV** to see per-scenario P95 baseline, current, percent change and verdict.

## Safety and limitations

- Benchmarks are **manual** and based on wall-clock `Time.unscaledDeltaTime` intervals. Unity Editor activity, VSync, frame caps and hardware affect results. They are not GPU or isolated render-thread timings.
- A valid suite has schema version 1, at least one capture, valid frame statistics and unique city+quality+scenario keys. Invalid JSON fails validation and does not overwrite the currently loaded suite.
- Unmatched seed labels are **not compared**; they count as missing or additional. Changing quality from Low to High also produces unmatched scenarios rather than a false regression.
- Both suites and their CSV can be kept in version control as benchmark evidence. They are **not save-game data**, and the feature does not alter maps, rendering, gameplay or save files.
- Unity Edit Mode CI validates algorithms and JSON handling, **not target FPS**. To establish true performance use identical hardware and standalone Development Build profiling.

## Acceptance checks

- [ ] Unity Edit Mode CI passes `City3DPerformanceSuiteTests`.
- [ ] Capture a suite of seeds 7 and 42; add one extra seed, save JSON and reload.
- [ ] Confirm a duplicate seed+quality+scenario is refused.
- [ ] Compare two suites with one regression, one improvement and one missing seed.
- [ ] Change quality or scenario label and verify no incorrect cross-scenario match.
- [ ] Export CSV and inspect distinct seed rows and percent changes.
