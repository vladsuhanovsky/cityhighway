# GitHub Markdown 3D performance review

The **City Highways → 3D Objects → Performance Scenario Suite** editor window can export a GitHub-compatible Markdown report from the same validated data as the versioned JSON review artifact.

## How to use

1. Capture or import baseline and current benchmark suites. Keep generated Magnate seed, camera, viewport, workload and hardware consistent.
2. Set the allowed P95 regression percentage. Review the overall status, warnings, coverage and per-seed details in Unity.
3. Optionally enter **Baseline revision** and **Current revision** (manual labels, not validated against Git).
4. Choose **Export GitHub PR Markdown...**. Paste the generated Markdown into the related pull request or attach the file. Keep both original suite JSON files for raw snapshot traceability.

## Output

- Overall verdict, P95 allowance, revision labels and UTC timestamp.
- Coverage and counts for matching/passed/regressed/missing/additional/invalid/legacy scenarios.
- GitHub-flavored table for each case with city, quality, scenario, status, baseline P95, current P95 and percentage change.
- Missing and additional cases have a literal dash rather than fake zero timings. Incomparable cases have no percentage value.
- User-provided city, scenario and revision labels are escaped for table separators, markup angle brackets and line breaks.
- Reminder that wall-clock Unity frame intervals are advisory and do not replace GPU profiling or manually verified seeds/cameras.

## Acceptance

- [ ] Unity Edit Mode CI including `City3DPerformanceMarkdownReportTests`.
- [ ] Export and paste into GitHub PR; verify Markdown table cells remain aligned even with `|` in scenario names.
- [ ] Missing/additional scenarios are shown, with dashes for absent timings.
- [ ] Revision metadata, UTC timestamp and overall status are visible.
- [ ] Run Play Mode measurements on comparable hardware; the Edit Mode tests do not establish frame rate performance.

No gameplay, save-game data, map generation, CI scheduling, automatic posting to GitHub, or graphics quality behavior is changed.
