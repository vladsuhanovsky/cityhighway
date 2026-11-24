// Supabase RPC boundaries; the database owns atomic conflict handling.
// This module is dependency-free so its JS contract can be regression-tested
// without contacting production Supabase or starting a Unity editor.

function throwRpc(error, fallback) {
  if (error) throw new Error(error.message || fallback);
}

export async function upsertPersonalBest(db, playerId, row) {
  const { data, error } = await db.rpc("record_upsert_best", {
    p_player: playerId,
    p_mode: row.mode,
    p_map_path: row.map_path,
    p_map_name: row.map_name,
    p_score: row.score,
    p_won: row.won,
    p_seats: row.seats,
    p_seed: row.seed,
    p_replay: row.replay,
    p_game_version: row.game_version,
  });
  throwRpc(error, "personal best database update failed");
  if (!data || typeof data.improved !== "boolean" ||
      !Number.isInteger(data.best))
    throw new Error("invalid personal best RPC response");
  return { improved: data.improved, best: data.best };
}

export async function creditClubGameOnce(db, args) {
  if (!/^[0-9a-f]{64}$/.test(args.fingerprint))
    throw new Error("invalid game fingerprint");
  const { data, error } = await db.rpc("club_contribute_once", {
    p_club: args.clubId,
    p_week: args.week,
    p_player: args.playerId,
    p_kind: args.kind,
    p_target: args.target,
    p_amount: args.amount,
    p_submission_hash: args.fingerprint,
  });
  throwRpc(error, "club contribution database update failed");
  if (!data || typeof data.accepted !== "boolean" ||
      typeof data.closed !== "boolean")
    throw new Error("invalid club contribution RPC response");
  return { accepted: data.accepted, closed: data.closed };
}
