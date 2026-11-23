# Persistent performance baselines for City Highways 3D

## Why
The 3D Scene Quality Audit previously kept the performance baseline only in memory. Restarting Unity lost the baseline and prevented comparisons of the same fixed city or Logistic Magnate random seed between builds. This feature saves one trusted, versioned baseline as JSON and lets the auditor load it at any later editor session.

## Workflow
1. Open **City Highways → 3D Objects → Scene Quality Audit**, start Play Mode 3D and collect at least 60 valid frame intervals after warmup.
2. Set an explicit **Scenario (seed / camera / viewport)** label, e.g. `magnate;seed=42;camera=central;viewport=1920x1080;gpu=workstation1`. Keep hardware, VSync, gameplay load and camera motion constant.
3. Capture recent frame statistics, choose **Save frame snapshot as baseline** and click **Save baseline JSON...**. Save the file outside Assets or in a chosen benchmark directory. The file is not a save game.
4. Later, after reopening Unity or switching to a newer build, open the audit window and choose **Load baseline JSON...** even before a new Play Mode capture.
5. Run the same city, graphics quality, seed and labelled scenario. Capture current stats. The existing P95 regression verdict now compares the new snapshot against the reloaded baseline.
6. Export the existing CSV; it includes current and baseline row data with scenario and regression verdict.

## Data and validation
- JSON schema version: `1`. The file stores city, graphics quality, manually entered scenario, UTC capture timestamp, and full summary (frame count, average/P95/P99/max ms, estimated FPS, approx 1% low FPS).
- The import checks the version, non-empty city/scenario, quality in 0..2, at least 60 samples, positive finite mean/P95/P99/max and ordered P95 <= P99 <= max. Empty, corrupted or unsupported files are rejected without overwriting a valid in-memory baseline.
- Export also refuses invalid captures with a clear dialog. Saving and loading happen only when clicked; no automatic disk writes or effects on gameplay.
- JSON baseline files can be version-controlled for repeatable manual benchmarks. **Unity CI Edit Mode test success does not establish a performance result**: timing is collected from the machine and scene running Play Mode.

## Limitations
- Scenario comparison is based on an explicitly typed label, not automatic verification of Magnate seed, camera, viewport or hardware. Do not change the label to make unlike runs look comparable.
- `Time.unscaledDeltaTime` frame intervals include Editor overhead and frame caps; they are not isolated GPU/CPU time. Use standalone builds and the Unity Profiler to validate a regression.
- City map generation, game rules, saved games, render settings, and quality selections are unchanged.

## Acceptance
- [ ] Unity Edit Mode CI includes `City3DPerformanceBaselineTests` (serialization round-trip, unsupported version, invalid captures, comparison after import).
- [ ] Save a valid Cyrillic-city baseline JSON from a fixed city and load it after restarting Unity.
- [ ] Load a baseline before entering Play Mode, capture a fresh run of the same Magnate seed/quality/scenario and check the comparison.
- [ ] Try a corrupted JSON and wrong version; verify the existing baseline remains.
- [ ] Confirm current/loaded baseline CSV rows still report P95 regression verdict and scenario.