import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const sync = source("Assets/CityHighways/Presentation/CloudProfiles.cs");
const schedule = source("Assets/CityHighways/Presentation/CloudProfileSyncSchedule.cs");
const profiles = source("Assets/CityHighways/Presentation/Profiles.cs");
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
  const reset = sync.slice(sync.indexOf("static void EnsureSteamAccount()"), sync.indexOf("public static string ServerId"));
  const begin = sync.slice(sync.indexOf("public static void SyncNow()"), sync.indexOf("static void Send(PlayerProfile"));
  const send = sync.slice(sync.indexOf("static void Send(PlayerProfile"), sync.indexOf("static bool Apply("));

  assert.match(reset, /unchecked \{ _accountEpoch\+\+; \}[\s\S]*?Schedule\.Reset\(Now\);[\s\S]*?ServerId = "";[\s\S]*?Messages\.Clear\(\);/);
  assert.match(begin, /EnsureSteamAccount\(\);[\s\S]*?if \(!Schedule\.TryBegin\(Now, out int token\)\) return;[\s\S]*?int epoch = _accountEpoch;/);
  assert.match(sync, /static bool Current\(int epoch, string owner, int token\) =>\s*_accountEpoch == epoch && CurrentSteamId\(\) == owner && Schedule\.IsCurrent\(token\);/);
  assert.match(begin, /steam\.RequestWebApiTicket\(TicketIdentity, ticket =>\s*\{\s*EnsureSteamAccount\(\);\s*if \(!Current\(epoch, owner, token\) \|\| !Schedule\.WaitingForTicket\) return;/);
  assert.match(begin, /if \(!Schedule\.BeginHttp\(token\)\) return;\s*try \{ Send\(profile, owner, ticket, epoch, token\); \}/);
  assert.match(send, /operation\.completed \+= _ =>\s*\{\s*EnsureSteamAccount\(\);\s*if \(!Current\(epoch, steamId, token\)\)\s*\{\s*web\.Dispose\(\);\s*return;\s*\}/);
  assert.match(schedule, /public void Reset\(float now\)\s*\{\s*Advance\(\);/);
  assert.match(schedule, /public bool TryBegin\(float now, out int token\)[\s\S]*?if \(Busy \|\| now < _retryAt\) return false;\s*Advance\(\);/);
  assert.match(schedule, /public bool IsCurrent\(int token\) => Busy && token != 0 && token == _token;/);
});

test("profile sync response must match the authenticated Steam identity before applying actions", () => {
  const send = sync.slice(sync.indexOf("static void Send(PlayerProfile"), sync.indexOf("static bool Apply("));
  assert.match(send, /if \(!string\.Equals\(response\.id, "steam:" \+ steamId, StringComparison\.Ordinal\) \|\|\s*!string\.Equals\(response\.platformUserId, steamId, StringComparison\.Ordinal\)\)\s*\{\s*Fail\(token, "Steam profile sync identity mismatch"\);\s*return;\s*\}/);
  assert.match(send, /if \(!ReferenceEquals\(Target, profile\)\)\s*\{[\s\S]*?return;\s*\}\s*if \(!Apply\(profile, response\)\)\s*\{\s*Fail\(token, "Local profile could not be saved\."\);\s*return;\s*\}[\s\S]*?ServerId = response\.id;\s*Schedule\.Complete\(token, Now\);/);
  assert.ok(send.indexOf("Steam profile sync identity mismatch") < send.indexOf("Apply(profile, response)"));
  assert.ok(send.indexOf("Apply(profile, response)") < send.indexOf("ServerId = response.id"));
});

test("cloud profile upload and receipt acknowledgement require successful local persistence", () => {
  const begin = sync.slice(sync.indexOf("public static void SyncNow()"), sync.indexOf("static void Send(PlayerProfile"));
  const apply = sync.slice(sync.indexOf("static bool Apply("), sync.indexOf("static void ApplyAction("));
  assert.match(begin, /if \(!Profiles\.TrySaveLocal\(\)\)\s*\{\s*Fail\(token, "Local profile could not be saved\."\);\s*return;\s*\}\s*if \(!Schedule\.BeginHttp\(token\)\) return;/);
  assert.match(apply, /if \(response\.actions == null \|\| response\.actions\.Any\(a => a == null \|\| a\.id <= 0\)\)\s*throw new InvalidOperationException/);
  assert.match(apply, /bool receiptsChanged = profile\.appliedActions\.RemoveAll/);
  assert.match(apply, /if \(changed \|\| appliedNew \|\| receiptsChanged\)[\s\S]*?if \(!Profiles\.TrySaveLocal\(\)\) return false;/);
  assert.match(profiles, /public static bool TrySaveLocal\(\)[\s\S]*?ReplayStorageIO\.WriteAtomic\(FilePath, JsonUtility\.ToJson\(Book, true\)\);\s*return true;[\s\S]*?catch \(Exception e\)[\s\S]*?return false;/);
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
  assert.match(ghosts, /_ = SocialCloud\.CurrentSteamAccount/);
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

test("signed-out menus cannot show a cached SteamId's pending report count", () => {
  assert.match(delivery, /public static int PendingReportsForCurrentAccount[\s\S]*?EnsureSocialAccount\(\);[\s\S]*?string owner = _accountScope\.Owner/);
});

test("direct social club state access always refreshes the current account scope", () => {
  assert.match(social, /public static ClubState Club[\s\S]*?get \{ EnsureSocialAccount\(\); return _club; \}/);
});
