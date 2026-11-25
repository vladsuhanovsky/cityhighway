# Localization and HUD audit — stabilization batch 1

## Localization (aa0da5f, Unity CI #511)

The existing scanner reported 1,577 RU/EN catalog entries and 94 direct UI candidates. Catalog parity and placeholder checks passed. The first 20 logged candidates are Achievement declarations using localized Title/Description getters, not proof of visible Russian strings. Reviewed Achievements.cs and both catalogs: all 28 achievement IDs and 10 daily task IDs have the expected title/description keys. Reviewed MagnateView tooltips/results samples, which have explicit RU/EN branches. User names and old stored log strings should not be translated blindly.

This patch extends the existing audit: validate dynamic achievement/daily keys in both catalogs, classify known fallback declarations, retain original candidate count and output unresolved candidates separately. No translation strings or rules altered. The scanner remains heuristic, including interpolated code; unresolved candidates need manual review at runtime. The original raw count is not an actual defect count.

Manual acceptance: launch all five Classic maps, Magnate, campaign, replay, rules, settings, lobby and results in RU and EN, switch language, verify long labels at 1280x800 and 1920x1080. Capture exact screen/seed for a real defect. Additional languages and translation of unsupported historical raw messages are out of scope.

## Classic HUD

Verified from GameView.cs:
- Tick calls Refresh only on _dirty (around 1295), not on every frame. Do not assume per-frame full HUD rebuild.
- Timer tick marks _dirty each integer-second transition (around 1411). Refresh then invokes event/goals/ghost, map visibility, face-up cards, player rows, contracts, log, status, hand, overlays and map (around 2320).
- RefreshMap iterates all roads and calls GetPaymentOptions plus CanBuildRoadFree when the player can act (around 2820), even on timer-only refresh. This is the strongest targeted profiling candidate.
- RefreshPlayers formats score twice and builds per-player readouts; RefreshHand and RefreshFaceUp update labels/control states even when state is unchanged.
- MapView.Tick always calls Repaint; separately measure camera/animation and idle rendering. These are potential costs, not measured allocations.
- Dirty flag also protects controller focus restoration, changes of language, network state, handoff, actions and transient notifications. Do not skip those updates.

Measure in Unity Profiler Timeline / GC Alloc using the existing performance suite on the same build, map, quality and resolution: 60s idle with no timer; 60s with timer; road preview; 10 repeated build actions; state resync/undo; language change. Compare full Refresh count and self-time, GetPaymentOptions allocation, GC.Alloc/frame, P95/P99 and map repaint. Capture both current and candidate build. Next optimization: isolate timer-only status text/color update without changing command legality or modal/focus refresh. No performance improvement or profiling result is claimed in this audit.

Save audit supplement: existing LegacySaveFixtures exercises version-tagged synthetic v2, v4, v10, v13 snapshots. Real older-build fixtures remain outstanding.

## Batch 3 — HUD/record banner changes

GameView now selects full refresh when _dirty is set, clock-only refresh when the displayed countdown or timer reset needs repaint, and no refresh otherwise. The clock renderer only writes the status label and warning color. Timer expiry and actions retain the full state refresh and bot logic. Full refresh wins on simultaneous dirty flags (state/language/notifications). The full refresh uses existing focus restoration. Player score text is formatted once for both readouts. Added markers CityHighways.HUD.FullRefresh, CityHighways.HUD.ClockRefresh and CityHighways.HUD.MapRefresh. This removes an identified timer-driven full-refresh call path; it is not a measured FPS or allocation improvement. Capture same seed/settings before and after; verify idle timer, paused overlay, reset, timeout, handoff, controller focus and network acknowledgement on a real session.

Localization: prior PR #135 passed with 55 heuristic candidates needing context review. In this batch, translated record challenge/raid buttons, tooltip, rival and personal record summaries, ghost author and mode names and displayed city names. User-entered names and record identifiers remain unchanged. Mastery title declarations already use mastery.node.*.title keys; GameSetup choices have separate English fields; tutorial titles use EnglishFirstGameTutorialTitles. Do not classify those as proven visible defects. New unit tests cover English/Russian record text, timer formatting, thresholds and refresh priority. Play Mode adds an isolated attached UI Toolkit clock-renderer smoke test; full GameView lifecycle and visual/runtime profiling remain open.
