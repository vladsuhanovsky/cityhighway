# Compare two repeated 3D performance capture series

The **City Highways → 3D Objects → Performance Scenario Suite** window now supports a read-only comparison between an archived baseline repeat journal and the current repeat journal.

## Workflow

1. Create 3 or more separate repeat captures for the baseline build using **Record repeat and restart sampling**. Save the repeat journal JSON separately.
2. Run the current build under manually matched procedural seed, city, graphics quality, camera, scene workload, viewport and hardware conditions. Record 3 or more current repeats.
3. Select **Load baseline repeat journal...** under **Compare repeat series (read-only)**. Invalid/malformed JSON is rejected without replacing an already loaded archive or editing current captures.
4. Adjust **Allowed median P95 regression (%)** as appropriate. Review the verdict and the change in median P95 when comparison conditions permit it.
5. Export **repeat comparison CSV** or **repeat comparison Markdown** to attach a review summary to GitHub. Use **Close baseline repeat journal** to stop comparing.

## Verdicts and guards

- **Invalid**: one journal fails validation or the selected regression tolerance is invalid.
- **DifferentScenario**: the two runs use different manually entered city, quality or scenario identity. No P95 delta is displayed.
- **DifferentEnvironment**: recorded runtime environment signatures differ. No P95 delta is displayed.
- **InsufficientRepeats**: fewer than three samples in either series. No P95 delta is displayed.
- **UnstableRuns**: either series has run-to-run P95 range greater than 10% of median. No P95 delta is displayed.
- **Regression**: comparable current median P95 rises by more than the allowed percentage.
- **Improvement**: comparable current median P95 falls by more than the allowed percentage.
- **WithinThreshold**: both comparable series lie within the configured change threshold.

The calculation compares **median of per-run P95 values**, not pooled frames. Reports include baseline/current medians, repeat counts, range percentages, the verdict, and a signed percentage only when the data pass all gates. Floating-point rounding at the exact threshold does not trigger a regression.

## Limits

This is an advisory comparison in Unity Editor, not an authenticated performance test or confidence interval. Equal manually entered labels and observed Unity/GPU/resolution metadata **do not** prove the same engine seed, camera pose, build provenance, thermal state, background load or rendering workload.

Existing CURRENT/BASELINE scenario suites, the repeat journal v1 schema, saved games, rendering and procedural generation remain unchanged.

## Acceptance

- [ ] Unity Edit Mode CI passes `City3DPerformanceRepeatComparisonTests`.
- [ ] Equal valid conditions and three stable runs show median P95 changes.
- [ ] Different seed or GPU, missing repeats, or noisy series suppress numerical percentage with an explanation.
- [ ] Both Markdown and CSV escape user-entered labels and do not fabricate deltas for incompatible series.
- [ ] Invalid baseline imports leave the active repeat journal and existing comparison reference unchanged.
