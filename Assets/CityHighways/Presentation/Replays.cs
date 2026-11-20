using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Повтор партии: начальные условия (игроки, правила, зерно генератора, компании) и все действия по порядку.
    /// Партия детерминирована, поэтому по ним она воспроизводится точь-в-точь — так же, как загружается сохранение.
    /// Весит десятки килобайт: снимков состояния тут нет.
    /// </summary>
    [Serializable]
    public class ReplayFile
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string id = "";
        public string date = "";
        /// <summary>"classic" — соревнование, "coop" — кооператив (как GameStat.mode).</summary>
        public string mode = "classic";
        public string map = "";
        public string mapPath = "";
        public string scenario = "";
        /// <summary>Чей повтор: ник автора (профиль на этом компьютере или друг из облака).</summary>
        public string author = "";
        /// <summary>"local" — своя партия, "friend" — рекорд друга, "club" — партия клубного товарища.</summary>
        public string source = "local";
        /// <summary>Место автора за столом; -1 — неизвестно.</summary>
        public int authorSeat = -1;
        public int authorScore;
        public bool won;
        public bool online;
        public int seconds;
        public int turns;
        public string[] players;
        public int[] scores;
        public bool[] humans;
        public GameStartInfo start;
        public GameCommand[] history;

        public bool IsCoop => mode == "coop";
        public int Actions => history?.Length ?? 0;
        public string ModeTitle => IsCoop ? "Кооператив" : "Соревнование";
    }

    /// <summary>
    /// Хранилище повторов: папка replays рядом с сохранениями, небольшой индекс для списков и правило уборки —
    /// хранится не больше <see cref="Keep"/> повторов, но лучшая партия на каждой карте в каждом режиме не удаляется.
    /// </summary>
    public static class Replays
    {
        public const int Keep = 60;

        [Serializable]
        public class Entry
        {
            public string id;
            public string date;
            public string mode;
            public string map;
            public string mapPath;
            public string author;
            public int authorScore;
            public bool won;
            public int actions;
        }

        [Serializable]
        class Index
        {
            public List<Entry> entries = new List<Entry>();
        }

        /// <summary>Открыть повтор (выставляет приложение: показывает экран просмотра).</summary>
        public static Action<ReplayFile> Play;

        static Index _index;

        public static string Folder => Path.Combine(Application.persistentDataPath, "replays");
        static string IndexPath => Path.Combine(Folder, "index.json");
        static string FileOf(string id) => Path.Combine(Folder, id + ".json");

        public static IReadOnlyList<Entry> Entries => Load().entries;

        static Index Load()
        {
            if (_index != null) return _index;
            _index = new Index();
            try
            {
                if (File.Exists(IndexPath)) _index = JsonUtility.FromJson<Index>(File.ReadAllText(IndexPath)) ?? new Index();
                // Индекс потерян или устарел — убираем записи без файлов.
                _index.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id) || !File.Exists(FileOf(e.id)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать список повторов: " + e.Message);
                _index = new Index();
            }
            return _index;
        }

        static void SaveIndex()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(IndexPath, JsonUtility.ToJson(Load()));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось записать список повторов: " + e.Message);
            }
        }

        /// <summary>
        /// Собрать повтор законченной (или идущей) партии. null — у партии нет истории (загружена из очень старого
        /// сохранения) или это не партия на карте из Resources.
        /// </summary>
        public static ReplayFile Capture(Game game, GameSetup setup, float seconds, bool online, int localSeat = -1)
        {
            if (game?.StartInfo == null || game.History == null) return null;
            string mapPath = setup?.mapResourcePath ?? "";
            if (string.IsNullOrEmpty(mapPath)) return null;
            var start = JsonUtility.FromJson<GameStartInfo>(JsonUtility.ToJson(game.StartInfo));
            var players = game.Players.Select(p => p.Name).ToArray();
            var humans = game.Players.Select(p => setup == null || p.Index >= setup.players.Count || !setup.players[p.Index].isBot).ToArray();
            int[] scores = game.Result != null
                ? game.Players.Select(p => game.Result.Ranking.FirstOrDefault(s => s.PlayerIndex == p.Index)?.Total ?? 0).ToArray()
                : game.Players.Select(p => p.RoadPoints + p.GoalPoints + p.LuckPoints).ToArray();
            int seat = localSeat;
            if (seat < 0 || seat >= players.Length)
            {
                string me = Profiles.Active?.name ?? "";
                seat = Array.FindIndex(players, n => string.Equals(n, me, StringComparison.OrdinalIgnoreCase));
                if (seat < 0 || !humans[seat]) seat = Array.IndexOf(humans, true);
            }
            var winners = game.Result != null ? new HashSet<int>(game.Result.Winners.Select(w => w.PlayerIndex)) : new HashSet<int>();
            return new ReplayFile
            {
                id = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + UnityEngine.Random.Range(1000, 9999),
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                mode = game.IsCooperative ? "coop" : "classic",
                map = game.Map.Name,
                mapPath = mapPath,
                scenario = setup != null ? Scenarios.Title((ScenarioKind)Mathf.Clamp(setup.scenario, 0, Scenarios.All.Count - 1)) : "",
                author = seat >= 0 ? players[seat] : (Profiles.Active?.name ?? ""),
                authorSeat = seat,
                authorScore = seat >= 0 ? scores[seat] : 0,
                won = seat >= 0 && winners.Contains(seat),
                online = online,
                seconds = Mathf.RoundToInt(seconds),
                turns = game.TurnNumber,
                players = players,
                scores = scores,
                humans = humans,
                start = start,
                history = game.History.ToArray()
            };
        }

        /// <summary>Сохранить повтор партии на диск; возвращает его id (или null).</summary>
        public static string Save(Game game, GameSetup setup, float seconds, bool online, int localSeat = -1)
        {
            var replay = Capture(game, setup, seconds, online, localSeat);
            return replay != null && Store(replay) ? replay.id : null;
        }

        /// <summary>Положить повтор в хранилище (свой или скачанный).</summary>
        public static bool Store(ReplayFile replay)
        {
            if (replay == null || string.IsNullOrEmpty(replay.id)) return false;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FileOf(replay.id), JsonUtility.ToJson(replay));
                var index = Load();
                index.entries.RemoveAll(e => e.id == replay.id);
                index.entries.Add(new Entry
                {
                    id = replay.id,
                    date = replay.date,
                    mode = replay.mode,
                    map = replay.map,
                    mapPath = replay.mapPath,
                    author = replay.author,
                    authorScore = replay.authorScore,
                    won = replay.won,
                    actions = replay.Actions
                });
                Prune(index);
                SaveIndex();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось сохранить повтор: " + e.Message);
                return false;
            }
        }

        /// <summary>Лишние повторы — самые старые, кроме лучших по карте и режиму у каждого автора.</summary>
        static void Prune(Index index)
        {
            if (index.entries.Count <= Keep) return;
            var protectedIds = new HashSet<string>(index.entries
                .GroupBy(e => (e.mode, e.mapPath, (e.author ?? "").ToLowerInvariant()))
                .Select(g => g.OrderByDescending(e => e.authorScore).First().id));
            foreach (var old in index.entries.OrderBy(e => e.date, StringComparer.Ordinal).ToList())
            {
                if (index.entries.Count <= Keep) break;
                if (protectedIds.Contains(old.id)) continue;
                index.entries.Remove(old);
                try
                {
                    File.Delete(FileOf(old.id));
                }
                catch (Exception)
                {
                    // Файл занят или уже удалён — уберём в следующий раз.
                }
            }
        }

        public static bool Exists(string id) => !string.IsNullOrEmpty(id) && File.Exists(FileOf(id));

        public static ReplayFile Read(string id)
        {
            if (!Exists(id)) return null;
            try
            {
                var replay = JsonUtility.FromJson<ReplayFile>(File.ReadAllText(FileOf(id)));
                return replay?.start != null ? replay : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать повтор: " + e.Message);
                return null;
            }
        }

        /// <summary>Лучший сохранённый повтор автора в режиме (и на карте, если задана).</summary>
        public static Entry Best(string author, string mode, string mapPath = null) =>
            Load().entries
                .Where(e => e.mode == mode && (mapPath == null || e.mapPath == mapPath)
                            && string.Equals(e.author, author, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.authorScore).ThenByDescending(e => e.date, StringComparer.Ordinal)
                .FirstOrDefault();

        public static void Open(string id)
        {
            var replay = Read(id);
            if (replay != null) Play?.Invoke(replay);
        }

        public static void Clear()
        {
            try
            {
                if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось удалить повторы: " + e.Message);
            }
            _index = new Index();
        }

        // ---------------------------------------------------------------- Для облака: сжатие

        /// <summary>Повтор в компактном виде для отправки: JSON → gzip → base64 (в 8–10 раз меньше).</summary>
        public static string Pack(ReplayFile replay)
        {
            if (replay == null) return "";
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(replay));
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    gzip.Write(bytes, 0, bytes.Length);
                return Convert.ToBase64String(output.ToArray());
            }
        }

        public static ReplayFile Unpack(string packed)
        {
            if (string.IsNullOrEmpty(packed)) return null;
            try
            {
                var bytes = Convert.FromBase64String(packed);
                using (var input = new MemoryStream(bytes))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip, Encoding.UTF8))
                {
                    var replay = JsonUtility.FromJson<ReplayFile>(reader.ReadToEnd());
                    return replay?.start != null ? replay : null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось распаковать повтор: " + e.Message);
                return null;
            }
        }
    }
}
