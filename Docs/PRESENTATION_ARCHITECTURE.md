# City Highways — Presentation refactoring

## Goal

Reduce the responsibilities of the Unity UI Toolkit gameplay screen without
changing game rules, saved games, game commands, or the player-facing visuals.
The Core layer remains the authority for validation and game state transitions.

## Current boundaries

| Module | Responsibility |
| --- | --- |
| `GameView.cs` | Gameplay screen composition, input coordination, turn state and animation orchestration |
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

- Separate remaining gameplay animation construction from screen coordination.
- Move HUD and status-bar rendering into reusable widgets with explicit inputs.
- Map tooltip text and read-only queries extracted to `GameTooltipText`; keep event positioning and viewer/handoff decisions in the screen. Next: extract reusable tooltip positioning and HUD refresh logic.
- Replace direct, cross-panel field access gradually with explicit interfaces
  once the behavior is protected by regression tests.

Avoid splitting files just for a smaller line count. Future components should
have coherent responsibilities, explicit dependencies and focused tests.

## Tooltip boundary (roadmap continuation)

`GameTooltipText` has no VisualElement, callback, or GameView dependency. The screen passes the current viewer, bot status, handoff and action eligibility; the formatter reads existing Core methods and localization. Contract titles share the same formatter. Six RU/EN test cases cover read-only road state transitions, hidden handoff information, and player/pieces values. Manual smoke checks: hover road/location/player/resources before and after building, during hotseat handoff, in RU/EN, and at narrow/wide viewport sizes. No claims about manual QA or performance measurements are made by this extraction.
