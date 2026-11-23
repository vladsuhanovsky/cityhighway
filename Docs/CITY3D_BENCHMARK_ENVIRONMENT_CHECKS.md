# Reproducible 3D benchmark capture environments

This feature attaches **automatically observed runtime environment metadata** to new performance suite captures, to avoid reporting a performance PASS when measurements come from different machines or graphics configurations.

## Collected metadata

When the tester clicks **Add active 3D capture to CURRENT suite**, the window records:
- Unity version and platform
- Graphics device name as reported by Unity
- Screen width and height
- `QualitySettings.vSyncCount`
- `Application.targetFrameRate`

These fields are added as an **optional** `environment` object to the existing baseline JSON schema v1. Previously saved JSON files are readable and valid without it; they have unknown environment provenance.

## Evaluation

The multi-seed suite comparison now distinguishes:
- **Match**: both captures provide valid identical environment metadata. Normal P95 verdicts apply.
- **Different**: both provide metadata but at least one observed field differs. The matched scenario is **Rejected/Invalid**, never Pass or Regression.
- **Incomplete**: only one side includes metadata, or one side is invalid. Comparison is **Rejected/Invalid**.
- **BothLegacy**: neither side has metadata. The original manual scenario comparison remains available for compatibility, **but matched environment conditions have not been verified**. Re-capture both suites for reliable results.

The Unity window displays environment details for each entry and a warning when a matched pair cannot be compared. The CSV exports baseline and current environment summaries, retaining the status `Invalid` when necessary.

## Important limits

- The random map **seed**, camera pose, game simulation state, build/commit, device power profile and background workload are **not** automatically verified. The manual scenario label still needs seed and camera information; keep everything else controlled yourself.
- Screen size and GPU name are observations at capture time, not a promise that hardware and drivers are identical. Unity Editor overhead and VSync can dominate wall-clock frame intervals.
- This change does not alter frame sampling, the map generator, save-games, frame caps, gameplay or graphics settings.

## Acceptance

- [ ] Unity Edit Mode tests pass including `City3DPerformanceEnvironmentTests`.
- [ ] Capture one Magnate seed, export suite JSON and verify the optional environment fields.
- [ ] Compare identical scenario/quality with different GPU/viewport/VSync fields: the result is Invalid, not Pass.
- [ ] Load an older v1 baseline without metadata: it remains valid, and mixed metadata results in Invalid.
- [ ] CSV includes `BaselineEnvironment` and `CurrentEnvironment` strings and preserves Cyrillic city names.
