import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const social = source("Assets/CityHighways/Presentation/SocialCloud.cs");
const gate = source("Assets/CityHighways/Presentation/SocialRequestGate.cs");
const menu = source("Assets/CityHighways/Presentation/MenuClubPage.cs");

test("newest request gate has a nonzero token and rejects stale callbacks", () => {
  assert.match(gate, /public int Begin\(\)/);
  assert.match(gate, /if \(_revision == 0\) _revision = 1/);
  assert.match(gate, /revision != 0 && revision == _revision/);
  assert.match(gate, /public void Invalidate\(\) => Begin\(\)/);
});

test("friends and club GETs use independent request generations", () => {
  assert.match(social, /static readonly SocialRequestGate _friendRequests/);
  assert.match(social, /static readonly SocialRequestGate _clubRequests/);
});

test("account switch invalidates both pending GETs and mutation flags", () => {
  assert.match(social, /_friendRequests\.Invalidate\(\);[\s\S]*?_clubRequests\.Invalidate\(\);/);
  assert.match(social, /_clubBusy = false;[\s\S]*?_clubActionBusy = false;/);
});

test("a newer friends refresh wins over an older response or error", () => {
  assert.match(social, /int requestId = _friendRequests\.Begin\(\)/);
  const friends = social.slice(social.indexOf("public static void RefreshFriends("),
    social.indexOf("public static void OpenRecordReplay("));
  assert.equal((friends.match(/if \(!_friendRequests\.IsCurrent\(requestId\)\) return;/g) || []).length, 2);
  assert.match(friends, /_friendsBusy = false;/);
});

test("a zero-friend result invalidates older HTTP refreshes before clearing cache", () => {
  const friends = social.slice(social.indexOf("public static void RefreshFriends("),
    social.indexOf("public static void OpenRecordReplay("));
  assert.match(friends, /int requestId = _friendRequests\.Begin\(\);[\s\S]*?if \(ids\.Length == 0\)[\s\S]*?FriendRecords\.Clear\(\);/);
});

test("malformed friends response never removes the previous valid records", () => {
  const friends = social.slice(social.indexOf("public static void RefreshFriends("),
    social.indexOf("public static void OpenRecordReplay("));
  assert.match(friends, /if \(response\?\.records != null\)[\s\S]*?FriendRecords\.Clear\(\)/);
  assert.match(friends, /response\.records\.Where\(r => r != null\)/);
  assert.match(friends, /keeping cached data/);
});

test("refreshes respect ongoing club mutations", () => {
  assert.match(social, /if \(!_clubBusy && !_clubActionBusy && now - _clubAt > ClubPeriod\) RefreshClub\(\)/);
  const club = social.slice(social.indexOf("public static void RefreshClub("),
    social.indexOf("static void ClubAction("));
  assert.match(club, /if \(_clubActionBusy\)[\s\S]*?return;/);
  assert.match(club, /int requestId = _clubRequests\.Begin\(\);/);
});

test("club GET ignores stale callbacks and retains state on a null parsed response", () => {
  const club = social.slice(social.indexOf("public static void RefreshClub("),
    social.indexOf("static void ClubAction("));
  assert.equal((club.match(/if \(!_clubRequests\.IsCurrent\(requestId\)\) return;/g) || []).length, 2);
  assert.match(club, /if \(state != null\)[\s\S]*?Club = state;/);
  assert.match(club, /retaining last good state/);
});

test("club mutations are serialized and invalidate outstanding GETs before POST", () => {
  const action = social.slice(social.indexOf("static void ClubAction("),
    social.indexOf("public static void CreateClub("));
  assert.match(action, /if \(_clubActionBusy\)[\s\S]*?failed\?\.Invoke/);
  assert.match(action, /int requestId = _clubRequests\.Begin\(\);/);
  assert.match(action, /_clubActionBusy = true;[\s\S]*?Send<ClubState>/);
  assert.match(action, /if \(!_clubRequests\.IsCurrent\(requestId\)\) return;/);
});

test("failed club mutation clears its busy flag and re-fetches instead of applying null state", () => {
  const action = social.slice(social.indexOf("static void ClubAction("),
    social.indexOf("public static void CreateClub("));
  assert.match(action, /if \(state == null\)[\s\S]*?_clubAt = -999f;/);
  assert.match(action, /_clubActionBusy = false;[\s\S]*?_clubAt = -999f;/);
  assert.match(action, /Club = state;/);
});

test("club list rejects missing collection on HTTP 200 and caps valid results", () => {
  const list = social.slice(social.indexOf("public static void ListClubs("),
    social.indexOf("public static string ClubTag"));
  assert.match(list, /if \(response\?\.clubs == null\)/);
  assert.match(list, /failed\?\.Invoke/);
  assert.match(list, /\.Take\(30\)\.ToArray\(\)/);
});

test("club search ignores late or cross-session callbacks and keeps good results on failures", () => {
  const search = menu.slice(menu.indexOf("void Search()"), menu.indexOf("void BuildFriends()"));
  assert.match(search, /int requestId = _searchRevision/);
  assert.match(search, /string owner = SocialCloud\.CurrentSteamAccount/);
  assert.match(search, /int generation = SocialCloud\.CurrentAccountGeneration/);
  assert.equal((search.match(/if \(requestId != _searchRevision \|\|/g) || []).length, 2);
  assert.match(search, /!SocialCloud\.BelongsToCurrentSession\(owner, generation\)/);
  assert.doesNotMatch(search, /_found = new SocialCloud\.ClubBrief\[0\]/);
});

test("club page changes search epoch when account changes", () => {
  assert.match(menu, /_viewSteamGeneration != generation \|\| _viewSteamAccount != owner/);
  assert.match(menu, /unchecked \{ _searchRevision\+\+; \}/);
  assert.match(menu, /_found = null;/);
});
