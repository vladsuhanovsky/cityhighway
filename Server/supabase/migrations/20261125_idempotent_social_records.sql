-- Stage 3: transactional personal-best update + idempotent club contributions.
-- Apply this migration in staging BEFORE deploying the new social Edge Function.
-- Existing social reports and RLS remain otherwise unchanged.

-- Do not trust a read-then-upsert pair: two concurrent requests can race
-- and overwrite a better score with an older/lower result.
create or replace function public.record_upsert_best(
  p_player text, p_mode text, p_map_path text, p_map_name text,
  p_score integer, p_won boolean, p_seats integer, p_seed integer,
  p_replay text, p_game_version text
)
returns jsonb
language plpgsql security definer set search_path = ''
as $$
declare
  inserted_score integer;
  previous_score integer;
begin
  if p_mode not in ('classic', 'coop') or
     length(coalesce(p_map_path, '')) not between 1 and 200 or
     length(coalesce(p_map_name, '')) not between 1 and 64 or
     p_score not between -100 and 1000 or
     p_seats not between 1 and 8 or
     length(coalesce(p_replay, '')) < 1 or
     length(p_replay) > 716800 then
    raise exception 'invalid record submission';
  end if;

  insert into public.records
    (player_id, mode, map_path, map_name, score, won, seats, seed,
     replay, game_version, created_at)
  values
    (p_player, p_mode, p_map_path, p_map_name, p_score, p_won, p_seats,
     p_seed, p_replay, p_game_version, now())
  on conflict (player_id, mode, map_path)
  do update set
    map_name = excluded.map_name,
    score = excluded.score,
    won = excluded.won,
    seats = excluded.seats,
    seed = excluded.seed,
    replay = excluded.replay,
    game_version = excluded.game_version,
    created_at = excluded.created_at
  where excluded.score > public.records.score
  returning score into inserted_score;

  if inserted_score is not null then
    return jsonb_build_object('improved', true, 'best', inserted_score);
  end if;

  select score into previous_score from public.records
    where player_id = p_player and mode = p_mode and map_path = p_map_path;

  if previous_score is null then
    raise exception 'record disappeared during update';
  end if;
  return jsonb_build_object('improved', false, 'best', previous_score);
end;
$$;

revoke all on function public.record_upsert_best(
  text, text, text, text, integer, boolean, integer, integer, text, text
) from public, anon, authenticated;
grant execute on function public.record_upsert_best(
  text, text, text, text, integer, boolean, integer, integer, text, text
) to service_role;

-- One validated deterministic start + command history may contribute at
-- most once per Steam profile, even after another club is joined or the
-- client retries the same upload after a timeout.
create table if not exists public.club_report_receipts (
  player_id text not null references public.players (id) on delete cascade,
  submission_hash text not null check (submission_hash ~ '^[0-9a-f]{64}$'),
  -- Historical receipt must outlive a club's deletion: otherwise
  -- the same replay could be reused in another club after disbanding.
  club_id uuid not null,
  week text not null,
  created_at timestamptz not null default now(),
  primary key (player_id, submission_hash)
);
create index if not exists club_report_receipts_club_week_idx
  on public.club_report_receipts (club_id, week);

alter table public.club_report_receipts enable row level security;
create policy club_report_receipts_admin_read on public.club_report_receipts
  for select to authenticated using ((select private.is_admin()));

-- INSERT ... ON CONFLICT DO NOTHING serializes identical concurrent keys.
-- The receipt and existing club_contribute call execute in ONE transaction:
-- any contribution error rolls back the receipt too.
create or replace function public.club_contribute_once(
  p_club uuid, p_week text, p_player text, p_kind text, p_target integer,
  p_amount integer, p_submission_hash text
)
returns jsonb
language plpgsql security definer set search_path = ''
as $$
declare
  inserted boolean := false;
  completed boolean := false;
begin
  if p_submission_hash is null or
     p_submission_hash !~ '^[0-9a-f]{64}$' or
     p_amount is null or p_amount not between 0 and 1000 or
     p_target is null or p_target < 1 then
    raise exception 'invalid club report';
  end if;

  -- A stale membership read in the Edge Function cannot contribute to
  -- a club that the player has already left.
  if not exists (
    select 1 from public.club_members
      where club_id = p_club and player_id = p_player
      for key share
  ) then
    raise exception 'club membership changed';
  end if;

  insert into public.club_report_receipts
    (player_id, submission_hash, club_id, week)
  values (p_player, p_submission_hash, p_club, p_week)
  on conflict (player_id, submission_hash) do nothing
  returning true into inserted;

  if not coalesce(inserted, false) then
    return jsonb_build_object('accepted', false, 'closed', false);
  end if;

  completed := public.club_contribute(
    p_club, p_week, p_player, p_kind, p_target, p_amount);
  return jsonb_build_object('accepted', true, 'closed', completed);
end;
$$;

revoke all on function public.club_contribute_once(
  uuid, text, text, text, integer, integer, text
) from public, anon, authenticated;
grant execute on function public.club_contribute_once(
  uuid, text, text, text, integer, integer, text
) to service_role;
