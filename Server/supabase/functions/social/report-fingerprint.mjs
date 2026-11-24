// A stable per-game fingerprint for idempotent club progress. Unlike the
// replay's id/date/gzip bytes, the deterministic start and command history
// are not changed by local file names, serialization order, or re-packing.
// The replay was already schema-checked by validateReplayReport.
const MAX_CANONICAL_DEPTH = 64;

function canonical(value, depth = 0) {
  if (depth > MAX_CANONICAL_DEPTH) throw new Error("replay fingerprint: nesting limit");
  if (value === null) return "null";
  if (Array.isArray(value))
    return "[" + value.map((item) => canonical(item, depth + 1)).join(",") + "]";
  if (typeof value === "object") {
    const keys = Object.keys(value).sort();
    return "{" + keys.map((key) =>
      JSON.stringify(key) + ":" + canonical(value[key], depth + 1)).join(",") + "}";
  }
  if (typeof value === "string" || typeof value === "number" ||
      typeof value === "boolean")
    return JSON.stringify(value);
  throw new Error("replay fingerprint: unsupported value");
}

export function replayIdentityPayload(mode, mapPath, replay) {
  if ((mode !== "classic" && mode !== "coop") ||
      typeof mapPath !== "string" || !replay ||
      !replay.start || !Array.isArray(replay.history) ||
      replay.history.length === 0)
    throw new Error("replay fingerprint: missing game identity");
  return canonical({
    mode,
    mapPath,
    start: replay.start,
    history: replay.history,
  });
}

export async function replayFingerprint(mode, mapPath, replay) {
  const bytes = new TextEncoder().encode(replayIdentityPayload(mode, mapPath, replay));
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest),
    (byte) => byte.toString(16).padStart(2, "0")).join("");
}
