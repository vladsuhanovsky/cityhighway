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
