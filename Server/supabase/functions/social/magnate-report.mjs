// Magnate has no deterministic command-history replay today. A fresh per-match
// 128-bit client nonce provides retry idempotency, NOT authoritative proof of
// gameplay, completed rounds, winner or score.
const RUN_ID = /^[0-9a-f]{32}$/;
const ZERO_ID = "0".repeat(32);

const fail = (why) => { throw new Error("invalid magnate report: " + why); };

export function validateMagnateReport(body) {
  if (!body || typeof body !== "object" || Array.isArray(body) ||
      body.mode !== "magnate") fail("mode");
  if (typeof body.runId !== "string" || !RUN_ID.test(body.runId) ||
      body.runId === ZERO_ID) fail("runId");
  if (typeof body.mapName !== "string" ||
      body.mapName.length < 1 || body.mapName.length > 64 ||
      /[\x00-\x1f\x7f]/.test(body.mapName)) fail("mapName");
  if (!Number.isSafeInteger(body.score)) fail("score");
  if (typeof body.won !== "boolean") fail("won");
  if (!body.stats || typeof body.stats !== "object" ||
      Array.isArray(body.stats)) fail("stats");
  if (!Number.isSafeInteger(body.stats.delivered) ||
      body.stats.delivered < 0 || body.stats.delivered > 10000)
    fail("stats.delivered");
  if (typeof body.stats.won !== "boolean" ||
      body.stats.won !== body.won) fail("stats.won");
  return body.runId;
}

export async function magnateReportFingerprint(body) {
  const runId = validateMagnateReport(body);
  // Never collide with a canonical Classic/Coop replay fingerprint.
  // The DB receipt key is (playerId, SHA-256). The user ID itself is
  // authenticated by the existing Steam ticket verification.
  const bytes = new TextEncoder().encode("magnate/v1/" + runId);
  const hash = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(hash), (byte) =>
    byte.toString(16).padStart(2, "0")).join("");
}
