# Compare a prospective 3D capture to the loaded baseline

The Unity **City Highways → 3D Objects → Performance Scenario Suite** window provides an advisory **Baseline preflight** before a new CURRENT capture is added.

## Outcome

- **NoBaseline:** No valid baseline suite has been loaded. A new capture can be recorded for a future baseline, without claiming a regression comparison.
- **MissingScenario:** The exact city + quality + manually named scenario does not exist in baseline. The new capture will be *Additional* in diagnostics, not a Pass. Confirm to continue.
- **IncompatibleEnvironment:** A matching baseline scenario exists but recorded runtime conditions differ (Unity version, platform, GPU, resolution, VSync or target FPS). Confirm to continue.
- **UnknownLegacyEnvironment:** Matching scenario found but environment metadata is missing/invalid on either side. Confirm to continue.
- **Ready:** Scenario label and observed runtime metadata agree. This still does not prove the procedural Magnate seed, camera setup or workload are identical.

The new check does not change the game, stored suite JSON, diagnostic verdict or procedural generation. Warnings are shown before capture, and the user can **Capture anyway** or **Cancel**. The original scenario-label preflight for seed/camera/viewport formatting remains independent; it can also request acknowledgement.

## Acceptance

- [ ] Unity Edit Mode CI passes `City3DPerformanceBaselinePreflightTests`.
- [ ] A newly created capture without baseline is allowed.
- [ ] Different seed or graphics quality is flagged as MissingScenario.
- [ ] Different GPU or resolution is flagged as IncompatibleEnvironment.
- [ ] Legacy baseline metadata and absent current environment are flagged as unknown.
- [ ] A matched capture displays Ready only with matching valid environment metadata.
- [ ] Warnings are informational and do not silently modify or remove captures.
