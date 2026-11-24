// City Highways — социальные функции: рекорды и повторы (призраки, рейды), клубы строителей.
//
// Вход как у player-sync: платформа + билет Steam (GetAuthTicketForWebApi("cityhighways")). Функция проверяет билет
// у Steam и только так узнаёт SteamID. Игрок должен уже быть в таблице players (его создаёт player-sync).
//
// Действия (поле action):
//   report       — итог партии: рекорд на карте (с повтором, если он лучше прежнего) и вклад в недельную цель клуба;
//   friends      — лучшие результаты друзей (список SteamID из клиента Steam), по желанию — на одной карте и в режиме;
//   replay       — повтор рекорда по id;
//   club         — свой клуб: участники, недельная цель, вклад, логотипы, рекорды участников;
//   club_list    — открытые клубы (поиск по названию или тегу);
//   club_create  — создать клуб (название, тег, стартовый логотип);
//   club_join    — вступить по коду приглашения или в открытый клуб по id;
//   club_leave   — выйти (последний участник — клуб удаляется; владелец передаёт клуб самому давнему участнику);
//   club_logo    — владелец выбирает герб из заработанных логотипов;
//   club_open    — владелец открывает или закрывает клуб для вступления без кода.
//
// Секреты: те же, что у player-sync (STEAM_WEB_API_KEY, STEAM_APP_ID, для разработки ALLOW_UNVERIFIED=1).
import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2";
import { validateReplayReport } from "./report-validator.mjs";
import { replayFingerprint } from "./report-fingerprint.mjs";
import { upsertPersonalBest, creditClubGameOnce } from "./report-persistence.mjs";
import { readBoundedBody, RequestTooLargeError } from "./body-limit.mjs";

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { ...cors, "Content-Type": "application/json" } });
}

type Verified = { userId: string; verified: boolean } | { error: string };

async function verifySteam(ticket: string, claimed: string): Promise<Verified> {
  const key = Deno.env.get("STEAM_WEB_API_KEY") ?? "";
  const appId = Deno.env.get("STEAM_APP_ID") ?? "";
  if (key && appId && ticket) {
    const url = `https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key=${encodeURIComponent(key)}` +
      `&appid=${encodeURIComponent(appId)}&ticket=${encodeURIComponent(ticket)}&identity=cityhighways`;
    const response = await fetch(url);
    if (!response.ok) return { error: `steam http ${response.status}` };
    const data = await response.json();
    const params = data?.response?.params;
    if (!params || params.result !== "OK" || !params.steamid) return { error: "steam ticket rejected" };
    return { userId: String(params.steamid), verified: true };
  }
  if (Deno.env.get("ALLOW_UNVERIFIED") === "1" && /^\d{5,20}$/.test(claimed)) return { userId: claimed, verified: false };
  return { error: "steam verification is not configured" };
}

const int = (v: unknown) => (Number.isFinite(Number(v)) ? Math.trunc(Number(v)) : 0);
const str = (v: unknown, max = 200) => (typeof v === "string" ? v.slice(0, max) : "");
const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));

// ---------------------------------------------------------------- Недели и цели

