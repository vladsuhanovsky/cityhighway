# Stage 2: gamepad navigation in gameplay HUD and map camera

This is the **second controller-UX slice** following PR #76. It prepares the game for controller-driven menus and camera movement, but **does not make City Highways fully playable without a mouse yet**.

## What was already in place

- Unity Input System and a UI action map for D-pad/stick navigation, Submit and Cancel.
- Focusable home menu tiles and settings controls (PR #76).
- Mouse and keyboard map navigation: Q/E rotate, R/F tilt, Home camera reset, mouse wheel and minimap buttons for zoom.

## This PR

- The classic/cooperative `GameView` and Logistics Magnate `MagnateView` now maintain focus on visible UI Toolkit controls **after meaningful D-pad/stick or A/Submit input**. Small stick drift and ordinary mouse-only play do not automatically claim controller focus.
- Controller **B** invokes the existing registered Back action, if present, without synthesizing an unsafe "cancel" of mandatory game choices. English `← Back` and Russian `← Назад` are recognized when buttons are constructed.
- Controller camera input is polled at the shared `MapView`, so it is available in both play modes, with no gameplay rule change:
  - **LB / RB**: rotate the 3D camera left / right
  - **X / Y**: lower / raise the 3D camera angle
  - **LT / RT**: zoom out / in (also works for flat 2D map)
  - **L3**: reset zoom/pan and 3D camera orientation
- The existing UI Navigate inputs (D-pad and sticks), and **A / B**, are not reused for camera adjustment.
- Camera inputs are ignored when the map is not interactive, when its UI panel is not present, or when a text input has focus. Existing Q/E/R/F/Home behavior remains available.

## Edit Mode coverage

`GamepadCameraMotionTests` covers opposite/simultaneous shoulders, X/Y tilt, trigger noise/analogue pressure/clamping, reset intent and zero movement. `UiGamepadFocusTests` validates recognition of both localized Back labels.

These are **unit tests of controller intentions and menu helper behavior**. They do not simulate real hardware or certify Unity UI Toolkit dispatch. The Edit Mode CI job must pass before merging.

## Manual acceptance: in Unity Editor or a standalone build

1. With a connected gamepad, open a classic/cooperative game. Use the D-pad to focus a HUD button, press A to activate it, and press B on an appropriate "Back" screen; do not accidentally commit a payment or end a turn.
2. Repeat in Logistics Magnate. Verify controller focus remains visible after opening/closing HUD panels.
3. In the 3D city, test LB/RB rotation, X/Y tilt, LT/RT zoom and L3 reset; verify camera remains centered, no shake/drift and normal mouse controls still work.
4. In 2D mode, verify triggers still zoom and shoulder/face buttons do not rotate a nonexistent 3D camera.
5. Open an overlay with a text field or a modal payment action. Camera should not move while `MapView.Interactive` is disabled or text input has focus.
6. Repeat the checks with RU and EN UI, reduced motion, 1280×800 resolution and a low UI scale.
7. Disconnect and reconnect the gamepad. Confirm keyboard/mouse still work without controller.
8. Confirm Steam Deck build behavior on real hardware; no certification claim is made by CI.

## Follow-up

Controller selection of road segments and stations, contextual button prompts, game card browsing, consistent focus scrolling in long panels, and text entry/on-screen keyboard remain outstanding before complete gamepad/Steam Deck support.

## Controller focus recovery after UI rebuilding (follow-up)

This slice hardens the **existing** gamepad UI-navigation path in both
Classic/cooperative and Logistics Magnate; it does not invent new
gamepad buttons, change game rules or emulate Steam Input.

- Rebuilt card, layer and HUD controls may advertise stable
  `gamepad-*` identities. Controller navigation restores the newly
  eligible element after a refresh rather than leaving focus on a
  detached UI Toolkit object.
- Focus restoration searches **only within the currently active
  modal, handoff, results or overlay scope**. A matching HUD control
  behind a blocking screen cannot steal focus.
- Only a currently visible **and enabled** control can be restored.
  Anonymous elements and the pointer-transparent controller hint
  label are not restoration targets.
- The gamepad focus-return memory is cleared at the beginning of a
  different or resumed game in both modes. Internal Classic Undo/Redo
  state reconstruction keeps the current session's focus memory.
- Classic Undo/Redo, log, menu and settings top-bar actions now have
  stable names for controller navigation. If the player currently
  focuses Undo or Redo and that button disables after a state change,
  focus transfers to the enabled neighbor or to the log action.
  Controller focus is not moved while a blocking modal or the
  virtual map cursor is active.
- Both Undo and Redo availability now refresh immediately after a
  successful action, so a gamepad user does not have to wait for an
  additional frame before selecting the next valid control.

### Focus QA on real hardware

1. Start a local all-human Classic match. With gamepad navigation,
   focus Undo, undo the last reversible action and confirm that focus
   moves to the Redo button if Undo is disabled.
2. Redo the last undone action. If Redo is now disabled, verify focus
   moves to Undo (or the journal if both are unavailable).
3. Open a modal or drawer, change an action's availability, and
   confirm that focus never transfers to a button behind the modal.
4. Open card selection, change the data to rebuild the card list, and
   confirm the selected named card stays focused if still eligible.
5. Repeat in Magnate for rebuilding layers, changing view and
   opening/closing a popup or handoff.
6. Target the map with R3; Undo/Redo focus repair must not interrupt
   the map cursor or consume its inputs.
7. Leave a game, start a different game, and reconnect the controller:
   the old game's focused button must not be resurrected.
8. Repeat with RU/EN, 1280x800 Steam Deck layout and the Deck
   trackpad switching to pointer input.

Editor tests verify stable focus-name rules, blocking-scope selection,
fallback decisions, disabled/hidden controls, and safety with detached
trees. Hardware/controller UI interaction remains a manual gate.
