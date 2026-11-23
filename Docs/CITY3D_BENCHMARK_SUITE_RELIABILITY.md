# 3D benchmark suite reliability improvements

This package strengthens manual performance comparisons for normal cities and Logistic Magnate generated seeds without altering the game, save data, quality settings or existing benchmark JSON schema (v1).

## Improvements

1. **Require a top-level suite format version.** Suite JSON import now parses an independent version header and rejects files where `version` is missing or unsupported, even when the contained capture records have their own version. Invalid files are rejected before replacing loaded baseline/current suites.
2. **Explicit recapture.** In **City Highways → 3D Objects → Performance Scenario Suite**, adding a second measurement with the same city + quality + manually-labelled scenario now offers **Replace / Cancel**. The original measurement remains untouched unless Replace is chosen. Replacements do not add duplicate entries.
3. **Targeted cleanup.** Each baseline and current scenario has a confirmed **Remove** action. Other seeds and quality levels remain untouched; there is no need to clear the whole suite when one manual capture was unsuitable.
4. **Edit Mode regression tests.** Missing top-level versions, failed duplicate replacement, successful replacement without changing other seed entries and exact-key removal across quality levels are covered.

## Usage and measurement caveats

Scenario keys are literal labels entered by the tester. They do **not** automatically authenticate the generated random seed, hardware, viewport or camera. Keep the environment unchanged for each baseline/current pair. Performance metrics remain Unity frame intervals (not GPU timings), and are only captured when the user clicks in Play Mode. A green Unity Edit Mode CI means that the data-handling code passed tests, **not** that the game achieved a performance target.

## Acceptance

- [ ] Unity Edit Mode CI passes the extended `City3DPerformanceSuiteTests`.
- [ ] Import a JSON file with valid entries but no top-level version: reject without changing the existing suite.
- [ ] Capture the same Magnate seed twice and choose Cancel: the original capture must remain.
- [ ] Capture it again and choose Replace: one entry with the new P95 value, without affecting other seeds.
- [ ] Remove one quality level for a seed; other quality levels and seeds must remain.
