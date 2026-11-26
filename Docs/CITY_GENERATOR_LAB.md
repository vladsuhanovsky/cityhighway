# City Generator Lab v0.2

Open **City Highways > City Generator Lab** in Unity outside Play Mode.
Enter a seed and click Generate and validate, or run 1–512 consecutive seeds.
The batch can be cancelled between seeds; the last generated layout is shown as a 2D topology preview.
No generator settings are changed. Requested and actual seed are separate because the existing generator retries with different seeds.
Failures are automatically exported under Library/CityHighways/CitySeedDiagnostics; the selected result can be exported to a chosen directory.
JSON schema 1 contains map name, districts, locations, roads, river centerlines, hub IDs/types, public roads, error messages, elapsed wall-clock time and Unity version.
If generation throws before returning a city, the report contains the seed and error but not rejected internal attempts.
CI range tests exercise 64 seeds plus six runs for determinism; full graph connectivity, location/road intersections, existing water checks and hub references are checked. Public-road connectivity is NOT required (the generator deliberately excludes graph bridges).
A future extension can expose generator layout presets, 3D footprint audit, attempt-by-attempt reports, and a captured build/commit ID. No 3D mesh or performance certification here.

## Optional 3D footprint audit (v0.2)
Enable **Audit 3D footprints (slower; no meshes)** before generating or running a seed range. The ordinary topology-only workflow remains the default. Schema 2 appends layout3D: renderer-prepared water centerlines (including horizon extensions), hub footprints from the production placement planner, water/bank/road half widths in world units and coded warnings with object IDs and measurements. The renderer and diagnostics call the same flat XZ river preparation function, including authored spline smoothing, dense generated samples and tributary trimming. The preview clips extensions to the map bounds; JSON retains the full prepared lines. Cyan is prepared water; amber/orange outlines are hub footprints; orange roads and markers are flagged objects. The existing blue river is the saved centerline.

| Warning code | Meaning |
|---|---|
| hub-bank-overlap | Planned hub rectangle approaches the prepared river centerline closer than water plus bank width |
| hub-marker-bank-overlap | Hub node center is within that width |
| road-water-clearance | Non-bridge road center segment approaches water closer than water plus road half width |
| bridge-without-water | Bridge segment does not approach modeled water (distance -1 means no prepared water) |
| short-road-junction-overlap | Road shorter than two junction arms |
| crosswalk-envelope-overlap | Junction direction pair has overlapping zebra-stripe bounding rectangles; measured value is angle in degrees, limit 0 is not a threshold angle |

Warnings are risk signals, not proof of final mesh clipping or bad map rules. Crosswalk envelope includes unpainted gaps. Road test uses full node-to-node centerline, not trimmed/elevated mesh. Hub footprints match planned sites, not arbitrary prefab extents. Neither terrain height nor rendering is validated. A passing topology report can contain 3D warnings. The generator's rules, saved-map data and seed retry policy are unchanged. The diagnostic is opt-in and does not execute during a normal match. Failed topology and warned seeds export automatically in the Lab, and all four sampled 3D reports (1, 7, 42, 2026) export to artifacts/city-3d-layout during Edit Mode CI. Actual warning counts remain unknown until CI. Use the exported requested/actual seeds and object IDs to reproduce and inspect the scene in Play Mode before promoting a warning to a hard generator gate or changing road paint.

## v0.3 — crosswalk conflict resolution

Baseline from successful PR #137 Edit Mode run #529 (artifact 15064877691, HEAD f9d1bd6): seeds 1, 7, 42 and 2026 produced respectively 35, 36, 24 and 23 overlapping crosswalk envelope pairs (118 total). No other layout warning codes were present in these four reports. These are geometric risk counts, not a count of confirmed visual defects.

The 3D renderer now retains all asphalt junction arms, but selects a deterministic non-overlapping subset of full-size zebra markings. It tries each cyclic starting arm in the existing angular order and keeps the largest greedy result, ties going to the first starting arm. This is not an exact maximum independent-set solver. Omitted crosswalks are intentional visual tradeoffs; no road geometry, rules, seed generation or save formats change. The audit runs the same selection and warns on remaining overlaps; legacyOverlapPairs records the former risk count. Additional schema-2 fields: crosswalkCandidates, crosswalksDrawn, crosswalksSuppressed, legacyOverlapPairs, crosswalkAdjustedJunctions. The Lab highlights adjusted nodes in purple and reports candidate/selected counts. CI adds four strengthened baseline tests and seven new test cases (five synthetic cases and two ranges of eight seeds). Visual acceptance of omitted markings and other mesh behavior remains manual.

