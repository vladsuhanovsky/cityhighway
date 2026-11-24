import test from "node:test";
import assert from "node:assert/strict";
import { upsertPersonalBest, creditClubGameOnce } from "./report-persistence.mjs";

const hash = "a".repeat(64);
const best = () => ({
  mode: "classic",
  map_path: "Moscow",
  map_name: "Москва",
  score: 127,
  won: true,
  seats: 2,
  seed: 42,
  replay: "H4sIAAAAAA",
  game_version: "2.0",
});

const club = () => ({
  clubId: "11111111-1111-1111-1111-111111111111",
  week: "2026-W48",
  playerId: "steam:76561190000123456",
  kind: "contracts",
  target: 20,
  amount: 4,
  fingerprint: hash
});

const dbWith = (data, error = null) => {
  const calls = [];
  return {
    calls,
    rpc: async (method, params) => {
      calls.push({ method, params });
      return { data, error };
    }
  };
};

test("personal best writes via single atomic database RPC", async () => {
  const db = dbWith({ improved: true, best: 127 });
  assert.deepEqual(await upsertPersonalBest(db, "steam:12345", best()),
    { improved: true, best: 127 });
  assert.equal(db.calls.length, 1);
  assert.equal(db.calls[0].method, "record_upsert_best");
  assert.deepEqual(db.calls[0].params, {
    p_player: "steam:12345",
    p_mode: "classic",
    p_map_path: "Moscow",
    p_map_name: "Москва",
    p_score: 127,
    p_won: true,
    p_seats: 2,
    p_seed: 42,
    p_replay: "H4sIAAAAAA",
    p_game_version: "2.0",
  });
});

test("a worse score is returned as not improved with actual best", async () => {
  const db = dbWith({ improved: false, best: 200 });
  assert.deepEqual(await upsertPersonalBest(db, "steam:12345", best()),
    { improved: false, best: 200 });
});

test("equal score is a non-improvement, preserving previously stored replay", async () => {
  const db = dbWith({ improved: false, best: 127 });
  assert.deepEqual(await upsertPersonalBest(db, "steam:12345", best()),
    { improved: false, best: 127 });
});

test("record RPC errors propagate instead of reporting a false improvement", async () => {
  const db = dbWith(null, { message: "permission denied" });
  await assert.rejects(() => upsertPersonalBest(db, "steam:12345", best()),
    /permission denied/);
});

test("record RPC missing or malformed response is an error", async () => {
  for (const data of [null, {}, { improved: "true", best: 100 },
    { improved: true, best: "100" }]) {
    const db = dbWith(data);
    await assert.rejects(() => upsertPersonalBest(db, "steam:12345", best()),
      /invalid personal best RPC response/);
  }
});

test("club receipts use a single SQL RPC with exact validated fingerprint", async () => {
  const db = dbWith({ accepted: true, closed: false });
  assert.deepEqual(await creditClubGameOnce(db, club()),
    { accepted: true, closed: false });
  assert.equal(db.calls.length, 1);
  assert.equal(db.calls[0].method, "club_contribute_once");
  assert.deepEqual(db.calls[0].params, {
    p_club: club().clubId,
    p_week: "2026-W48",
    p_player: club().playerId,
    p_kind: "contracts",
    p_target: 20,
    p_amount: 4,
    p_submission_hash: hash
  });
});

test("duplicate receipts never claim credit or club completion", async () => {
  const db = dbWith({ accepted: false, closed: false });
  assert.deepEqual(await creditClubGameOnce(db, club()),
    { accepted: false, closed: false });
});

test("single accepted submission can complete the club objective", async () => {
  const db = dbWith({ accepted: true, closed: true });
  assert.deepEqual(await creditClubGameOnce(db, club()),
    { accepted: true, closed: true });
});

test("club RPC failure propagates instead of silently giving progress", async () => {
  const db = dbWith(null, { message: "club membership changed" });
  await assert.rejects(() => creditClubGameOnce(db, club()),
    /club membership changed/);
});

test("club RPC response shape is strictly checked", async () => {
  for (const data of [null, {}, { accepted: 1, closed: false },
    { accepted: true, closed: "false" }]) {
    const db = dbWith(data);
    await assert.rejects(() => creditClubGameOnce(db, club()),
      /invalid club contribution RPC response/);
  }
});

test("malformed fingerprints never hit the club database", async () => {
  for (const fingerprint of ["", "abc", "A".repeat(64), "g".repeat(64),
    "a".repeat(63), null, undefined]) {
    const db = dbWith({ accepted: true, closed: false });
    await assert.rejects(() => creditClubGameOnce(db, { ...club(), fingerprint }),
      /invalid game fingerprint/);
    assert.equal(db.calls.length, 0);
  }
});
