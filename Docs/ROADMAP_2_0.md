# City Highways — development roadmap 2.0

Status: proposed product/engineering roadmap, agreed starting point for the stabilization batch on 2026-11-25. It supplements, does not silently close, the original roadmap issue #115 and visual issue #110.

## Working agreement

- Large coherent batches, separate commits for completed work; one Draft PR at a time. User manually reports tests passed/failed. Verify all required workflows on exact final HEAD; no auto-merge or automatic next batch.
- Preserve compatibility, deterministic game rules, existing menu and Interface Editor. Do not change live Steam/Supabase configuration without separate release setup.
- CI green is implementation evidence, not runtime acceptance. Distinguish verified code, automated test results, playtest hypotheses and release blockers.

## Stage 6 — stabilization (P0)

Batch 1, merged PR #134 (1b7c1b6): optional ring fix; geometry preflight; save/replay compatibility audit and version guard; 64-seed stress range plus determinism; reproducible diagnostics and City Generator Lab foundation; localization classifier and HUD audit; isolated Play Mode smoke and CI. Final HEAD 2e6f347: 1,341 Edit Mode tests and 2 Play Mode tests passed; localization audit passed (56 unresolved heuristic candidates). Manual Lab and visual acceptance remain open.

Next gates:
- [ ] Repair any CI failures, especially new generator coverage. Preserve real failed seeds and regression cases. Capture 3D river/hub/road reproduction.
- [ ] Batch 2: non-destructive load failure, bounded Classic/campaign save I/O, previous snapshot backup and explicit Editor recovery, save-summary localization. PR #135 merged (5a193f8): 1,352 Edit Mode tests, 2 Play Mode tests, Interface Editor and main menu visual guard passed. Manual recovery smoke remains open. Historical golden fixtures, runtime recovery UX, Magnate persistence and map revision identity remain open (see SAVE_RECOVERY.md).
- [ ] Full standalone boot/menu/game/return tests with isolated data; actual controller and Deck 1280x800.
- [ ] Resolve real localization defects, review unresolved scanner candidates; RU/EN visual matrix and no clipped text.
- [ ] Batch 3: split clock-only refresh from full HUD, add three profiler scopes, reuse score string, localize record/raid banner, add policy and runtime label tests. Pending CI and actual profiling; measure payment allocations and map repaint before further optimization.
- [ ] Measure current comparable City3D cold/warm startup, frame P95/P99 and memory across repeated games. Existing performance suite; no speculative speed claims.

## Stage 7 — City Generator Lab and graphics (P1)

Extend the Lab with bounded validated layout presets, river/hub/bridge/road clearance, 3D footprint checks, failed attempt trace and seed archive. Shared geometry between generator and renderer; retain generation reproducibility and save compatibility. Visual differentiation of city architecture and optional atmospheric effects only after measured budgets. Acceptance: agreed seed corpus and actual 2D/3D inspection, no invalid crossings or inaccessible required hubs.

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
