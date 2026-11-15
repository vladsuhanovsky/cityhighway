using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Сценарии (Зима, Большая Москва, Год ремонта, Выборы мэра) и выбор компании из двух.</summary>
    public class ScenariosTests
    {
        static GameConfig Scenario(ScenarioKind kind) => Scenarios.Apply(new GameConfig(), kind);

        static void SkipTurn(Game game)
        {
            int player = game.CurrentPlayerIndex;
            while (game.CurrentPlayerIndex == player && game.Phase != GamePhase.GameOver)
                Assert.IsTrue(game.DrawCardFromDeck().Success);
        }

        // ================================================================== Общее

        [Test]
        public void Apply_ChangesCopy_NotBase()
        {
            var baseConfig = new GameConfig();
            var winter = Scenarios.Apply(baseConfig, ScenarioKind.Winter);
            Assert.AreEqual((int)ScenarioKind.Winter, winter.Scenario);
            Assert.IsTrue(winter.PermanentSnow);
            Assert.IsFalse(baseConfig.PermanentSnow);
            Assert.AreEqual(26, baseConfig.CardsPerResource[0], "Массивы базовых настроек не тронуты");
            winter.RoadPiecesByPlayers[2] = 1;
            Assert.AreEqual(35, baseConfig.RoadPiecesByPlayers[2], "Копия массивов своя");
            Assert.AreEqual(ScenarioKind.Standard, Scenarios.Of(baseConfig));
            foreach (var kind in Scenarios.All)
            {
                Assert.IsNotEmpty(Scenarios.Title(kind));
                Assert.IsNotEmpty(Scenarios.Changes(kind));
            }
        }

        [Test]
        public void Winter_SameCardCount()
        {
            var standard = new GameConfig();
            var winter = Scenario(ScenarioKind.Winter);
            Assert.AreEqual(standard.CardsPerResource.Sum() + standard.MachineryCards, winter.CardsPerResource.Sum() + winter.MachineryCards);
        }

        // ================================================================== Зима

        [Test]
        public void Winter_GravelPricierAllGame_ConcreteInSnowfall_PermitRemoves()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.Winter));
            var gravel = game.Map.GetRoad("r5");    // щебень, длина 2
            var concrete = game.Map.GetRoad("r4");  // бетон, длина 3
            Assert.AreEqual(3, game.BuildCost(gravel), "Щебёнка дороже и без снегопада");
            Assert.AreEqual(3, game.BuildCost(concrete));

            game.Debug_StartEvent(CityEventKind.Snow);
            Assert.AreEqual(3, game.BuildCost(gravel), "Надбавка не складывается");
            Assert.AreEqual(4, game.BuildCost(concrete), "Зимой снегопад бьёт и по бетону");

            game.Debug_SetInfluence(0, 5);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.Permit).Success);
            Assert.AreEqual(2, game.BuildCost(gravel));
            Assert.AreEqual(3, game.BuildCost(concrete));
        }

        [Test]
        public void Winter_PermitAllowedWithoutEvent()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.Winter));
            game.Debug_SetInfluence(0, 5);
            Assert.IsNull(game.GetInfluenceBlocker(InfluenceAction.Permit));
            var standard = TestCities.Started(TestCities.Small(), 2);
            standard.Debug_SetInfluence(0, 5);
            Assert.IsNotNull(standard.GetInfluenceBlocker(InfluenceAction.Permit), "В обычной Москве без события разрешение не нужно");
        }

        // ================================================================== Большая Москва

        [Test]
        public void GreaterMoscow_ExpansionsOpen_MorePiecesAndContracts()
        {
            var config = Scenario(ScenarioKind.GreaterMoscow);
            var map = TestCities.Moscow();
            var game = new Game(map, new[] { "А", "Б", "В" }, config, 5);
            foreach (var district in map.Districts.Where(d => d.IsExpansion))
                Assert.IsTrue(game.IsDistrictOpen(district.Id), district.Id);
            Assert.AreEqual(new GameConfig().RoadPiecesFor(3) + 8, game.Players[0].RoadPieces);
            Assert.AreEqual(4, game.Players[0].PendingContracts.Count);

            var standard = new Game(map, new[] { "А", "Б", "В" }, new GameConfig(), 5);
            Assert.IsTrue(map.Districts.Where(d => d.IsExpansion).All(d => !standard.IsDistrictOpen(d.Id)));
        }

        [Test]
        public void GreaterMoscow_PiecesOverrideWins()
        {
            var config = Scenarios.Apply(new GameConfig { RoadPiecesOverride = 20 }, ScenarioKind.GreaterMoscow);
            Assert.AreEqual(20, config.RoadPiecesFor(4));
        }

        // ================================================================== Год ремонта

        [Test]
        public void RepairYear_ClosesRoadsEachRound_NextVisibleAhead()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.RepairYear));
            Assert.AreEqual(3, game.ScheduledRepairRoads.Count);
            Assert.AreEqual(3, game.NextRepairRoads.Count);
            Assert.IsFalse(game.ScheduledRepairRoads.Intersect(game.NextRepairRoads).Any(), "Следующие — другие участки");
            var closed = game.ScheduledRepairRoads.First();
            Assert.IsTrue(game.IsUnderRepair(closed));
            StringAssert.Contains("Год ремонта", game.GetBuildBlocker(closed) ?? "");

            var promised = game.NextRepairRoads.ToList();
            SkipTurn(game);
            SkipTurn(game);
            CollectionAssert.AreEquivalent(promised, game.ScheduledRepairRoads, "Закрыли обещанные");
        }

        [Test]
        public void RepairYear_PermitLetsBuildOnScheduledRoad()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.RepairYear));
            var closed = game.Map.GetRoad(game.ScheduledRepairRoads.OrderBy(id => id).First());
            game.Debug_SetHand(0, Enumerable.Repeat(Resource.Machinery, closed.Length).ToArray());
            Assert.AreEqual(0, game.GetPaymentOptions(closed.Id).Count);
            game.Debug_SetInfluence(0, 5);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.Permit).Success);
            Assert.Greater(game.GetPaymentOptions(closed.Id).Count, 0);
            var pay = game.GetPaymentOptions(closed.Id).First();
            Assert.IsTrue(game.BuildRoad(closed.Id, pay.Resource, pay.Machinery).Success);
        }

        [Test]
        public void RepairYear_LongRoadGivesInfluence()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.RepairYear));
            foreach (var p in game.Players) p.ContractList.Clear();
            var road = game.Map.Roads.Where(r => r.Length >= 3).OrderBy(r => r.Id).First();
            game.Debug_SetInfluence(0, 1);
            if (game.IsUnderRepair(road.Id)) Assert.IsTrue(game.UseInfluence(InfluenceAction.Permit).Success);
            int before = game.Players[0].Influence;
            game.Debug_SetHand(0, Enumerable.Repeat(Resource.Machinery, road.Length).ToArray());
            var pay = game.GetPaymentOptions(road.Id).First();
            Assert.IsTrue(game.BuildRoad(road.Id, pay.Resource, pay.Machinery).Success);
            Assert.AreEqual(before + 1, game.Players[0].Influence, "Первая дорога: за район не платят, за длину — +1");
        }

        [Test]
        public void RepairYear_SurvivesSave()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.RepairYear));
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            CollectionAssert.AreEquivalent(game.ScheduledRepairRoads, restored.ScheduledRepairRoads);
            CollectionAssert.AreEqual(game.NextRepairRoads, restored.NextRepairRoads);
            Assert.AreEqual(ScenarioKind.RepairYear, restored.Scenario);
        }

        // ================================================================== Выборы мэра

        [Test]
        public void Elections_Config()
        {
            var c = Scenario(ScenarioKind.Elections);
            var standard = new GameConfig();
            Assert.AreEqual(2, c.AuctionCount);
            Assert.AreEqual(standard.InfluencePerContract + 1, c.InfluencePerContract);
            Assert.AreEqual(1, c.BribeDiscountCost);
            Assert.AreEqual(6, c.MayorFriendBonus);
        }

        [Test]
        public void Elections_TwoAuctions_BothContractsCountAsMayors()
        {
            var game = TestCities.Started(TestCities.Moscow(), 3, Scenario(ScenarioKind.Elections));
            for (int auction = 0; auction < 2; auction++)
            {
                foreach (var p in game.Players) game.Debug_SetInfluence(p.Index, 5);
                game.Debug_StartAuction();
                Assert.AreEqual(GamePhase.Bidding, game.Phase, $"Аукцион {auction + 1}");
                Assert.IsTrue(game.Bid(3).Success);
                Assert.IsTrue(game.Bid(1).Success);
                Assert.IsTrue(game.Bid(0).Success);
                Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            }
            Assert.AreEqual(2, game.AuctionsHeld);
            Assert.IsTrue(game.AuctionHeld);
            Assert.AreEqual(2, game.Players[0].Contracts.Count(c => game.Players[0].IsMayorContract(c.Id)), "Оба контракта мэрии — с бонусом");
        }

        [Test]
        public void Elections_FriendOfMayor_GetsBonus()
        {
            var game = TestCities.Started(TestCities.Small(), 2, Scenario(ScenarioKind.Elections));
            game.Debug_SetInfluence(0, 10);
            game.Debug_SetFaceUp(Resource.Gravel, Resource.Gravel, Resource.Gravel, Resource.Gravel, Resource.Gravel);
            Assert.IsTrue(game.UseInfluence(InfluenceAction.RefreshMarket).Success);
            game.Debug_Finish();
            var me = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            var other = game.Result.Ranking.First(s => s.PlayerIndex == 1);
            Assert.AreEqual(6, me.FriendBonus);
            Assert.AreEqual(0, other.FriendBonus);

            var standard = TestCities.Started(TestCities.Small(), 2);
            standard.Debug_SetInfluence(0, 10);
            standard.Debug_SetFaceUp(Resource.Gravel, Resource.Gravel, Resource.Gravel, Resource.Gravel, Resource.Gravel);
            Assert.IsTrue(standard.UseInfluence(InfluenceAction.RefreshMarket).Success);
            standard.Debug_Finish();
            Assert.IsTrue(standard.Result.Ranking.All(s => s.FriendBonus == 0), "Только в «Выборах мэра»");
        }

        // ================================================================== Выбор компании

        static Game Drafted(params CharacterKind[] characters) =>
            new Game(TestCities.Small(), characters.Select((c, i) => "Игрок " + (i + 1)).ToList(), new GameConfig { EventsEnabled = false }, 9, characters);

        [Test]
        public void Draft_OffersTwoDistinct_DisjointWhenPossible()
        {
            var game = Drafted(Characters.Draft, Characters.Draft);
            var a = game.Players[0].CharacterOffer;
            var b = game.Players[1].CharacterOffer;
            Assert.AreEqual(2, a.Count);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(2, a.Distinct().Count());
            Assert.IsFalse(a.Intersect(b).Any(), "Шести персонажей хватает на двоих без повторов");
            Assert.AreEqual(CharacterKind.None, game.Players[0].Character);
        }

        [Test]
        public void Draft_SkipsCharactersChosenInMenu()
        {
            var game = Drafted(CharacterKind.Bridger, Characters.Draft);
            Assert.AreEqual(CharacterKind.Bridger, game.Players[0].Character);
            Assert.AreEqual(0, game.Players[0].CharacterOffer.Count);
            Assert.IsFalse(game.Players[1].CharacterOffer.Contains(CharacterKind.Bridger));
        }

        [Test]
        public void Draft_ChooseThenKeepContracts_StartsGame()
        {
            var game = Drafted(Characters.Draft, Characters.Draft);
            Assert.AreEqual(GamePhase.SetupContracts, game.Phase);
            var offer = game.Players[0].CharacterOffer.ToList();
            var notOffered = Characters.All.First(k => !offer.Contains(k));
            Assert.IsFalse(game.ChooseCharacter(0, notOffered).Success, "Только из предложенных");

            foreach (var p in game.Players)
                Assert.IsTrue(game.KeepInitialContracts(p.Index, p.PendingContracts.Take(2).Select(c => c.Id).ToList()).Success);
            Assert.AreEqual(GamePhase.SetupContracts, game.Phase, "Пока компании не выбраны, партия не начинается");
            Assert.AreEqual(0, game.ActingPlayerIndex);

            Assert.IsTrue(game.ChooseCharacter(0, offer[0]).Success);
            Assert.AreEqual(offer[0], game.Players[0].Character);
            Assert.IsFalse(game.ChooseCharacter(0, offer[1]).Success, "Второй раз нельзя");
            Assert.AreEqual(1, game.ActingPlayerIndex);
            Assert.IsTrue(game.ChooseCharacter(1, game.Players[1].CharacterOffer[0]).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
        }

        [Test]
        public void Draft_Foreman_GetsFreeRoads()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                var game = new Game(TestCities.Small(), new[] { "А", "Б" }, new GameConfig { EventsEnabled = false }, seed,
                    new[] { Characters.Draft, Characters.Draft });
                var holder = game.Players.FirstOrDefault(p => p.CharacterOffer.Contains(CharacterKind.Foreman));
                if (holder == null) continue;
                Assert.IsTrue(game.ChooseCharacter(holder.Index, CharacterKind.Foreman).Success);
                Assert.AreEqual(game.Config.ForemanFreeRoads, holder.FreeRoadsLeft);
                return;
            }
            Assert.Fail("Прораб ни разу не выпал");
        }

        [Test]
        public void Draft_SurvivesSave_AndReplays()
        {
            var game = Drafted(Characters.Draft, Characters.Draft);
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            CollectionAssert.AreEqual(game.Players[1].CharacterOffer, restored.Players[1].CharacterOffer);

            Assert.IsTrue(game.ChooseCharacter(1, game.Players[1].CharacterOffer[1]).Success);
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(game.Players[1].Character, replayed.Players[1].Character);
            CollectionAssert.AreEqual(game.Players[0].CharacterOffer, replayed.Players[0].CharacterOffer);
        }

        // ================================================================== Боты во всех сценариях

        [Test]
        public void BotGames_EveryScenario_WithDraft_FinishAndReplay()
        {
            foreach (var kind in Scenarios.All)
            {
                var config = Scenario(kind);
                config.EventChance = 1f;
                var names = new[] { "А", "Б", "В", "Г" };
                var game = new Game(TestCities.Moscow(), names, config, 50 + (int)kind,
                    names.Select(_ => Characters.Draft).ToList());
                var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(i + 21)).ToArray();
                for (int step = 0; step < 8000 && game.Phase != GamePhase.GameOver; step++)
                {
                    var result = bots[game.ActingPlayerIndex].Act(game);
                    Assert.IsTrue(result.Success, $"{kind}: {result.Error}");
                }
                Assert.AreEqual(GamePhase.GameOver, game.Phase, kind.ToString());
                Assert.IsTrue(game.Players.All(p => p.Character != CharacterKind.None), $"{kind}: все выбрали компанию");
                if (kind == ScenarioKind.Elections) Assert.AreEqual(2, game.AuctionsHeld, "Два аукциона");
                var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
                Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()), kind.ToString());
            }
        }
    }
}
