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
        static string FileOf(string id) => ReplayStorageIO.PathFor(Folder, id);

        public static IReadOnlyList<Entry> Entries => Load().entries;

        static Index Load()
        {
            if (_index != null) return _index;
            _index = new Index();
            bool repaired = false;
            try
            {
                if (File.Exists(IndexPath))
                {
                    string json = ReplayStorageIO.ReadBoundedUtf8(IndexPath, ReplayStorageIO.MaxIndexBytes);
                    _index = JsonUtility.FromJson<Index>(json) ?? new Index();
                }
                if (_index.entries == null)
                {
                    _index.entries = new List<Entry>();
                    repaired = true;
                }
                // Duplicate and unsafe index keys must not shadow files or
                // cause the UI to render the same replay multiple times.
                var unique = new HashSet<string>(StringComparer.Ordinal);
                if (_index.entries.RemoveAll(e => e == null ||
                    !ReplayStorageIO.IsSafeId(e.id) || !unique.Add(e.id) ||
                    !File.Exists(FileOf(e.id))) > 0)
                    repaired = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать список повторов: " + e.Message);
                _index = new Index();
                repaired = true;
            }

            // If the process stopped after writing a replay but before its
            // index, reconstruct the missing entry on the next startup.
            try
            {
                if (RecoverOrphans(_index) > 0 || repaired) SaveIndex();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось восстановить индекс повторов: " + e.Message);
            }
            return _index;
        }

        static int RecoverOrphans(Index index)
        {
            var known = new HashSet<string>(index.entries.Select(e => e.id), StringComparer.Ordinal);
            int recovered = 0;
            // Only safe, unindexed candidates are read; the scan is capped.
            foreach (string id in ReplayStorageIO.UnindexedIds(Folder, known))
            {
                var replay = Read(id);
                if (replay == null) continue;
                known.Add(id);
                index.entries.Add(EntryOf(replay));
                recovered++;
            }
            if (recovered > 0) Prune(index);
            return recovered;
        }

        static void SaveIndex()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                ReplayStorageIO.WriteAtomic(IndexPath, JsonUtility.ToJson(Load()));
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
                id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 16),
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

        static Entry EntryOf(ReplayFile replay) => new Entry
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
        };

        /// <summary>Положить повтор в хранилище (свой или скачанный).</summary>
        public static bool Store(ReplayFile replay)
        {
            if (replay == null || !ReplayStorageIO.IsSafeId(replay.id)) return false;
            try
            {
                Directory.CreateDirectory(Folder);
                // Load the prior index before writing this file, otherwise
                // first-use orphan recovery briefly indexes it twice.
                var index = Load();
                ReplayStorageIO.WriteAtomic(FileOf(replay.id), JsonUtility.ToJson(replay));
                index.entries.RemoveAll(e => e.id == replay.id);
                index.entries.Add(EntryOf(replay));
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
            // Keep the best for each author/map/mode, but cap the number of
            // protected groups too. Otherwise >Keep unique groups made the
            // documented storage limit impossible to enforce.
            var protectedIds = new HashSet<string>(index.entries
                .GroupBy(e => (e.mode, e.mapPath, (e.author ?? "").ToLowerInvariant()))
                .Select(g => g.OrderByDescending(e => e.authorScore)
                    .ThenByDescending(e => e.date, StringComparer.Ordinal).First())
                .OrderByDescending(e => e.date, StringComparer.Ordinal)
                .Take(Keep).Select(e => e.id));
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

        public static bool Exists(string id) => ReplayStorageIO.IsSafeId(id) && File.Exists(FileOf(id));

        public static ReplayFile Read(string id)
        {
            if (!Exists(id)) return null;
            try
            {
                string json = ReplayStorageIO.ReadBoundedUtf8(FileOf(id), ReplayStorageIO.MaxLocalReplayBytes);
                var replay = JsonUtility.FromJson<ReplayFile>(json);
                // The file name is the index key: never accept a replay that
                // silently claims a different key after disk corruption.
                return replay != null && replay.start != null && replay.id == id
                    ? replay : null;
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

        // Mirror server-side social/report-validator.mjs limits so a gzip
        // bomb returned from a compromised or stale endpoint cannot allocate
        // unbounded memory in the Unity client.
        public const int MaxCloudReplayBase64 = 700 * 1024;
        public const int MaxCloudReplayJsonBytes = 4 * 1024 * 1024;
        public const int MaxCloudReplayActions = 20000;

        static bool ValidReplayBase64(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxCloudReplayBase64 ||
                encoded.Length % 4 != 0) return false;

            int paddingAt = encoded.Length;
            for (int i = 0; i < encoded.Length; i++)
            {
                char c = encoded[i];
                if (c == '=')
                {
                    if (paddingAt == encoded.Length) paddingAt = i;
                    if (i < encoded.Length - 2) return false;
                }
                else
                {
                    if (paddingAt != encoded.Length) return false;
                    if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') &&
                        (c < '0' || c > '9') && c != '+' && c != '/')
                        return false;
                }
            }
            return encoded.Length - paddingAt <= 2;
        }

        // Keep this list aligned with VALID_COMMAND_KINDS in the server's
        // report-validator.mjs. Unknown actions cannot reach game replay.
        static readonly HashSet<string> AllowedCloudReplayActions =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "keep_initial", "draw_face_up", "draw_deck", "build", "build_free",
                "draw_contracts", "keep_contracts", "pass", "draw_luck",
                "choose_luck", "upgrade", "propose_trade", "respond_trade",
                "influence", "bid", "bribe", "accuse", "choose_character"
            };

        static bool ValidCloudReplay(ReplayFile replay)
        {
            if (replay == null || replay.version != ReplayFile.CurrentVersion ||
                (replay.mode != "classic" && replay.mode != "coop") ||
                !ReplayStorageIO.IsSafeId(replay.id) ||
                string.IsNullOrEmpty(replay.mapPath) ||
                replay.start == null || replay.start.players == null ||
                replay.players == null || replay.scores == null || replay.humans == null ||
                replay.players.Length < 1 || replay.players.Length > 8 ||
                replay.players.Length != replay.scores.Length ||
                replay.players.Length != replay.humans.Length ||
                replay.players.Length != replay.start.players.Length ||
                replay.authorSeat < 0 || replay.authorSeat >= replay.players.Length ||
                !replay.humans[replay.authorSeat] ||
                replay.scores[replay.authorSeat] != replay.authorScore ||
                replay.history == null || replay.history.Length == 0 ||
                replay.history.Length > MaxCloudReplayActions ||
                replay.turns < 0 || replay.turns > 1000000)
                return false;

            foreach (var command in replay.history)
                if (command == null || !AllowedCloudReplayActions.Contains(command.kind) ||
                    command.player < -1 || command.player >= replay.players.Length)
                    return false;
            return true;
        }

        /// <summary>
        /// Bounded cloud replay decode. The max decompressed *byte count* is
        /// enforced while reading the gzip stream, before JSON allocation.
        /// </summary>
        public static ReplayFile UnpackCloud(string packed)
        {
            if (!ValidReplayBase64(packed)) return null;
            try
            {
                var bytes = Convert.FromBase64String(packed);
                if (bytes.Length > MaxCloudReplayBase64) return null;
                using (var input = new MemoryStream(bytes))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int received;
                    while ((received = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length > MaxCloudReplayJsonBytes - received)
                            return null;
                        output.Write(buffer, 0, received);
                    }
                    string json = new UTF8Encoding(false, true).GetString(output.ToArray());
                    var replay = JsonUtility.FromJson<ReplayFile>(json);
                    return ValidCloudReplay(replay) ? replay : null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось распаковать облачный повтор: " + e.Message);
                return null;
            }
        }

        /// <summary>Legacy callers also use the bounded decoder.</summary>
        public static ReplayFile Unpack(string packed) => UnpackCloud(packed);
    }
}
