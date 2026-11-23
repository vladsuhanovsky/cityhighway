# Full 3D capture checklist and GitHub export

The Unity **City Highways → 3D Objects → Performance Scenario Suite** window now provides an auditable capture checklist covering **all** relevant BASELINE and CURRENT scenarios.

## Improvements

- **Missing** — a baseline scenario has no matching current capture.
- **Incompatible** — matching manual scenario labels have different or incomplete observed runtime environments.
- **Extra** — a current scenario has no exact baseline key. It is now a visible row, not merely a count.
- **Ready** — both the exact manual label and recorded runtime conditions match. It does **not** prove that the actual random generator seed, camera, load or frame rate is equal.

Rows are sorted with issues first (Missing → Incompatible → Extra → Ready), followed by city, quality and exact scenario name. The editor defaults to showing issues and offers **Show ready checklist rows** to inspect all results. **Use label** copies a baseline scenario into the capture text field for missing or incompatible rows, but never changes active city, quality or procedural generation.

## Exports

**Export collection checklist CSV...** includes the complete checklist with city, quality, scenario, state, presence, environment match and reason; commas, quotation marks and multiline text are quoted for spreadsheet tools.

**Export collection checklist Markdown...** generates a GitHub-friendly report with collection counts, state and reason per row, escaping Markdown separators and tags.

Both exports are derived from one read-only report object and include extra captures. An invalid CURRENT suite is explicitly reported; it is not treated as a completed benchmark. The tool does not synthesize missing sample times or claim a performance PASS based on capture readiness.

## Verification

- [ ] Unity Edit Mode CI passes both `City3DPerformanceSuiteReadinessTests` and `City3DPerformanceChecklistExportTests`.
- [ ] Missing, incompatible, extra and ready seed scenarios appear in the checklist and both exports.
- [ ] Scenario labels with pipes, markup characters, commas and quotation marks export correctly.
- [ ] An invalid CURRENT suite warns about its invalidity and never produces a complete status.
- [ ] Existing suite JSON and other archived reports remain compatible.
- [ ] Manually verify UI and exports in Unity; Edit Mode tests do not establish real FPS.

No modifications to gameplay, save files, random city generation or CI scheduling.
