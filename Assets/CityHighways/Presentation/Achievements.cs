using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Что игрок сделал за партию — по этим фактам проверяются достижения и задание дня.</summary>
    public sealed class GameFacts
    {
        public bool Won;
        public int Players;
        public ScenarioKind Scenario;
        public CharacterKind Character;
        public int Total;
        public int Completed;
        public int Failed;
        public int Bridges;
        public int TollsEarned;
        public int NetworkDistricts;
        public int BribesGiven;
        public int CorrectAccusations;
        public int Upgrades;
        public bool FinalOrder;
        public bool MayorContractDone;
        public int MaxTraffic;
        public int LongestRoad;
        /// <summary>Кооператив «Город против всех» и сколько заказов команда просрочила.</summary>
        public bool Cooperative;
        public int CoopFailedOrders;
        /// <summary>Название карты партии (Москва, Амстердам, Чунцин).</summary>
        public string Map;

        public static GameFacts Of(Game game, int index)
        {
            var player = game.Players[index];
            var score = game.Result.Ranking.First(s => s.PlayerIndex == index);
            return new GameFacts
            {
                Won = game.Result.Winners.Any(w => w.PlayerIndex == index),
                Players = game.Players.Count,
                Scenario = game.Scenario,
                Character = player.Character,
                Total = score.Total,
                Completed = score.CompletedContracts,
                Failed = score.FailedContracts,
                Bridges = player.Roads.Count(r => r.IsBridge),
                TollsEarned = score.TollsEarned,
                NetworkDistricts = score.NetworkDistricts,
                BribesGiven = player.BribesGiven,
                CorrectAccusations = player.CorrectAccusations,
                Upgrades = player.Upgrades,
                FinalOrder = game.FinalOrderWinner == index,
                MayorContractDone = score.MayorBonus > 0,
                MaxTraffic = player.Roads.Count == 0 ? 0 : player.Roads.Max(r => game.TrafficOn(r.Id)),
                LongestRoad = player.Roads.Count == 0 ? 0 : player.Roads.Max(r => r.Length),
                Cooperative = game.IsCooperative,
                CoopFailedOrders = game.OrdersFailed,
                Map = game.Map.Name
            };
        }
    }

    /// <summary>
    /// Достижение: разовое (выполняется за одну партию — Check) или накопительное (Progress по профилю до Target).
    /// Каждое даёт влияние в кошелёк профиля один раз. На саму партию ничто из этого не влияет.
    /// </summary>
    public sealed class Achievement
    {
        public string Id;
        public string Title;
        public string Description;
        public int Reward;
        public Func<GameFacts, bool> Check;
        public Func<PlayerProfile, int> Progress;
        public int Target = 1;
        public bool Cumulative => Progress != null;
    }

    /// <summary>Задание дня: одно на всех в этот день, награда — раз в день на профиль.</summary>
    public sealed class DailyTask
    {
        public string Id;
        public string Title;
        public Func<GameFacts, bool> Check;
    }

    public static class Achievements
    {
        public const int DailyReward = 4;

        /// <summary>Сценарии, за победы в которых даётся «Все сезоны».</summary>
        static readonly ScenarioKind[] Seasons =
            { ScenarioKind.Winter, ScenarioKind.GreaterMoscow, ScenarioKind.RepairYear, ScenarioKind.Elections };

        /// <summary>Карты для «Турне» — по названиям из JSON.</summary>
        static readonly string[] Cities = { "Москва", "Амстердам", "Чунцин" };

        public static readonly IReadOnlyList<Achievement> All = new[]
        {
            new Achievement { Id = "first_contract", Title = "Первый подряд", Description = "Выполните контракт.", Reward = 3, Check = f => f.Completed > 0 },
            new Achievement { Id = "first_win", Title = "Первая победа", Description = "Выиграйте партию.", Reward = 5, Check = f => f.Won },
            new Achievement { Id = "bridges5", Title = "Мостостроитель", Description = "Постройте за партию 5 мостов.", Reward = 5, Check = f => f.Bridges >= 5 },
            new Achievement { Id = "tolls15", Title = "Король проезда", Description = "Получите за партию 15 очков за проезд от соперников.", Reward = 6, Check = f => f.TollsEarned >= 15 },
            new Achievement { Id = "clean4", Title = "Без провалов", Description = "Выполните 4 контракта и ни одного не провалите.", Reward = 6, Check = f => f.Completed >= 4 && f.Failed == 0 },
            new Achievement { Id = "honest_win", Title = "Чистые руки", Description = "Выиграйте партию на троих и больше, не дав ни одной взятки.", Reward = 6, Check = f => f.Won && !f.Cooperative && f.Players >= 3 && f.BribesGiven == 0 },
            new Achievement { Id = "whistle", Title = "Разоблачитель", Description = "Верно обвините соперника во взятке.", Reward = 4, Check = f => f.CorrectAccusations > 0 },
            new Achievement { Id = "final_order", Title = "Финальный заказ", Description = "Выполните финальный заказ мэрии.", Reward = 4, Check = f => f.FinalOrder },
            new Achievement { Id = "mayor", Title = "Ставленник мэрии", Description = "Выполните контракт, выигранный на аукционе.", Reward = 5, Check = f => f.MayorContractDone },
            new Achievement { Id = "network8", Title = "Весь город", Description = "Соберите сеть на 8 районов.", Reward = 6, Check = f => f.NetworkDistricts >= 8 },
            new Achievement { Id = "traffic3", Title = "Час пик", Description = "Пусть по одной вашей дороге едут 3 маршрута соперников.", Reward = 4, Check = f => f.MaxTraffic >= 3 },
            new Achievement { Id = "upgrades3", Title = "Магистрали", Description = "Улучшите за партию 3 дороги до магистрали.", Reward = 4, Check = f => f.Upgrades >= 3 },
            new Achievement { Id = "score130", Title = "Рекордсмен", Description = "Наберите за партию 130 очков.", Reward = 6, Check = f => f.Total >= 130 },
            new Achievement { Id = "big_win", Title = "Против всех", Description = "Выиграйте партию на пятерых (в соревновании).", Reward = 8, Check = f => f.Won && !f.Cooperative && f.Players >= 5 },
            new Achievement { Id = "coop_win", Title = "Город спасён", Description = "Выиграйте кооперативную партию «Город против всех».", Reward = 8, Check = f => f.Won && f.Cooperative },
            new Achievement { Id = "coop_clean", Title = "Ни одного срыва", Description = "Выиграйте кооператив, не просрочив ни одного заказа города.", Reward = 10, Check = f => f.Won && f.Cooperative && f.CoopFailedOrders == 0 },
            new Achievement
            {
                Id = "seasons", Title = "Все сезоны", Description = "Выиграйте в каждом сценарии: Зима, Большой город, Год ремонта, Выборы мэра.",
                Reward = 15, Target = Seasons.Length, Progress = p => Seasons.Count(s => p.HasMark("win_scenario:" + s))
            },
            new Achievement
            {
                Id = "companies", Title = "Все компании", Description = "Выиграйте за каждого персонажа.",
                Reward = 15, Target = Characters.All.Count, Progress = p => Characters.All.Count(c => p.HasMark("win_character:" + c))
            },
            new Achievement
            {
                Id = "travel", Title = "Турне", Description = "Сыграйте партию на каждой карте: Москва, Амстердам, Чунцин.",
                Reward = 10, Target = Cities.Length, Progress = p => Cities.Count(c => p.HasMark("map:" + c))
            },
            new Achievement
            {
                Id = "veteran", Title = "Ветеран", Description = "Сыграйте 25 партий.",
                Reward = 10, Target = 25, Progress = p => p.games
            }
        };

        public static readonly IReadOnlyList<DailyTask> DailyTasks = new[]
        {
            new DailyTask { Id = "d_contracts3", Title = "Выполните 3 контракта за партию", Check = f => f.Completed >= 3 },
            new DailyTask { Id = "d_bridges2", Title = "Постройте 2 моста", Check = f => f.Bridges >= 2 },
            new DailyTask { Id = "d_tolls4", Title = "Получите 4 очка за проезд от соперников", Check = f => f.TollsEarned >= 4 },
            new DailyTask { Id = "d_upgrade", Title = "Улучшите дорогу до магистрали", Check = f => f.Upgrades >= 1 },
            new DailyTask { Id = "d_win", Title = "Выиграйте партию", Check = f => f.Won },
            new DailyTask { Id = "d_long", Title = "Постройте дорогу длиной 4 и больше", Check = f => f.LongestRoad >= 4 },
            new DailyTask { Id = "d_final", Title = "Выполните финальный заказ мэрии", Check = f => f.FinalOrder },
            new DailyTask { Id = "d_network6", Title = "Соберите сеть на 6 районов", Check = f => f.NetworkDistricts >= 6 },
            new DailyTask { Id = "d_scenario", Title = "Сыграйте партию в любом сценарии, кроме обычных правил", Check = f => f.Scenario != ScenarioKind.Standard },
            new DailyTask { Id = "d_clean", Title = "Выполните 2 контракта и ни одного не провалите", Check = f => f.Completed >= 2 && f.Failed == 0 }
        };

        public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd");

        /// <summary>Группа достижения на странице профиля.</summary>
        public static string CategoryOf(Achievement achievement)
        {
            switch (achievement.Id)
            {
                case "first_contract":
                case "clean4":
                case "mayor":
                case "final_order":
                    return "Контракты";
                case "bridges5":
                case "network8":
                case "upgrades3":
                case "traffic3":
                    return "Стройка";
                case "first_win":
                case "honest_win":
                case "big_win":
                case "score130":
                case "coop_win":
                case "coop_clean":
                    return "Победы";
                case "whistle":
                case "tolls15":
                    return "Проезд и политика";
                default:
                    return "Коллекции";
            }
        }

        public static readonly string[] Categories = { "Контракты", "Стройка", "Победы", "Проезд и политика", "Коллекции" };

        /// <summary>Задание дня: одно для всех профилей в этот день, по номеру дня — порядок перемешан, но предсказуем.</summary>
        public static DailyTask TaskFor(DateTime date)
        {
            int day = (int)(date.Date - new DateTime(2026, 1, 1)).TotalDays;
            int index = (int)(((long)day * 7 + 3) % DailyTasks.Count);
            if (index < 0) index += DailyTasks.Count;
            return DailyTasks[index];
        }

        public static DailyTask Today => TaskFor(DateTime.Now);

        public static bool Unlocked(PlayerProfile profile, Achievement achievement) => profile.achievements.Contains(achievement.Id);

        public static int ProgressOf(PlayerProfile profile, Achievement achievement) =>
            achievement.Cumulative ? Math.Min(achievement.Target, achievement.Progress(profile)) : Unlocked(profile, achievement) ? 1 : 0;

        /// <summary>Итог партии для одного профиля: открытые достижения и задание дня.</summary>
        public sealed class Outcome
        {
            public readonly List<Achievement> Unlocked = new List<Achievement>();
            public bool DailyDone;
            public int Reward => Unlocked.Sum(a => a.Reward) + (DailyDone ? DailyReward : 0);
        }

        /// <summary>
        /// Учесть партию в профиле: отметить победы для накопительных достижений, открыть новые достижения,
        /// засчитать задание дня (раз в день) и положить награду в кошелёк. Сохраняет вызывающий.
        /// Счётчик партий профиля должен быть уже увеличен.
        /// </summary>
        public static Outcome Apply(PlayerProfile profile, GameFacts facts, DateTime now)
        {
            var outcome = new Outcome();
            if (!string.IsNullOrEmpty(facts.Map)) profile.AddMark("map:" + facts.Map);
            if (facts.Won)
            {
                profile.AddMark("win_scenario:" + facts.Scenario);
                if (facts.Character != CharacterKind.None) profile.AddMark("win_character:" + facts.Character);
            }
            foreach (var achievement in All)
            {
                if (Unlocked(profile, achievement)) continue;
                bool done = achievement.Cumulative ? achievement.Progress(profile) >= achievement.Target : achievement.Check(facts);
                if (!done) continue;
                profile.achievements.Add(achievement.Id);
                outcome.Unlocked.Add(achievement);
            }
            string today = DateKey(now);
            if (profile.dailyDone != today && TaskFor(now).Check(facts))
            {
                profile.dailyDone = today;
                profile.dailyTotal++;
                outcome.DailyDone = true;
            }
            profile.wallet += outcome.Reward;
            return outcome;
        }
    }
}
