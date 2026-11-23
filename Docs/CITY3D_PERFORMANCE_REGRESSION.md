# 3D performance regression comparison

## What this adds
The common `City3DScene` frame monitor already captures rolling wall-clock frame intervals in both fixed cities and random Logistic Magnate maps. The Scene Quality Audit window now checks whether a newer sample is slower than a saved baseline **only when the captured conditions are comparable**.

## Reproducible Play Mode workflow
1. Open **City Highways → 3D Objects → Scene Quality Audit**, start a city in 3D and allow at least 60 recent frame samples after warmup.
2. Enter a nonempty **Scenario (seed / camera / viewport)** label, e.g. `magnate;seed=42;camera=central;viewport=1920x1080;machine=desktop1`. This is a manual label and does **not** automatically read or lock the generator seed, camera position, hardware or graphics settings.
3. Click **Capture recent frame statistics**, then **Save frame snapshot as baseline**.
4. Run the **same city name**, **same graphics quality**, **same Magnate seed**, camera movement, resolution, hardware, VSync and scenario. Capture a new sample. Keep the scenario label identical.
5. Set **Allowed P95 regression (%)** (default 10%). If P95 frame time increased by more than this percentage, a warning appears. A boundary equal to the threshold passes.
6. Export **frame statistics CSV**. Current and baseline rows include Scenario. The Current row includes `RegressionVerdict` and `P95RegressionPercent` when comparison is valid.

## Conservative comparison gate
- At least 60 frames in **both** samples.
- City names must match exactly. **Names alone do not establish the same random seed.**
- Graphics quality must match. Comparing Low vs High is still available as descriptive raw deltas, but is not classified as an optimization regression.
- Scenario labels must be nonempty and match. For Magnate use one seed and camera preset per baseline.
- Mean, P95 and P99 frame intervals must be positive finite numbers. Empty/invalid snapshots never pass.
- Regession is a strictly greater-than increase in P95 wall-clock frame interval (not estimated FPS). Negative changes mean improvement.

## Limitations
These are Unity `Time.unscaledDeltaTime` intervals, not isolated GPU or CPU timings. Editor overhead, VSync, foreground window focus and frame caps can distort results. The warning is **advisory**; no gameplay, save, city generation or automatic quality selection changes are made. Use a standalone Development Build and Unity Profiler for reproducible hardware decisions.

## Validation
- [ ] Unity CI Edit Mode tests, particularly `City3DPerformanceRegressionTests`.
- [ ] Save baseline and capture a slower sample under the same manually entered scenario; verify warning.
- [ ] Change city name, scenario label or quality independently; verify comparison is refused with an explanatory message.
- [ ] Compare Low vs High in the existing raw delta view without a false regression verdict.
- [ ] Export CSV with Cyrillic city names; check Scenario, RegressionVerdict, P95RegressionPercent columns.
- [ ] Check sample count fewer than 60 cannot report Pass.