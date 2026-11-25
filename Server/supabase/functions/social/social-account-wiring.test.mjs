import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const sync = source("Assets/CityHighways/Presentation/CloudProfiles.cs");
const social = source("Assets/CityHighways/Presentation/SocialCloud.cs");
const delivery = source("Assets/CityHighways/Presentation/SocialCloud.ReportQueue.cs");
const scope = source("Assets/CityHighways/Presentation/SocialAccountScope.cs");
const ghosts = source("Assets/CityHighways/Presentation/Ghosts.cs");
const menu = source("Assets/CityHighways/Presentation/MenuClubPage.cs");

test("Steam account epoch never treats A-B-A as the previous A network session", () => {
  assert.match(scope, /unchecked \{ _generation\+\+; \}/);
  assert.match(scope, /generation == _generation/);
  assert.match(scope, /SocialReportOutbox\.ValidOwner\(steamId\)/);
  assert.match(scope, /string\.Equals\(_owner, owner, StringComparison\.Ordinal\)/);
});

test("cloud profile sync clears stale state and ignores old ticket or HTTP callbacks", () => {
  assert.match(sync, /EnsureSteamAccount\(\);[\s\S]*?if \(_busy\) return;/);
  assert.match(sync, /ServerId = "";/);
  assert.match(sync, /Messages\.Clear\(\);/);
  assert.match(sync, /if \(_accountEpoch != epoch \|\| CurrentSteamId\(\) != owner\) return;/);
  assert.match(sync, /if \(_accountEpoch != epoch \|\| CurrentSteamId\(\) != steamId\)/);
});

test("profile sync response must match the authenticated Steam identity before applying actions", () => {
  assert.match(sync, /string\.Equals\(response\.id, "steam:" \+ steamId, StringComparison\.Ordinal\)/);
  assert.match(sync, /string\.Equals\(response\.platformUserId, steamId, StringComparison\.Ordinal\)/);
  assert.match(sync, /ServerId = response\.id;[\s\S]*?Apply\(profile, response\)/);
});

test("social availability cannot reuse another Steam account's previous profile sync", () => {
  assert.match(social, /CloudProfiles\.ServerId == "steam:" \+ steam\.SteamId/);
  assert.match(social, /CloudProfiles\.ServerId != "steam:" \+ steam\.SteamId/);
});

test("Steam session change clears cached friends, club, news and busy refresh flags", () => {
  assert.match(social, /if \(!_accountScope\.Update\(owner\)\) return;/);
  assert.match(social, /FriendRecords\.Clear\(\);[\s\S]*?Club = null;[\s\S]*?News\.Clear\(\);/);
  assert.match(social, /_friendsBusy = false;[\s\S]*?_clubBusy = false;/);
  assert.match(social, /public static void Tick\(\)[\s\S]*?EnsureSocialAccount\(\);[\s\S]*?if \(!Available\) return;/);
});

test("ticket callbacks and HTTP responses from older sessions cannot mutate social state", () => {
  assert.match(social, /string owner = _accountScope\.Owner;[\s\S]*?int epoch = _accountScope\.Generation/);
  assert.match(social, /if \(!_accountScope\.Matches\(owner, epoch\) \|\|[\s\S]*?steam\.SteamId != owner/);
  assert.match(social, /if \(expectedSteamId != null\)[\s\S]*?reportFailure\?\.Invoke\("Steam account changed", 0\)/);
  assert.match(social, /web\.SendWebRequest\(\)\.completed[\s\S]*?EnsureSocialAccount\(\);[\s\S]*?if \(!_accountScope\.Matches\(owner, epoch\)/);
});

test("queued reports stay owner-bound while social UI switches accounts", () => {
  assert.match(delivery, /static void QueueReport\(Request request, string key\)[\s\S]*?EnsureSocialAccount\(\);/);
  assert.match(delivery, /static void TrySendQueuedReport\(\)[\s\S]*?EnsureSocialAccount\(\);/);
  assert.match(delivery, /ReportOutbox\.Next\(steam\.SteamId, nowTicks\)/);
  assert.match(delivery, /expectedSteamId: entry\.ownerSteamId/);
});

test("friends and club ghosts cannot reuse old Steam session replay caches", () => {
  assert.match(ghosts, /SteamOwner = SocialCloud\.CurrentSteamAccount/);
  assert.match(ghosts, /SteamGeneration = SocialCloud\.CurrentAccountGeneration/);
  assert.match(ghosts, /!SocialCloud\.BelongsToCurrentSession\(SteamOwner, SteamGeneration\)/);
  assert.match(ghosts, /string account = SocialCloud\.CurrentSteamAccount/);
});

test("club menu resets session-bound invite, search and club results", () => {
  assert.match(menu, /string owner = SocialCloud\.CurrentSteamAccount/);
  assert.match(menu, /int generation = SocialCloud\.CurrentAccountGeneration/);
  assert.match(menu, /_viewSteamGeneration != generation \|\| _viewSteamAccount != owner/);
  assert.match(menu, /_found = null;[\s\S]*?_query = "";[\s\S]*?_code = "";/);
});

test("lobby club tag and logo never read stale session caches", () => {
  assert.match(social, /public static string ClubTag[\s\S]*?EnsureSocialAccount\(\);[\s\S]*?return Club != null/);
  assert.match(social, /public static string ClubLogoId[\s\S]*?EnsureSocialAccount\(\);[\s\S]*?return Club != null/);
});
