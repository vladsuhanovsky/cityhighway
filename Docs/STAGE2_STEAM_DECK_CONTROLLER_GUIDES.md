# Stage 2 — Steam Deck/controller discoverability and text entry

This continues Stage 2 after merged PR #80. It is a large but bounded usability slice: **localized control legends** and **safe native UI Toolkit text editing**. It does **not** add Steamworks dependencies, change gameplay actions, claim Steam Deck certification or attempt to programmatically invoke the SteamOS keyboard.

## What changes

- A pointer-transparent, non-focusable help strip appears after an intentional controller interaction in the main menu, classic/co-op gameplay and Logistics Magnate.
- While the map's virtual cursor is active, the strip is hidden; the cursor has its own R3/right-stick/D-pad/A/B hints from PR #79.
- While a blocking gameplay modal is active, the gameplay strip is hidden so it cannot overlap payment or handoff actions.
- Game mode help strips sit **above the lower gameplay action bar**, rather than covering its buttons.
- The main menu guide updates for RU/EN and the focused control. When a native `TextField` is in focus, it explains normal keyboard text entry and the Steam Deck **STEAM + X** shortcut.
- In a focused text field, gamepad B and keyboard Escape now **leave text editing first** rather than also navigating out of the page or dismissing a gameplay dialog. If an eligible sibling button is present, focus moves there. The field is still a native UI Toolkit `TextField`; existing value-change callbacks and persistence remain in place.
- Editable player names and profile fields display a contrasting border on focus and restore their normal border on blur.

## Why not open the SteamOS keyboard directly?

The game currently uses Unity UI Toolkit and Unity Input System; it does not depend on Steam Input/Steamworks SDK bindings to invoke the OS keyboard from C#. The official Valve Steam Support documentation describes **STEAM + X** as the shortcut to show the on-screen keyboard while Steam is running:

https://help.steampowered.com/en/faqs/view/671A-4453-E8D2-323C

This PR **documents and surfaces** the shortcut only. It does not promise automatic invocation when entering a text field or compatibility in every SteamOS/Desktop Mode combination. Actual Steam Deck behavior must be checked on hardware.

## UI/UX design

- The guide label uses UI Toolkit, `PickingMode.Ignore`, `focusable=false`, small wrapped type, and an unobtrusive translucent background.
- It is visible only when a controller is connected and controller navigation has been activated; mouse-only sessions and controller-disconnected views do not display it.
- Language selection is read from `LocalizationService.Language` in the existing frame update; no new persistent setting or localization catalog keys are introduced.
- The guide never calls a gameplay action, sends artificial keyboard events, opens a new modal or modifies Input System bindings.

## Tests (Unity 6000.6.8f1 Edit Mode)

- `GamepadControlGuideTests`: connection/activity visibility, targeting suppression, RU/EN hints for menu and both modes, Steam Deck keyboard shortcut, `TextField` ancestry, non-picking/non-focusable label, language refresh.
- `GamepadTextEntryTests`: inner text input ancestry, nearest eligible button on exit, hidden/disabled/detached focus targets, panel-detached exit safety, focus-ring registration.
- Existing controller tests for card navigation, map cursor, focus repair and scrolling must continue passing.

## Manual acceptance on Steam Deck / real gamepad

Test RU and EN, **1280×800** and desktop resolution, with both controller and mouse/keyboard:

1. Open the main menu without using the controller. The help strip should stay hidden; move the D-pad once and verify it appears with localized A/B controls.
2. Open New Game, select a player-name text field. A Steam Deck hint should replace navigation help. Press **STEAM + X** on real Steam Deck to bring up its OS keyboard, type a name and close the keyboard.
3. Press B while editing a name: it should exit the field, not leave New Game. Press B again: it should invoke the normal menu Back action.
4. Repeat with Escape on a physical keyboard and with profile text fields. Save/reopen to verify normal string validation and persistence.
5. Start classic/co-op. Check the help sits above the lower action bar and never intercepts a pointer click. Enter R3 map targeting: the ordinary guide disappears and the map-specific guide remains.
6. In Magnate, check its own camera/targeting hint, placement above the truck action bar and suppression during modal/popup navigation.
7. Open Settings, Rules, payment and handoff screens. The context-specific guide must not obstruct modal buttons or break B/Back behavior.
8. Disconnect the controller and switch to the mouse. Help must disappear without affecting ordinary keyboard navigation.

## Remaining Stage 2 work

This PR does not cover: automatic SteamOS keyboard invocation through Steam Input, dynamic controller-brand glyphs, native Steam achievements bridge, full Steam Deck build packaging/testing, controller-specific UI navigation in every custom input field, undo history or music. These remain separate testable iterations.

No CI monitoring or automated merging is enabled. The user reports results manually.

## Follow-up: real pointer handoff and focused controls

The Steam Deck controller refinement adds a pointer-driven handoff and more
resilient focus recovery without simulating mouse clicks or invoking SteamOS APIs.

- A mouse or Deck trackpad movement of at least 3 UI input units in a frame
  releases controller focus in the main menu and both gameplay modes. Smaller
  jitter is ignored; a fresh D-pad, left-stick or A input reclaims controller
  focus. Disconnecting the controller also clears menu focus-ownership state.
- If a focusable scroll container has an eligible action inside it, initial
  controller focus selects that action before the scroll container itself.
- When dynamic cards temporarily have duplicate focus names, restoration
  ignores hidden/disabled stale cards and can find the visible replacement.
- Leaving a text field with B/Escape can now select a visible enabled button
  inside an adjacent row wrapper. It never selects controls nested *inside*
  the field that is being exited.
- Unity Edit Mode regression tests cover mouse jitter threshold, nested
  scroll focus, duplicate card names, nested row action targets and
  protection from re-entering a field's own internal controls.

Manual Steam Deck acceptance extension:

1. Navigate New Game by D-pad, move the trackpad **without clicking** and
   verify the help strip disappears; use A/D-pad again to reclaim it.
2. Repeat in classic/co-op and Logistics Magnate. Confirm a moving mouse
   never activates a gameplay action.
3. Open a long scrollable Rules/Contracts/Orders list with the controller.
   The initial focus should land on a real visible control, not just the
   scroll viewport, and ScrollTo should still reveal it.
4. Open a dialog with hidden old/rebuilt cards and verify navigation
   recovers to the visible current card.
5. Focus a text input next to a styled button wrapper, press B/Escape and
   verify only editing ends; a second B performs the usual Back.
6. Check hot-seat, keyboard/mouse and 1280x800 RU/EN layouts for regressions.

**Limit:** Physical Steam Deck/Steam Input emulation has not been exercised by
Edit Mode CI. This is a controller usability improvement, not certification.
