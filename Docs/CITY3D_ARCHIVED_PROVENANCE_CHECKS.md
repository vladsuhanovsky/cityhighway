# Historical P95 comparison provenance

This change makes archival review comparisons more conservative: matching manually entered baseline revision labels and P95 thresholds alone do not establish matching capture hardware.

## Additional review data

Each **new** review artifact schema v1 scenario includes two optional fields:

- `hasCurrentEnvironment`: explicitly indicates whether the current capture had valid automatically observed environment metadata.
- `currentEnvironmentSignature`: the captured Unity version, runtime platform, graphics device, screen size, VSync count and target FPS cap in a stable textual summary.

Older review JSON files omit these fields and remain importable. Their lack of environment information is interpreted as **unknown**, never silently as equivalent to a new capture.

## Archived comparisons

Scenarios continue to match by city + quality + manually labelled scenario. Status transitions, added and removed scenarios remain visible. A numeric *current P95 movement* is shown only when:

1. Both reports have the same non-empty, manually entered baseline revision label;
2. Their allowed P95 regression thresholds agree;
3. Both matched rows have valid comparable current P95 measurements;
4. Both rows explicitly contain matching nonempty recorded current-environment signatures.

Otherwise movement is hidden and an explicit reason explains whether baseline labels/thresholds, measurements or environment provenance block the comparison. The editor and Markdown report show totals for comparable and blocked matched rows.

## Limits

The signature summarizes runtime observations, not an independently authenticated device ID, driver version, system load or Git build. Seeds, cameras and revision labels remain manually entered. The archival comparison is an advisory audit, not a new FPS benchmark or proof of experimental equality.

## Acceptance

- [ ] Unity Edit Mode CI passes `City3DArchivedProvenanceTests`.
- [ ] Matching signatures and valid labels show P95 movement, with a warning about manually controlled conditions.
- [ ] Changed GPU/resolution hides P95 movement but keeps the status transition.
- [ ] Legacy review with no metadata still loads, but its P95 movement is hidden.
- [ ] Malformed provenance presence/summary pairs are rejected.
- [ ] GitHub Markdown and the Unity archive viewer show blocked counts and reasons.

No change to game logic, random city generation, save games, frame capture sampling or automated scheduling.
