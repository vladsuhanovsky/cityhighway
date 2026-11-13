using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Сделки между игроками, улучшение дорог и новые события (Парад, Снегопад, Инвестор).</summary>
    public class DealsUpgradesEventsTests
    {
        static Game Fresh() => TestCities.Started(TestCities.Small());

        // ================================================================== Сделки

        [Test]
        public void Trade_Accepted_SwapsCards_AndDoesNotSpendTurn()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            game.Debug_SetHand(1, Resource.Steel);
            int total = game.Debug_CountAllCards();

            Assert.IsTrue(game.CanProposeTrade());
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 2, Resource.Steel, 1).Success);
            Assert.AreEqual(GamePhase.RespondingTrade, game.Phase);
            Assert.AreEqual(1, game.ActingPlayerIndex, "Отвечает получатель");
            Assert.AreEqual(0, game.CurrentPlayerIndex, "Ход по-прежнему у предложившего");

            Assert.IsTrue(game.RespondTrade(true).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            Assert.AreEqual(0, game.ActingPlayerIndex);
            Assert.AreEqual(1, game.Players[0].CardCount(Resource.Steel));
            Assert.AreEqual(0, game.Players[0].CardCount(Resource.Asphalt));
            Assert.AreEqual(2, game.Players[1].CardCount(Resource.Asphalt));
            Assert.AreEqual(total, game.Debug_CountAllCards());
            Assert.IsTrue(game.LastTradeAccepted);

            Assert.IsFalse(game.CanProposeTrade(), "Одна сделка за ход");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Steel, 1, Resource.Asphalt, 1).Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success, "Основное действие ещё доступно");
        }

        [Test]
        public void Trade_Declined_ChangesNothing()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt);
            game.Debug_SetHand(1, Resource.Steel);
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 1).Success);
            Assert.IsTrue(game.RespondTrade(false).Success);
            Assert.AreEqual(1, game.Players[0].CardCount(Resource.Asphalt));
            Assert.AreEqual(1, game.Players[1].CardCount(Resource.Steel));
            Assert.IsFalse(game.LastTradeAccepted);
            Assert.AreEqual(1, game.TradeNumber);
        }

        [Test]
        public void Trade_RecipientWithoutCards_AutoDeclined()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt);
            game.Debug_SetHand(1);
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 1).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase, "Отвечать нечем — окна у получателя нет");
            Assert.IsFalse(game.LastTradeAccepted);
            Assert.IsTrue(game.TradeUsedThisTurn);
        }

        [Test]
        public void Trade_Validation()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt);
            Assert.IsFalse(game.ProposeTrade(0, Resource.Asphalt, 1, Resource.Steel, 1).Success, "Себе нельзя");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Asphalt, 1).Success, "Одинаковые ресурсы");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 2, Resource.Steel, 1).Success, "Нет двух асфальтов");
            Assert.IsFalse(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 4).Success, "Больше 3 карт");
            Assert.IsTrue(game.CanProposeTrade(), "Неудачные попытки не тратят сделку");
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsFalse(game.CanProposeTrade(), "После начала действия сделку не предложить");
        }

        [Test]
        public void Trade_BotRespondsAndRecordsAsCommands()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            game.Debug_SetHand(1, Resource.Steel);
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 2, Resource.Steel, 1).Success);
            Assert.IsTrue(new SmartBot(1).Act(game).Success, "Бот отвечает на сделку");
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            var last = game.History.Skip(game.History.Count - 2).ToArray();
            Assert.AreEqual(GameCommand.ProposeTradeKind, last[0].kind);
            Assert.AreEqual(0, last[0].player);
            Assert.AreEqual(GameCommand.RespondTradeKind, last[1].kind);
            Assert.AreEqual(1, last[1].player, "Ответ записан от имени получателя");
        }

        [Test]
        public void Trade_PendingSurvivesSave()
        {
            var game = Fresh();
            game.Debug_SetHand(0, Resource.Asphalt);
            game.Debug_SetHand(1, Resource.Steel);
            Assert.IsTrue(game.ProposeTrade(1, Resource.Asphalt, 1, Resource.Steel, 1).Success);
            string json = JsonUtility.ToJson(game.CreateSave());
            var restored = Game.FromSave(game.Map, JsonUtility.FromJson<GameSaveData>(json));
            Assert.AreEqual(json, JsonUtility.ToJson(restored.CreateSave()));
            Assert.AreEqual(GamePhase.RespondingTrade, restored.Phase);
            Assert.AreEqual(1, restored.ActingPlayerIndex);
            Assert.IsTrue(restored.RespondTrade(true).Success);
            Assert.AreEqual(1, restored.Players[0].CardCount(Resource.Steel));
        }

        // ================================================================== Улучшение дорог

        [Test]
        public void Upgrade_MakesOwnRoadAMainRoad()
        {
            var game = Fresh();
            game.Debug_GiveRoad(0, "r4"); // бетон, длина 3 → улучшение за 2 карты
            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete);
            var road = game.Map.GetRoad("r4");
            Assert.AreEqual(2, game.UpgradeCost(road));
            Assert.IsNull(game.GetUpgradeBlocker("r4"));
            Assert.AreEqual(1, game.GetUpgradeOptions("r4").Count);

            int points = game.Players[0].RoadPoints;
            Assert.IsTrue(game.UpgradeRoad("r4", Resource.Concrete, 0).Success);
            Assert.IsTrue(game.IsRushHourRoad("r4"));
            Assert.AreEqual(game.Config.TollPerRoad * game.Config.RushHourTollMultiplier, game.TollFor("r4"));
            Assert.AreEqual(points + game.Config.UpgradePoints, game.Players[0].RoadPoints);
            Assert.AreEqual(0, game.Players[0].HandSize);
            Assert.AreEqual(1, game.CurrentPlayerIndex, "Улучшение — это ход");
        }

        [Test]
        public void Upgrade_Blockers()
        {
            var game = Fresh();
            game.Debug_GiveRoad(1, "r4");
            game.Debug_SetHand(0, Resource.Concrete, Resource.Concrete);
            Assert.IsNotNull(game.GetUpgradeBlocker("r4"), "Чужая дорога");
            Assert.IsNotNull(game.GetUpgradeBlocker("r7"), "Непостроенная дорога");
            Assert.IsFalse(game.UpgradeRoad("r4", Resource.Concrete, 0).Success);

            game.Debug_GiveRoad(0, "r1");
            game.Debug_SetHand(0, Resource.Concrete);
            Assert.AreEqual(0, game.GetUpgradeOptions("r1").Count, "Асфальтовую дорогу бетоном не улучшить");
            Assert.IsFalse(game.UpgradeRoad("r1", Resource.Concrete, 0).Success);
        }

        // ================================================================== Новые события

        [Test]
        public void Parade_ClosesRoadsTouchingCenter()
        {
            var game = Fresh();
            game.Debug_StartEvent(CityEventKind.Parade);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt, Resource.Gravel, Resource.Gravel);
            Assert.IsTrue(game.IsParadeClosed("r1"));
            Assert.IsFalse(game.IsParadeClosed("r5"));
            Assert.AreEqual(0, game.GetPaymentOptions("r1").Count);
            StringAssert.Contains("Парад", game.GetBuildBlocker("r1"));
            Assert.IsTrue(game.BuildRoad("r5", Resource.Gravel, 0).Success, "Вдали от Центра строить можно");
        }

        [Test]
        public void Snow_MakesGravelRoadsDearer()
        {
            var game = Fresh();
            game.Debug_StartEvent(CityEventKind.Snow);
            Assert.AreEqual(2 + game.Config.SnowExtraCost, game.BuildCost(game.Map.GetRoad("r5")));
            Assert.AreEqual(2, game.BuildCost(game.Map.GetRoad("r1")), "Асфальт снег не трогает");
            game.Debug_SetHand(0, Resource.Gravel, Resource.Gravel);
            Assert.AreEqual(0, game.GetPaymentOptions("r5").Count, "Двух карт щебня теперь мало");
        }

        [Test]
        public void Investor_AddsPointsForRoadsThisRound()
        {
            var game = Fresh();
            game.Debug_StartEvent(CityEventKind.Investor);
            game.Debug_SetHand(0, Resource.Asphalt, Resource.Asphalt);
            Assert.IsTrue(game.BuildRoad("r1", Resource.Asphalt, 0).Success);
            Assert.AreEqual(game.Config.PointsForLength(2) + game.Config.InvestorBonus, game.Players[0].RoadPoints);
        }

        [Test]
        public void EventDeck_HasNewEvents()
        {
            var game = TestCities.Started(TestCities.Small(), 2, null, 1, true);
            var config = game.Config;
            int expected = config.RushHourEvents + config.RepairEvents + config.SubsidyEvents
                           + config.ParadeEvents + config.SnowEvents + config.InvestorEvents;
            Assert.AreEqual(expected - 1, game.EventDeckCount, "Одно событие уже открыто");
        }

        [Test]
        public void EventChance_ControlsHowOftenEventsHappen([Values(0f, 0.5f, 1f)] float chance)
        {
            var config = new GameConfig { EventsEnabled = true, EventChance = chance };
            var game = new Game(TestCities.Moscow(), new[] { "А", "Б" }, config, 5);
            var bots = new IBot[] { new SmartBot(1), new SmartBot(2) };
            int rounds = 0, withEvent = 0, lastTurn = -1;
            for (int i = 0; i < 400 && game.Phase != GamePhase.GameOver; i++)
            {
                // Начало круга: ход вернулся к первому игроку.
                if (game.Phase == GamePhase.AwaitingAction && game.CurrentPlayerIndex == 0 && game.TurnNumber != lastTurn)
                {
                    lastTurn = game.TurnNumber;
                    rounds++;
                    if (game.CurrentEvent != null) withEvent++;
                }
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            }
            Assert.Greater(rounds, 15);
            if (chance == 0f) Assert.AreEqual(0, withEvent, "Шанс 0 — событий нет");
            else if (chance == 1f) Assert.AreEqual(rounds, withEvent, "Шанс 1 — событие каждый круг");
            else Assert.That(withEvent, Is.InRange(rounds / 5, rounds * 4 / 5), "Шанс 0,5 — примерно в половине кругов");
        }

        // ================================================================== Всё вместе

        [Test]
        public void SmartBots_WithTradesAndUpgrades_PlayAndReplay([Values(7, 8)] int seed)
        {
            var game = new Game(TestCities.Moscow(), new[] { "А", "Б", "В", "Г" }, null, seed);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(seed * 10 + i)).ToArray();
            int guard = 0;
            while (game.Phase != GamePhase.GameOver)
            {
                var result = bots[game.ActingPlayerIndex].Act(game);
                Assert.IsTrue(result.Success, result.Error);
                Assert.Less(++guard, 8000);
            }
            Assert.AreEqual(game.Config.TotalCards, game.Debug_CountAllCards());
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
