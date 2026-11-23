# Portable performance review artifact for pull requests

The **Performance Scenario Suite** window now exports a versioned, read-only JSON review artifact to attach to a pull request or keep next to benchmark records.

## Usage

1. Use **City Highways → 3D Objects → Performance Scenario Suite** to load or capture baseline and current suites, using the same city, graphics quality, manually-entered Magnate seed, camera, viewport, hardware and workload.
2. Set the P95 allowed regression threshold. Verify the overall verdict and coverage are meaningful.
3. Optionally enter **Baseline revision** and **Current revision** labels (for example, Git commit hashes). These are manually supplied and not checked against the current build.
4. Click **Export review artifact JSON...**, then attach the file to the pull request. Keep the original baseline/current suite JSON files separately if you want full raw snapshot data.

## What is included

Schema v1 stores UTC export time, optional revision labels, overall advisory status, P95 allowed threshold, coverage percent, counts (matched/pass/regression/invalid/missing/additional/legacy) and **all** scenario rows sorted with problem scenarios first. Each row has city, quality, manual scenario label, status, P95 baseline/current, percentage change and presence indicators. Missing/extra/invalid cases do not claim a comparable percentage change; use `hasComparableDelta` to interpret the numeric field.

## Limitations

- Artifact is a report, not a replay or an automated performance CI check. Unity frame intervals include Editor overhead and frame caps.
- The manually entered revision and scenario labels do not verify Git commit, random Magnate seed, camera, hardware workload or scene state.
- The existing baseline/current JSON files remain the authoritative input snapshots; the review artifact deliberately summarizes them.
- Unity Edit Mode CI checks serialization and deterministic verdict logic only. Actual performance must be measured on matching hardware.

## Validation

- [ ] Unity Edit Mode CI passes the new `City3DPerformanceReviewArtifactTests`.
- [ ] Export a PASS with matching baseline/current scenarios and review revision labels.
- [ ] Export missing/extra seed scenarios and verify no invented percentage changes.
- [ ] Export a P95 regression and verify verdict and percentage change.
- [ ] Keep baseline/current suite files with the report for review traceability.

No gameplay, map generation, save-game format, or autonomous CI handling has been changed.
