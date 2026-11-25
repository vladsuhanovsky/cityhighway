# Save/replay compatibility audit

Observed in main aa0da5f and the Workshop draft:
- GameSaveData current version 14, oldest supported 2. Game.Load tries replay from version 5 then falls back to FromSave. Legacy snapshot branches include versions 3/7/10/11/13. ReplayFile version 1; local replay requires the current version, while cloud additionally requires explicit start.config and bounded input.
- SaveSystem writes via ReplayStorageIO.WriteAtomic. Campaign uses a profile-scoped checkpoint and versioned progress.
- Save and replay identify maps by Resources path; no immutable map content hash. Do not replace existing IDs or modify historical rule/seed semantics before designing compatibility.
- Found and fixed a concrete bug: Game.Load could replay a future-version save before FromSave's version check. Moved validation ahead of replay.
- GameView deletes the original file on load failure: this is a separate high-priority recovery risk, particularly for a future build or missing map. Replace automatic deletion with explicit discard/recovery before shipping.
- SameStateAs compares selected fields only (phase, player, turn, decks and parts of player state), not every field. It is not an exact state checksum. A stronger complete state comparison requires historical fixtures and deliberate migration design.
- Added current-version serialized round trips for three shipped maps and rejection tests for unsupported versions. These are synthetic current fixtures, not actual historical v2–v13 saves.
- Remaining: archive actual saves/replays per released version; compare all authoritative state and RNG continuation, explicit schema validation and map revision identity; bounded save reads, backup rotation, Magnate persistence audit. No format/version bump or network change.

## Batch 2 follow-up

The former destructive GameView load failure path now preserves the original file. Classic and campaign snapshot reads/writes are bounded, and a structurally readable scope-matching previous supported snapshot is retained as .bak. Explicit Editor recovery for the normal Classic slot validates with Game.Load, archives original bytes, then atomically restores the backup. See SAVE_RECOVERY.md. This does not certify exact historical simulation or support automatic rollback; historical fixtures remain required.
