// Pure replay intake checks shared by the Supabase Edge Function and
// dependency-free Node regression tests. These checks establish consistency
// with a client-supplied replay, NOT authoritative game-rule verification.
export const MAX_REPLAY_BASE64 = 700 * 1024;
export const MAX_REPLAY_JSON_BYTES = 4 * 1024 * 1024;
export const MAX_REPLAY_ACTIONS = 20000;
export const REPLAY_VERSION = 1;

const fail = (reason) => { throw new Error("invalid replay report: " + reason); };
const object = (v) => v !== null && typeof v === "object" && !Array.isArray(v);
const boundedInt = (v, lo, hi) => Number.isInteger(v) && v >= lo && v <= hi;
const reportString = (v, max) => typeof v === "string" && v.length > 0 &&
  v.length <= max && !/[\x00-\x1f\x7f]/.test(v);

/** Avoid silent integer coercion/clamping when accepting a claimed score. */
export function validateRecordFields(body) {
  if (!object(body)) fail("request is not an object");
  if (body.mode !== "classic" && body.mode !== "coop") fail("mode");
  if (!reportString(body.mapPath, 200)) fail("mapPath");
  if (!reportString(body.mapName, 64)) fail("mapName");
  if (!boundedInt(body.score, -100, 1000)) fail("score");
  if (!boundedInt(body.seed, -2147483648, 2147483647)) fail("seed");
  if (!boundedInt(body.seats, 1, 8)) fail("seats");
  if (typeof body.won !== "boolean") fail("won");
  if (typeof body.replay !== "string" || body.replay.length === 0 ||
      body.replay.length > MAX_REPLAY_BASE64) fail("replay size");
  if (!object(body.stats)) fail("stats");
  for (const key of ["roadPieces", "contracts", "coopOrders"]) {
    if (!boundedInt(body.stats[key], 0, 10000))
      fail("stats." + key);
  }
  if (typeof body.stats.won !== "boolean" ||
      body.stats.won !== body.won)
    fail("stats.won");
  return body;
}

/** Decode gzip incrementally, refusing zip bombs before JSON.parse. */
export async function unpackReplay(encoded) {
  if (typeof encoded !== "string" || encoded.length === 0 ||
      encoded.length > MAX_REPLAY_BASE64 ||
      encoded.length % 4 !== 0 ||
      !/^[A-Za-z0-9+/]+={0,2}$/.test(encoded))
    fail("base64");

  let compressed;
  try {
    const bytes = atob(encoded);
    compressed = Uint8Array.from(bytes, (c) => c.charCodeAt(0));
  } catch {
    fail("base64");
  }
  if (compressed.length > MAX_REPLAY_BASE64) fail("compressed size");

  let reader;
  try {
    reader = new Blob([compressed]).stream()
      .pipeThrough(new DecompressionStream("gzip")).getReader();
    const chunks = [];
    let length = 0;
    while (true) {
      const part = await reader.read();
      if (part.done) break;
      length += part.value.byteLength;
      if (length > MAX_REPLAY_JSON_BYTES) fail("decompressed size");
      chunks.push(part.value);
    }
    const output = new Uint8Array(length);
    let at = 0;
    for (const chunk of chunks) {
      output.set(chunk, at);
      at += chunk.byteLength;
    }
    return JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(output));
  } catch (error) {
    if (error instanceof Error && error.message.startsWith("invalid replay report:"))
      throw error;
    fail("gzip or JSON");
  } finally {
    if (reader) {
      try { await reader.cancel(); } catch { /* completed stream */ }
      try { reader.releaseLock(); } catch { /* already released */ }
    }
  }
}

function validateReplayMetadata(body, replay) {
  if (!object(replay) || replay.version !== REPLAY_VERSION)
    fail("replay version");
  if (replay.mode !== body.mode || replay.mapPath !== body.mapPath ||
      replay.map !== body.mapName) fail("replay map/mode");
  if (replay.authorScore !== body.score || replay.won !== body.won)
    fail("replay score/outcome");
  if (!boundedInt(replay.authorSeat, 0, body.seats - 1))
    fail("author seat");
  if (!Array.isArray(replay.players) ||
      !Array.isArray(replay.scores) ||
      !Array.isArray(replay.humans) ||
      replay.players.length !== body.seats ||
      replay.scores.length !== body.seats ||
      replay.humans.length !== body.seats)
    fail("player arrays");
  if (!replay.players.every((name) => typeof name === "string" && name.length <= 128) ||
      !replay.scores.every((score) => boundedInt(score, -100000, 100000)) ||
      !replay.humans.every((human) => typeof human === "boolean"))
    fail("player entries");
  if (!replay.humans[replay.authorSeat] ||
      replay.scores[replay.authorSeat] !== body.score)
    fail("author is not a human with the claimed score");
  if (!object(replay.start) || replay.start.seed !== body.seed)
    fail("initial seed");
  if (!Array.isArray(replay.history) ||
      replay.history.length === 0 ||
      replay.history.length > MAX_REPLAY_ACTIONS ||
      !replay.history.every(object))
    fail("action history");
  if (!boundedInt(replay.turns, 0, 1000000))
    fail("turn count");
}

/**
 * Accept only a bounded, structurally consistent client replay. The client
 * still controls every payload field; rule-by-rule authoritative validation
 * must be built separately before scores can be described as verified.
 */
export async function validateReplayReport(body) {
  validateRecordFields(body);
  const replay = await unpackReplay(body.replay);
  validateReplayMetadata(body, replay);
  return { score: body.score, mode: body.mode, replay };
}
