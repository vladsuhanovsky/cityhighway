# Stage 2: focusable cards, contracts and map-layer controls

## Scope

This PR follows merged controller menu/HUD work in PRs #76 and #77. It extends **UI Toolkit keyboard/gamepad focus** to more custom controls, without changing core gameplay, saves, economy, replay actions or map generation.

### Supported controller interactions

- **Face-up resource cards:** navigate to a card and press **A (Submit)** to use the **same existing** `Game.DrawFaceUpCard` operation as mouse click. Cards that are empty, unavailable outside a human drawing phase, or forbidden when drawing a second machinery card are removed from controller focus traversal. The original click still delegates to existing game validation.
- **Contract cards:** navigate to a pending contract and press A to toggle its selection, or to an owned contract to pin/unpin its route. The focused contract also highlights its route in the map just like hover. Focus uses a decorative outline with `PickingMode.Ignore` that cannot swallow pointer clicks.
- **Contract carousel:** moving focus to a card (including a child of the card) automatically scrolls the horizontal strip to its slot. Pointer drag and horizontal wheel behavior remain as before.
- **Dynamic refresh:** contract cards have stable semantic UI names; if the UI rebuilds after selection, controller focus attempts to return to the corresponding contract rather than jumping to the initial HUD button.
- **Map layer toggles** in both classic/cooperative and Logistics Magnate games receive Submit action and an outline; they still use their original persisted toggle handlers.
- The **minimap north-reset compass**, previously pointer-only, receives Submit action.

The UI already supports controller navigation and submission via the Input System UI action map. This PR does not duplicate those events, map buttons to game-rule methods directly or change the input asset.

## Test coverage

- `GamepadSelectableTests`: custom card focusability, non-picking outline, Submit origin isolation, disabled cards/ancestors, validation.
- `CardCarouselFocusTests`: direct/nested child focus, unrelated controls and slot reordering.
- `UiGamepadFocusTests`: stable named focus lookup with hidden/disabled card rejection.
- Existing Edit Mode tests must remain green in Unity 6000.6.8f1.

## Manual acceptance

Test with mouse, keyboard and controller in **RU and EN** at 1280×800 and a desktop resolution:

1. Open classic/coop gameplay, view face-up cards in the Actions panel. Use D-pad/left stick and **A** to draw a card. Verify that the game allows it only during the normal drawing phase. A second machinery card and an empty slot should not receive controller focus.
2. Select and deselect multiple pending contracts with A. After the selection UI rebuilds, verify that focus remains on the same named card when it still exists; commit with the existing confirmation button.
3. Open owned contracts and pin/unpin their route with A. Moving focus between cards should update route highlighting. Leaving a card restores any pinned route.
4. Navigate through contracts in a narrow window; the horizontal carousel should reveal the focused item without needing drag gestures. Test existing mouse wheel and drag behavior afterward.
5. Open both modes' map-layer panels and toggle layers with A. Verify the correct layer visibility and that preferences persist.
6. Focus the minimap compass and press A; check it resets orientation. Verify a mouse click still works.
7. Validate that Submit never triggers both a parent card and a child control. Check that overlays and hidden cards do not become focus targets.
8. Confirm save/reload and keyboard/mouse handling match before this PR.

## Still outside this PR

- Selecting a **road or station directly on the map** with a gamepad-controlled targeting cursor.
- Full gamepad navigation through every modal, text entry and on-screen keyboard.
- Steam Deck hardware/build support and Steam achievements integration.

These remain independent Stage 2 changes. The above PR expands card/HUD navigation, but is **not** proof that the game is fully playable with a controller. Physical controller verification is necessary in addition to Edit Mode CI.
