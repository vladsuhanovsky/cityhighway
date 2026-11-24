# Stage 2 — Steam achievements bridge for existing in-game unlocks

## Scope

The game already tracks a canonical local achievement catalog (`Achievements.All`), evaluates achievements at the end of classic/cooperative and Logistics Magnate games, and saves unlocked string IDs in `PlayerProfile.achievements`. This PR adds an **optional Steam publication path**; it does not change unlock rules, wallet rewards, matchmaking, cloud profiles, replay format or player save data.

## Architecture

- `ISteamAchievements` lives alongside `ISteamPlatform` in `CityHighways.Net`. The separate capability means no changes to existing platform mocks and no Steamworks dependency in the Core/Net/Presentation assemblies.
- The conditional `CityHighways.Steam` assembly implements the optional publisher via `SteamPlatform` (built **only when com.rlabrecque.steamworks.net is installed**). It registers with `NetPlatforms.SteamAchievements`, requests local Steam user stats, and listens to `UserStatsReceived_t` and `UserStatsStored_t`.
- `SteamAchievementSync.Tick` runs after `CloudProfiles.Tick` in the game's existing Unity update loop, and compares the active account's list every three seconds. It publishes only when the current `SteamId` exactly matches `Profiles.Active.steamId`.
- All emitted IDs must exist in the game's canonical `Achievements.All` catalog. Null, duplicate and unknown IDs are filtered; the list is sorted deterministically.
- If the active account or unlocked ID list changes, it queues the complete list of already-earned achievements. This reconciles achievements from previous play sessions and cloud profile refreshes, rather than only reacting to a transient end-of-game event.
- The Steam adapter queues the requested IDs until current-user stats are ready. It checks `GetAchievement`, calls `SetAchievement` for missing unlocks, then calls `StoreStats` to persist the changes and trigger Steam notifications.
- Pending staged writes remain queued if the `StoreStats` request or callback fails, with delayed retries (30 seconds). An unpublished/unknown Steam API ID cannot trigger a hot loop of `StoreStats`; failed stats initialization is retried at 15-second intervals.
- Offline/local profiles and profiles belonging to other Steam users never publish through the optional bridge.

## Requirements before real Steam activation

**This PR does not configure Steamworks Partner achievements for you.** In the game's real AppID:

1. Configure achievements in Steamworks Partner with API Names exactly matching the IDs listed in `Achievements.All` (`first_win`, `first_contract`, `mg_win`, etc.), or explicitly change the mapping and corresponding tests before shipping. Translated display names and icons belong in Steamworks metadata, *not* API IDs.
2. Publish those Steamworks changes, ensure stats/achievements are enabled for the app, and test using a real Steam account owning the game.
3. Install/enable the `com.rlabrecque.steamworks.net` Unity package and its native libraries in the actual Steam build. The CI's Edit Mode job may omit this optional package; that means **the Steam adapter is not compiled or exercised in that CI configuration**.
4. Replace the repo's development `steam_appid.txt` value **480** with the correct AppID for local testing (or launch through Steam). AppID 480 is a shared Steam test app and must not be treated as the game's production AppID.
5. Test an initial unlock, a cumulative unlock, restart/reconciliation, network disconnect/reconnect and an existing remote-unlocked achievement. Confirm the Steam overlay notification and persistent Steam status after relaunch.
6. For local hot-seat multiplayer, only the current signed-in Steam user's **active Steam-bound profile** is synced. Other people's local profiles must not unlock achievements in the signed-in Steam account.

## Known limitations and verification

- Steamworks.NET is an **optional conditional assembly**; the current repository manifest does not require that package, so Edit Mode tests exercise the cross-platform logic, not the actual native Steam API.
- No server-side verification or anti-cheat guarantee is added. This is client-side achievement publishing based on existing local profile state.
- Steam might reject missing/unpublished achievement names; the adapter keeps them queued and retries later. Logs/Partner configuration must be inspected on real hardware.
- The implementation uses the established request-stats + callback flow for compatibility with Steamworks.NET, even though some newer Steam SDK documentation describes `RequestCurrentStats` as deprecated.

References:
- https://partner.steamgames.com/doc/api/ISteamUserStats (SetAchievement, GetAchievement, StoreStats, stats readiness callbacks)
- https://github.com/rlabrecque/Steamworks.NET-Test/blob/master/Assets/Scripts/SteamUserStatsTest.cs

## Acceptance

- [ ] Edit Mode CI passes for the latest head SHA in Unity 6000.6.8f1.
- [ ] Tests prove exact Steam account matching; local, foreign, unknown and duplicate IDs are not queued.
- [ ] With Steamworks.NET installed, compile the conditional Steam assembly and run a Steam build with a real AppID.
- [ ] Partner metadata uses the exact canonical API IDs and is published.
- [ ] Confirm a new achievement is unlocked and persisted on Steam; restart/offline/reconnect and ensure the missing achievements are reconciled.
- [ ] Verify non-Steam builds still load profiles, award achievements and run games with zero Steam SDK dependency.

No automated CI monitoring or merging is enabled.
