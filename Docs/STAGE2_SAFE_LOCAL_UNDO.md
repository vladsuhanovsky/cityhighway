# Stage 2 — Safe local undo (initial playable slice)

## What this adds

The Classic/co-op HUD now has a localized **Undo action** button. Undo is
implemented by deterministic replay from the original seed and original
command history with the last command omitted, **not** by manually reversing
money, cards, road ownership or the random-number generator.

Before attempting to undo, the full available history must be replayed
and checked against the **current live snapshot**. This protects an older or
corrupt save that fell back to snapshot loading despite retaining a partial
command history. The expensive validation only runs when Undo is requested,
not on every frame or HUD refresh.

The current live `Game` is untouched while a replacement is built. If
replay fails, the original remains authoritative. The view swaps the game
only after replay succeeds, unhooks the old listeners, cancels pending
presentation tweens, rebuilds card/HUD/road and 3D projections, reconstructs
the journal from replay events, and saves the restored game.

The action journal may not reproduce pre-game introductory messages; it
contains only the reconstructed gameplay events and the explicit undo
notification. Local stats and profile rewards cannot be undone.

## Strict safety policy

**Undo is deliberately a small feature, not a general-purpose timeline.**

Allowed only in local Classic/co-op with **all-human players**, no tutorial,
no time limit, no hidden-hand handoff, a full command history, no blocking
modal/drawer/overlay, and no active controller map targeting or blocking
animation. It is disabled for online games, bots, tutorials, timed games,
legacy saves with missing replay history and already-finished games.

At this stage only these last command types may be undone:
- draw one face-up card
- draw one card from the deck
- build a road
- build a free road
- upgrade a road

No undo for initial contract/character choices, contract selection, auctions,
bids, trade requests/responses, bribery, accusations, influence operations,
luck draws/choices or end-of-game rewards. The UI never creates a new undo
command on the network; the server protocol and saves remain unchanged.

The button reverses **the last allowed command globally**, so in an
all-human shared-screen game both players must agree before undoing the
previous player's final draw or construction. A player can undo the second
draw of a turn to restore the first-draw state.

## Automated tests

`GameUndoTests` covers exact hand/deck/history restoration, deterministic
redraw after undo, second-draw turn ownership, rejection across all five
session restrictions, rejected setup/secret commands, allowed command
catalog, legacy snapshot-only games, failure immutability and journal
reconstruction. The pre-existing `CommandsTests` verify deterministic
full-game replay and saved-game round trips.

## Manual acceptance (Unity + Steam Deck)

1. Start a local all-human Classic game with hidden-hands off and turn
   timer off. Choose initial contracts; Undo should be disabled.
2. Draw from the deck. Undo should become available; activate it and
   verify the hand, deck and current phase are restored exactly.
3. Draw twice, undo the second draw, and verify the earlier player's
   unfinished turn is restored. Redraw and check the same card is dealt.
4. Build a road or upgrade one, undo it, and confirm road ownership,
   resources, points, journal, minimap, 3D road visuals and the save
   reflect the restored state. Exit and resume to verify persistence.
5. Try with bots, online lobby, tutorial, timed game or hidden-hand
   handoff: Undo must remain disabled.
6. Open a blocking modal, controller map cursor or in-progress
   animation: it must not trigger an undo.
7. Switch RU↔EN, gamepad A/B navigation and 1280×800 Steam Deck UI.
8. Finish a match. Undo must remain disabled; credited achievements,
   statistics and replay files must not be rolled back.
9. Load an old save with no command history: Undo must remain disabled.

**CI boundary:** Edit Mode tests prove the replay state invariants and
compile the UI integration. Human inspection is still required for
recreated 3D scene correctness, audio/transition behavior, focus return,
the exact top-bar layout and save-resume UX on a real build.

No CI polling or automated PR merge is enabled. The developer reports
completion explicitly.

## Undo interaction improvements

- The HUD button is now compact, **Undo / Отменить**, leaving room for
  the journal and menu buttons at 1280×800. Hovering reveals which
  reversible action would be restored (deck or face-up card, construction,
  free construction, road upgrade) and the shortcut.
