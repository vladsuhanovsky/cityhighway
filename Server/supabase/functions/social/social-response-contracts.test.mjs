import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const transport = source("Assets/CityHighways/Presentation/SocialCloud.cs");
const ack = source("Assets/CityHighways/Presentation/SocialReportAck.cs");
const contracts = source("Assets/CityHighways/Presentation/SocialReadContracts.cs");
const tests = source("Assets/CityHighways/Tests/SocialReadContractsTests.cs");
const backend = source("Server/supabase/functions/social/index.ts");

test("all three social read endpoints validate before JSON defaults reach UI", () => {
  assert.match(transport, /request\.action == "friends" &&[\s\S]*?SocialReadContracts\.ValidateFriends\(text\)/);
  assert.match(transport, /request\.action == "club_list" &&[\s\S]*?SocialReadContracts\.ValidateClubList\(text\)/);
  assert.match(transport, /request\.action == "club" \|\|[\s\S]*?request\.action == "club_open"/);
  assert.match(transport, /SocialReadContracts\.TryParseClub\(text, out var state\)/);
  assert.match(transport, /done\?\.Invoke\(state as T\)/);
});

test("incomplete HTTP 200 responses trigger failed callbacks, not cache mutation", () => {
  assert.match(transport, /Problem = "Invalid friends response";[\s\S]*?failed\?\.Invoke\(Problem\);[\s\S]*?return;/);
  assert.match(transport, /Problem = "Invalid club list response";[\s\S]*?failed\?\.Invoke\(Problem\);[\s\S]*?return;/);
  assert.match(transport, /Problem = "Invalid club state response";[\s\S]*?failed\?\.Invoke\(Problem\);[\s\S]*?return;/);
});

test("contract helper reuses bounded JSON parser with duplicate-key rejection", () => {
  assert.match(contracts, /SocialReportAck\.TryReadRoot\(json, MaxCharacters, out root\)/);
  assert.match(ack, /json\.Length > maxCharacters/);
  assert.match(ack, /const int MaxDepth = 12/);
  assert.match(ack, /fields\.ContainsKey\(key\)/);
  assert.match(ack, /const int MaxMembers = 256/);
});

test("client read contracts can safely handle the server's two-hundred-friend limit", () => {
  assert.match(backend, /\.slice\(0, 400\)/);
  assert.match(backend, /\.limit\(200\)/);
  assert.match(contracts, /Records\(root, "records", 200\)/);
  assert.match(tests, /FriendsAcceptServerMaximumTwoHundredRecords/);
});

test("server no-club response is not interpreted as an empty phantom club", () => {
  assert.match(backend, /if \(!membership\) return \{ club: null \};/);
  assert.match(contracts, /if \(club == null\) state\.club = null;/);
  assert.match(tests, /ClubNullRemainsNullRatherThanUnityPhantomObject/);
});

test("complete club membership data verifies both nested objects and arrays", () => {
  assert.match(backend, /members: \(members \?\? \[\]\)\.map/);
  assert.match(backend, /week: \{ id: week\.id, endsAt: week\.endsAt, \.\.\.goal \}/);
  assert.match(backend, /weeksDone: \(done \?\? \[\]\)\.length/);
  assert.match(backend, /records: \(records \?\? \[\]\)\.map\(recordOut\)/);
  assert.match(contracts, /!ClubInfo\(club\) \|\| !Members\(root\) \|\| !Week\(root\)/);
  assert.match(contracts, /!Integer\(root, "weeksDone", 0, 1000000\) \|\| !Logos\(root\)/);
  assert.match(contracts, /!Records\(root, "records", 40\)/);
});

test("record contract checks identity, numeric types and required metadata", () => {
  assert.match(contracts, /SocialReportAck\.ReadLong\(id, 1, long\.MaxValue, out _\)/);
  assert.match(contracts, /Text\(r, "playerId", 20, required: true\)/);
  assert.match(contracts, /Integer\(r, "score", -100000, 100000\)/);
  assert.match(contracts, /Boolean\(r, "won"\)/);
  assert.match(contracts, /Integer\(r, "seats", 1, 8\)/);
});

test("club search validates complete bounded list rather than silently dropping malformed entries", () => {
  assert.match(backend, /limit\(30\)/);
  assert.match(contracts, /Array\(root, "clubs", 30, out var clubs\)/);
  assert.match(contracts, /Integer\(club, "members", 0, 30\)/);
  assert.match(tests, /ClubListRejectsIncompleteOrDuplicatedEntries/);
});

test("report ACK path and idempotent outbox handling remain intact", () => {
  assert.match(transport, /if \(request\.action == "report"\)/);
  assert.match(transport, /SocialReportAck\.TryParse\(text, out var ack\)/);
  assert.match(transport, /reportFailure\?\.Invoke\(Problem, 0\)/);
});

test("NUnit regressions exercise missing fields, malformed arrays and preserved club null", () => {
  assert.match(tests, /FriendsRejectMissingMalformedOrSpoofedRecordCollections/);
  assert.match(tests, /FriendsRejectExcessiveArrayOrResponseSize/);
  assert.match(tests, /ClubRejectsMissingTypedFields/);
  assert.match(tests, /ClubListRejectsMoreThanThirtyItems/);
});
