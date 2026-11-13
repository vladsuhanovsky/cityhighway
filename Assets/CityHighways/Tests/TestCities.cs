using System.IO;
using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>
    /// Маленький город для тестов:
    ///
    ///   n1 ──r1(2, асфальт)── c1 ──r3(2, мост)── s1
    ///   n2 ──r2(1, любой)───┘  │                  │ r6(1, асфальт)
    ///   n2 ──r7(4, бетон)── e1 ┴─r4(3, бетон)     s2
    ///                       e1 ──r5(2, щебень)── s2
    ///
    /// Районы: north (n1, n2), center (c1), south (s1, s2), east (e1).
    /// </summary>
    internal static class TestCities
    {
        public static CityMap Small()
        {
            var districts = new[]
            {
                new District("north", "Север"), new District("center", "Центр"),
                new District("south", "Юг"), new District("east", "Восток")
            };
            var locations = new[]
            {
                new Location("n1", "Север-1", "north", 0.2f, 0.1f),
                new Location("n2", "Север-2", "north", 0.4f, 0.1f),
                new Location("c1", "Центр", "center", 0.3f, 0.4f),
                new Location("s1", "Юг-1", "south", 0.2f, 0.8f),
                new Location("s2", "Юг-2", "south", 0.5f, 0.8f),
                new Location("e1", "Восток", "east", 0.8f, 0.4f)
            };
            var roads = new[]
            {
                new Road("r1", "n1", "c1", 2, Resource.Asphalt),
                new Road("r2", "n2", "c1", 1, null),
                new Road("r3", "c1", "s1", 2, Resource.Steel),
                new Road("r4", "c1", "e1", 3, Resource.Concrete),
                new Road("r5", "e1", "s2", 2, Resource.Gravel),
                new Road("r6", "s1", "s2", 1, Resource.Asphalt),
                new Road("r7", "n2", "e1", 4, Resource.Concrete)
            };
            var contracts = new[]
            {
                new Contract("k1", "north", "south", 5),
                new Contract("k2", "north", "east", 6),
                new Contract("k3", "center", "east", 4),
                new Contract("k4", "south", "east", 3),
                new Contract("k5", "north", "center", 3),
                new Contract("k6", "center", "south", 3)
            };
            return new CityMap("Тестоград", districts, locations, roads, contracts);
        }

        /// <summary>Тот же город плюс закрытый район «Окраина» (место f1, дорога r8 от Юг-2, контракт k7).</summary>
        public static CityMap SmallWithExpansion()
        {
            var small = Small();
            var districts = small.Districts.Concat(new[] { new District("far", "Окраина", true) });
            var locations = small.Locations.Concat(new[] { new Location("f1", "Окраина", "far", 0.9f, 0.9f) });
            var roads = small.Roads.Concat(new[] { new Road("r8", "s2", "f1", 2, Resource.Gravel) });
            var contracts = small.Contracts.Concat(new[] { new Contract("k7", "far", "north", 8) });
            return new CityMap("Тестоград+", districts, locations, roads, contracts);
        }

        /// <summary>
        /// Тот же город плюс сложные контракты:
        /// k8 — транзит Север — Юг через Восток; k9 — узел Центр с Севером и Востоком;
        /// k10 — узел Восток с Севером и Югом.
        /// </summary>
        public static CityMap SmallWithComplex()
        {
            var small = Small();
            var contracts = small.Contracts.Concat(new[]
            {
                new Contract("k8", "north", "south", 9, ContractKind.Transit, "east"),
                new Contract("k9", "center", "north", 7, ContractKind.Hub, "east"),
                new Contract("k10", "east", "north", 9, ContractKind.Hub, "south")
            });
            return new CityMap("Тестоград-сложный", small.Districts, small.Locations, small.Roads, contracts);
        }

        /// <summary>
        /// Тот же город с целями мэрии: t1 — свои дороги в обоих местах Севера (+5),
        /// t2 — один мост (+4), t3 — дорога длиной 3 и больше (+3).
        /// </summary>
        public static CityMap SmallWithGoals()
        {
            var small = Small();
            var goals = new[]
            {
                new MayorGoal("t1", "Весь Север", GoalKind.Locations, 2, 5, "north"),
                new MayorGoal("t2", "Мост", GoalKind.Bridges, 1, 4),
                new MayorGoal("t3", "Длинная дорога", GoalKind.LongRoads, 1, 3, length: 3)
            };
            return new CityMap("Тестоград-цели", small.Districts, small.Locations, small.Roads, small.Contracts, null, goals);
        }

        public static CityMap Moscow()
        {
            var path = Path.Combine(Application.dataPath, "CityHighways/Resources/CityHighways/Maps/moscow.json");
            return JsonUtility.FromJson<CityMapDto>(File.ReadAllText(path)).ToMap();
        }

        /// <summary>
        /// Партия после выбора стартовых контрактов (каждый оставил первые два).
        /// События по умолчанию выключены, чтобы случайный «Ремонт» не мешал проверкам правил.
        /// </summary>
        public static Game Started(CityMap map, int players = 2, GameConfig config = null, int seed = 42, bool events = false)
        {
            config = config ?? new GameConfig();
            config.EventsEnabled = events;
            if (events) config.EventChance = 1f; // тесты событий ждут событие в каждом круге
            var names = Enumerable.Range(1, players).Select(i => "Игрок " + i).ToList();
            var game = new Game(map, names, config, seed);
            foreach (var player in game.Players)
            {
                if (player.PendingContracts.Count == 0) continue;
                var keep = player.PendingContracts.Take(2).Select(c => c.Id).ToList();
                Assert.IsTrue(game.KeepInitialContracts(player.Index, keep).Success);
            }
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            return game;
        }
    }
}
