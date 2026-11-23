# Compare two archived 3D performance reviews

The Unity **Performance Scenario Suite** window can compare a previously exported review against a second archived review without modifying the active baseline/current capture suites.

## Workflow

1. Open **City Highways → 3D Objects → Performance Scenario Suite**.
2. Under **Archived review (read-only)**, click **Load archived review JSON...** and select the older review.
3. Click **Load second archive to compare...** and select the newer review. Both must pass the existing archived-JSON structural validation.
4. Review changed statuses, added and removed scenarios and matched scenario totals. Use **Export archived comparison Markdown...** for a GitHub PR report.
5. You may close the second archive or the whole archive viewer. Failed imports preserve the previous valid loaded review and do not affect live capture suites.

## Interpretation

- Match is based on exact **city + graphics quality + manually labelled scenario**; random Magnate seed labels are not verified by the generator.
- A scenario's status change (for example PASS → REGRESSION) is not a hardware benchmark; it compares historical verdicts from separate audits.
- Movement of **current P95 milliseconds** is displayed only when both report rows have comparable samples, the two archived reports use the exact same **non-empty baseline revision label**, and the allowed P95 thresholds match. Otherwise this metric is suppressed and the report warns about the mismatch.
- Revision labels and benchmark conditions are manually supplied, not confirmed against Git or physical hardware. Matching labels alone do not guarantee true experimental comparability. Full raw suite JSON should be retained if a technical audit is needed.
- Added and removed scenarios never receive fabricated P95 deltas. No aggregation across unlike city, seed or quality is attempted.

## Acceptance

- [ ] Unity Edit Mode CI passes `City3DArchivedReviewComparisonTests`.
- [ ] Archive with seed=7 versus seed=42: one added and one removed, no matched delta.
- [ ] Same scenario and baseline label, a status change: show status transition and conditional P95 movement.
- [ ] Different baseline labels, absent labels or different threshold: hide P95 movement.
- [ ] Corrupt archive JSON is rejected without mutating the currently displayed archive or active suites.
- [ ] Markdown escapes pipes and angle brackets correctly for GitHub tables.

No changes to the game renderer, logic, saves, random map generation or automatic CI workflow.
