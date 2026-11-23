# Actionable reasons for performance scenario audit results

The 3D Performance Scenario Suite audit now carries a **Reason** for every scenario outcome, so an `Invalid` status can be investigated without manually comparing full benchmark JSON files.

## Diagnostics

- **Missing:** no current capture shares the baseline city + quality + scenario.
- **Additional:** no baseline capture shares the current city + quality + scenario.
- **Regression:** P95 frame time exceeded the configured allowed increase.
- **Pass:** the matched P95 result is within the threshold (subject to manual scene/seed verification and overall-suite verdict).
- **Invalid:** the tool identifies missing environment metadata on one side, or the first differing observed condition in this order: Unity version, platform, GPU, viewport resolution, VSync, target frame rate. An unexpected frame comparison rejection is also described.

Reasons are added to the Unity diagnostic row and exported in the CSV's new `Reason` column. The version 1 review JSON gets an optional `reason` field on each scenario row; GitHub Markdown gains a `Reason` column with special characters escaped. Existing fields and the JSON schema version are retained for additive compatibility.

## Validation

- [ ] Unity Edit Mode CI passes `City3DPerformanceDiagnosticReasonsTests`.
- [ ] GPU mismatch and resolution mismatch create distinct reasons.
- [ ] Metadata missing on only one side is not reported as a valid match.
- [ ] Missing/additional seeds, P95 regression and VSync mismatch appear in exported reports.
- [ ] GitHub Markdown remains aligned when scenario labels contain `|` and angle brackets.

These are diagnostic annotations, not automatic proof of identical random Magnate seed, camera or machine load. No gameplay, saves, city generation or automated workflow changes are made.
