// City Highways — синхронизация профиля игрока.
//
// Игра присылает: платформу, билет входа (Steam: GetAuthTicketForWebApi("cityhighways")), профиль целиком,
// номера уже применённых действий администратора. Функция проверяет билет у Steam (ISteamUserAuth/
// AuthenticateUserTicket) и только так узнаёт SteamID — подделать чужой ID нельзя. Затем сохраняет профиль
// (кроме бана — его ставит только администратор), отмечает применённые действия и возвращает новые.
//
// Секреты функции: STEAM_WEB_API_KEY (издательский ключ Web API из Steamworks), STEAM_APP_ID.
// ALLOW_UNVERIFIED=1 — только для разработки (AppID 480 без своего ключа): SteamID берётся из запроса, профиль
// помечается verified=false.
import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2";

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
const list = (v: unknown) => (Array.isArray(v) ? v.filter((x) => typeof x === "string").slice(0, 2000) : []);

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return json({ error: "POST only" }, 405);
  const raw = await req.text();
  if (raw.length > 512 * 1024) return json({ error: "too large" }, 413);
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

  const id = `${platform}:${who.userId}`;
  const db = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!, {
    auth: { persistSession: false },
  });

  // Без проверки (режим разработки) нельзя перезаписать профиль, который уже входил с настоящим билетом.
  if (!who.verified) {
    const { data: existing } = await db.from("players").select("verified").eq("id", id).maybeSingle();
    if (existing?.verified) return json({ error: "profile is verified; ticket required" }, 401);
  }

  const p = body.profile ?? {};
  const row = {
    id,
    platform,
    platform_user_id: who.userId,
    name: str(p.name, 64),
    verified: who.verified,
    wallet: int(p.wallet),
    earned_total: int(p.earnedTotal),
    games: int(p.games),
    wins: int(p.wins),
    containers_opened: int(p.containersOpened),
    owned: list(p.owned),
    outfit: str(p.outfit, 1000),
    emblem: str(p.emblem, 64),
    title: str(p.title, 64),
    frame: str(p.frame, 64),
    achievements: list(p.achievements),
    profile: p,
    game_version: str(body.version, 32),
    last_seen: new Date().toISOString(),
  };
  const { error: upsertError } = await db.from("players").upsert(row, { onConflict: "id" });
  if (upsertError) return json({ error: upsertError.message }, 500);

  // Действия, которые игра уже применила, — отмечаем; остальные отдаём.
  const applied = Array.isArray(body.applied) ? body.applied.map(int).filter((n: number) => n > 0).slice(0, 500) : [];
  if (applied.length > 0) {
    await db.from("admin_actions").update({ applied_at: new Date().toISOString() })
      .eq("player_id", id).is("applied_at", null).in("id", applied);
  }
  const { data: player } = await db.from("players").select("banned, ban_reason").eq("id", id).single();
  const { data: actions } = await db.from("admin_actions").select("id, kind, amount, item_id, note")
    .eq("player_id", id).is("applied_at", null).order("id");

  return json({
    id,
    platformUserId: who.userId,
    verified: who.verified,
    banned: player?.banned ?? false,
    banReason: player?.ban_reason ?? "",
    actions: (actions ?? []).map((a) => ({ id: a.id, kind: a.kind, amount: a.amount, item: a.item_id, note: a.note })),
  });
});
