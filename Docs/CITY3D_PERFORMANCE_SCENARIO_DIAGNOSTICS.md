# 3D performance scenario diagnostics

This iteration extends the manual scenario suite audit with **coverage completeness, ranked regressions and a full comparison CSV**. The underlying FPS sampler, baseline file format and gameplay stay unchanged.

## Workflow

1. Open **City Highways → 3D Objects → Performance Scenario Suite** and load baseline/current JSON collections or capture fresh measurements.
2. For random Logistic Magnate maps, keep the original generator seed, graphics quality, camera, resolution, machine and workload in each manually written scenario label. The tool does not infer or verify the seed.
3. Review **Baseline scenario coverage**: matched baseline cases divided by baseline total. A different city, seed, graphics quality or scenario label is **missing**, not a performance pass; an unmatched current capture is **additional**.
4. The diagnostic list ranks **Regression**, **Invalid**, **Missing**, **Additional**, then **Pass**. The number of displayed rows is adjustable (5–100); the saved suite remains intact.
5. Export the comparison CSV. It now includes **all baseline and current scenarios** with the status, including missing and additional cases; absence of a measurement appears as an empty cell rather than a misleading zero.

## Interpretation

- A regression is based on the existing P95 frame-duration threshold and comparable city+quality+scenario keys.
- A report with zero regressions but missing seeds **does not** have complete coverage.
- “Complete coverage” requires every baseline scenario matched, no extra current scenarios, and no invalid comparisons. It is not a guarantee of performance.
- Unity Edit Mode CI checks pure calculations and CSV code compilation only; real frame-time performance must be measured in Play Mode on matching hardware, or with Unity Profiler in standalone builds.
- No automatic map switching, new gameplay code, save-game changes or unattended GitHub workflow is introduced.

## Validation

- [ ] Unity Edit Mode CI, especially `City3DPerformanceDiagnosticsTests`.
- [ ] Baseline seeds 7/42/99 versus current 7/42/100: one missing, one additional, at least two matched.
- [ ] A P95 regression shows first in the ranked diagnostics and in the exported CSV.
- [ ] Changing Low to High produces missing/additional rows rather than a false pass.
- [ ] Empty suite does not produce a green verdict or a misleading coverage percentage.
