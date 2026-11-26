# City Highways — development roadmap 2.0

Status: proposed product/engineering roadmap, agreed starting point for the stabilization batch on 2026-11-25. It supplements, does not silently close, the original roadmap issue #115 and visual issue #110.

## Working agreement

- Large coherent batches, separate commits for completed work; one Draft PR at a time. Wait for the user's manual test-completion message, then verify all applicable workflows succeeded on the exact final HEAD, including Edit Mode and Play Mode. Scheduled development is disabled at the user's request. Verify mergeability and squash merge with expected HEAD; open only one new Draft PR, never merge it before its own green CI. Stop for external or manual acceptance blockers; do not waive them.
- Preserve compatibility, deterministic game rules, existing menu and Interface Editor. Do not change live Steam/Supabase configuration without separate release setup.
- CI green is implementation evidence, not runtime acceptance. Distinguish verified code, automated test results, playtest hypotheses and release blockers.

## Stage 6 — stabilization (P0)

Batch 1, merged PR #134 (1b7c1b6): optional ring fix; geometry preflight; save/replay compatibility audit and version guard; 64-seed stress range plus determinism; reproducible diagnostics and City Generator Lab foundation; localization classifier and HUD audit; isolated Play Mode smoke and CI. Final HEAD 2e6f347: 1,341 Edit Mode tests and 2 Play Mode tests passed; localization audit passed (56 unresolved heuristic candidates). Manual Lab and visual acceptance remain open.

Next gates:
- [ ] Batch 4: optional 3D XZ layout audit using renderer-prepared rivers, planned hub footprints, road water clearance and crosswalk envelopes. City Generator Lab overlay and JSON exports; four baseline seeds recorded by CI. PR #137 merged (c162f93): 1,388 Edit Mode tests and Play Mode / Magnate river checks passed. Four CI reports contain 118 crosswalk-envelope conflict pairs; no other warning types in this sample. Visual inspection remains open.
- [ ] Batch 5: deterministic non-overlapping 3D crosswalk selection, shared audit counters and adjusted-node overlay, regression checks on original four seeds plus sixteen additional generated maps. PR #138 merged (b1fa164): 1,395 Edit Mode tests, Play Mode and Magnate river checks passed. Visual acceptance still open. Preserve topology and save compatibility.
- [ ] Batch 6: isolated checkpoint/load/replay continuation across five Classic city/scenario cases, all three co-op difficulties, all twelve campaign missions; four bounded deterministic Magnate matches and attached co-op UI smoke. PR #139 merged (9c366e4): final HEAD f50588e passed Edit Mode and Play Mode; standalone boot and actual full session/manual acceptance remain open.
- [ ] Batch 2: non-destructive load failure, bounded Classic/campaign save I/O, previous snapshot backup and explicit Editor recovery, save-summary localization. PR #135 merged (5a193f8): 1,352 Edit Mode tests, 2 Play Mode tests, Interface Editor and main menu visual guard passed. Manual recovery smoke remains open. Historical golden fixtures, runtime recovery UX, Magnate persistence and map revision identity remain open (see SAVE_RECOVERY.md).
- [ ] Full standalone boot/menu/game/return tests with isolated data; actual controller and Deck 1280x800.
- [ ] Resolve real localization defects, review unresolved scanner candidates; RU/EN visual matrix and no clipped text.
- [ ] Batch 3: split clock-only refresh from full HUD, add three profiler scopes, reuse score string, localize record/raid banner, add policy and runtime label tests. PR #136 merged (8fc9127): 1,377 Edit Mode tests, 3 Play Mode tests, Interface Editor and Social replay guard passed; shared license queue added. Actual profiling still pending; measure payment allocations and map repaint before further optimization.
- [ ] Measure current comparable City3D cold/warm startup, frame P95/P99 and memory across repeated games. Existing performance suite; no speculative speed claims.

## Stage 7 — City Generator Lab and graphics (P1)

Extend the Lab with bounded validated layout presets, river/hub/bridge/road clearance, 3D footprint checks, failed attempt trace and seed archive. Shared geometry between generator and renderer; retain generation reproducibility and save compatibility. Visual differentiation of city architecture and optional atmospheric effects only after measured budgets. Acceptance: agreed seed corpus and actual 2D/3D inspection, no invalid crossings or inaccessible required hubs.

