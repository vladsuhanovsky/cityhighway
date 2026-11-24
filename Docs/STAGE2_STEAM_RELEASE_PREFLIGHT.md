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

## Account-bound Steam achievement upload queue (Stage 2 hardening)

The optional Steamworks.NET publisher now uses a Steamworks-independent
`SteamAchievementAccountQueue` in the `CityHighways.Net` assembly.
This explicitly binds all pending, staged and unknown/unpublished
achievement IDs to one **currently signed-in local SteamID**.

### What the Steam build does

- Reads `SteamUser.GetSteamID()` when initializing, queueing,
  flushing, receiving stats callbacks and processing the normal
  `Poll()` loop. On an observed account change or Steam logout,
  pending/staged achievements, cached API-name warnings, StoreStats
  pending state, and the old user's avatar are discarded.
- A new SteamID requires a **fresh successful RequestCurrentStats**
  before `SetAchievement` or `StoreStats` can resume.
  Failed stats requests still observe the 15-second retry window.
- The profile bridge now prefers the optional
  `IAccountBoundSteamAchievements.TryQueueUnlockedAchievementsForAccount`
  contract. It passes the exact owner SteamID alongside the canonical,
  already-unlocked achievement IDs. The native publisher checks that
  the source SteamID **still matches** the local Steam account before
  accepting any IDs. This closes the profile-read-to-queue identity
  race in the original generic publisher.
- Account rejection returns **false** and does not update dispatch
  deduplication state. Future reconciliation can retry safely.
  Legacy `ISteamAchievements` publishers remain compatible with
  the bridge, but the native Steam implementation uses the stronger
  interface automatically.
- Existing idempotent `GetAchievement`/`SetAchievement` checks
  remain in place. Pending IDs are removed on remote-achieved lookup
  or after a matching account's successful StoreStats confirmation.
  Failed StoreStats keeps staged work for a 30-second retry, while
  unknown API names use the previous 60-second backoff.
- A StoreStats callback must find an active outstanding store and
  the current bound account still matching that store's account.
  **Steam's UserStatsStored callback does not contain a distinct
  per-request correlation token**; rapid account switches with
  overlapping outstanding native callbacks require real Steam QA.
  Editor tests can verify our account guards and queue state model,
  not the order in which Steam delivers asynchronous callbacks.

### Additional manual Steamworks QA

1. On a real registered AppID, unlock an achievement for account A.
   Ensure the Steam overlay reports it and it persists after restart.
2. Disconnect and reconnect Steam while a store is pending. Ensure
   local achievements survive and the account's retries eventually
   persist without duplicate UI rewards.
3. While IDs from A are pending/staged, switch to account B before
   callbacks are delivered. Ensure no A IDs are sent as B, no cached
   A avatar appears, and a fresh B stats request is required.
4. Switch A→B→A quickly with delayed StoreStats responses. Inspect
   the actual Steam overlay and remote achievements; asynchronous
   native callback ordering is outside Unity Edit Mode coverage.
5. With the wrong/offline profile active, verify that the native
   publisher rejects an account-mismatched source ID even if the
   account changes after the bridge has read the profile.
6. Repeat the check with non-Steam builds. The optional Steam
   assembly must remain absent and local achievements unaffected.

**Release status is unchanged:** neither a real AppID nor the
Steamworks.NET package is installed in the default project setup.
The `--release` preflight gate must continue to fail until actual
Steam build dependencies and partner achievement registration exist.

## Strict static release gate (follow-up)

The regular `python3 scripts/steam_release_preflight.py` command
intentionally checks **only** the in-game achievement catalog against
the documented Steamworks Partner API-name table. This remains usable
on non-Steam developer machines without Steamworks.NET.

The stricter `--release --app-id <REAL_APP_ID>` mode now rejects
additional misconfiguration before any build is described as Steam-ready:

- API names must be lowercase ASCII identifiers (`a-z`, `0-9`,
  underscores after the initial letter). Malformed IDs in the
  `Achievements.All` declarations **or** Partner table cannot be
  silently omitted from comparison. Duplicate/missing/extra names
  remain errors; daily tasks remain excluded.
- `--app-id` must be an ASCII, canonical, positive 32-bit decimal
  value (1–4,294,967,295) other than shared testing ID `480`.
  Leading zeroes, whitespace, negative/Unicode digits and overflow
  are rejected. If `steam_appid.txt` exists, it must match the
  supplied value; it may be absent from a packaged build.
- `Packages/manifest.json` must directly declare a nonempty
  Steamworks.NET package reference **and** `Packages/packages-lock.json`
  must resolve it as a depth-0 package with a nonempty version.
  A manifest-only entry is not enough.
- `Assets/CityHighways/Steam/CityHighways.Steam.asmdef` must reference
  both the Steamworks.NET package and CityHighways.Net, require
  `STEAMWORKS_NET`, and map that define to the Steamworks.NET package's
  Unity version define. `SteamPlatform.cs` must remain guarded by
  `#if STEAMWORKS_NET`, so non-Steam builds do not accidentally
  require Steamworks.NET.
- CI's **Steam achievement catalog preflight** now runs on PRs and
  main-branch pushes that change these inputs. It includes regression
  tests and explicitly verifies that test AppID 480 **fails** release
  mode. This is a negative control, not a production-release test.

### Release operator checklist

1. Create the game's real AppID and publish all canonical achievement
   API names and localized display metadata in Steamworks Partner.
2. Add the supported Steamworks.NET package to Unity Package Manager.
   Commit both `Packages/manifest.json` and a newly resolved
   `Packages/packages-lock.json`, and compile the optional
   `CityHighways.Steam` assembly with the real dependency installed.
3. Configure the correct AppID. Remove or replace the development
   `steam_appid.txt` used for test AppID 480.
4. Run the normal catalog check, unit tests, and then
   `python3 scripts/steam_release_preflight.py --release --app-id REAL_ID`.
5. Test a Steam build and a separate non-Steam build. Confirm actual
   Steam callbacks, account switching, achievement persistence, overlay
   and Steam Deck/gamepad behavior on real clients.

**Passing this static gate never means the Steam SDK was compiled,
the Partner backend was published, or a Steam release was certified.**
Those remain explicit manual requirements.
