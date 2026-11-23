# Overall advisory verdict for 3D scenario benchmarks

The City Highways Performance Scenario Suite now emits one conservative, **manual** overall review status in addition to the per-seed diagnostics and CSV.

## Status criteria (priority order)

1. **NO DATA**: either baseline or current suite is empty/invalid.
2. **INCOMPLETE**: valid suites exist but there are zero matched scenarios. Different seeds/qualities never count as success.
3. **REGRESSION**: at least one matched scenario's P95 frame interval exceeds the configured allowed increase. This has priority even if another seed is missing.
4. **INCOMPLETE**: at least one seed/quality scenario is missing or newly added, automatic environment metadata mismatch exists, or a comparison could not be validated.
5. **UNVERIFIED LEGACY**: all scenarios match and no regression is detected, but at least one pair has no automatically captured environment metadata on either side. The legacy comparison is still shown, but is not a verified PASS.
6. **PASS**: all scenarios match, all comparisons have matching recorded environment metadata, and no P95 regression is detected.

PASS is **not** a guarantee of reproducibility. The random Magnate seed, camera, device drivers, background load and simulation state remain manually controlled. The sampled metric is Unity wall-clock frame interval, not GPU time.

## Unity workflow

Open **City Highways → 3D Objects → Performance Scenario Suite**, load or capture baseline/current suites as before and set the allowed P95 regression percent. The window displays an Overall performance audit status and counts how many matched pairs use legacy unknown-environment data. For review handoff, choose **Export audit summary TXT...**. The text report contains verdict, allowed P95 threshold, coverage, counts and caveats. The existing detailed CSV export is unchanged.

## Validation

- [ ] Unity Edit Mode CI passes eight new `City3DPerformanceSuiteVerdictTests`.
- [ ] Same city/seed/quality/environment and improved P95 → PASS.
- [ ] Regression on one seed with other seeds missing → REGRESSION (not PASS).
- [ ] Different seeds, changed quality or mixed legacy/new environment → INCOMPLETE.
- [ ] Two old v1 files without environment metadata → UNVERIFIED LEGACY.
- [ ] Invalid/empty data → NO DATA.
- [ ] TXT report includes status, regression allowance, sample coverage and the manual-testing caveats.

No gameplay, random generation, save-file, graphics quality or automatic GitHub actions changes are introduced.
