using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    /// <summary>Городские события: смена каждый круг, «Час пик», «Ремонт», «Субсидия мэрии», «Новый район».</summary>
    public class EventsTests
    {
        static Game Started(bool events = false) => TestCities.Started(TestCities.Small(), events: events);

        static void PassRound(Game game)
        {
            // Каждый игрок берёт по две карты из колоды — круг заканчивается.
            for (int i = 0; i < game.Players.Count; i++)
            {
                Assert.IsTrue(game.DrawCardFromDeck().Success);
                Assert.IsTrue(game.DrawCardFromDeck().Success);
            }
        }

        [Test]
        public void Events_FirstOneOpensWhenGameStarts()
        {
            var game = Started(events: true);
            Assert.IsNotNull(game.CurrentEvent);
            Assert.AreEqual(1, game.EventNumber);
        }

        [Test]
        public void Events_NewEventEveryRound()
        {
            var game = Started(events: true);
            PassRound(game);
            Assert.AreEqual(0, game.CurrentPlayerIndex);
            Assert.AreEqual(2, game.EventNumber);
            Assert.IsNotNull(game.CurrentEvent);
        }

        [Test]
        public void Events_Disabled_NoEvents()
        {
            var game = Started(events: false);
            Assert.IsNull(game.CurrentEvent);
            PassRound(game);
            Assert.IsNull(game.CurrentEvent);
        }

        [Test]
        public void RushHour_RoadsBuiltNowCostDoubleToll()
        {
            var game = Started();
            game.Debug_StartEvent(CityEventKind.RushHour);
            game.Debug_SetHand(0, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success); // мост Центр — Юг игрока 0

            Assert.IsTrue(game.IsRushHourRoad("r3"));
            Assert.AreEqual(4, game.TollFor("r3"));

            game.Debug_GiveRoad(1, "r1"); // игрок 1: Север — Центр, дальше по чужому мосту
            var check = game.CheckContract(1, game.Map.GetContract("k1"));
            Assert.IsTrue(check.Completed);
            Assert.AreEqual(4, check.Toll, "Проезд по магистрали часа пик вдвое дороже");
        }

        [Test]
        public void RushHour_DoesNotAffectRoadsBuiltLater()
        {
            var game = Started();
            game.Debug_StartEvent(CityEventKind.RushHour);
            PassRound(game); // события выключены — следующий круг без события
            game.Debug_SetHand(0, Resource.Steel, Resource.Steel);
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success);
            Assert.AreEqual(2, game.TollFor("r3"));
        }

        [Test]
        public void Repair_ClosesRoadUntilRoundEnds()
        {
            var game = Started();
            game.Debug_StartEvent(CityEventKind.Repair, "r1");
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);

            Assert.IsFalse(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            StringAssert.Contains("Ремонт", game.GetBuildBlocker("r1"));
            Assert.AreEqual(0, game.GetPaymentOptions("r1").Count);

            PassRound(game);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success, "После круга ремонт закончился");
        }

        [Test]
        public void Repair_PicksFreeOpenRoads()
        {
            var game = Started();
            game.Debug_GiveRoad(1, "r4");
            game.Debug_StartEvent(CityEventKind.Repair);

            var closed = game.CurrentEvent.ClosedRoads;
            Assert.AreEqual(game.Config.RepairRoads, closed.Count);
            foreach (var id in closed)
            {
                Assert.IsNull(game.GetRoadOwner(id), "Ремонт не трогает построенные дороги");
                Assert.IsTrue(game.IsUnderRepair(id));
            }
        }

        [Test]
        public void Subsidy_BridgeCostsOneCardLess()
        {
            var game = Started();
            game.Debug_SetHand(0, Resource.Steel);
            Assert.IsFalse(game.BuildRoad("r3", Resource.Steel, 0).Success, "Без субсидии мост длиной 2 стоит 2 стали");

            game.Debug_StartEvent(CityEventKind.Subsidy);
            Assert.AreEqual(1, game.BuildCost(game.Map.GetRoad("r3")));
            Assert.AreEqual(2, game.BuildCost(game.Map.GetRoad("r1")), "Обычные дороги не дешевеют");
            Assert.IsTrue(game.BuildRoad("r3", Resource.Steel, 0).Success);
            Assert.AreEqual(33, game.Players[0].RoadPieces, "Участков уходит по длине");
            Assert.AreEqual(2, game.Players[0].RoadPoints, "Очки — по длине");
        }

        [Test]
        public void NewDistrict_OpensRoadsAndPutsContractsOnTop()
        {
            var game = TestCities.Started(TestCities.SmallWithExpansion());
            Assert.IsFalse(game.IsDistrictOpen("far"));
            Assert.IsFalse(game.IsRoadOpen("r8"));
            StringAssert.Contains("не открыт", game.GetBuildBlocker("r8"));
            Assert.IsFalse(game.Players.Any(p => p.Contracts.Any(c => c.Id == "k7")), "Контракта нового района нет в начале");

            game.Debug_StartEvent(CityEventKind.NewDistrict, "far");

            Assert.IsTrue(game.IsDistrictOpen("far"));
            Assert.IsTrue(game.IsRoadOpen("r8"));
            Assert.IsTrue(game.DrawContracts().Success);
            Assert.IsTrue(game.Players[0].PendingContracts.Any(c => c.Id == "k7"), "Контракт нового района — наверху колоды");
        }

        [Test]
        public void NewDistrict_ClosedRoadsAreNotUsedByContracts()
        {
            var game = TestCities.Started(TestCities.SmallWithExpansion());
            Assert.IsFalse(game.GetPaymentOptions("r8").Any());
            game.Debug_SetHand(0, Resource.Gravel, Resource.Gravel);
            Assert.IsFalse(game.BuildRoad("r8", Resource.Gravel, 0).Success);
        }

        [Test]
        public void Events_SurviveSaveAndLoad()
        {
            var map = TestCities.Moscow();
            var game = new Game(map, new[] { "Аня", "Борис" }, null, 3);
            var bot = new RandomBot(3);
            for (int i = 0; i < 80 && game.Phase != GamePhase.GameOver; i++) Assert.IsTrue(bot.Act(game).Success);
            if (game.Phase == GamePhase.GameOver) Assert.Ignore("Партия закончилась раньше");

            var restored = Game.FromSave(map, game.CreateSave());
            Assert.AreEqual(game.EventNumber, restored.EventNumber);
            Assert.AreEqual(game.CurrentEvent?.Kind, restored.CurrentEvent?.Kind);
            Assert.AreEqual(game.EventDeckCount, restored.EventDeckCount);
            foreach (var district in map.Districts)
                Assert.AreEqual(game.IsDistrictOpen(district.Id), restored.IsDistrictOpen(district.Id), district.Id);
            foreach (var road in map.Roads)
            {
                Assert.AreEqual(game.TollFor(road.Id), restored.TollFor(road.Id), road.Id);
                Assert.AreEqual(game.IsUnderRepair(road.Id), restored.IsUnderRepair(road.Id), road.Id);
            }
        }
    }
}