/** ISO-неделя: '2026-W47' и время её конца (понедельник 00:00 UTC следующей недели). */
function isoWeek(date = new Date()): { id: string; number: number; endsAt: string } {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  const day = d.getUTCDay() || 7;
  d.setUTCDate(d.getUTCDate() + 4 - day);
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  const week = Math.ceil(((d.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
  const monday = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  monday.setUTCDate(monday.getUTCDate() - ((monday.getUTCDay() || 7) - 1) + 7);
  return { id: `${d.getUTCFullYear()}-W${String(week).padStart(2, "0")}`, number: d.getUTCFullYear() * 53 + week, endsAt: monday.toISOString() };
}

// Цель недели одна на все клубы (по номеру недели), сколько нужно — по числу участников (не меньше чем на четверых).
const GOALS = [
  { kind: "roads", per: 250 },     // км дорог: участок = 10 км, партия ≈ 250–450 км
  { kind: "contracts", per: 5 },   // выполненных контрактов
  { kind: "orders", per: 3 },      // заказов города в кооперативе
  { kind: "wins", per: 2 },        // побед (в кооперативе — побед команды)
  { kind: "cargo", per: 15 },      // доставленных грузов в магнате
] as const;

function goalFor(weekNumber: number, members: number) {
  const goal = GOALS[weekNumber % GOALS.length];
  return { kind: goal.kind, target: goal.per * Math.max(4, members) };
}

function contribution(kind: string, stats: any): number {
  switch (kind) {
    case "roads": return clamp(int(stats?.roadPieces), 0, 60) * 10;
    case "contracts": return clamp(int(stats?.contracts), 0, 20);
    case "orders": return clamp(int(stats?.coopOrders), 0, 15);
    case "wins": return stats?.won ? 1 : 0;
    case "cargo": return clamp(int(stats?.delivered), 0, 80);
    default: return 0;
  }
}

// ---------------------------------------------------------------- Логотипы

// Логотип — строка 'форма.знак.цвет.ранг' (ClubLogo в игре рисует его сам). Ранг 0 — стартовый, 1–3 — заработанный.
const SHAPES = 6, GLYPHS = 12, COLORS = 12;

function hash(text: string): number {
  let h = 2166136261;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

function validLogo(logo: string, maxTier = 0): boolean {
  const parts = logo.split(".").map(Number);
  return parts.length === 4 && parts.every(Number.isInteger) && parts[0] >= 0 && parts[0] < SHAPES && parts[1] >= 0 &&
    parts[1] < GLYPHS && parts[2] >= 0 && parts[2] < COLORS && parts[3] >= 0 && parts[3] <= maxTier;
}

/** Уникальный логотип клуба за неделю: из id клуба и недели, ранг растёт с числом выполненных целей. */
function earnedLogo(clubId: string, week: string, completed: number, taken: Set<string>): string {
  const tier = clamp(completed, 1, 3);
  let h = hash(`${clubId}/${week}`);
  for (let attempt = 0; attempt < 50; attempt++) {
    const logo = `${h % SHAPES}.${Math.floor(h / SHAPES) % GLYPHS}.${Math.floor(h / (SHAPES * GLYPHS)) % COLORS}.${tier}`;
    if (!taken.has(logo)) return logo;
    h = hash(`${h}/${attempt}`);
  }
  return `${h % SHAPES}.${h % GLYPHS}.${h % COLORS}.${tier}`;
}

function inviteCode(): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  const bytes = crypto.getRandomValues(new Uint8Array(6));
  for (const b of bytes) code += alphabet[b % alphabet.length];
  return code;
}

// ---------------------------------------------------------------- Запрос

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return json({ error: "POST only" }, 405);
  let raw: string;
  try {
    raw = await readBoundedBody(req);
  } catch (error) {
    if (error instanceof RequestTooLargeError)
      return json({ error: "too large" }, 413);
    return json({ error: "invalid request encoding" }, 400);
  }
  let body: any;
  try {
    body = JSON.parse(raw);
  } catch {
    return json({ error: "bad json" }, 400);
  }

  const platform = str(body.platform, 16);
  if (platform !== "steam") return json({ error: "unsupported platform" }, 400);
  const who = await verifySteam(str(body.ticket, 4096), str(body.claimedId, 32));
  if ("error" in who) return json({ error: who.error }, 401);
  const me = `${platform}:${who.userId}`;

  const db = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!, {
    auth: { persistSession: false },
  });
  const { data: player } = await db.from("players").select("id, name, banned, verified").eq("id", me).maybeSingle();
  if (!player) return json({ error: "profile not synced yet" }, 404);
  if (player.banned) return json({ error: "banned" }, 403);
  // Без проверки (режим разработки) нельзя действовать за профиль, который уже входил с настоящим билетом.
  if (!who.verified && player.verified) return json({ error: "profile is verified; ticket required" }, 401);

  const action = str(body.action, 32);
  try {
    switch (action) {
      case "report": return json(await report(db, me, body));
      case "friends": return json(await friends(db, body));
      case "replay": return json(await replay(db, body));
      case "club": return json(await clubInfo(db, me));
      case "club_list": return json(await clubList(db, body));
      case "club_create": return json(await clubCreate(db, me, body));
      case "club_join": return json(await clubJoin(db, me, body));
      case "club_leave": return json(await clubLeave(db, me));
      case "club_logo": return json(await clubLogo(db, me, body));
      case "club_open": return json(await clubOpen(db, me, body));
      default: return json({ error: "unknown action" }, 400);
    }
  } catch (e) {
    return json({ error: e instanceof Error ? e.message : String(e) }, 400);
  }
});

