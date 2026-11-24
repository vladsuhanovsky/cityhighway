import test from "node:test";
import assert from "node:assert/strict";
import { validateMagnateReport, magnateReportFingerprint } from "./magnate-report.mjs";

const request = (runId = "a".repeat(32)) => ({
  mode: "magnate", runId, mapName: "Москва", score: 350, won: true,
  stats: { delivered: 25, won: true }
});
const bad = (data, reason) => assert.rejects(
  () => magnateReportFingerprint(data),
  (err) => err.message === "invalid magnate report: " + reason
);

test("accepts canonical Unity Guid N lowercase run ID", async () => {
  const data = request("4c76e724925c4fadbb05d8b26e45dd90");
  assert.equal(validateMagnateReport(data), data.runId);
  assert.match(await magnateReportFingerprint(data), /^[0-9a-f]{64}$/);
});

test("repeating a completed match gives the same SHA-256 receipt", async () => {
  const data = request();
  assert.equal(await magnateReportFingerprint(data),
    await magnateReportFingerprint({ ...data }));
});

test("a new Magnate run gets a different report identity", async () => {
  assert.notEqual(await magnateReportFingerprint(request("a".repeat(32))),
    await magnateReportFingerprint(request("b".repeat(32))));
});

test("score, deliveries, outcome and name changes do not bypass duplicate receipt", async () => {
  const original = request(), forged = request();
  forged.score = 999;
  forged.stats.delivered = 75;
  forged.won = false;
  forged.stats.won = false;
  forged.mapName = "Amsterdam";
  assert.equal(await magnateReportFingerprint(original),
    await magnateReportFingerprint(forged));
});

test("rejects missing, uppercase, dashed and all-zero IDs", async () => {
  for (const runId of [undefined, null, "", "A".repeat(32),
    "a".repeat(31), "a".repeat(33), "g".repeat(32),
    "00000000-0000-0000-0000-000000000001", "0".repeat(32)]) {
    await bad(request(runId), "runId");
  }
});

test("rejects run IDs that contain whitespace or invisible Unicode", async () => {
  for (const runId of ["a".repeat(31) + " ", "a".repeat(31) + "\n",
    "a".repeat(31) + "\u200b", "a".repeat(31) + "ñ"]) {
    await bad(request(runId), "runId");
  }
});

test("rejects malformed Magnate modes and envelopes", async () => {
  for (const value of [null, undefined, [], "magnate", {}]) {
    await bad(value, "mode");
  }
  await bad({ ...request(), mode: "classic" }, "mode");
});

test("rejects empty, control-containing and oversized map names", async () => {
  for (const value of ["", "\nМосква", "x".repeat(65), null, 12]) {
    await bad({ ...request(), mapName: value }, "mapName");
  }
});

test("accepts Cyrillic and other printable Unicode map names", async () => {
  assert.match(await magnateReportFingerprint({ ...request(), mapName: "Токио 東京" }),
    /^[0-9a-f]{64}$/);
});

test("rejects invalid score representations instead of silently parsing strings", async () => {
  for (const value of [NaN, Infinity, -Infinity, 42.5, "350", null, {}, 1e20]) {
    await bad({ ...request(), score: value }, "score");
  }
});

test("rejects invalid winner flag representations", async () => {
  for (const won of ["true", 1, null]) {
    await bad({ ...request(), won }, "won");
  }
});

test("rejects negative, fractional or forged-delivery counters", async () => {
  for (const delivered of [-1, 1.5, "10", NaN, 10001]) {
    await bad({ ...request(), stats: { delivered, won: true } }, "stats.delivered");
  }
});

test("rejects invalid or missing stats objects", async () => {
  for (const stats of [undefined, null, [], "x", 2]) {
    await bad({ ...request(), stats }, "stats");
  }
});

test("rejects inconsistent club win and displayed win", async () => {
  await bad({ ...request(), stats: { delivered: 20, won: false } }, "stats.won");
});

test("does not confuse reports from separate games played on same generated map", async () => {
  const a = request("1".repeat(32)), b = request("2".repeat(32));
  assert.equal(a.mapName, b.mapName);
  assert.notEqual(await magnateReportFingerprint(a), await magnateReportFingerprint(b));
});
