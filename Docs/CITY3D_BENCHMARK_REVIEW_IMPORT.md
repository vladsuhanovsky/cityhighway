# Import and inspect archived 3D performance review artifacts

Previously the Performance Scenario Suite exported versioned review JSON but could not re-open it. This update provides a read-only archive viewer with strict structural validation. It does **not** re-create the original benchmark measurements or prove the manually supplied revision/seed labels.

## How to use

1. Open **City Highways → 3D Objects → Performance Scenario Suite**.
2. Under **Archived review (read-only)** click **Load archived review JSON...** and choose a saved `CityHighwaysPerformanceReview.json`.
3. Inspect the original overall verdict, baseline/current revision labels, coverage and scenario-level reasons. The archived view is distinct from the actively loaded baseline/current suite.
4. Click **Export archived review Markdown...** to regenerate a GitHub-ready report from the archived JSON, or **Close archived review** to dismiss it.

## Validation guarantees

- Only explicit schema version 1 is accepted. Missing or unknown versions, malformed JSON, empty data and invalid metadata are rejected.
- Per-scenario identity keys must be unique, each status must match its sample presence flags, and comparable P95 deltas must agree with recorded frame times.
- Summary counts, coverage percentage and overall verdict must agree with scenario rows. Forged PASS status with regression rows will not load.
- Invalid input does **not** clear a valid archive already loaded in the window, and never changes baseline/current captures.
- Original report JSON files are treated as untrusted inputs, but cannot independently authenticate hardware, user-entered revisions or original capture data. Preserve the original suite JSON for further verification.

## Acceptance checks

- [ ] Unity Edit Mode CI, including `City3DPerformanceReviewImportTests`.
- [ ] Load a valid exported review with Cyrillic city names and re-export Markdown.
- [ ] Reject wrong/missing schema versions, mismatched status/counts and invented P95 changes.
- [ ] Keep a previously opened archive visible after a failed import.
- [ ] Confirm working baseline/current suite values remain untouched when an archive is opened.

No gameplay, render, random generation, save-game schema, or automated GitHub workflow changes are introduced.