type Db = ReturnType<typeof createClient>;

// ---------------------------------------------------------------- Рекорды

async function report(db: Db, me: string, body: any) {
  const mode = str(body.mode, 16);
  const mapPath = str(body.mapPath, 200);
  // A client-provided score is not authoritative. Reject malformed
  // or internally inconsistent compressed replays before any DB write
  // or club contribution; do not silently clamp a forged result.
  if (mode === "classic" || mode === "coop")
    await validateReplayReport(body);
  else if (mode !== "magnate")
    throw new Error("invalid report mode");

  const score = mode === "magnate"
    ? clamp(int(body.score), -100, 1000)
    : body.score;
  const stats = body.stats ?? {};
  let improved = false, best = score;

  if (mode === "classic" || mode === "coop") {
    const { data: old } = await db.from("records").select("id, score").eq("player_id", me).eq("mode", mode)
      .eq("map_path", mapPath).maybeSingle();
    if (!old || score > old.score) {
      const row = {
        player_id: me,
        mode,
        map_path: mapPath,
        map_name: str(body.mapName, 64),
        score,
        won: !!body.won,
        seats: clamp(int(body.seats), 1, 8),
        seed: int(body.seed),
        // Bounded/decoded/metadata-checked by validateReplayReport.
        // Never truncate a gzip-base64 stream into an unreadable record.
        replay: body.replay,
        game_version: str(body.version, 32),
        created_at: new Date().toISOString(),
      };
      const { error } = await db.from("records").upsert(row, { onConflict: "player_id,mode,map_path" });
      if (error) throw new Error(error.message);
      improved = true;
    } else best = old.score;
  }

  // Вклад в недельную цель клуба.
  let club: unknown = null;
  const { data: member } = await db.from("club_members").select("club_id").eq("player_id", me).maybeSingle();
  if (member) {
    const week = isoWeek();
    const { data: existing } = await db.from("club_weeks").select("goal_kind, goal_target").eq("club_id", member.club_id)
      .eq("week", week.id).maybeSingle();
    let kind = existing?.goal_kind, target = existing?.goal_target;
    if (!existing) {
      const { count } = await db.from("club_members").select("player_id", { count: "exact", head: true }).eq("club_id", member.club_id);
      const goal = goalFor(week.number, count ?? 1);
      kind = goal.kind;
      target = goal.target;
    }
    const amount = contribution(kind!, stats);
    const { data: closed, error } = await db.rpc("club_contribute", {
      p_club: member.club_id, p_week: week.id, p_player: me, p_kind: kind, p_target: target, p_amount: amount,
    });
    if (error) throw new Error(error.message);
    let logo: string | null = null;
    if (closed) {
      // Цель закрыта — клубу новый уникальный логотип.
      const { data: logos } = await db.from("club_logos").select("logo").eq("club_id", member.club_id);
      const { count: done } = await db.from("club_weeks").select("week", { count: "exact", head: true })
        .eq("club_id", member.club_id).not("completed_at", "is", null);
      logo = earnedLogo(member.club_id, week.id, done ?? 1, new Set((logos ?? []).map((l: any) => l.logo)));
      await db.from("club_logos").insert({ club_id: member.club_id, logo, week: week.id });
    }
    club = { kind, amount, closed: !!closed, logo };
  }
  return { improved, best, club };
}