- **Ctrl+Z** works on Windows, Linux and Steam Deck desktop layout;
  **Command+Z** works on macOS. Shift-modified shortcuts, such as
  Ctrl+Shift+Z, are not intercepted. The system/Windows key is **not**
  treated as Command outside macOS.
- No keyboard undo when a `TextField` is focused: text entry keeps its
  normal undo behavior. The shortcut uses the same availability guard
  as the HUD button, including blocked dialogs, turn timers, bots,
  network sessions and active map targeting.
- A shared-screen last command that belongs to the **previous player**
  must be confirmed with a second click or shortcut within **six
  seconds**. The first press displays a localized warning naming that
  player; it does not change the game. A new action, a new game or the
  expiry clears pending confirmation.
- A successful undo plays a short original descending sound effect.
  No third-party audio clip or asset is introduced.

### Focused manual checks

1. With one local human action recorded, verify Undo happens in one click,
   Ctrl+Z, or controller Submit on the enabled button.
2. With two human seats, draw the second card so the turn advances.
   Press Undo: the confirmation warning and button text should appear.
   Press again within six seconds to restore the previous turn. After
   six seconds, a new first press should ask again.
3. Open a text-entry field and press Ctrl+Z: typing should be handled by
   that field, not by the game.
4. With a modal, map cursor, blocking animation or disabled session,
   shortcuts should not rewind a command.
5. Switch RU↔EN, inspect the button tooltip and player-owner warning, and
   verify the new sound effect is subtle relative to the master volume.

## Redo of a locally undone action (Stage 2 extension)

A second compact **Redo / Повторить** button now appears next to Undo
in the game HUD. It restores the last undone deck draw, face-up card
draw, road construction, free construction or road upgrade.

### Safety and state consistency

- The game keeps up to **eight** previously undone game states **in
  memory only**. Each successful Undo pushes the former state; each
  Redo consumes exactly one previously undone step in reverse order.
- Redo performs a **new deterministic replay** of the exact command
  history, not a reattachment to an old mutable Game object. The
  recorded commands must be identical in **kind and all payload fields**
  through the current prefix. The full current and prior game states
  must both match their own replay results; the reconstructed result
  must match the target's saved state before it can become live.
- Any new game-rule action after Undo invalidates the whole redo
  timeline. Starting, loading, resynchronizing or leaving a session
  also clears the in-memory history.
- Redo is disabled for online games, bots, tutorials, timed games,
  hidden-hand handoff, incomplete replay history, game-over states
  and blocked UI/controller map target interactions, just like Undo.
- UI rebuilding, event unsubscription, journal reconstruction and
  autosaving are shared with Undo. No game-end achievement credits,
  statistics, or Steam progress can be replayed by this feature.
- Redo history does not survive an application restart. A saved
  game reflects the **currently visible** state; once reloaded the
  previous Undo/Redo stack is intentionally absent.

### Input and smoke-test checklist

1. In a local all-human Classic game with no timer and visible hands,
   draw one card, Undo and Redo: verify identical card, deck,
   current player, action log and save file.
2. Draw twice to end a turn. Undo the second card (the existing
   previous-player confirmation is required), then Redo: the turn
   should advance exactly as before.
3. Undo twice and Redo twice; verify exact original resources, roads
   and turn ownership. A ninth Undo should drop the oldest redo slot,
   not grow the in-memory stack indefinitely.
4. Undo once, perform a **different** valid action and verify the Redo
   button is disabled (the new action branched the timeline).
5. Keyboard: **Ctrl+Y** or **Ctrl+Shift+Z** on Windows/Linux/Steam Deck;
   **Command+Shift+Z** on macOS. Preserve text-field editing and
   shortcuts while a modal or map cursor is active.
6. Check gamepad HUD focus, RU↔EN language switch, 1280×800 Steam Deck
   top bar, audio feedback and an exit/resume cycle.
7. Verify no Redo on a finished match, after a network resync or after
   replacing a session with a new game.

New Edit Mode tests validate the deterministic timeline, safe refusal
of diverged/tampered histories, deep command payload comparison,
cross-session denial, repeated undo/redo and keyboard gates.
Manual Unity visual/gamepad testing remains required.
