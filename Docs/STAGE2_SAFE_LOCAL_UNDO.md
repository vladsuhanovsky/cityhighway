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
