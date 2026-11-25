using System;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ReplayLocalSanityTests
    {
        static ReplayFile Valid() => new ReplayFile
        {
            id = "safe-001", version = ReplayFile.CurrentVersion,
            mode = "classic", mapPath = "Maps/demo",
            players = new[] { "A", "B" }, humans = new[] { true, false },
            scores = new[] { 10, 5 }, authorSeat = 0,
            start = new GameStartInfo
            {
                players = new[] { "A", "B" }, characters = new[] { 0, 0 },
                config = new GameConfig()
            },
            history = new[] { new GameCommand { kind = GameCommand.PassKind, player = 0 } }
        };

        [Test]
        public void CompleteLocalReplayHasReadableStructure()
        {
            Assert.IsTrue(ReplayLocalSanity.IsReadable(Valid()));
        }

        [Test]
        public void UnfinishedGameWithEmptyHistoryAndUnknownAuthorIsReadable()
        {
            var replay = Valid();
            replay.history = new GameCommand[0];
            replay.authorSeat = -1;
            Assert.IsTrue(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void LegacyLocalReplayWithoutOptionalCharactersOrHumanArrayIsReadable()
        {
            var replay = Valid();
            replay.humans = null;
            replay.start.characters = null;
            Assert.IsTrue(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void NullReplayIsNotReadable()
        {
            Assert.IsFalse(ReplayLocalSanity.IsReadable(null));
        }

        [Test]
        public void UnsupportedVersionOrModeIsRejected()
        {
            var replay = Valid();
            replay.version = 2;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.mode = "unknown";
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void UnsafeReplayIdCannotBeUsedForLocalStorage()
        {
            var replay = Valid();
            replay.id = "../outside";
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void MissingMapOrGameConfigurationIsRejected()
        {
            var replay = Valid();
            replay.mapPath = "";
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.start.config = null;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.start = null;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void PlayerAndScoreArrayMismatchesAreRejected()
        {
            var replay = Valid();
            replay.scores = new[] { 10 };
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.players = new[] { "A" };
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.humans = new[] { true };
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.start.characters = new[] { 0 };
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void MissingOrEmptyPlayerNameIsRejected()
        {
            var replay = Valid();
            replay.start.players[0] = "";
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.players[0] = null;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void AuthorSeatOutsidePlayersIsRejected()
        {
            var replay = Valid();
            replay.authorSeat = 2;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay.authorSeat = -2;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void MissingOrInvalidActionHistoryIsRejected()
        {
            var replay = Valid();
            replay.history = null;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.history[0] = null;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.history[0].kind = "";
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
            replay = Valid();
            replay.history[0].player = 5;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void NegativeTurnCountIsRejected()
        {
            var replay = Valid();
            replay.turns = -1;
            Assert.IsFalse(ReplayLocalSanity.IsReadable(replay));
        }

        [Test]
        public void LocalReplaySupportsCooperativeGames()
        {
            var replay = Valid();
            replay.mode = "coop";
            Assert.IsTrue(ReplayLocalSanity.IsReadable(replay));
        }
    }
}
