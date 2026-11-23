# Advisory frame-time budgets for procedural 3D cities

Open **City Highways → 3D Objects → Scene Quality Audit** while running a 3D city in Play Mode.

## Workflow
1. Capture recent frame statistics after the existing two-second sampler warmup.
2. Choose **Low**, **Medium**, **High target FPS** (initial investigation suggestions: 30 / 45 / 60). These editor-window targets are not hardware guarantees and do not modify rendering or VSync.
3. The report evaluates the **P95 wall-clock frame time** against the selected quality's limit, `1000 / targetFPS` milliseconds. A value exactly at the limit passes; exceeding it produces an advisory warning.
4. At least 60 valid frames are required. Without enough samples the result is **InsufficientSamples**, not an FPS failure. Setting a target to 0 disables its budget check.
5. Compare a baseline to another quality using the same city/seed, viewport, camera and hardware. Export Current and Baseline rows as CSV: `TargetFPS` and `P95BudgetStatus` are included.

## Limitations
- The monitor uses `Time.unscaledDeltaTime` in Unity Play Mode: frame intervals include gameplay, Editor overhead, VSync and caps. This is **not** GPU or isolated CPU rendering time.
- P95 is robust to very rare spikes, so inspect P99 and MaxMs as well. A status of Pass does not mean no hitches.
- The numeric targets are advisory. For device quality gates, repeat test scenarios in standalone Development Builds with Unity Profiler and the same Magnate seed.
- No changes to city generation, graphics settings, quality selection, saves, rules or seed determinism.

## Validation
- [ ] Unity CI runs `City3DFrameBudgetTests` plus the prior frame sampler tests.
- [ ] Under 60 samples shows InsufficientSamples and not an over-budget warning.
- [ ] Force a target higher than observed P95 allows: warning appears; target 0 disables warnings.
- [ ] Set different Low/Medium/High targets and compare the same random Magnate seed at each quality.
- [ ] Export CSV; inspect `TargetFPS` and `P95BudgetStatus` for current and baseline rows.