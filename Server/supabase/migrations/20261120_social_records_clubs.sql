-- City Highways: рекорды игроков (призраки и рейды), повторы, клубы строителей с недельными целями и логотипами.
-- Всё пишет только функция social (ключ service_role). Анонимам таблицы закрыты RLS, администраторам — чтение.

-- Лучший результат игрока на карте в режиме, вместе с повтором (gzip + base64 JSON ReplayFile).
create table if not exists public.records (
  id bigint generated always as identity primary key,
  player_id text not null references public.players (id) on delete cascade,
  mode text not null check (mode in ('classic', 'coop')),
  map_path text not null,
  map_name text not null default '',
  score integer not null,
  won boolean not null default false,
  seats integer not null default 2,          -- сколько игроков было за столом
  seed integer not null default 0,
  replay text not null default '',
  game_version text not null default '',
  created_at timestamptz not null default now(),
  unique (player_id, mode, map_path)
);
create index if not exists records_map_idx on public.records (mode, map_path, score desc);

-- Клубы.
create table if not exists public.clubs (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(name) between 3 and 32),
  tag text not null check (char_length(tag) between 2 and 4),
  invite_code text not null unique,
  is_open boolean not null default true,
  logo text not null default '',
  created_by text references public.players (id) on delete set null,
  created_at timestamptz not null default now()
);
create unique index if not exists clubs_name_idx on public.clubs (lower(name));

-- Участник состоит не больше чем в одном клубе.
create table if not exists public.club_members (
  player_id text primary key references public.players (id) on delete cascade,
  club_id uuid not null references public.clubs (id) on delete cascade,
  role text not null default 'member' check (role in ('owner', 'member')),
  joined_at timestamptz not null default now()
);
create index if not exists club_members_club_idx on public.club_members (club_id);

-- Недельная цель клуба: вид, сколько нужно, сколько набрано.
create table if not exists public.club_weeks (
  club_id uuid not null references public.clubs (id) on delete cascade,
  week text not null,                       -- ISO-неделя, '2026-W47'
  goal_kind text not null check (goal_kind in ('roads', 'contracts', 'orders', 'wins', 'cargo')),
  goal_target integer not null check (goal_target > 0),
  progress integer not null default 0,
  completed_at timestamptz,
  primary key (club_id, week)
);

-- Вклад участника в недельную цель.
create table if not exists public.club_contrib (
  club_id uuid not null references public.clubs (id) on delete cascade,
  week text not null,
  player_id text not null references public.players (id) on delete cascade,
  amount integer not null default 0,
  games integer not null default 0,
  primary key (club_id, week, player_id)
);
create index if not exists club_contrib_player_idx on public.club_contrib (player_id);

-- Заработанные логотипы клуба (за выполненные недельные цели) и стартовый.
create table if not exists public.club_logos (
  club_id uuid not null references public.clubs (id) on delete cascade,
  logo text not null,
  week text not null default '',
  earned_at timestamptz not null default now(),
  primary key (club_id, logo)
);

-- Добавить вклад атомарно: строка недели и вклада создаются при первом вкладе; цель отмечается выполненной.
-- Возвращает true, если именно этот вклад закрыл цель (тогда функция выдаёт логотип).
create or replace function public.club_contribute(p_club uuid, p_week text, p_player text, p_kind text, p_target integer, p_amount integer)
returns boolean language plpgsql security definer set search_path = '' as $$
declare
  closed boolean := false;
  before_progress integer;
  after_progress integer;
  target integer;
begin
  insert into public.club_weeks (club_id, week, goal_kind, goal_target)
  values (p_club, p_week, p_kind, greatest(1, p_target))
  on conflict (club_id, week) do nothing;

  insert into public.club_contrib (club_id, week, player_id, amount, games)
  values (p_club, p_week, p_player, greatest(0, p_amount), 1)
  on conflict (club_id, week, player_id)
  do update set amount = public.club_contrib.amount + greatest(0, p_amount), games = public.club_contrib.games + 1;

  select progress, goal_target into before_progress, target from public.club_weeks
  where club_id = p_club and week = p_week for update;
  after_progress := before_progress + greatest(0, p_amount);
  update public.club_weeks set progress = after_progress,
    completed_at = case when completed_at is null and after_progress >= goal_target then now() else completed_at end
  where club_id = p_club and week = p_week;
  closed := before_progress < target and after_progress >= target;
  return closed;
end;
$$;
revoke all on function public.club_contribute(uuid, text, text, text, integer, integer) from public, anon, authenticated;
grant execute on function public.club_contribute(uuid, text, text, text, integer, integer) to service_role;

-- RLS: анонимам и игрокам ничего (игра ходит через функцию), администраторам — чтение для панели.
alter table public.records enable row level security;
alter table public.clubs enable row level security;
alter table public.club_members enable row level security;
alter table public.club_weeks enable row level security;
alter table public.club_contrib enable row level security;
alter table public.club_logos enable row level security;

create policy records_admin_read on public.records for select to authenticated using ((select private.is_admin()));
create policy clubs_admin_read on public.clubs for select to authenticated using ((select private.is_admin()));
create policy club_members_admin_read on public.club_members for select to authenticated using ((select private.is_admin()));
create policy club_weeks_admin_read on public.club_weeks for select to authenticated using ((select private.is_admin()));
create policy club_contrib_admin_read on public.club_contrib for select to authenticated using ((select private.is_admin()));
create policy club_logos_admin_read on public.club_logos for select to authenticated using ((select private.is_admin()));
