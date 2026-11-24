import test from "node:test";
import assert from "node:assert/strict";
import { replayFingerprint, replayIdentityPayload } from "./report-fingerprint.mjs";

const game = () => ({
  id: "local-2026-123",
  date: "2026-11-24 23:00",
  source: "local",
  author: "Player",
  authorSeat: 0,
  authorScore: 130,
  won: true,
  start: {
    seed: 123,
    config: { rounds: 8, difficulty: 0 },
    players: ["Player", "Bot"]
  },
  history: [
    { kind: "draw_deck", player: 0 },
    { kind: "build", player: 0, road: "a-b", machinery: 0 }
  ]
});

const fp = (g = game(), mode = "classic", mapPath = "Moscow") =>
  replayFingerprint(mode, mapPath, g);

test("stable fingerprint is a lower-case SHA-256 hex digest", async () => {
  const hash = await fp();
  assert.match(hash, /^[0-9a-f]{64}$/);
});

test("same replay content always produces the same fingerprint", async () => {
  assert.equal(await fp(), await fp());
});

test("rewriting timestamp or replay ID does not credit a game twice", async () => {
  const a = game(), b = game();
  b.id = "new-id";
  b.date = "2027-01-01";
  b.author = "renamed-local-profile";
  b.source = "friend";
  assert.equal(await fp(a), await fp(b));
});

test("changing reported score and result metadata cannot bypass replay receipt", async () => {
  const original = game(), forged = game();
  forged.authorScore = 1000;
  forged.won = false;
  forged.authorSeat = 1;
  assert.equal(await fp(original), await fp(forged));
});

test("changing JSON object property order cannot bypass deduplication", async () => {
  const a = game(), b = game();
  b.start = {
    players: ["Player", "Bot"],
    config: { difficulty: 0, rounds: 8 },
    seed: 123
  };
  b.history = [
    { player: 0, kind: "draw_deck" },
    { machinery: 0, road: "a-b", player: 0, kind: "build" },
  ];
  assert.equal(await fp(a), await fp(b));
});

test("changing gzip container bytes does not affect game identity", async () => {
  const a = game(), b = game();
  a.replay = "packed-data-one";
  b.replay = "another-gzip-representation";
  assert.equal(await fp(a), await fp(b));
});

test("different gameplay seed produces a distinct receipt", async () => {
  const changed = game();
  changed.start.seed++;
  assert.notEqual(await fp(), await fp(changed));
});

test("different command history produces a distinct receipt", async () => {
  const changed = game();
  changed.history[1].road = "b-c";
  assert.notEqual(await fp(), await fp(changed));
});

test("changing order of commands changes game identity", async () => {
  const changed = game();
  changed.history.reverse();
  assert.notEqual(await fp(), await fp(changed));
});

test("different map path cannot share the same game receipt", async () => {
  assert.notEqual(await fp(game(), "classic", "Moscow"),
    await fp(game(), "classic", "Amsterdam"));
});

test("Classic and Coop are separate replay identities", async () => {
  assert.notEqual(await fp(game(), "classic"), await fp(game(), "coop"));
});

test("same start and commands under different game configs are distinct", async () => {
  const changed = game();
  changed.start.config.rounds = 10;
  assert.notEqual(await fp(), await fp(changed));
});

test("fingerprint input rejects empty action histories", async () => {
  const changed = game();
  changed.history = [];
  await assert.rejects(() => fp(changed), /missing game identity/);
});

test("fingerprint input rejects Magnate without replay", async () => {
  await assert.rejects(() => fp(game(), "magnate"), /missing game identity/);
});

test("fingerprint limits deeply nested malformed gameplay data", () => {
  let nested = 0;
  for (let i = 0; i < 70; i++) nested = { nested };
  const changed = game();
  changed.history[0].payload = nested;
  assert.throws(() => replayIdentityPayload("classic", "Moscow", changed),
    /nesting limit/);
});

test("fingerprint refuses data types that cannot be represented in JSON", () => {
  const changed = game();
  changed.history[0].unsafe = undefined;
  assert.throws(() => replayIdentityPayload("classic", "Moscow", changed),
    /unsupported value/);
});
