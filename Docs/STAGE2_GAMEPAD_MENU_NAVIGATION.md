# Stage 2: Gamepad and Steam Deck — menu navigation foundation

## Goal

Make existing UI Toolkit menu controls usable with a controller without rewriting game rules or changing mouse/keyboard behavior. This PR is **a first slice**, **not** a claim of full Steam Deck compatibility or gamepad control of the 2D/3D playfield.

## Existing project facilities

- The project already uses Unity Input System (`com.unity.inputsystem`), with a `UI` action map in `Assets/Settings/InputSystem_Actions.inputactions`.
- The UI map contains `Navigate`, `Submit`, `Cancel`, plus mouse/pointer actions. `Navigate` includes Gamepad D-pad and sticks; `Submit`/`Cancel` use control usages.
- UI Toolkit's native navigation events remain responsible for moving focus and activating standard Buttons. We do **not** separately synthesize Submit events or add a second competing navigation event loop.

## Changes

1. Custom `MenuButton` tiles on the home screen can receive focus and `NavigationSubmitEvent`. Their existing click action is reused, and focus provides the same highlighted look as pointer hover. A focused badge child cannot also trigger the parent tile.
2. `ProfileCard` can be focused and opened with Submit. The original mouse click remains functional.
3. Regular `Ui.Button` controls display a focus ring, so users can see which action is selected without a cursor.
4. Custom `SwitchToggle` has focus and a Submit toggle; `ValueSlider` has focus, horizontal navigation increments/decrements the value and runs its normal release callback.
5. The menu wakes/repairs its focus only after meaningful Gamepad D-pad, stick or Submit input; small analog stick drift does not steal focus from mouse-only play.
6. On menu-page changes, focus is moved away from controls that belong to a now-hidden page. Gamepad **B** invokes the existing nested `Ui.GoBack` behavior. Keyboard Escape remains supported.
7. Visibility filtering respects hidden ancestors and disabled elements, so a menu modal or hidden page is not selected as the first focused element.

## Test coverage

`UiGamepadFocusTests` checks first visible focusable control, hidden pages, disabled ancestors, unrelated elements, detached/empty trees and dead-zone detection. Tests do not simulate a physical gamepad, UI Toolkit panel focus, navigation repeat delay or Steam Deck hardware.

## Manual acceptance checklist

Test in **Russian and English** at normal and reduced UI scales.

- With only the mouse, verify home tiles, profile, all settings controls, Escape and nested back buttons still work.
- With a controller connected, from the home menu press D-pad/stick: a visible item gains focus. Move between items and press **A** (Submit) to open.
- Visit Settings and navigate through real buttons, switches and sliders. Press **A** on a switch. Press left/right on a focused slider and verify its value is saved; press up/down to leave the slider.
- Press **B** (Back) inside a nested menu page and make sure it returns to the intended previous section (rather than going back twice).
- Enter a submenu, return Home, and confirm focus is not lost on a hidden page. Use the mouse afterward to verify it can take over naturally.
- Confirm a badge's nested button is not activated together with its containing menu tile.
- Check tab/Enter/Escape on keyboard, small stick drift, settings persistence and controls at 1280 × 800 resolution (Steam Deck-like viewport).

## Known gaps before Steam Deck release

- Controller mapping and input dispatch must be tested on real hardware and with the project's runtime UI EventSystem. CI Edit Mode tests cannot establish that Input System navigation reaches the panel as expected.
- Gameplay map selection/camera, overlays, cards, keyboard entry, on-screen keyboard, focus scroll in long lists and multiplayer actions are **not** fully controller-integrated by this PR.
- Steam Deck verification also requires actual build testing, readable text, input glyphs, fullscreen behavior, performance and Steam platform packaging.

Next slices should prioritize gameplay input routing, modal focus/scroll retention, contextual gamepad hints and a Steam Deck build smoke test.
