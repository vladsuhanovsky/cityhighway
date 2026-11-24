import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const sql = readFileSync(
  new URL("../../migrations/20261125_idempotent_social_records.sql", import.meta.url),
  "utf8"
);

test("personal best update is a single conditional conflict operation", () => {
  assert.match(sql, /create or replace function public\.record_upsert_best\(/i);
  assert.match(sql, /on conflict \(player_id, mode, map_path\)/i);
  assert.match(sql, /do update set[\s\S]*?where excluded\.score > public\.records\.score/i);
  assert.match(sql, /returning score into inserted_score/i);
});

test("personal best RPC remains inaccessible to game clients and anonymous roles", () => {
  assert.match(sql, /revoke all on function public\.record_upsert_best\([\s\S]*?\) from public, anon, authenticated;/i);
  assert.match(sql, /grant execute on function public\.record_upsert_best\([\s\S]*?\) to service_role;/i);
});

test("a single per-player game hash is the receipt primary key", () => {
  assert.match(sql, /create table if not exists public\.club_report_receipts/i);
  assert.match(sql, /primary key \(player_id, submission_hash\)/i);
  assert.match(sql, /submission_hash text not null check \(submission_hash ~ '\^\[0-9a-f\]\{64\}\$'\)/i);
});

test("receipt insert and contribution execute in one security definer RPC", () => {
  const pos = sql.indexOf("create or replace function public.club_contribute_once(");
  assert.ok(pos >= 0);
  const body = sql.slice(pos);
  const insert = body.indexOf("insert into public.club_report_receipts");
  const conflict = body.indexOf("on conflict (player_id, submission_hash) do nothing");
  const contribution = body.indexOf("public.club_contribute(");
  assert.ok(insert >= 0 && insert < conflict && conflict < contribution);
  assert.match(body, /return jsonb_build_object\('accepted', false, 'closed', false\)/);
  assert.match(body, /return jsonb_build_object\('accepted', true, 'closed', completed\)/);
});

test("receipt insert checks current membership in same SQL transaction", () => {
  assert.match(sql, /from public\.club_members\s+where club_id = p_club and player_id = p_player/i);
  assert.match(sql, /raise exception 'club membership changed'/);
});

test("receipt table enforces RLS and service-only RPC", () => {
  assert.match(sql, /alter table public\.club_report_receipts enable row level security;/);
  assert.match(sql, /revoke all on function public\.club_contribute_once\([\s\S]*?\) from public, anon, authenticated;/);
  assert.match(sql, /grant execute on function public\.club_contribute_once\([\s\S]*?\) to service_role;/);
});

test("new migration is additive, with no destructive schema operations", () => {
  assert.doesNotMatch(sql, /\bdrop\s+(?:table|function|schema)\b/i);
  assert.doesNotMatch(sql, /\btruncate\b/i);
});
