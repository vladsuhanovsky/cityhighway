# 3D benchmark collection readiness and guided capture

The **City Highways → 3D Objects → Performance Scenario Suite** window now shows a pre-comparison collection checklist so a manually gathered CURRENT benchmark set can be completed against a loaded BASELINE before interpreting any performance verdict.

## Workflow

1. Load a valid BASELINE suite JSON. An empty CURRENT suite is permitted: the checklist will show every missing baseline scenario without requiring CURRENT to be valid yet.
2. Inspect **Capture collection checklist**. It reports ready, missing, incompatible and extra CURRENT captures, plus how many old baseline labels fail the advisory seed/camera/viewport syntax preflight.
3. For a missing or incompatible row, click **Use label** to copy its exact scenario label into the capture input. Check that the active city's name, graphics quality, actual Magnate seed, camera, workload and screen resolution also match. Copying the label does **not** change city or quality or set the procedural seed.
4. Start Play Mode, collect at least 60 valid frame samples, then capture. The editor confirms any preflight warnings and reports invalid or short frame captures before trying to add/replace.
5. Click **Export collection checklist CSV...** to save all baseline scenarios, capture-present and environment-compatibility flags, and diagnostic reasons.
6. When ready, use the existing baseline/current comparison and export review artifacts as before. A complete collection checklist is not itself a performance PASS; P95 regressions and manually controlled conditions still matter.

## Technical details

- Exact city + graphics quality + scenario label is used to match baseline entries. A new unmatched seed counts as extra, not as a pass.
- Captured environment must be known and agree with the baseline. Both-legacy environments remain incompatible for readiness (though historical audit comparisons still have their explicit legacy handling).
- Seed/camera/viewport **syntax** warnings are advisory and do not silently discard valid old suites; readiness can be complete while such old labels still require review.
- The editor reads the observed capture environment once per OnGUI pass and uses that same object for label preflight, baseline preflight and the saved capture, avoiding inconsistent metadata within a single action.
- No stored JSON format, gameplay, procedural map generation or save-game schema changes.

## Test acceptance

- [ ] Unity Edit Mode CI passes `City3DPerformanceSuiteReadinessTests`.
- [ ] Empty baseline produces no false ready state; valid baseline with empty current lists all missing scenarios.
- [ ] Exactly matching environment produces ready, changed GPU produces incomparable.
- [ ] Different seed is reported as extra plus missing, not matched.
- [ ] Existing legacy free-form label is flagged for manual inspection, not rejected.
- [ ] In Unity, **Use label** fills the correct value and CSV has a row for every baseline scenario.
- [ ] Under 60 samples the editor explains why capture is invalid.
