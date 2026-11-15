using System;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>Достижения и задание дня: открытие один раз, награда в кошелёк, накопительные, факты партии.</summary>
    public class AchievementsTests
    {
        static readonly DateTime Day = new DateTime(2026, 11, 15);

        [Test]
        public void OneGameAchievement_UnlocksOnce_PaysOnce()
        {
            var profile = new PlayerProfile();
            var facts = new GameFacts { Completed = 1, Failed = 1 };
            var first = Achievements.Apply(profile, facts, Day);
            Assert.IsTrue(first.Unlocked.Any(a => a.Id == "first_contract"));
            int wallet = profile.wallet;
            Assert.AreEqual(first.Reward, wallet);

            var again = Achievements.Apply(profile, facts, Day);
            Assert.IsFalse(again.Unlocked.Any(a => a.Id == "first_contract"), "Второй раз не открывается");
            Assert.AreEqual(wallet + again.Reward, profile.wallet);
        }

        [Test]
        public void Daily_OncePerDay_NextDayAgain()
        {
            var task = Achievements.TaskFor(Day);
            Assert.AreSame(task, Achievements.TaskFor(Day.AddHours(5)), "Весь день одно задание");
            var facts = FactsFor(task);
            var profile = new PlayerProfile();
            Assert.IsTrue(Achievements.Apply(profile, facts, Day).DailyDone);
            Assert.IsFalse(Achievements.Apply(profile, facts, Day.AddHours(3)).DailyDone, "Раз в день");
            Assert.AreEqual(1, profile.dailyTotal);

            var tomorrow = Day.AddDays(1);
            Assert.IsTrue(Achievements.Apply(profile, FactsFor(Achievements.TaskFor(tomorrow)), tomorrow).DailyDone);
            Assert.AreEqual(2, profile.dailyTotal);
        }

        [Test]
        public void Daily_AllTasksAppear()
        {
            var seen = Enumerable.Range(0, Achievements.DailyTasks.Count).Select(d => Achievements.TaskFor(Day.AddDays(d)).Id).Distinct().Count();
            Assert.AreEqual(Achievements.DailyTasks.Count, seen, "За столько дней — все задания по разу");
        }

        /// <summary>Факты, которые выполняют задание (перебор простых вариантов).</summary>
        static GameFacts FactsFor(DailyTask task)
        {
            var facts = new GameFacts
            {
                Won = true, Completed = 4, Failed = 0, Bridges = 3, TollsEarned = 5, Upgrades = 1, LongestRoad = 5,
                FinalOrder = true, NetworkDistricts = 7, Scenario = ScenarioKind.Winter, Players = 2
            };
            Assert.IsTrue(task.Check(facts), task.Id);
            return facts;
        }

        [Test]
        public void Seasons_Cumulative_UnlocksAfterAllScenarios()
        {
            var profile = new PlayerProfile();
            var seasons = Achievements.All.First(a => a.Id == "seasons");
            foreach (var scenario in new[] { ScenarioKind.Winter, ScenarioKind.GreaterMoscow, ScenarioKind.RepairYear })
                Achievements.Apply(profile, new GameFacts { Won = true, Scenario = scenario }, Day);
            Assert.IsFalse(Achievements.Unlocked(profile, seasons));
            Assert.AreEqual(3, Achievements.ProgressOf(profile, seasons));
            Achievements.Apply(profile, new GameFacts { Won = false, Scenario = ScenarioKind.Elections }, Day);
            Assert.IsFalse(Achievements.Unlocked(profile, seasons), "Проигрыш не считается");
            var outcome = Achievements.Apply(profile, new GameFacts { Won = true, Scenario = ScenarioKind.Elections }, Day);
            Assert.IsTrue(outcome.Unlocked.Contains(seasons));
        }

        [Test]
        public void Veteran_CountsProfileGames()
        {
            var profile = new PlayerProfile { games = 24 };
            var veteran = Achievements.All.First(a => a.Id == "veteran");
            Achievements.Apply(profile, new GameFacts(), Day);
            Assert.IsFalse(Achievements.Unlocked(profile, veteran));
            profile.games = 25;
            Assert.IsTrue(Achievements.Apply(profile, new GameFacts(), Day).Unlocked.Contains(veteran));
        }

        [Test]
        public void Facts_FromBotGame()
        {
            var game = TestCities.Started(TestCities.Moscow(), 3, new GameConfig(), seed: 61, events: true);
            var bots = Enumerable.Range(0, 3).Select(i => (IBot)new SmartBot(i + 5)).ToArray();
            for (int step = 0; step < 5000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            for (int i = 0; i < 3; i++)
            {
                var facts = GameFacts.Of(game, i);
                var score = game.Result.Ranking.First(s => s.PlayerIndex == i);
                Assert.AreEqual(score.Total, facts.Total);
                Assert.AreEqual(3, facts.Players);
                Assert.AreEqual(game.Players[i].Roads.Count(r => r.IsBridge), facts.Bridges);
                Assert.GreaterOrEqual(facts.LongestRoad, 1);
            }
            Assert.IsTrue(game.Result.Winners.All(w => GameFacts.Of(game, w.PlayerIndex).Won));
        }

        [Test]
        public void Ids_Unique()
        {
            Assert.AreEqual(Achievements.All.Count, Achievements.All.Select(a => a.Id).Distinct().Count());
            Assert.AreEqual(Achievements.DailyTasks.Count, Achievements.DailyTasks.Select(t => t.Id).Distinct().Count());
            Assert.IsTrue(Achievements.All.All(a => a.Reward > 0 && (a.Cumulative || a.Check != null)));
        }
    }
}
