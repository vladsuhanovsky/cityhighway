import test from "node:test";
import assert from "node:assert/strict";
import { gzipSync } from "node:zlib";
import {
  MAX_REPLAY_BASE64,
  MAX_REPLAY_JSON_BYTES,
  MAX_REPLAY_ACTIONS,
  validateRecordFields,
  unpackReplay,
  validateReplayReport,
} from "./report-validator.mjs";

const baseReplay = () => ({
  version: 1,
  id: "20261124-100000-1234",
  date: "2026-11-24 10:00",
  mode: "classic",
  map: "Москва",
  mapPath: "CityHighways/Maps/Moscow",
  author: "Human",
  authorSeat: 0,
  authorScore: 120,
  won: true,
  turns: 5,
  players: ["Human", "Bot"],
  scores: [120, 50],
  humans: [true, false],
  start: { seed: -17 },
  history: [{ type: 1, player: 0 }],
});

const pack = (replay) => gzipSync(Buffer.from(JSON.stringify(replay), "utf8")).toString("base64");
const request = (replay = baseReplay()) => ({
  action: "report",
  mode: replay.mode,
  mapPath: replay.mapPath,
  mapName: replay.map,
  score: replay.authorScore,
  won: replay.won,
  seats: replay.players.length,
  seed: replay.start.seed,
  replay: pack(replay),
  stats: { roadPieces: 12, contracts: 2, coopOrders: 0, won: replay.won },
});
const reject = (body, part) => assert.rejects(() => validateReplayReport(body),
  (error) => error.message.includes(part));

test("accepts valid serialized Classic replay", async () => {
  const incoming = request();
  const result = await validateReplayReport(incoming);
  assert.equal(result.score, 120);
  assert.equal(result.mode, "classic");
  assert.equal(result.replay.history.length, 1);
});

test("accepts valid cooperative replay with Cyrillic map name", async () => {
  const replay = baseReplay();
  replay.mode = "coop";
  replay.map = "Амстердам";
  replay.won = false;
  replay.authorScore = -15;
  replay.scores[0] = -15;
  const result = await validateReplayReport(request(replay));
  assert.equal(result.replay.mode, "coop");
  assert.equal(result.replay.authorScore, -15);
});

test("rejects client score not matching replay author score", async () => {
  const incoming = request();
  incoming.score = 1000;
  await reject(incoming, "replay score/outcome");
});

test("rejects client outcome not matching replay", async () => {
  const incoming = request();
  incoming.won = false;
  await reject(incoming, "replay score/outcome");
});

test("rejects forged total within replay score array", async () => {
  const replay = baseReplay();
  replay.scores[0] = 999;
  await reject(request(replay), "author is not a human");
});

test("rejects replay from a different map", async () => {
  const incoming = request();
  incoming.mapPath = "CityHighways/Maps/Amsterdam";
  await reject(incoming, "replay map/mode");
});

test("rejects mismatched map display names", async () => {
  const incoming = request();
  incoming.mapName = "Другая карта";
  await reject(incoming, "replay map/mode");
});

test("rejects mismatched Classic/cooperative mode", async () => {
  const incoming = request();
  incoming.mode = "coop";
  await reject(incoming, "replay map/mode");
});

test("rejects altered seed", async () => {
  const incoming = request();
  incoming.seed += 1;
  await reject(incoming, "initial seed");
});

test("rejects author seat outside array", async () => {
  const replay = baseReplay();
  replay.authorSeat = 5;
  await reject(request(replay), "author seat");
});

test("rejects a bot posing as the reporting player", async () => {
  const replay = baseReplay();
  replay.humans[0] = false;
  await reject(request(replay), "author is not a human");
});

test("rejects inconsistent player array sizes", async () => {
  const replay = baseReplay();
  replay.humans.pop();
  await reject(request(replay), "player arrays");
});

test("rejects unsupported replay format version", async () => {
  const replay = baseReplay();
  replay.version = 2;
  await reject(request(replay), "replay version");
});

test("rejects missing game start metadata", async () => {
  const replay = baseReplay();
  const incoming = request(replay);
  replay.start = null;
  incoming.replay = pack(replay);
  await reject(incoming, "initial seed");
});

test("rejects absent and malformed command history", async () => {
  const replay = baseReplay();
  replay.history = [];
  await reject(request(replay), "action history");
  replay.history = [null];
  await reject(request(replay), "action history");
});

test("rejects absurd action history length", async () => {
  const replay = baseReplay();
  replay.history = Array.from({ length: MAX_REPLAY_ACTIONS + 1 }, () => ({}));
  await reject(request(replay), "action history");
});

test("rejects score and seat clamping instead of silently changing results", async () => {
  for (const value of [1001, -101, 12.5, "120", null, NaN]) {
    const incoming = request();
    incoming.score = value;
    await reject(incoming, "score");
  }
  for (const value of [0, 9, "2", 1.5]) {
    const incoming = request();
    incoming.seats = value;
    await reject(incoming, "seats");
  }
});

test("rejects malformed modes, identifiers and outcomes", async () => {
  for (const value of ["magnate", "spectator", "", null]) {
    const incoming = request();
    incoming.mode = value;
    await reject(incoming, "mode");
  }
  const incoming = request();
  incoming.mapPath = "bad\npath";
  await reject(incoming, "mapPath");
  const badOutcome = request();
  badOutcome.won = "true";
  await reject(badOutcome, "won");
});

test("rejects bad base64 instead of treating it as an empty replay", async () => {
  for (const value of ["", "*".repeat(8), "AA==junk", "abc", "Y Q=="]) {
    const incoming = request();
    incoming.replay = value;
    await reject(incoming, value === "" ? "replay size" : "base64");
  }
});

test("rejects valid base64 containing non-gzip bytes", async () => {
  const incoming = request();
  incoming.replay = Buffer.from("not a gzip", "utf8").toString("base64");
  await reject(incoming, "gzip or JSON");
});

test("rejects a decompression bomb within the compressed upload cap", async () => {
  const compressed = gzipSync(Buffer.from("x".repeat(MAX_REPLAY_JSON_BYTES + 4096)));
  assert.ok(compressed.toString("base64").length < MAX_REPLAY_BASE64);
  await assert.rejects(() => unpackReplay(compressed.toString("base64")),
    /decompressed size/);
});

test("rejects compressed payload exceeding upload size", async () => {
  const incoming = request();
  incoming.replay = "A".repeat(MAX_REPLAY_BASE64 + 1);
  await reject(incoming, "replay size");
});

test("handles invalid encoded UTF-8 without crashing edge runtime", async () => {
  const incoming = request();
  incoming.replay = gzipSync(Uint8Array.from([0xff, 0xfe, 0xff])).toString("base64");
  await reject(incoming, "gzip or JSON");
});

test("metadata-only checks do not implicitly verify gameplay score", async () => {
  const replay = baseReplay();
  replay.authorScore = 900;
  replay.scores[0] = 900;
  const accepted = await validateReplayReport(request(replay));
  assert.equal(accepted.score, 900);
  // This is intentionally accepted: replay engine verification remains
  // a separate Stage 3 requirement. Matching client fields are not proof.
});

test("validates request fields without needing decompression", () => {
  const incoming = request();
  assert.equal(validateRecordFields(incoming), incoming);
  incoming.seed = "17";
  assert.throws(() => validateRecordFields(incoming), /seed/);
});
