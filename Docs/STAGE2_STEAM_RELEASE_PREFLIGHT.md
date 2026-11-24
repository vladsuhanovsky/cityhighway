# Stage 2 — Steam release preflight

This static check does **not** run Unity or Steamworks.NET, and does not prove the live Steam overlay or StoreStats integration works.

## Commands

From the repository root:

```bash
python3 scripts/steam_release_preflight.py
python3 -m unittest discover -s scripts -p 'test_*.py'
python3 scripts/steam_release_preflight.py --release --app-id YOUR_REAL_APP_ID
```

The default catalog check compares the canonical `Achievements.All` achievement IDs with `Docs/STEAM_ACHIEVEMENT_API_IDS.md`, rejecting duplicates, missing, and unknown IDs. It deliberately excludes `DailyTasks`.

The stricter release check also requires a valid real non-480 AppID, an installed `com.rlabrecque.steamworks.net` dependency in `Packages/manifest.json`, and a matching `steam_appid.txt` when that development file exists.

**Expected on the current default branch:** the catalog-only check passes, but `--release` fails: the project still references the shared test AppID `480` and does not install Steamworks.NET. Do not bypass these safeguards to label a release as Steam-ready.

## Manual signoff beyond this script

- Install Steamworks.NET for the Steam build, compile conditional `CityHighways.Steam` assembly and verify that the platform initializes under the actual game AppID.
- Publish all achievement API names in Steamworks Partner; test a first unlock and overlay notification.
- Relaunch the game and verify persistence, repeat unlock idempotence, reconnect, and previously earned local/cloud unlocks.
- Verify that a different SteamID, offline profile, and hotseat local player cannot unlock the signed-in account's Steam achievements.
- Check controller A/B, R3 map cursor, Steam+X text input and 1280x800 layout on actual Steam Deck.
- Check non-Steam builds continue to function without Steamworks.NET.

Do not automatically merge a release candidate based on static checks alone.

## Achievement reconciliation / recovery (Stage 2)

The `SteamAchievementSync` bridge now uses a separate,
Steamworks-independent `SteamAchievementDispatch` policy:

- Eligible achievements are **always** the canonical, case-sensitive IDs
  already unlocked by the **active profile whose steamId exactly matches
  the currently signed-in SteamId**. A blank, missing, or mismatched
  account is not eligible.
- IDs are deduplicated and sorted before queueing. No unrecognized or
  arbitrary client-provided IDs can reach the optional publisher.
- A new publisher (Steam initialization or reinitialization), active
  profile object, Steam account or unlocked-ID set triggers immediate
  full reconciliation at the next normal three-second sync tick.
- Unchanged achievements are queued again after **120 seconds**.
  Because the optional Steam platform keeps an idempotent set and checks
  `GetAchievement` before `SetAchievement`, periodic dispatch can
  recover from transient disconnects without re-awarding an already
  unlocked achievement.
- Failed `QueueUnlockedAchievements` calls are **not** recorded as
  successful dispatches. The outer bridge catches Steam errors and
  retries after a 30-second cooldown without interrupting gameplay.
- If Steam is unavailable or a profile is no longer authorized, previous
  deduplication state is invalidated. Editor play-mode teardown also
  clears the account and publisher cache.

### Manual Steam-only QA required

With a **real Steam AppID**, Steamworks.NET installed and all IDs
published in Steamworks Partner, confirm the following:

1. Earn a new in-game achievement: after the game writes the local
   profile, verify Steam overlay unlock and Steamworks stats storage.
2. Kill Steam/reconnect or temporarily break stats storage. Local
   achievements must remain in the profile; after reconnection and
   periodic synchronization, queued IDs should eventually persist.
3. Switch active profiles while Steam remains signed into the same
   account. Only the exact matching profile's unlocked IDs may publish.
4. Launch as a **different Steam account**. No unlocks from the previous
   account's profile may be published to the current account.
5. Launch with a missing Steam client or ordinary non-Steam Unity build:
   no null exceptions and no gameplay interruption.
6. Simulate an unpublished API name: verify the existing SteamPlatform
   warning and delayed retry behavior, not a tight StoreStats loop.

**Scope:** the new Unity Edit Mode tests validate dispatch policy,
account filtering and retry scheduling with a fake publisher. They do
not invoke SteamAPI, verify remote persistence, install Steamworks.NET
or make test AppID 480 production-ready. The release preflight gate
above remains mandatory.
