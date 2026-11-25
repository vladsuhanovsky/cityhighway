using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    public enum CampaignGoal { Win, Contracts, Bridges, RoadPieces, TeamCrisis }

    public sealed class CampaignMission
    {
        public readonly string Id, Map, Ru, En;
        public readonly int Seed, Seats, Pieces, Target;
        public readonly ScenarioKind Scenario;
        public readonly bool Coop;
        public readonly CoopDifficultyKind Difficulty;
        public readonly CampaignGoal Goal;

        public CampaignMission(string id, string map, string ru, string en, int seed, int seats, int pieces,
            ScenarioKind scenario, bool coop, CoopDifficultyKind difficulty, CampaignGoal goal, int target)
        {
            Id = id; Map = "CityHighways/Maps/" + map; Ru = ru; En = en; Seed = seed; Seats = seats; Pieces = pieces;
            Scenario = scenario; Coop = coop; Difficulty = difficulty; Goal = goal; Target = target;
        }

        public GameSetup CreateSetup(string name, string owner, bool sound)
        {
            var setup = new GameSetup
            {
                magnate = false, cooperative = Coop, coopDifficulty = (int)Difficulty, scenario = (int)Scenario,
                roadPieces = Pieces, mapResourcePath = Map, hideCardsBetweenTurns = false, sound = sound,
                campaignMission = Id, campaignOwner = owner, turnSeconds = 0
            };
            setup.players.Add(new PlayerSlot(string.IsNullOrWhiteSpace(name) ? "Player" : name, false) { character = 0 });
            for (int i = 1; i < Seats; i++)
                setup.players.Add(new PlayerSlot("Bot " + i, true, BotLevel.Smart) { character = 0 });
            return setup;
        }

        public bool Matches(GameSetup setup) => setup != null && setup.IsCampaign && setup.campaignMission == Id &&
            !setup.magnate && !setup.tutorial && setup.cooperative == Coop && setup.coopDifficulty == (int)Difficulty &&
            setup.scenario == (int)Scenario && setup.roadPieces == Pieces && setup.mapResourcePath == Map &&
            setup.players != null && setup.players.Count == Seats && setup.players.All(p => p != null && p.character == 0) &&
            !setup.players[0].isBot && setup.players.Skip(1).All(p => p.isBot && p.botLevel == BotLevel.Smart);

        public bool Passed(Game game)
        {
            if (game?.Result == null || game.Phase != GamePhase.GameOver || game.IsCooperative != Coop) return false;
            var result = game.Result;
            if (Coop ? !result.TeamWon : !result.Winners.Any(w => w.PlayerIndex == 0)) return false;
            switch (Goal)
            {
                case CampaignGoal.Contracts: return result.Ranking.Any(r => r.PlayerIndex == 0 && r.CompletedContracts >= Target);
                case CampaignGoal.Bridges: return game.Players[0].Roads.Count(r => r.IsBridge) >= Target;
                case CampaignGoal.RoadPieces: return game.Players[0].Roads.Sum(r => r.Length) >= Target;
                case CampaignGoal.TeamCrisis: return game.Crisis <= Target;
                default: return true;
            }
        }
    }

    public static class CampaignCatalog
    {
        static CampaignMission M(int n, string map, string ru, string en, int seats, int pieces, ScenarioKind scenario,
            CampaignGoal goal, int target = 0, bool coop = false, CoopDifficultyKind difficulty = CoopDifficultyKind.Normal)
            => new CampaignMission("mission-" + n.ToString("00"), map, ru, en, 26000 + n * 137, seats, pieces, scenario, coop, difficulty, goal, target);

        public static readonly IReadOnlyList<CampaignMission> All = Array.AsReadOnly(new[]
        {
            M(1, "moscow", "Первый подряд", "First contract", 2, 18, ScenarioKind.Standard, CampaignGoal.Win),
            M(2, "amsterdam", "Через каналы", "Across the canals", 2, 20, ScenarioKind.Standard, CampaignGoal.Bridges, 1),
            M(3, "chongqing", "Горные маршруты", "Mountain routes", 2, 22, ScenarioKind.Standard, CampaignGoal.Contracts, 2),
            M(4, "moscow", "Зимний город", "Winter city", 2, 24, ScenarioKind.Winter, CampaignGoal.Win),
            M(5, "amsterdam", "Дороги на ремонте", "Roadworks", 3, 24, ScenarioKind.RepairYear, CampaignGoal.RoadPieces, 12),
            M(6, "chongqing", "Сезон поставок", "Supply season", 3, 26, ScenarioKind.SupplySeason, CampaignGoal.Contracts, 2),
            M(7, "moscow", "Большой город", "Growing city", 3, 30, ScenarioKind.GreaterMoscow, CampaignGoal.RoadPieces, 18),
            M(8, "amsterdam", "Городской совет", "City council", 3, 27, ScenarioKind.Elections, CampaignGoal.Win),
            M(9, "moscow", "Спасти город", "Save the city", 3, 30, ScenarioKind.Standard, CampaignGoal.Win, coop: true, difficulty: CoopDifficultyKind.Easy),
            M(10, "amsterdam", "Общая сеть", "Shared network", 3, 30, ScenarioKind.Standard, CampaignGoal.Win, coop: true),
            M(11, "chongqing", "Кризис поставок", "Supply crisis", 3, 32, ScenarioKind.SupplySeason, CampaignGoal.TeamCrisis, 8, true),
            M(12, "moscow", "Финальный вызов", "Final challenge", 4, 35, ScenarioKind.Standard, CampaignGoal.Win, coop: true, difficulty: CoopDifficultyKind.Hard)
        });

        public static CampaignMission Find(string id) => All.FirstOrDefault(m => m.Id == id);
        public static int Index(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return i;
            return -1;
        }

        public static bool Unlocked(string id, int completed)
        {
            int index = Index(id);
            return index >= 0 && index <= Math.Max(0, Math.Min(All.Count, completed));
        }

        public static int Advance(string id, int completed)
        {
            completed = Math.Max(0, Math.Min(All.Count, completed));
            return Index(id) == completed ? completed + 1 : completed;
        }
    }
}
