-- City Highways: профили игроков (Steam, позже Epic), действия администратора, администраторы.
-- Итоговое состояние после миграций players_admin_schema и private_helper_functions (уже применены к проекту
-- cityhighways, nswqxnjyxlrtfzdlounk). Для нового проекта — выполнить целиком.

create table public.players (
  id text primary key,                         -- 'steam:<SteamID64>' или 'epic:<ProductUserId>'
  platform text not null check (platform in ('steam', 'epic')),
  platform_user_id text not null,              -- совпадает с ID профиля в магазине
  name text not null default '',
  verified boolean not null default false,     -- вход подтверждён сервером (билет Steam / токен Epic)
  wallet integer not null default 0,
  earned_total integer not null default 0,
  games integer not null default 0,
  wins integer not null default 0,
  containers_opened integer not null default 0,
  owned jsonb not null default '[]'::jsonb,
  outfit text not null default '',
  emblem text not null default '',
  title text not null default '',
  frame text not null default '',
  achievements jsonb not null default '[]'::jsonb,
  profile jsonb not null default '{}'::jsonb,
  game_version text not null default '',
  banned boolean not null default false,
  ban_reason text not null default '',
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  last_seen timestamptz not null default now(),
  unique (platform, platform_user_id)
);
create index players_name_idx on public.players (lower(name));
create index players_last_seen_idx on public.players (last_seen desc);

create table public.admins (
  user_id uuid primary key references auth.users (id) on delete cascade,
  created_at timestamptz not null default now()
);

create table public.admin_actions (
  id bigint generated always as identity primary key,
  player_id text not null references public.players (id) on delete cascade,
  kind text not null check (kind in ('grant_influence', 'set_wallet', 'grant_item', 'revoke_item', 'message')),
  amount integer not null default 0,
  item_id text not null default '',
  note text not null default '',
  created_by uuid references auth.users (id) on delete set null,
  created_at timestamptz not null default now(),
  applied_at timestamptz
);
create index admin_actions_pending_idx on public.admin_actions (player_id) where applied_at is null;

create schema if not exists private;
grant usage on schema private to authenticated;

create or replace function private.is_admin()
returns boolean language sql stable security definer set search_path = '' as $$
  select exists (select 1 from public.admins where user_id = (select auth.uid()));
$$;
revoke all on function private.is_admin() from public, anon;
grant execute on function private.is_admin() to authenticated;

alter table public.players enable row level security;
alter table public.admins enable row level security;
alter table public.admin_actions enable row level security;

create policy "admins read players" on public.players for select to authenticated using ((select private.is_admin()));
create policy "admins update players" on public.players for update to authenticated using ((select private.is_admin())) with check ((select private.is_admin()));
create policy "admins delete players" on public.players for delete to authenticated using ((select private.is_admin()));
create policy "admins read actions" on public.admin_actions for select to authenticated using ((select private.is_admin()));
create policy "admins add actions" on public.admin_actions for insert to authenticated with check ((select private.is_admin()));
create policy "admins delete actions" on public.admin_actions for delete to authenticated using ((select private.is_admin()) and applied_at is null);
create policy "admins see admins" on public.admins for select to authenticated using ((select private.is_admin()));

create or replace function private.admin_actions_set_author()
returns trigger language plpgsql set search_path = '' as $$
begin
  new.created_by := (select auth.uid());
  return new;
end;
$$;
revoke all on function private.admin_actions_set_author() from public, anon, authenticated;
create trigger admin_actions_author before insert on public.admin_actions
  for each row execute function private.admin_actions_set_author();

create or replace function private.touch_updated_at()
returns trigger language plpgsql set search_path = '' as $$
begin
  new.updated_at := now();
  return new;
end;
$$;
revoke all on function private.touch_updated_at() from public, anon, authenticated;
create trigger players_touch before update on public.players
  for each row execute function private.touch_updated_at();