Batch 1 (implementation pending CI): bounded diagnostic seed ranges (1–512, no Int32 overflow), quick/standard topology and 3D audit presets, fallback seed counts and boundary tests. These presets control diagnostic workload only, not map generation parameters. Failed-attempt tracing, seed archive browsing and visual acceptance remain open.

## Approved priority interlude — Profile 2.0 + Living City (P0/P1)

User approved [#145](https://github.com/vladsuhanovsky/cityhighway/issues/145) on 2026-11-26. After #144 merged (2f01c37; exact HEAD 12adad5: 1,461 Edit Mode tests, Play Mode, river geometry, Interface Editor passed; no C# warnings in Edit Mode log), temporarily defer configurable City Generator Lab layouts. See [Profile 2.0 + Living City](PROFILE_2_0_LIVING_CITY.md) for the detailed thirteen packages and acceptance gates. Begin with five primary routes, overview, canonical statistics, wallet header and legacy feature retention. Character/wardrobe assets, company/garage, cosmetic construction vehicles and avatars, observation camera and collection follow. Do not mark earlier manual acceptance or deferred Stage 7 tasks complete. Manual test confirmation stays in force.

## Stage 8 — interface and accessibility (P1)

Improve gameplay dashboards, map search and contextual action previews; preserve existing layers, controls and Interface Editor. Editable hotkeys and component scale, theme import/export and multi-resolution preview after UX review. Acceptance: mouse/keyboard/controller, localization, compact layout, reduced motion and color-independent ownership.

## Stage 9 — AI and Balance Lab (P1)

Unify deterministic headless simulations for mode/map/scenario/player count/AI policy; record win rates, duration, deadlocks, economy and confidence intervals. Add bot difficulty/personality only with measured fair behavior. Do not promise balance from a few seeded wins.

## Stage 10 — Logistics Magnate 2.0 (P1)

Prototype production chains and traffic/capacity decisions on the existing turn model; evaluate fleet upgrades, dynamic city and later multimodal logistics as distinct experiments. Maintain understandable turn duration, resource choices and readability. Gate large rule changes on playtests, backwards compatible saves/replays and AI support.

## Stage 11 — co-op and campaign (P2)

Playtest existing three difficulties, twelve missions and new maps first. Scenario objectives, campaign briefings, optional goals and medals, cooperative roles; avoid parallel duplicated progression systems. Acceptance: completion, failure/retry, checkpoint and profile isolation.

## Stage 12 — network/cloud reliability (P1)

Actual LAN/Steam/UGS acceptance; explicit identity and reconnect after restart, conflict handling and idempotent cloud writes, authoritative validation; difficulty/scenario/official-map record separation. Host migration only after protocol design. No live DB/schema changes in this batch.

## Stage 13 — custom maps and Workshop (P2)

Finish strict data-only versioned import/export; immutable content identity, isolated local-only setup/save/replay/rewards, geometry/editor/preview and safe package paths. Native subscription, update and publish only after real AppID, Steamworks.NET and Partner ISteamUGC setup and review. Current PR's validator is a foundation, not a runnable mod system.

## Stage 14 — experiments (P2)

Daily fixed-seed challenge first (evaluate existing related systems, separate local and verified server scores); sandbox, survival and multi-city career only after core UX/retention evidence. Tournament/ranking seasons post-release candidates.

## Stage 15 — commercial release (P0, begin external blockers in parallel)

Real registered AppID rather than 480, package dependency and native callbacks, published achievements, cloud quotas and privacy/recovery; standalone Windows release builds; Deck/controller; actual multiplayer and cloud service QA; closed playtest, demo/store assets and release notes. Workshop is optional for initial launch. No final signoff until all required gates pass.

## Proposed measurements (not current performance claims)

60 FPS desktop and stable 30 FPS Deck at agreed hardware/settings; target P95 frame <=16.7ms for a 60 FPS configuration; ordinary map <=3s, complex generation <=5s, action response <=100ms; no sustained memory growth over repeated matches. Validate feasibility on actual hardware and define the workload before using these as gates.

No fixed delivery dates or unsupported completion percentages. Reprioritize after each measured acceptance batch.

Stage 7 progress: PR #141 merged (9d09bc8), 1,431 Edit Mode tests and Play Mode passed; the menu, Interface Editor, social replay and river guard path filters did not match its changed files. Next batch adds per-seed summary rows, independent counts, cancelled batch exports, paging, problem filtering and reproduction of requested seeds. CI and Editor UI acceptance pending. Layout parameter presets and attempt-by-attempt trace remain separate work.

Stage 7 progress: PR #142 merged (345babe8), 1,437 Edit Mode tests and Play Mode passed. Batch 3 implements bounded archive browsing/import, structural summary validation, partial-run support, baseline comparison by shared requested seed and the existing reproduction action. Geometry metrics compare only seeds passing in both runs. Twelve new Edit Mode cases cover import, malformed/oversized data, validation, bounded listing and comparison. CI and manual Editor acceptance pending; failed-attempt tracing and actual layout presets remain open.

Stage 7 progress: PR #143 merged (9d98b2d), 1,449 Edit Mode tests and Play Mode passed. Batch 4 adds opt-in generator attempt recording, stable rejection reasons and validation messages, partial counts, paginated Lab trace, automatic retry report export and a fixed tutorial diagnostic action. Eight new Edit Mode cases cover standard/tutorial traced vs untraced outputs, trace ordering/reset and report JSON. Existing attempt budgets, RNG calls and acceptance rules retained; exact historical golden map corpus, configurable layout presets and manual Editor/3D acceptance remain open. CI pending.

Stage 7 batch 4 follow-up (user-reported Unity Editor warnings): CI run 541 reproduced 27 distinct project CS0618 diagnostics, 22 from eleven unsorted object searches and five from obsolete UI Toolkit PreventDefault calls. Migrate all eleven searches to the single FindObjectsInactive overload (preserve Include/Exclude and existing ordering/ambiguity logic). Keep StopPropagation for Submit/Cancel, retain HUD trickle-down interception, and explicitly ignore focus navigation on active MapView NavigationMoveEvent. Four lookup regression cases cover inactive, disabled and duplicate configurations. No suppression pragmas, editor upgrade or gameplay changes. Recheck exact final HEAD compiler warnings and CI; manual controller focus/submit/cancel acceptance remains open.

Profile 2.0 interlude: #146 merged (3bed5cc), 1,468 Edit Mode tests, Play Mode, Interface Editor and main-menu visual guard passed. Batch 2 introduces catalog-backed appearance snapshots, validated optional modular rig binding/Inspector, item-only wardrobe thumbnails and fourteen new tests. Actual rigged assets and 3D profile/world presentation are not delivered; CI and manual visual acceptance pending. See Docs/PROFILE_2_0_LIVING_CITY.md.

Profile 2.0 progress: #146 merged (3bed5cc), 1,468 Edit Mode tests, Play Mode, Interface Editor and main-menu guard passed, no C# warnings in Edit Mode log. Character 2.0 batch 2 adds normalized appearance snapshots, explicit modular prefab bindings and an isolated Editor 3D Lab with a placeholder silhouette; nine additional Edit Mode cases. Actual production character art, skinning, animation, wardrobe integration and visual acceptance remain open. See Docs/PROFILE_2_0_LIVING_CITY.md. CI pending. Both authoring paths are currently experimental; consolidate appearance and rig strategy before runtime integration. Combined HEAD requires fresh CI.

Profile 2.0 batch 3a: #148 merged (a2bd19c); unified appearance normalization, canonical library entry points, compatibility adapter and Lab zoom/reset. Four additional Edit Mode cases and expanded assembly checks. CI pending. Actual runtime preview, asset fit and production rig selection remain open.

Profile 2.0 batch 3b: #149 merged (186f3fa); optional runtime ProfileAvatarRig preview integrated with Overview/Wardrobe, fixed resource paths, 2D fallback, bounded rotation/zoom and resource lifecycle. Five new Edit Mode cases. CI pending; no actual authored production prefabs, animation or measured runtime visual/performance acceptance. See Docs/PROFILE_2_0_LIVING_CITY.md.

Profile 2.0 batch 3c: #150 merged (eb7f313); explicit Editor generator for two static modular prototype prefabs, full existing catalog binding coverage and three new Edit Mode cases. Generated assets not committed; local generation and real preview validation pending. Production art and animation remain open.
