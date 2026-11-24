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
