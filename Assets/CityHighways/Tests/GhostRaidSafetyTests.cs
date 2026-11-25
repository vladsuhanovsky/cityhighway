using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class GhostRaidSafetyTests
    {
        [Test]
        public void OnePendingIntentCannotOpenTwice()
        {
            var request = new GhostBannerRequest();
            Assert.IsTrue(request.TryBegin(out int first));
            Assert.IsFalse(request.TryBegin(out int ignored));
            Assert.IsTrue(request.Pending);
            Assert.IsTrue(request.TryComplete(first));
            Assert.IsFalse(request.TryComplete(first));
            Assert.IsFalse(request.Pending);
        }

        [Test]
        public void NavigationInvalidatesOldSuccessAndFailureWithoutClearingNewRequest()
        {
            var request = new GhostBannerRequest();
            Assert.IsTrue(request.TryBegin(out int first));
            request.Cancel();
            Assert.IsTrue(request.TryBegin(out int second));
            Assert.IsFalse(request.TryComplete(first));
            Assert.IsTrue(request.Pending);
            Assert.IsTrue(request.TryComplete(second));
        }

        [Test]
        public void UnchangedRecordRetainsSelectionButAccountOrMetadataChangeDoesNot()
        {
            var a = new Ghost { RecordId = 7, Name = "Friend", Score = 42, SteamOwner = "A", SteamGeneration = 1, Source = "friend", MapPath = "map" };
            var b = new Ghost { RecordId = 7, Name = "Friend", Score = 42, SteamOwner = "A", SteamGeneration = 1, Source = "friend", MapPath = "map" };
            Assert.IsTrue(GhostBannerRequest.SameRecord(a, b));
            b.SteamGeneration++;
            Assert.IsFalse(GhostBannerRequest.SameRecord(a, b));
            b.SteamGeneration--;
            b.Score++;
            Assert.IsFalse(GhostBannerRequest.SameRecord(a, b));
            Assert.IsFalse(GhostBannerRequest.SameRecord(a, null));
        }

        static Ghost BuildGhost(CityMap map)
        {
            var game = TestCities.Started(map);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            Assert.IsTrue(game.DrawCardFromDeck().Success);
            return new Ghost { Replay = new ReplayFile { start = game.StartInfo, authorSeat = 0, history = game.History.ToArray() } };
        }

        [Test]
        public void SuccessfulTimelineRemainsAvailable()
        {
            var map = TestCities.Small();
            var ghost = BuildGhost(map);
            ghost.BuildTimeline(map);
            Assert.IsNotNull(ghost.ScoreByRound);
            Assert.Greater(ghost.ScoreByRound.Length, 1);
        }

        [Test]
        public void InvalidTailClearsPreviouslyValidTimeline()
        {
            var map = TestCities.Small();
            var ghost = BuildGhost(map);
            ghost.BuildTimeline(map);
            Assert.IsNotNull(ghost.ScoreByRound);
            ghost.Replay.history = ghost.Replay.history.Concat(new[] { new GameCommand { kind = "invalid_command", player = 0 } }).ToArray();
            ghost.BuildTimeline(map);
            Assert.IsNull(ghost.ScoreByRound);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidAuthorSeatCannotPublishTimeline(int seat)
        {
            var map = TestCities.Small();
            var ghost = BuildGhost(map);
            ghost.Replay.authorSeat = seat;
            ghost.BuildTimeline(map);
            Assert.IsNull(ghost.ScoreByRound);
        }

        [Test]
        public void EmptyHistoryDoesNotInventComparison()
        {
            var map = TestCities.Small();
            var ghost = BuildGhost(map);
            ghost.Replay.history = new GameCommand[0];
            ghost.BuildTimeline(map);
            Assert.IsNull(ghost.ScoreByRound);
        }
    }
}
