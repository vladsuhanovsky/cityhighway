# Repeated 3D benchmark capture workflow

This change adds an independent *repeat journal* to **City Highways → 3D Objects → Performance Scenario Suite**. It provides run-to-run reproducibility information without pooling unrelated frame intervals, changing the normal CURRENT/BASELINE suites or changing the game.

## Recommended workflow

1. Open the 3D scene in Play Mode and set the actual seed, camera, scene workload and graphics quality manually.
2. Enter the exact scenario label, for example `magnate;seed=42;camera=central;viewport=1920x1080`. This label is not authenticated against the procedural generator.
3. Press **Restart sampling (clear + warm up)** and wait for the two-second warm-up and at least 60 valid frame intervals.
4. In **Repeat captures (separate from CURRENT suite)**, press **Record repeat and restart sampling**. The current frame snapshot is stored in an independent journal, then the monitor starts a new sample window.
5. Repeat at least **3 separate windows** with unchanged scene/camera/quality, ideally after separately checking comparable system workload. The journal accepts up to **20 repeats**, and rejects different scenario labels, city, quality, missing environment metadata, mismatched observed runtime conditions and duplicate capture timestamps.
6. Inspect the **median P95**, **run-to-run P95 range** (max minus min as percent of median) and **median absolute deviation**. More than **10% range** with at least 3 repeats triggers an *advisory warning*, not an automatic regression verdict.
7. Use **Undo last repeat** to remove an accidental last recording, or **Clear repeat journal** (with confirmation) to start another scenario.

## Persistence and review

- **Save repeat journal JSON...** exports independent schema v1 journal data.
- **Load repeat journal JSON...** rejects malformed, unsupported and internally inconsistent saved files. It asks before replacing a nonempty journal; CURRENT and BASELINE suites remain unchanged.
- **Export repeat CSV...** exports each recorded window with exact scene label, timestamp, frame count, P95 and observed environment.
- **Export repeat Markdown...** creates a GitHub-ready summary and a table of individual runs with escaped user labels.

## Important limits

This is an *observational repeatability checklist*, not a statistical confidence interval or an automatic performance gate. Matching environment fields do not authenticate hardware, engine seed, camera, shader warm-up, CPU/GPU load or identical system conditions. Individual frame spikes are diagnosed by the existing stability analyzer. The journal does not silently average independent samples into a saved baseline, and does not alter the old P95 definition, benchmark suite JSON, game saves, gameplay or random map generation.

## Acceptance checks

- [ ] Unity Edit Mode CI succeeds with `City3DPerformanceRepeatSeriesTests` and `City3DPerformanceRepeatAnalysisTests`.
- [ ] Three identical P95 windows show zero variability; an extreme outlier triggers the advisory warning.
- [ ] Different seed/GPU or duplicate captured timestamp cannot enter the journal.
- [ ] JSON round-trip retains each snapshot, while corrupted/mismatched JSON is rejected without losing the open journal.
- [ ] CSV quotation and Markdown escaping preserve arbitrary scenario labels.
- [ ] In Unity, recording a repeat starts a fresh sampling window without modifying CURRENT/BASELINE; Undo and Clear affect only the journal.
