import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const replays = source("Assets/CityHighways/Presentation/Replays.cs");
const cloud = source("Assets/CityHighways/Presentation/SocialCloud.cs");
const ghosts = source("Assets/CityHighways/Presentation/Ghosts.cs");
const contracts = source("Assets/CityHighways/Presentation/SocialReadContracts.cs");
const match = source("Assets/CityHighways/Presentation/SocialReplayMatch.cs");
const safetyTests = source("Assets/CityHighways/Tests/SocialReplaySafetyTests.cs");
const backend = source("Server/supabase/functions/social/index.ts");
const validator = source("Server/supabase/functions/social/report-validator.mjs");

test("client and server agree on encoded replay and expanded size limits", () => {
  assert.match(replays, /MaxCloudReplayBase64 = 700 \* 1024/);
  assert.match(replays, /MaxCloudReplayJsonBytes = 4 \* 1024 \* 1024/);
  assert.match(replays, /MaxCloudReplayActions = 20000/);
  assert.match(validator, /MAX_REPLAY_BASE64 = 700 \* 1024/);
  assert.match(validator, /MAX_REPLAY_JSON_BYTES = 4 \* 1024 \* 1024/);
  assert.match(validator, /MAX_REPLAY_ACTIONS = 20000/);
});

test("base64 input is rejected before decompression if malformed or oversized", () => {
  assert.match(replays, /static bool ValidReplayBase64\(string encoded\)/);
  assert.match(replays, /encoded\.Length > MaxCloudReplayBase64/);
  assert.match(replays, /encoded\.Length % 4 != 0/);
  assert.match(replays, /if \(!ValidReplayBase64\(packed\)\) return null;/);
});

test("gzip output is incrementally bounded before allocating JSON", () => {
  assert.match(replays, /while \(\(received = gzip\.Read\(buffer, 0, buffer\.Length\)\) > 0\)/);
  assert.match(replays, /if \(output\.Length > MaxCloudReplayJsonBytes - received\)/);
  assert.match(replays, /output\.Write\(buffer, 0, received\)/);
  assert.match(replays, /new UTF8Encoding\(false, true\)\.GetString\(output\.ToArray\(\)\)/);
});

test("legacy unpack entrypoint cannot bypass bounded replay decoding", () => {
  assert.match(replays, /public static ReplayFile Unpack\(string packed\) => UnpackCloud\(packed\);/);
});

test("downloaded replay structure and author game metadata are checked", () => {
  assert.match(replays, /replay\.version != ReplayFile\.CurrentVersion/);
  assert.match(replays, /replay\.players\.Length != replay\.scores\.Length/);
  assert.match(replays, /replay\.players\.Length != replay\.start\.players\.Length/);
  assert.match(replays, /replay\.scores\[replay\.authorSeat\] != replay\.authorScore/);
  assert.match(replays, /replay\.history\.Length > MaxCloudReplayActions/);
});

test("HTTP replay endpoint validates response before constructing Unity response", () => {
  assert.match(cloud, /if \(request\.action == "replay" &&[\s\S]*?!SocialReadContracts\.ValidateReplay\(text\)\)/);
  assert.match(cloud, /Problem = "Invalid social replay response";[\s\S]*?failed\?\.Invoke\(Problem\);/);
  assert.match(contracts, /SocialReportAck\.TryReadRoot\(json, 768 \* 1024, out var root\)/);
  assert.match(contracts, /Text\(root, "replay", Replays\.MaxCloudReplayBase64, required: true\)/);
  assert.match(contracts, /Text\(root, "author", 128\)/);
});

test("client envelope matches current backend response shape", () => {
  assert.match(backend, /return \{ replay: data\.replay, author: \(data as any\)\.players\?\.name \?\? "" \};/);
});

test("cloud replay is checked against selected record before display", () => {
  assert.match(cloud, /var replay = Replays\.UnpackCloud\(response\?\.replay\)/);
  assert.match(cloud, /if \(!SocialReplayMatch\.Matches\(record, replay\)\)/);
  assert.match(cloud, /done\?\.Invoke\(replay\)/);
  assert.match(match, /record\.score != replay\.authorScore/);
  assert.match(match, /record\.won != replay\.won/);
  assert.match(match, /record\.seats > 0 && replay\.players\?\.Length != record\.seats/);
});

test("cloud ghost retains full original record metadata for replay verification", () => {
  assert.match(ghosts, /Seats = record\.seats/);
  assert.match(ghosts, /mapPath = MapPath,/);
  assert.match(ghosts, /mapName = MapName,/);
  assert.match(ghosts, /score = Score,/);
  assert.match(ghosts, /won = Won,/);
  assert.match(ghosts, /seats = Seats/);
});

test("older Steam session cannot publish a downloaded replay to ghost cache", () => {
  assert.match(ghosts, /if \(!SocialCloud\.BelongsToCurrentSession\(SteamOwner, SteamGeneration\)\)/);
  assert.match(ghosts, /Replay = replay;/);
});

test("NUnit cases cover decode bomb, invalid gzip, types and selected-record mismatch", () => {
  assert.match(safetyTests, /RejectsZipBombBeforeAllocatingUnboundedDecodedString/);
  assert.match(safetyTests, /RejectsMalformedGzipAndInvalidUtf8/);
  assert.match(safetyTests, /ReplayMetadataMustMatchChosenRecord/);
  assert.match(safetyTests, /ValidReplayEnvelopeRequiresTypedAuthorAndBase64Field/);
});
