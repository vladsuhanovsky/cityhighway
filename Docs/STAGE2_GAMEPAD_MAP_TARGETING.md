# Stage 2: controller targeting on the map (large iteration after PR #78)

## Scope

The mouse/keyboard remains supported. This PR enables an **explicit virtual targeting mode** for the shared MapView used by classic, cooperative and Logistics Magnate games. It reuses the same event and game-rule validation as mouse interaction, and does not change any save, replay, game economy or network protocol.

### Controls

| Gamepad control | Behavior |
| --- | --- |
| **R3** (right-stick click) | Enter/exit map targeting (only on a permitted local player's turn) |
| **Right stick** | Move the virtual target in UI Toolkit pixel coordinates |
| **D-pad** (while targeting) | Fine target adjustment for narrow roads and station tokens |
| **A** (while targeting) | Select object underneath the virtual target using MapView's existing station-first `Click()` logic |
| **B** (while targeting) | Exit map targeting without also executing the HUD's Back action |
| **D-pad / left stick** (outside targeting) | Existing UI Toolkit HUD navigation |
| **A / B** (outside targeting) | Existing Submit / Back |
| **LB/RB, X/Y, LT/RT, L3** | Existing camera rotate, tilt, zoom and reset (PR #77) |

Right-stick navigation entries were removed **only from the UI Navigate map** of the `InputSystem_Actions.inputactions` file to avoid focus switching while aiming. Left stick, D-pad, keyboard and joystick UI bindings remain. The separate Player/Look action's right-stick binding is untouched.

### Visual feedback

The aim reticle is a purely visual, non-picking UI Toolkit element layered above the shared map. Its ring becomes green when a selectable road or station is under the cursor. A small, localized RU/EN hint shows the controls; a compact `R3: aim on map` / `R3: выбрать на карте` affordance appears only when a controller is present and the map may be targeted. Both reticle and prompts respect map viewport resizing. The marker cannot consume pointer clicks.

The same hover callbacks drive road and station tooltips as on mouse. **Stations take precedence over overlapping roads in Logistics Magnate**, matching the actual click semantics.

### Safety / focus behavior

The virtual cursor is disabled for bot turns, handoffs, mandatory blocking overlays, modal menus, result screens and ongoing animations. It is also disabled without a valid map/panel, on a disconnected controller or during text field editing. Targeting is never implicit: entering requires deliberate R3 input.

- When targeting begins, the map explicitly receives focus, and NavigationMove/Submit/Cancel are consumed to avoid triggering an underlying HUD action. Root-level Submit guards in both games protect against stale HUD focus.
- After A invokes an existing map selection, any opened blocking overlay exits map targeting. UI focus is directed to the visible blocking overlay, not the HUD behind it.
- When B/R3 exits targeting, controller navigation can resume on the HUD. B is consumed rather than also opening another menu or canceling a payment.
- When the user returns to a physical mouse by clicking the map, targeting exits **without** forcing controller focus back onto the HUD.
- The virtual pointer position is clamped to the map bounds with a 14-pixel inset. A 0.22 radial stick dead zone and 50-ms per-frame timestep cap reduce drift and long-frame jumps.
- Right-stick motion moves at up to 620 UI pixels/s and D-pad fine motion at 145 UI pixels/s.

MapView's normal hit-testing and per-mode OnRoadClicked/OnLocationClicked handlers still gate which actions are legal; a green target ring merely means a map object was found, **not** that it can be purchased.

### Edit Mode tests

- `GamepadMapCursorStateTests`: initial centering, reset, movement directions, frame cap, dead zone, diagonal normalization, edge/small-screen clamping, resolution changes and local-coordinate rect bounds.
- `GamepadMapTargetPolicyTests`: valid human turn, bot turns, overlays, modals, handoffs and busy animation.
- `UiGamepadFocusTests`: B consumption while targeting, priority of modal focus scope, hidden/disabled/detached modal rejection.
- `GamepadHudIconTests`: native HUD and minimap icon buttons remain focusable.
- All previously merged gamepad/card/UI tests must continue to pass.

## Manual acceptance on a real controller

Test in Unity 6000.6.8f1 and a standalone build, including **Steam Deck viewport 1280 × 800** and the two supported UI languages.

1. Start a normal classic human turn with the gamepad. The game map displays the R3 hint; do not show it during a bot turn or blocking overlay.
2. Press R3, use right stick to aim at a road, then A. Verify the same payment/upgrade dialog as mouse (no duplicate action). Close the dialog and return to targeting.
3. Aim at empty terrain and press A; nothing should be purchased or selected. Try D-pad fine nudges on thin roads.
4. Press B in targeting mode. The reticle disappears, focus returns to a HUD control and no other modal or menu is dismissed. Press R3 again to enter/exit.
5. Use A outside targeting to operate a normal HUD button or a resource/contract card; confirm the map is **not** clicked at the same time.
6. Switch to physical mouse, click/drag the map, rotate camera and zoom with the wheel; verify the virtual reticle has exited and focus isn't stolen back.
7. In Logistics Magnate, aim at a station icon overlapping a road, press A, verify **station action has priority**; aim separately at a road to invoke its usual phase-dependent action.
8. Trigger a turn handoff, game over, Magnate popup, settings modal and a bot phase: map targeting must be blocked or dismissed.
9. Rotate/zoom the camera while targeting, then aim again; confirm the reticle and hover correspond to the new projected road position.
10. Resize the window while targeting; confirm the target remains inside the visible map area and labels don't steal pointer input.
11. Disconnect the controller mid-targeting; mouse/keyboard must remain operational.
12. Navigate back into a payment or handoff dialog with the gamepad and verify focus is kept within the visible modal, not on underlying HUD buttons.

## Known follow-ups

This is **a functional foundation, not Steam Deck certification**. A physical controller test must verify Input System UI event dispatch and event ordering in Unity builds. Remaining Stage 2 tasks include complete controller navigation of every custom modal and long scrolling view, text entry/on-screen keyboard, contextual glyphs/tooltips, and broader Steam/achievement/undo/music work.

No user-initiated CI monitoring is enabled: the user reports CI completion manually.