async function friends(db: Db, body: any) {
  const ids = (Array.isArray(body.ids) ? body.ids : []).map((x: unknown) => str(x, 24)).filter((x: string) => /^\d{5,20}$/.test(x))
    .slice(0, 400).map((x: string) => `steam:${x}`);
  if (ids.length === 0) return { records: [] };
  let query = db.from("records").select("id, player_id, mode, map_path, map_name, score, won, seats, created_at, players(name)")
    .in("player_id", ids);
  const mode = str(body.mode, 16), mapPath = str(body.mapPath, 200);
  if (mode) query = query.eq("mode", mode);
  if (mapPath) query = query.eq("map_path", mapPath);
  const { data, error } = await query.order("score", { ascending: false }).limit(200);
  if (error) throw new Error(error.message);
  return { records: (data ?? []).map(recordOut) };
}

function recordOut(r: any) {
  return {
    id: r.id,
    playerId: String(r.player_id).replace(/^steam:/, ""),
    name: r.players?.name ?? "",
    mode: r.mode,
    mapPath: r.map_path,
    mapName: r.map_name,
    score: r.score,
    won: r.won,
    seats: r.seats,
    date: r.created_at,
  };
}

async function replay(db: Db, body: any) {
  const id = int(body.recordId);
  const { data, error } = await db.from("records").select("replay, players(name)").eq("id", id).maybeSingle();
  if (error) throw new Error(error.message);
  if (!data || !data.replay) throw new Error("replay not found");
  return { replay: data.replay, author: (data as any).players?.name ?? "" };
}

// ---------------------------------------------------------------- Клубы

async function myMembership(db: Db, me: string) {
  const { data } = await db.from("club_members").select("club_id, role").eq("player_id", me).maybeSingle();
  return data as { club_id: string; role: string } | null;
}

async function clubInfo(db: Db, me: string) {
  const membership = await myMembership(db, me);
  if (!membership) return { club: null };
  const clubId = membership.club_id;
  const week = isoWeek();
  const [{ data: club }, { data: members }, { data: weekRow }, { data: contrib }, { data: logos }, { data: done }] = await Promise.all([
    db.from("clubs").select("id, name, tag, invite_code, is_open, logo").eq("id", clubId).single(),
    db.from("club_members").select("player_id, role, joined_at, players(name)").eq("club_id", clubId).order("joined_at"),
    db.from("club_weeks").select("goal_kind, goal_target, progress, completed_at").eq("club_id", clubId).eq("week", week.id).maybeSingle(),
    db.from("club_contrib").select("player_id, amount, games").eq("club_id", clubId).eq("week", week.id),
    db.from("club_logos").select("logo, week, earned_at").eq("club_id", clubId).order("earned_at"),
    db.from("club_weeks").select("week").eq("club_id", clubId).not("completed_at", "is", null),
  ]);
  const goal = weekRow
    ? { kind: weekRow.goal_kind, target: weekRow.goal_target, progress: weekRow.progress, completed: !!weekRow.completed_at }
    : { ...goalFor(week.number, members?.length ?? 1), progress: 0, completed: false };
  const byPlayer = new Map((contrib ?? []).map((c: any) => [c.player_id, c]));
  const memberIds = (members ?? []).map((m: any) => m.player_id);
  // Рекорды участников — чтобы смотреть повторы партий клубных товарищей.
  const { data: records } = memberIds.length > 0
    ? await db.from("records").select("id, player_id, mode, map_path, map_name, score, won, seats, created_at, players(name)")
      .in("player_id", memberIds).order("score", { ascending: false }).limit(40)
    : { data: [] as any[] };
  return {
    club: {
      id: club!.id, name: club!.name, tag: club!.tag, code: club!.invite_code, open: club!.is_open, logo: club!.logo,
      owner: membership.role === "owner",
    },
    members: (members ?? []).map((m: any) => ({
      name: m.players?.name ?? "",
      role: m.role,
      me: m.player_id === me,
      amount: (byPlayer.get(m.player_id) as any)?.amount ?? 0,
      games: (byPlayer.get(m.player_id) as any)?.games ?? 0,
    })),
    week: { id: week.id, endsAt: week.endsAt, ...goal },
    weeksDone: (done ?? []).length,
    logos: (logos ?? []).map((l: any) => ({ logo: l.logo, week: l.week })),
    records: (records ?? []).map(recordOut),
  };
}

