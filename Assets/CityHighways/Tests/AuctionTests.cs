using System.Linq;
using CityHighways.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>Аукцион мэрии: запуск, тайные ставки, победитель, ничья, без ставок, бонус в конце, сохранение.</summary>
    public class AuctionTests
    {
        static Game Fresh(int players = 3) => TestCities.Started(TestCities.Small(), players);

        [Test]
        public void Auction_HighestBidWins_PaysOnlyWinner()
        {
            var game = Fresh();
            game.Debug_SetInfluence(0, 5);
            game.Debug_SetInfluence(1, 5);
            game.Debug_SetInfluence(2, 5);
            int deckBefore = game.ContractDeckCount;
            game.Debug_StartAuction();
            Assert.AreEqual(GamePhase.Bidding, game.Phase);
            var contract = game.AuctionContract;
            Assert.IsNotNull(contract);
            Assert.AreEqual(deckBefore - 1, game.ContractDeckCount, "Контракт снят с колоды");

            Assert.AreEqual(0, game.ActingPlayerIndex, "Ставят по кругу от текущего игрока");
            Assert.IsTrue(game.Bid(2).Success);
            Assert.IsTrue(game.HasBid(0));
            Assert.AreEqual(1, game.ActingPlayerIndex);
            Assert.IsFalse(game.Bid(6).Success, "Нельзя поставить больше, чем есть");
            Assert.IsTrue(game.Bid(4).Success);
            Assert.IsTrue(game.Bid(1).Success);

            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
            Assert.AreEqual(0, game.CurrentPlayerIndex, "Ход возвращается текущему игроку");
            Assert.AreEqual(1, game.LastAuction.Winner);
            Assert.AreEqual(4, game.LastAuction.Price);
            Assert.AreEqual(1, game.Players[1].Influence, "Победитель платит свою ставку");
            Assert.AreEqual(5, game.Players[0].Influence, "Проигравшие не платят");
            Assert.AreEqual(5, game.Players[2].Influence);
            Assert.Contains(contract, game.Players[1].Contracts.ToList());
            Assert.AreEqual(contract.Id, game.Players[1].MayorContractId);
            Assert.IsTrue(game.AuctionHeld);
        }

        [Test]
        public void Auction_Tie_GoesToPlayerBehind()
        {
            var game = Fresh();
            for (int i = 0; i < 3; i++) game.Debug_SetInfluence(i, 3);
            game.Players[0].RoadPoints = 10;
            game.Players[1].RoadPoints = 2;
            game.Debug_StartAuction();
            Assert.IsTrue(game.Bid(3).Success);
            Assert.IsTrue(game.Bid(3).Success);
            Assert.IsTrue(game.Bid(0).Success);
            Assert.AreEqual(1, game.LastAuction.Winner, "При равенстве — отстающий");
        }

        [Test]
        public void Auction_NoBids_ContractReturnsUnderDeck()
        {
            var game = Fresh();
            for (int i = 0; i < 3; i++) game.Debug_SetInfluence(i, 2);
            int deckBefore = game.ContractDeckCount;
            game.Debug_StartAuction();
            for (int i = 0; i < 3; i++) Assert.IsTrue(game.Bid(0).Success);
            Assert.AreEqual(-1, game.LastAuction.Winner);
            Assert.AreEqual(deckBefore, game.ContractDeckCount);
            Assert.IsTrue(game.Players.All(p => p.MayorContractId == null));
        }

        [Test]
        public void Auction_PlayersWithoutInfluence_SkipAutomatically()
        {
            var game = Fresh();
            game.Debug_SetInfluence(2, 4);
            game.Debug_StartAuction();
            Assert.AreEqual(2, game.ActingPlayerIndex, "Без влияния ставка 0 ставится сама");
            Assert.IsTrue(game.Bid(1).Success);
            Assert.AreEqual(2, game.LastAuction.Winner);

            var empty = Fresh();
            empty.Debug_StartAuction();
            Assert.AreEqual(GamePhase.AwaitingAction, empty.Phase, "Ни у кого нет влияния — аукцион сразу закрыт");
            Assert.AreEqual(-1, empty.LastAuction.Winner);
        }

        [Test]
        public void Auction_OnlyOncePerGame()
        {
            var game = Fresh();
            game.Debug_StartAuction();
            Assert.IsTrue(game.AuctionHeld);
            game.Debug_StartAuction();
            Assert.AreEqual(GamePhase.AwaitingAction, game.Phase);
        }

        [Test]
        public void Auction_StartsAtRoundStart_AfterThirdOfPieces()
        {
            var game = Fresh(2);
            game.Debug_SetInfluence(1, 2);
            int start = game.Config.RoadPiecesFor(2);
            game.Debug_SetPieces(0, start - (int)System.Math.Ceiling(start * game.Config.AuctionTriggerShare));
            // Конец круга: оба игрока берут по две карты.
            for (int k = 0; k < 4; k++) Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.AreEqual(GamePhase.Bidding, game.Phase, "В начале нового круга начинается аукцион");
            Assert.AreEqual(1, game.ActingPlayerIndex, "У игрока 0 нет влияния — ставит игрок 1");
        }

        [Test]
        public void MayorContract_Completed_GivesBonus()
        {
            var game = TestCities.Started(TestCities.Small(), 2);
            foreach (var p in game.Players) p.ContractList.Clear();
            game.Debug_SetInfluence(0, 3);
            game.Debug_StartAuction();
            Assert.IsTrue(game.Bid(1).Success); // у игрока 1 нет влияния — ставка 0 уже стоит
            var contract = game.LastAuction.Contract;
            Assert.AreEqual(0, game.LastAuction.Winner);
            // Строим путь контракта подарками для теста.
            foreach (var road in game.Map.Roads) game.Debug_GiveRoad(0, road.Id);
            game.Debug_Finish();
            var score = game.Result.Ranking.First(s => s.PlayerIndex == 0);
            Assert.AreEqual(game.Config.AuctionBonus, score.MayorBonus);
            Assert.AreEqual(contract.Points, score.ContractPoints);
        }

        [Test]
        public void Auction_SurvivesSaveMidBidding()
        {
            var game = Fresh();
            for (int i = 0; i < 3; i++) game.Debug_SetInfluence(i, 4);
            game.Debug_StartAuction();
            Assert.IsTrue(game.Bid(2).Success);
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            var restored = Game.FromSave(game.Map, data);
            Assert.AreEqual(GamePhase.Bidding, restored.Phase);
            Assert.AreEqual(game.AuctionContract.Id, restored.AuctionContract.Id);
            Assert.AreEqual(1, restored.ActingPlayerIndex);
            Assert.IsTrue(restored.HasBid(0));
            Assert.IsTrue(restored.Bid(3).Success);
            Assert.IsTrue(restored.Bid(0).Success);
            Assert.AreEqual(1, restored.LastAuction.Winner);
        }

        [Test]
        public void BotGame_HoldsAuction_AndReplaysExactly()
        {
            var game = TestCities.Started(TestCities.Moscow(), 4, new GameConfig(), seed: 23, events: true);
            var bots = Enumerable.Range(0, 4).Select(i => (IBot)new SmartBot(i + 7)).ToArray();
            for (int step = 0; step < 5000 && game.Phase != GamePhase.GameOver; step++)
                Assert.IsTrue(bots[game.ActingPlayerIndex].Act(game).Success);
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.IsTrue(game.AuctionHeld, "За партию прошёл аукцион");
            var replayed = Game.Replay(game.Map, game.StartInfo, game.History);
            Assert.AreEqual(JsonUtility.ToJson(game.CreateSave()), JsonUtility.ToJson(replayed.CreateSave()));
        }
    }
}
