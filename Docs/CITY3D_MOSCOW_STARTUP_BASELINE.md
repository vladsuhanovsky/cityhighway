# Moscow 3D startup: first measured baseline and targeted terrain work

## User-provided measurements (2026-11-24)

These measurements came from **two** manually exported `city3d-generation-stages.csv` files for **Москва**, quality **1**, Unity **6000.6.8f1**. They describe full scene construction at two different starts of the same pre-optimization revision; they are **not** an A/B test.

| Stage | Run 1 (ms) | Run 2 (ms) | Mean (ms) |
| --- | ---: | ---: | ---: |
| Rendering and materials | 1760.859 | 1586.554 | 1673.707 |
| Ground | 1142.884 | 1038.304 | 1090.594 |
| Rivers and relief | 694.552 | 685.108 | 689.830 |
| City planning | 136.090 | 120.648 | 128.369 |
| Decals | 120.075 | 108.572 | 114.324 |
| Trees | 109.139 | 96.886 | 103.012 |
| **Whole measured build** | **4137.303** | **3722.326** | **3929.815** |

The top three account for roughly **87–89%** of the recorded build time. They dominate startup, but this does **not** establish which call inside those stages is responsible.

## Change in this PR

In `TerrainTexture.Bake`, a 3D terrain texture previously still populated a flat tree candidate list, checked candidate distances against roads and rivers, and sorted the list, even though actual 2D tree painting is guarded by `!city3D`. In 3D, the visible trees are separate scene meshes and cards; the generated flat candidates are discarded without ever affecting the output texture.

- Skip building road/river exclusion segment lists that are only used by flat building/tree placement when producing 3D terrain.
- Preserve the park **forest floor** background and footprint shadows in the 3D texture.
- Skip flat tree candidate generation, outskirt candidate scans, and tree sorting for 3D terrain.
- Leave the flat (2D) terrain path and its flat tree rendering intact.

This removes provably unused work in a measured hotspot. **No quantitative speed-up is claimed** until the same scene is measured after this change.

## Further CPU Profiler detail

The new `City3D.Hotspot/` Timeline samples are nested inside existing `City3D.Build/` samples. The ten markers subdivide:

- Rendering setup, city atlas access, 3D terrain albedo baking, ground normal map baking.
- Main ground grid geometry, mesh upload/normals/tangents and outskirts generation.
- Within `TerrainTexture.Bake`, base pixel fill, building footprint overlays and texture upload.

To inspect them, enter Play Mode with the Unity Profiler **recording before the city is built**, pause capture after startup, and select the frame containing `City3D.Build/`. Expand the child `City3D.Hotspot/` scopes on CPU Usage → Timeline. An active **Live** view during recording does not display the saved frame details.

## Acceptance and remeasurement

1. Unity Edit Mode CI succeeds; open a flat (2D) map and a 3D map to visually verify backgrounds, park floors, building shadows and trees.
2. On the **same machine, graphics quality, map and Unity version**, build Moscow three more times after merging and save each stage-timings CSV. Run each time from the same Editor state; note warm/cold cache and the exact Git SHA.
3. Compare per-stage time distributions, not just a single total. Inspect `City3D.Hotspot/Terrain albedo bake` and `City3D.Hotspot/Ground normal bake` under CPU Timeline.
4. A Profiler frame showing ~66 ms/15 FPS by itself does not prove that startup time caused low gameplay FPS. Analyze normal gameplay frame time separately.

The CI tests cover 3D texture generation, cache behavior, independent 2D terrain generation and ProfilerMarker availability. They are correctness smoke tests, **not** frame-time benchmarks. Uploaded raw CSVs are not added to the repository by this PR.


## Follow-up measurements after PR #72 (2026-11-24)

Three captures were supplied for **Москва**, quality **1**, Unity **6000.6.8f1**, after merging PR #72.

| Stage | Capture 1 (ms) | Capture 2 (ms) | Capture 3 (ms) | New mean (ms) |
| --- | ---: | ---: | ---: | ---: |
| Rendering and materials | 1364.39 | 1412.64 | 1395.88 | 1390.97 |
| Ground | 1060.60 | 1023.58 | 1048.82 | 1044.34 |
| Rivers and relief | 690.90 | 700.02 | 676.58 | 689.17 |
| Trees | 79.40 | 87.58 | 77.02 | 81.33 |
| **Whole measured build** | **3501.16** | **3555.89** | **3519.19** | **3525.41** |

The prior two-capture mean was **3929.81 ms**, giving an observed change of **−404.40 ms (−10.3%)**. This is an *observational comparison*, not a causal estimate: small and unmatched sample sizes, warm/cold caches, Editor workload and timing jitter are uncontrolled.

The latest screen recording did not expose an expanded CPU Timeline with `City3D.Hotspot/` child durations. PR #73 adds separate direct wall-clock hotspot CSV capture from the scene itself to make it easier to identify the dominant texture-baking versus geometry-upload operation.

Three earlier post-PR #72 captures were at **quality 2** and are **not directly comparable** to the original quality-1 baseline. Do not pool them with this table.