## Batch history and seed reproduction

The Lab retains the latest batch summary in memory, including a row for every completed requested seed: actual seed, generation/pass status, topology issue messages, warning codes/count, elapsed diagnostic time, city size and path to a full report when exported. The summary automatically exports a uniquely named `city-batch-*.json` to Library/CityHighways/CitySeedDiagnostics, including cancelled/partial batches. Use Export batch summary JSON to save a copy elsewhere. Counts are derived from rows; partial batches do not mark unrun seeds as failed. The optional problems filter includes failed, warned and fallback seeds. Navigate 12 rows per page and use Reproduce to regenerate the original requested seed with the batch's audit setting, not the fallback seed. Reproduction uses the current generator version and is not a guarantee across code changes. Full diagnostic exports remain failure/warning-only; successful/fallback-only rows retain summary data. Historical JSON files are not loaded into the window yet. No game or save data is changed.

## Archive browsing and baseline comparison

Open **Diagnostic archive and comparison** in the City Generator Lab, then **Refresh local archive**. Up to 50 recent summaries are offered from a non-recursive scan of at most 300 matching local files. This is a bounded view, not a complete chronological history. Select **View summary** or **Use as baseline**; arbitrary exported summary JSON can also be chosen explicitly using the import buttons. Import is read-only, with a 4 MiB strict UTF-8 size limit, supported schema, seed bounds, ordered rows and consistency checks. Embedded report paths are informational and are never opened automatically. Cancelled batches are valid; a rejected import leaves the previous selection intact. Reproduce regenerates the original requested seed using the selected batch's diagnostic mode and current generator code.

Choose one archived baseline and another selected batch, or run a new batch. The comparison requires matching topology/3D diagnostic mode and joins completed rows by requested seed. It reports shared and unmatched rows, new/resolved failures and, only for seeds passing in both runs, changes in actual seed, city counts and warning totals. A failed or cancelled generation is not interpreted as a resolved geometry warning. These are summary comparisons, not a topology/content hash, exact reproduction across generator versions, or a performance benchmark. Archive selection and baseline are in-memory UI state; exported JSON remains on disk. No gameplay or save data is changed. Manual Editor UI acceptance remains open.

## Generation attempt trace and fixed tutorial city

Each single or range diagnostic now records the original generator attempts, not a second simulation. Expand **Generation attempts** to see reason counts and up to eight attempts per page. Entries contain attempted seed, 1-based attempt index, profile (`standard`/`tutorial`), outcome, locations/roads/component counts when available (`-1` = not reached), and the original validation messages where available. Stable outcome codes: `airport-placement`, `station-placement`, `port-placement`, `center-placement`, `location-count`, `disconnected-graph`, `map-validation`, `water-validation`, `tutorial-hub-count`, `accepted`. `started` means the attempt did not reach a recorded outcome (e.g. unexpected exception), not successful generation.

The Standard algorithm retains its 60-attempt limit and stride 7919, including Int32 wraparound. The existing fixed tutorial algorithm retains up to 400 attempts and the existing Standard fallback (up to 460 total trace entries); the tutorial request and individual attempt profiles are distinct. **Generate fixed tutorial city** runs the actual tutorial path, not a new configurable variant. The ordinary range and archive reproduction remain Standard-only. Gameplay callers need not allocate a trace. The diagnostic report's existing schema 1/2 is extended with optional `generationProfile` and `attempts` fields; save/replay formats are unchanged. Failed, warned and retried diagnostics are exported automatically in the Lab, even if the eventual fallback is valid. Timings include diagnostics, not solely generator work.

Regression tests compare traced and untraced outputs and inspect ordered trace and JSON round trips; this is not a historical pre-change golden corpus or proof of visual appearance. Layout tuning/custom parameter presets remain a separate task. No new production layouts or balance changes are introduced.
