# Classic / campaign save recovery

## Behavior
- GameView no longer deletes a save on failed restoration. It displays a RU/EN error and leaves the file available for recovery.
- The existing JSON wire format remains unchanged. Bounded strict UTF-8 read/write, maximum 16 MiB, minimum player-array shape checks, and campaign owner/mission filtering protect the normal save reader.
- Before writing a new snapshot, retain the preceding structurally readable supported-version scope-matching snapshot as a sibling `.bak` via the existing atomic file primitive. Invalid, oversized or future-version primary snapshots do not rotate into the backup. An explicit new game/write may still replace the old primary; back up precious files before starting a different game. A backup is not guaranteed to be playable until validated.
- Backup writes and primary writes are individually atomic, not a two-file transaction. Unexpected IO/permission errors abort the write; SaveSystem logs the error. Explicit discard and completed-game cleanup remove both primary and backup, but do not erase recovery archives. No automatic fallback to a potentially older or different match.

## Developer recovery
Outside Play Mode, use **City Highways > Recover Classic Save From Backup** in Unity. This menu only targets the standard Classic/co-op slot under Application.persistentDataPath. It requires confirmation. It reads the backup, validates its scope and loads the game model with the current map. If successful it copies original primary bytes to a unique `.before-restore-<guid>` file, then atomically replaces the primary with the backup. The backup remains intact. Restore may lose recent turns; inspect the archive before deleting it. If validation or archival fails, the original is not replaced. No profile rewards, online records or replay publication are invoked.

Campaign checkpoints receive their own scope-matching backup; the Editor menu does not automatically restore them. For manual recovery, close the game, copy the checkpoint and backup to a separate directory first, then replace the checkpoint with its .bak only for the same campaign owner and mission. Do not touch progress.json. Runtime campaign eligibility still uses CampaignStorage.CanEnter.

## Tests and limitations
Eleven isolated temp-directory NUnit cases cover first/next writes, rotation, corrupt/empty/future primary, oversized read/write, explicit discard, normal/campaign scope, owner mismatch, explicit restore/archive and unsupported backup rejection. No player files used. Actual disk failure, OS-specific filesystem behavior, manual Editor recovery, old binary golden fixtures, historical rule identity, Magnate persistence, runtime recovery UI and measured save overhead remain open. No local Unity execution.
