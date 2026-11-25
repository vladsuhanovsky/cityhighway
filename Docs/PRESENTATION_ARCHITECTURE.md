# City Highways — Presentation refactoring

## Goal

Reduce the responsibilities of the Unity UI Toolkit gameplay screen without
changing game rules, saved games, game commands, or the player-facing visuals.
The Core layer remains the authority for validation and game state transitions.

## Current boundaries

| Module | Responsibility |
| --- | --- |
| `GameView.cs` | Gameplay screen composition, input coordination, turn state and animation orchestration |
| `GameFxSnapshot.cs` | Detached presentation state capture and deterministic card-flight planning |
| `GameFxVisuals.cs` | Temporary card, coin and floating-text UI construction and animation |
| `GameFxNotifications.cs` | Toast, localized transient errors, event glow and notification reset |
| `GameFxLifetime.cs` | Scope-owned effect scheduling and temporary UI element cleanup |
| `GameHudReadout.cs` | Classic player/status/final-round display data without VisualElements |
| `GameTooltipText.cs` | Read-only Classic road, location, player, resource and pieces tooltips; shared contract titles | 
| `GameView.Contracts.cs` | Contract selection and cards, pinned paths, route highlighting |
| `GameView.Panels.cs` | Sheets, player drawer, modal windows and settings controls |
| `GameView.Journal.cs` | Journal entries, translations of historic messages and scroll position |
| `GameView.Raid.cs` | Ghost challenge gameplay presentation |
| `MapView.cs` | 2D/3D map overlay, painting and map pointer events |
| `HudWidgets.cs` | Reusable card carousel and HUD widgets |
| `GameFxTuning.cs` | Pure timing and layout tuning for presentation effects |

The `GameView.*.cs` files form **one C# partial class**. Moving methods between
these files is a source organization change, not a runtime or API boundary.

## Invariants

1. The UI must not calculate authoritative costs, awards, or legality.
   Call Core's `Game` methods for those decisions.
2. A refactor must not change serialization or replay commands.
3. Keep RU/EN and reduced-motion behavior intact.
4. Extractions must preserve callbacks and event unsubscribe behavior.
5. Keep contract highlighting, journal scrolling and panel animations
   functional in 2D and 3D gameplay.
6. The Unity Edit Mode suite and localization audit must pass after each
   extraction, followed by manual UI review at narrow and wide resolutions.

## Next decomposition targets

- Snapshot capture and card-flight planning extracted to `GameFxSnapshot`; card, coin and floating-text construction is in GameFxVisuals; remaining map/luck effects and event orchestration are still coordinated by GameView.
- Player HUD readout, turn instruction and final-round text extracted to `GameHudReadout`; widget composition and binding remain in GameView.
- Map tooltip text and read-only queries extracted to `GameTooltipText`; keep event positioning and viewer/handoff decisions in the screen. Next: extract reusable tooltip positioning and HUD refresh logic.
- Replace direct, cross-panel field access gradually with explicit interfaces
  once the behavior is protected by regression tests.

Avoid splitting files just for a smaller line count. Future components should
have coherent responsibilities, explicit dependencies and focused tests.

## Tooltip boundary (roadmap continuation)

`GameTooltipText` has no VisualElement, callback, or GameView dependency. The screen passes the current viewer, bot status, handoff and action eligibility; the formatter reads existing Core methods and localization. Contract titles share the same formatter. Six RU/EN test cases cover read-only road state transitions, hidden handoff information, and player/pieces values. Manual smoke checks: hover road/location/player/resources before and after building, during hotseat handoff, in RU/EN, and at narrow/wide viewport sizes. No claims about manual QA or performance measurements are made by this extraction.

## HUD / animation-state boundary

The snapshot captures the same Core values as before; only presentation state is detached. Card-flight planning preserves resource/player order, face-up slot matching, and 0.15-second stagger. A mismatched player count is ignored instead of producing invalid flight indices. The HUD readout uses explicit session flags and Core's visible score; it does not read private card identities for player rows. Existing tween callbacks, audio, UI elements, save/replay and network flows are unchanged. Eight parameterized Edit Mode cases cover snapshot capture, card-flight planning, status precedence and RU/EN player display data. Manual check: draw cards (face-up and deck), build roads, observe bot/online/hotseat status, final round and reduced motion. No measured performance claim.

## Effect lifetime / reset boundary

Classic GameView now schedules its effects via GameFxLifetime. Beginning or replacing a party cancels only that scope and removes temporary cards, coins, and floating labels; persistent HUD and tutorial elements remain separately owned. Toast, tooltip, event glow and deferred start are reset. MapView clears its transient route, construction and particle effects when the map game changes. Tweener supports scope cancellation and skips cancelled callbacks in an in-progress Tick snapshot (also for global Clear). Undo/Redo uses Begin's scoped reset instead of a global clear. Five Edit Mode cases cover cancellation, completion, reset during callbacks, clear during callbacks and scope isolation. Verify visual playback and online resync manually; no timing or performance claims.

## Notification boundary

`GameFxNotifications` owns toast creation and replacement, transient error lifetime and live RU/EN rendering, and event glow. GameView still chooses when to notify and plays audio. Notification timing, existing styles and reset behavior are preserved. Four Edit Mode cases cover overlapping toasts, reset, expiry and live language changes. Manual UI confirmation remains required.

## Temporary visual construction boundary

`GameFxVisuals` constructs cards, coins and floating numbers using the existing GameFxLifetime scope. The screen still chooses destinations, card reveal/privacy, sound and completion actions. The original motion formulas, durations and blocking flags are retained. Three Edit Mode cases cover visual cleanup, normal coin completion, nonblocking effects and cancellation. Card visibility and effects still require manual Unity QA. No performance measurements or release readiness claims.
