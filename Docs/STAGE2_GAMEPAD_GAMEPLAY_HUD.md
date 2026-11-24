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
