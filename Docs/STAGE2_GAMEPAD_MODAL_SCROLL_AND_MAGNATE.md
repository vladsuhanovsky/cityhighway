# Stage 2: Controller focus in dialogs, scrolling views and Magnate cards

This is the next large batch after merged PR #79 (virtual map cursor). It continues the existing Unity Input System / UI Toolkit architecture. No game rules, network messages, saves, prices, AI turns or replay semantics are changed.

## Problems addressed

1. **Long scrolling panels.** UI Toolkit's normal focus navigation could move onto an item outside a ScrollView viewport. Players previously needed the mouse wheel, scrollbar or pointer drag to expose it.
2. **Focus on the modal backdrop.** Some game modals (particularly in Logistics Magnate) focus a backdrop element that listens for Escape, rather than the actionable controls inside it.
3. **Lost HUD focus when closing modals.** Returning from payment, handoff, settings or other blocking windows could jump to the first unrelated HUD element.
4. **Pointer-only Magnate custom widgets.** Bottom-section cards, trucks, cargo rows and cargo filters used ClickEvent but did not implement NavigationSubmit for a keyboard/controller.

## Implementation

- `GamepadScrollFocus.Register` listens for focus entering a descendant of a ScrollView's content container. If it is attached, enabled and visible, `ScrollView.ScrollTo(focused)` reveals the control. All the project's `Ui.StyleScroller` scroll views receive this behavior automatically, including action/contract sheets, journal, goals, rules, player lists and Magnate popup panels. Existing wheel, dragging and vertical/horizontal scrolling are not replaced.
- `UiGamepadFocus.FirstEligible` examines descendants before the root backdrop. `EnsureFocus` replaces backdrop-only focus with an available descendant when possible. This prevents an Escape handler container from monopolizing the gamepad focus when it also contains usable controls.
- `GamepadFocusReturn` remembers the controller's last eligible non-map HUD control while normal gameplay is active. When a blocking modal returns to the ordinary HUD scope, it attempts to restore that control **only if still visible, attached and enabled**. If it was destroyed/hidden, the existing first-eligible focus fallback applies. Both the classic/co-op and Magnate views have their own independent memory.
- When opened by controller, classic/co-op settings/rules and Magnate settings/rules attempt to focus their first actionable control immediately. Mouse-initiated interactions do not run the controller focus-first branch.
- The Magnate bottom-section cards, truck chips, clickable cargo detail rows and cargo-type filter chips use the existing `GamepadSelectable.Attach` (A/Submit), reusing their original click action and its validation. Their named UI elements let focus be restored after the popup rebuilds.
- Existing nested cargo row Pick buttons continue to process their own Submit: the parent GamepadSelectable refuses Submit events originating from children, so controller A cannot both open details and pick a cargo in one action.

## Tests

- `GamepadScrollFocusTests`: nested/flat scroll descendants, detached/unrelated elements, null content and registration on vertical/horizontal scrolls.
- Extended `UiGamepadFocusTests`: focusable backdrops prefer child actions, hidden/disabled or detached HUD controls do not become return targets, the map is never remembered as HUD focus, and detached panels are safe.
- All existing Stage 2 tests from PRs #76–79 must still pass under Unity 6000.6.8f1 Edit Mode CI.

## Real controller acceptance

Run on a connected controller, with mouse and keyboard regressions afterward, using **RU and EN**, and resolutions **1280×800** plus a desktop size:

1. Open classic gameplay and navigate through a long Contracts panel. Moving focus down the list must scroll its content and keep the selected control visible; check mouse wheel and contract carousel still work.
2. Navigate long Settings and Rules screens, including the Magnate rules panel. Verify focused options remain in view without using a mouse.
3. Open and close Payment, Upgrade, handoff, and Settings/Rules panels with gamepad. When the previously focused HUD control still exists, focus should return there rather than always starting from the very first HUD button.
4. Check modal priority: when a modal is open, controller navigation must not operate the HUD behind it, and the first actionable item should receive focus rather than the backdrop.
5. In Magnate, open bottom-panel sections with A, select individual truck chips and navigate cargo rows with A. Focus a nested **Pick** button and press A: it must *not* also open cargo details.
6. Open cargo filters in Magnate. Focus each cargo-kind chip and toggle with A; verify filtering, popup refresh and focus preservation.
7. Use the virtual map cursor (R3 + right stick + A) to open a game action window, complete or close it, then return to the HUD. Ensure no duplicate action or unexpected B Back navigation.
8. Change window size, UI scale and language while a panel is visible; test hidden/disabled targets, focus after refresh, mouse takeover and controller reconnect.

## Known limits

This does not certify real controller event dispatch or Steam Deck compatibility. Some custom screens may still require individual accessibility work, and text fields/on-screen keyboard support are separate tasks. Hardware verification remains essential: Unity CI validates Edit Mode logic, not controller-specific event ordering and scrolling in a rendered build.

No automatic CI monitoring or merging is enabled; testing results are handled only after the user reports completion.
