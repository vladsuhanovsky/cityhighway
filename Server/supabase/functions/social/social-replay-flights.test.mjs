import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const gate = source("Assets/CityHighways/Presentation/SocialReplayFlights.cs");
const cloud = source("Assets/CityHighways/Presentation/SocialCloud.cs");
const match = source("Assets/CityHighways/Presentation/SocialReplayMatch.cs");
const ghosts = source("Assets/CityHighways/Presentation/Ghosts.cs");
const tests = source("Assets/CityHighways/Tests/SocialReplayFlightsTests.cs");

test("multiple replay subscribers share a pending request by record ID", () => {
  assert.match(gate, /readonly Dictionary<long, Flight> _flights/);
  assert.match(gate, /if \(_flights\.TryGetValue\(record\.id, out var existing\)\)/);
  assert.match(gate, /existing\.Subscribers\.Add/);
  assert.match(gate, /return false;/);
  assert.match(gate, /_flights\.Add\(record\.id, flight\)/);
});

test("new replay requests are sent once for each in-flight record", () => {
  const fetch = cloud.slice(cloud.indexOf("public static void FetchReplay("),
    cloud.indexOf("public static void ReportGame("));
  assert.match(fetch, /if \(!_replayFlights\.Begin\(record, done, failed, out int token\)\)/);
  assert.match(fetch, /return;/);
  assert.match(fetch, /Send<ReplayResponse>/);
});

test("replay callbacks use unique generations and do not complete stale requests", () => {
  assert.match(gate, /unchecked \{ _nextToken\+\+; \}/);
  assert.match(gate, /if \(_nextToken == 0\) _nextToken = 1;/);
  assert.match(gate, /flight\.Token != token\) return false;/);
  assert.match(gate, /_flights\.Remove\(id\)/);
  assert.match(tests, /AtoBtoAOldHttpResponseCannotFinishNewRequestWithSameId/);
});

test("all replay flights are cancelled as soon as the account changes", () => {
  const ensure = cloud.slice(cloud.indexOf("static void EnsureSocialAccount()"),
    cloud.indexOf("public static bool Available"));
  assert.match(ensure, /if \(!_accountScope\.Update\(owner\)\) return;/);
  assert.match(ensure, /_replayFlights\.CancelAll/);
  assert.match(gate, /_flights\.Clear\(\)/);
  assert.match(tests, /SwitchingSteamAccountCancelsAllPendingDownloads/);
});

test("a new account cannot revive a stale Steam request even with identical record ID", () => {
  assert.match(gate, /if \(!_flights\.TryGetValue\(id, out var flight\) \|\| flight\.Token != token\) return false;/);
  assert.match(gate, /public bool Complete\(long id, int token, ReplayFile replay, string mismatchError\)/);
  assert.match(gate, /public bool Fail\(long id, int token, string error\)/);
  assert.match(tests, /AtoBtoAOldHttpResponseCannotFinishNewRequestWithSameId/);
});

test("subscriber snapshot prevents mutable card from weakening metadata checks", () => {
  assert.match(gate, /static SocialCloud\.RecordInfo Snapshot\(SocialCloud\.RecordInfo record\)/);
  assert.match(gate, /Record = Snapshot\(record\)/);
  assert.match(tests, /RecordMetadataIsCopiedWhenSubscriberJoins/);
});

test("every coalesced viewer still verifies its own clicked score and map", () => {
  assert.match(gate, /SocialReplayMatch\.Matches\(subscriber\.Record, replay\)/);
  assert.match(gate, /NotifySuccess\(subscriber, replay\)/);
  assert.match(gate, /NotifyFailure\(subscriber, mismatchError\)/);
  assert.match(match, /record\.score != replay\.authorScore/);
  assert.match(match, /record\.mapPath, replay\.mapPath/);
});

test("failure from HTTP or a bad replay notifies everyone and permits retry", () => {
  assert.match(cloud, /_replayFlights\.Fail\(recordId, token,/);
  assert.match(cloud, /error => _replayFlights\.Fail\(recordId, token, error\)/);
  assert.match(gate, /foreach \(var subscriber in subscribers\)[\s\S]*?NotifyFailure\(subscriber, error\)/);
  assert.match(tests, /FailedHttpResponseReleasesAllSubscribersAndAllowsRetry/);
});

test("no callback can block subsequent replay subscribers", () => {
  assert.match(gate, /catch \(Exception e\) \{ Debug\.LogWarning/);
  assert.match(tests, /ThrowingFirstSubscriberCannotStarveSecondSubscriber/);
});

test("callback dispatch detaches the old request before another one can begin", () => {
  assert.match(gate, /_flights\.Remove\(id\); \/\/ Detach before callbacks may start another load/);
  assert.match(tests, /CallbackCanStartNewFlightAfterPreviousWasDetached/);
});

test("ghosts still pass full record metadata and enforce their own Steam session", () => {
  assert.match(ghosts, /mapPath = MapPath/);
  assert.match(ghosts, /score = Score/);
  assert.match(ghosts, /won = Won/);
  assert.match(ghosts, /seats = Seats/);
  assert.match(ghosts, /SocialCloud\.BelongsToCurrentSession\(SteamOwner, SteamGeneration\)/);
});

test("cloud response still validates and decodes replay prior to fan-out", () => {
  const fetch = cloud.slice(cloud.indexOf("public static void FetchReplay("),
    cloud.indexOf("public static void ReportGame("));
  assert.match(fetch, /Replays\.UnpackCloud\(response\?\.replay\)/);
  assert.match(fetch, /if \(replay == null\)/);
  assert.match(fetch, /_replayFlights\.Complete\(recordId, token, replay,/);
});