async function clubList(db: Db, body: any) {
  const q = str(body.query, 32).replace(/[%_,()]/g, "").trim();
  let query = db.from("clubs").select("id, name, tag, is_open, logo, club_members(count)").eq("is_open", true);
  if (q) query = query.or(`name.ilike.%${q}%,tag.ilike.%${q}%`);
  const { data, error } = await query.order("created_at", { ascending: false }).limit(30);
  if (error) throw new Error(error.message);
  return {
    clubs: (data ?? []).map((c: any) => ({
      id: c.id, name: c.name, tag: c.tag, logo: c.logo, members: c.club_members?.[0]?.count ?? 0,
    })),
  };
}

async function clubCreate(db: Db, me: string, body: any) {
  if (await myMembership(db, me)) throw new Error("already in a club");
  const name = str(body.name, 32).trim();
  const tag = str(body.tag, 4).trim().toUpperCase();
  if (name.length < 3) throw new Error("name too short");
  if (!/^[A-ZА-ЯЁ0-9]{2,4}$/.test(tag)) throw new Error("bad tag");
  const logo = validLogo(str(body.logo, 16)) ? str(body.logo, 16) : "0.0.0.0";
  const { data: club, error } = await db.from("clubs")
    .insert({ name, tag, invite_code: inviteCode(), logo, created_by: me, is_open: body.open !== false })
    .select("id").single();
  if (error) throw new Error(error.code === "23505" ? "name taken" : error.message);
  await db.from("club_members").insert({ player_id: me, club_id: club.id, role: "owner" });
  await db.from("club_logos").insert({ club_id: club.id, logo, week: "" });
  return clubInfo(db, me);
}

async function clubJoin(db: Db, me: string, body: any) {
  if (await myMembership(db, me)) throw new Error("already in a club");
  const code = str(body.code, 12).trim().toUpperCase();
  const id = str(body.clubId, 64);
  const query = code ? db.from("clubs").select("id, is_open").eq("invite_code", code) : db.from("clubs").select("id, is_open").eq("id", id);
  const { data: club } = await query.maybeSingle();
  if (!club) throw new Error("club not found");
  if (!code && !club.is_open) throw new Error("club is closed");
  const { count } = await db.from("club_members").select("player_id", { count: "exact", head: true }).eq("club_id", club.id);
  if ((count ?? 0) >= 30) throw new Error("club is full");
  const { error } = await db.from("club_members").insert({ player_id: me, club_id: club.id, role: "member" });
  if (error) throw new Error(error.message);
  return clubInfo(db, me);
}

async function clubLeave(db: Db, me: string) {
  const membership = await myMembership(db, me);
  if (!membership) return { club: null };
  await db.from("club_members").delete().eq("player_id", me);
  const { data: rest } = await db.from("club_members").select("player_id, role").eq("club_id", membership.club_id).order("joined_at");
  if (!rest || rest.length === 0) await db.from("clubs").delete().eq("id", membership.club_id);
  else if (membership.role === "owner" && !rest.some((m: any) => m.role === "owner"))
    await db.from("club_members").update({ role: "owner" }).eq("player_id", rest[0].player_id);
  return { club: null };
}

async function clubLogo(db: Db, me: string, body: any) {
  const membership = await myMembership(db, me);
  if (!membership || membership.role !== "owner") throw new Error("owner only");
  const logo = str(body.logo, 16);
  const { data: owned } = await db.from("club_logos").select("logo").eq("club_id", membership.club_id).eq("logo", logo).maybeSingle();
  if (!owned) throw new Error("logo not earned");
  await db.from("clubs").update({ logo }).eq("id", membership.club_id);
  return clubInfo(db, me);
}

async function clubOpen(db: Db, me: string, body: any) {
  const membership = await myMembership(db, me);
  if (!membership || membership.role !== "owner") throw new Error("owner only");
  await db.from("clubs").update({ is_open: !!body.open }).eq("id", membership.club_id);
  return clubInfo(db, me);
}
