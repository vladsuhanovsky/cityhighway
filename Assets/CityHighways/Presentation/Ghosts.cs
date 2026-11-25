using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Призрак — чужой (или свой) лучший результат на карте в режиме: «Твой друг Влад набрал 97 очков на карте
    /// «Москва». Сможешь побить его рекорд?». С повтором его партии можно сыграть рейд: та же карта, то же зерно
    /// колоды, те же соперники и компании — партия начинается точь-в-точь как у него, дальше всё решают ваши ходы.
    /// </summary>
    public sealed class Ghost
    {
        public string Name = "";
        public int Score;
        public bool Won;
        public string Mode = "classic";
        public string MapPath = "";
        public string MapName = "";
        /// <summary>"friend" — друг Steam, "club" — клубный товарищ, "local" — другой профиль на этом компьютере, "self" — свой рекорд.</summary>
        public string Source = "local";
        public string Date = "";
        /// <summary>Облачный рекорд (друг, клуб); 0 — местный.</summary>
        public long RecordId;
        /// <summary>Местный повтор (Replays).</summary>
        public string ReplayId;
        /// <summary>Повтор, загруженный для рейда.</summary>
        public ReplayFile Replay;
        /// <summary>Cloud replay must not be reused after a Steam session switch.</summary>
        public string SteamOwner;
        public int SteamGeneration;
        /// <summary>Счёт призрака (дороги + цели + удача) к началу каждого круга — для сравнения по ходу рейда.</summary>
        public int[] ScoreByRound;

        public bool IsCoop => Mode == "coop";
        public string ModeTitle => IsCoop ? "в кооперативе" : "в соревновании";

        /// <summary>Как назвать автора в баннере.</summary>
        public string Who
        {
            get
            {
                switch (Source)
                {
                    case "friend": return $"Твой друг {Name}";
                    case "club": return $"Клубный товарищ {Name}";
                    case "self": return "Ваш личный рекорд —";
                    default: return $"{Name} (на этом компьютере)";
                }
            }
        }

        public string Challenge =>
            Source == "self"
                ? $"Ваш личный рекорд на карте «{MapName}» {ModeTitle} — {Score} очк. Сможете побить его?"
                : $"{Who} набрал {Score} очк. на карте «{MapName}» {ModeTitle}. Сможешь побить его рекорд?";

        public static Ghost FromRecord(SocialCloud.RecordInfo record, string source) => new Ghost
        {
            Name = record.name,
            Score = record.score,
            Won = record.won,
            Mode = record.mode,
            MapPath = record.mapPath,
            MapName = record.mapName,
            Source = source,
            Date = record.date,
            RecordId = record.id,
            SteamOwner = SocialCloud.CurrentSteamAccount,
            SteamGeneration = SocialCloud.CurrentAccountGeneration
        };

        public static Ghost FromEntry(Replays.Entry entry, bool self) => new Ghost
        {
            Name = entry.author,
            Score = entry.authorScore,
            Won = entry.won,
            Mode = entry.mode,
            MapPath = entry.mapPath,
            MapName = entry.map,
            Source = self ? "self" : "local",
            Date = entry.date,
            ReplayId = entry.id
        };

        /// <summary>Загрузить повтор призрака (местный — с диска, облачный — с сервера).</summary>
        public void LoadReplay(Action<ReplayFile> done, Action<string> failed)
        {
            if ((Source == "friend" || Source == "club") &&
                !SocialCloud.BelongsToCurrentSession(SteamOwner, SteamGeneration))
            {
                failed?.Invoke(LocalizationService.Language == "en"
                    ? "This friend's replay belongs to a previous Steam session."
                    : "Повтор друга относится к предыдущему Steam-сеансу.");
                return;
            }

            if (Replay != null)
            {
                done?.Invoke(Replay);
                return;
            }
            if (!string.IsNullOrEmpty(ReplayId))
            {
                Replay = Replays.Read(ReplayId);
                if (Replay != null) done?.Invoke(Replay);
                else failed?.Invoke("Повтор этой партии не сохранился.");
                return;
            }
            if (RecordId > 0)
            {
                SocialCloud.FetchReplay(new SocialCloud.RecordInfo { id = RecordId, name = Name }, replay =>
                {
                    replay.source = Source == "club" ? "club" : "friend";
                    Replay = replay;
                    done?.Invoke(replay);
                }, failed);
                return;
            }
            failed?.Invoke("У этого рекорда нет повтора.");
        }

        /// <summary>Посчитать счёт призрака по кругам, проиграв его партию (быстро: только действия, без интерфейса).</summary>
        public void BuildTimeline(CityMap map)
        {
            ScoreByRound = null;
            var replay = Replay;
            if (replay?.start == null || map == null || replay.authorSeat < 0) return;
            try
            {
                var start = replay.start;
                var characters = (start.characters ?? new int[start.players.Length]).Select(c => (CharacterKind)c).ToList();
                var game = new Game(map, start.players, start.config, start.seed, characters);
                int players = game.Players.Count;
                var scores = new List<int> { 0 };
                int AuthorScore() => game.Players[replay.authorSeat].RoadPoints + game.Players[replay.authorSeat].GoalPoints + game.Players[replay.authorSeat].LuckPoints;
                foreach (var command in replay.history ?? new GameCommand[0])
                {
                    if (!game.Execute(command).Success) break;
                    if (game.Phase == GamePhase.SetupContracts) continue;
                    int round = (game.TurnNumber - 1) / Math.Max(1, players) + 1;
                    while (scores.Count <= round) scores.Add(AuthorScore());
                    scores[round] = AuthorScore();
                }
                ScoreByRound = scores.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось посчитать ход партии призрака: " + e.Message);
            }
        }
    }

    /// <summary>Где искать призраков: друзья Steam, клуб, другие профили этого компьютера, свой рекорд.</summary>
    public static class Ghosts
    {
        /// <summary>Все призраки на карте в режиме — лучший результат каждого автора, от большего счёта к меньшему.</summary>
        public static List<Ghost> For(string mode, string mapPath)
        {
            // Ensure any social caches from an old Steam account are cleared
            // before reading them into the new game's ghost selection.
            _ = SocialCloud.CurrentSteamAccount;
            var list = new List<Ghost>();
            if (string.IsNullOrEmpty(mapPath)) return list;
            string me = Profiles.Active?.name ?? "";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in SocialCloud.FriendRecords.Where(r => r.mode == mode && r.mapPath == mapPath).OrderByDescending(r => r.score))
                if (seen.Add("f:" + record.playerId)) list.Add(Ghost.FromRecord(record, "friend"));

            var club = SocialCloud.Club;
            if (club != null && club.HasClub && club.records != null)
            {
                var mine = club.members?.FirstOrDefault(m => m.me)?.name ?? me;
                foreach (var record in club.records.Where(r => r.mode == mode && r.mapPath == mapPath).OrderByDescending(r => r.score))
                {
                    if (string.Equals(record.name, mine, StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add("f:" + record.playerId)) list.Add(Ghost.FromRecord(record, "club"));
                }
            }

            // Другие профили на этом компьютере (горячий стул) — по сохранённым повторам.
            foreach (var group in Replays.Entries.Where(e => e.mode == mode && e.mapPath == mapPath && !string.IsNullOrEmpty(e.author))
                         .GroupBy(e => e.author, StringComparer.OrdinalIgnoreCase))
            {
                var best = group.OrderByDescending(e => e.authorScore).First();
                bool self = string.Equals(best.author, me, StringComparison.OrdinalIgnoreCase);
                if (self) continue;
                if (seen.Add("l:" + best.author)) list.Add(Ghost.FromEntry(best, false));
            }

            list = list.OrderByDescending(g => g.Score).ToList();
            // Свой рекорд — в конце: им можно бросить вызов самому себе, если других нет.
            var own = Replays.Best(me, mode, mapPath);
            if (own != null) list.Add(Ghost.FromEntry(own, true));
            return list;
        }

        /// <summary>Главный призрак для баннера: лучший чужой результат, иначе свой рекорд.</summary>
        public static Ghost Best(string mode, string mapPath) => For(mode, mapPath).FirstOrDefault();
    }
}
