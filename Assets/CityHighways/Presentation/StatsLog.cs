using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public class PlayerStat
    {
        public string name;
        /// <summary>human, smart или random.</summary>
        public string kind;
        public int total;
        public int roadPoints;
        public int contractPoints;
        public int completedContracts;
        public int failedContracts;
        public int tollsPaid;
        public int tollsEarned;
        public int jamFees;
        public int networkDistricts;
        public int roadPiecesLeft;
        public bool winner;
        /// <summary>Название персонажа («» — без персонажа).</summary>
        public string character;
        public int goalPoints;
        public int luckPoints;
        public int characterPoints;
    }

    [Serializable]
    public class GameStat
    {
        public string date;
        public string map;
        public int turns;
        public int seconds;
        public PlayerStat[] results;
    }

    /// <summary>Итоги каждой партии — по строке JSON в cityhighways_stats.jsonl (удобно смотреть баланс).</summary>
    public static class StatsLog
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "cityhighways_stats.jsonl");

        public static void Append(Game game, GameSetup setup, float seconds)
        {
            if (game?.Result == null) return;
            try
            {
                var winners = new HashSet<int>(game.Result.Winners.Select(w => w.PlayerIndex));
                var stat = new GameStat
                {
                    date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    map = game.Map.Name,
                    turns = game.TurnNumber,
                    seconds = Mathf.RoundToInt(seconds),
                    results = game.Result.Ranking.Select(s => new PlayerStat
                    {
                        name = s.PlayerName,
                        kind = Kind(setup, s.PlayerIndex),
                        total = s.Total,
                        roadPoints = s.RoadPoints,
                        contractPoints = s.ContractPoints,
                        completedContracts = s.CompletedContracts,
                        failedContracts = s.FailedContracts,
                        tollsPaid = s.TollsPaid,
                        tollsEarned = s.TollsEarned,
                        jamFees = s.JamFees,
                        networkDistricts = s.NetworkDistricts,
                        roadPiecesLeft = game.Players[s.PlayerIndex].RoadPieces,
                        winner = winners.Contains(s.PlayerIndex),
                        character = s.Character == CharacterKind.None ? "" : Characters.Title(s.Character),
                        goalPoints = s.GoalPoints,
                        luckPoints = s.LuckPoints,
                        characterPoints = s.CharacterPoints
                    }).ToArray()
                };
                File.AppendAllText(FilePath, JsonUtility.ToJson(stat) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось записать статистику: " + e.Message);
            }
        }

        static string Kind(GameSetup setup, int index)
        {
            if (setup == null || index >= setup.players.Count || !setup.players[index].isBot) return "human";
            return setup.players[index].botLevel == BotLevel.Random ? "random" : "smart";
        }

        /// <summary>Все записанные партии, от старых к новым. Битые строки пропускаются.</summary>
        public static List<GameStat> LoadAll()
        {
            var list = new List<GameStat>();
            try
            {
                if (!File.Exists(FilePath)) return list;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var stat = JsonUtility.FromJson<GameStat>(line);
                        if (stat?.results != null && stat.results.Length > 0) list.Add(stat);
                    }
                    catch
                    {
                        // Строка повреждена — пропускаем.
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать статистику: " + e.Message);
            }
            return list;
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось удалить статистику: " + e.Message);
            }
        }

        public static string Summary()
        {
            try
            {
                if (!File.Exists(FilePath)) return "";
                int games = 0, humanGames = 0, humanWins = 0;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var stat = JsonUtility.FromJson<GameStat>(line);
                    if (stat?.results == null) continue;
                    games++;
                    if (!stat.results.Any(r => r.kind == "human")) continue;
                    humanGames++;
                    if (stat.results.Any(r => r.kind == "human" && r.winner)) humanWins++;
                }
                if (games == 0) return "";
                return humanGames > 0
                    ? $"Сыграно партий: {games} · побед людей: {humanWins} из {humanGames}"
                    : $"Сыграно партий: {games}";
            }
            catch
            {
                return "";
            }
        }
    }
}
