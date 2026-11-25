using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class SocialReplaySafetyTests
    {
        static ReplayFile ValidReplay()
        {
            return new ReplayFile
            {
                version = ReplayFile.CurrentVersion,
                id = "game-123",
                mode = "classic",
                mapPath = "Maps/demo",
                map = "Demo",
                author = "Friend",
                authorSeat = 0,
                authorScore = 97,
                won = true,
                turns = 1,
                players = new[] { "Friend", "Bot" },
                scores = new[] { 97, 20 },
                humans = new[] { true, false },
                start = new GameStartInfo
                {
                    players = new[] { "Friend", "Bot" },
                    characters = new[] { 0, 0 },
                    seed = 42,
                    config = new GameConfig()
                },
                history = new[] { new GameCommand { kind = GameCommand.PassKind, player = 0 } }
            };
        }

        static SocialCloud.RecordInfo Record() => new SocialCloud.RecordInfo
        {
            id = 42,
            mode = "classic",
            mapPath = "Maps/demo",
            mapName = "Demo",
            score = 97,
            won = true,
            seats = 2
        };

        static string GzipBase64(string raw)
        {
            var bytes = Encoding.UTF8.GetBytes(raw);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                    gzip.Write(bytes, 0, bytes.Length);
                return Convert.ToBase64String(output.ToArray());
            }
        }

        [Test]
        public void ValidCloudReplayRoundTrip()
        {
            string packed = Replays.Pack(ValidReplay());
            var decoded = Replays.UnpackCloud(packed);
            Assert.IsNotNull(decoded);
            Assert.AreEqual("Maps/demo", decoded.mapPath);
            Assert.AreEqual(97, decoded.authorScore);
            Assert.AreEqual(1, decoded.history.Length);
            Assert.IsTrue(SocialReplayMatch.Matches(Record(), decoded));
        }

        [Test]
        public void LegacyUnpackEntryPointIsAlsoBounded()
        {
            Assert.IsNotNull(Replays.Unpack(Replays.Pack(ValidReplay())));
            Assert.IsNull(Replays.Unpack(GzipBase64(new string('Z',
                Replays.MaxCloudReplayJsonBytes + 1))));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("%%%=")]
        [TestCase("AAAA\n")]
        [TestCase("A===")]
        [TestCase("AAAA===")]
        public void RejectsInvalidBase64WithoutThrowing(string encoded)
        {
            Assert.IsNull(Replays.UnpackCloud(encoded));
        }

        [Test]
        public void RejectsCompressedInputWithExcessiveEncodedSize()
        {
            Assert.IsNull(Replays.UnpackCloud(new string('A', Replays.MaxCloudReplayBase64 + 4)));
        }

        [Test]
        public void RejectsZipBombBeforeAllocatingUnboundedDecodedString()
        {
            string packed = GzipBase64(new string('Z', Replays.MaxCloudReplayJsonBytes + 1));
            Assert.Less(packed.Length, Replays.MaxCloudReplayBase64);
            Assert.IsNull(Replays.UnpackCloud(packed));
        }

        [Test]
        public void RejectsMalformedGzipAndInvalidUtf8()
        {
            Assert.IsNull(Replays.UnpackCloud(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })));
            var invalidUtf8 = new byte[] { 0xff, 0xfe, 0xfd };
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                    gzip.Write(invalidUtf8, 0, invalidUtf8.Length);
                Assert.IsNull(Replays.UnpackCloud(Convert.ToBase64String(output.ToArray())));
            }
        }

        [Test]
        public void RejectsUnsupportedReplayVersionAndGameMode()
        {
            var replay = ValidReplay();
            replay.version = 99;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
            replay = ValidReplay();
            replay.mode = "magnate";
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
        }

        [Test]
        public void RejectsMissingStartAndPlayerArrays()
        {
            var replay = ValidReplay();
            replay.start = null;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
            replay = ValidReplay();
            replay.humans = null;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
            replay = ValidReplay();
            replay.start.players = null;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
        }

        [Test]
        public void RejectsInconsistentAuthorSeatAndScore()
        {
            var replay = ValidReplay();
            replay.authorSeat = 5;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
            replay = ValidReplay();
            replay.scores[0] = 44;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
            replay = ValidReplay();
            replay.humans[0] = false;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
        }

        [Test]
        public void RejectsMissingOrInconsistentActionHistory()
        {
            var replay = ValidReplay();
            replay.history = null;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
            replay = ValidReplay();
            replay.history[0].player = 9;
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
        }

        [Test]
        public void RejectsUnknownCloudCommandKind()
        {
            var replay = ValidReplay();
            replay.history[0].kind = "unknown_action";
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
        }

        [Test]
        public void ReplayMetadataMustMatchChosenRecord()
        {
            var replay = ValidReplay();
            var record = Record();
            Assert.IsTrue(SocialReplayMatch.Matches(record, replay));
            record.mapPath = "Maps/other";
            Assert.IsFalse(SocialReplayMatch.Matches(record, replay));
            record = Record();
            record.mode = "coop";
            Assert.IsFalse(SocialReplayMatch.Matches(record, replay));
            record = Record();
            record.score = 95;
            Assert.IsFalse(SocialReplayMatch.Matches(record, replay));
            record = Record();
            record.won = false;
            Assert.IsFalse(SocialReplayMatch.Matches(record, replay));
            record = Record();
            record.seats = 3;
            Assert.IsFalse(SocialReplayMatch.Matches(record, replay));
            record = Record();
            record.mapName = "Different";
            Assert.IsFalse(SocialReplayMatch.Matches(record, replay));
        }

        [Test]
        public void IdOnlyReplayLookupRetainsCompatibility()
        {
            Assert.IsTrue(SocialReplayMatch.Matches(
                new SocialCloud.RecordInfo { id = 42 }, ValidReplay()));
            Assert.IsFalse(SocialReplayMatch.Matches(
                new SocialCloud.RecordInfo { id = 0 }, ValidReplay()));
            Assert.IsFalse(SocialReplayMatch.Matches(null, ValidReplay()));
        }

        [Test]
        public void ValidReplayEnvelopeRequiresTypedAuthorAndBase64Field()
        {
            string packed = Replays.Pack(ValidReplay());
            Assert.IsTrue(SocialReadContracts.ValidateReplay(
                "{\"replay\":\"" + packed + "\",\"author\":\"Friend\"}"));
            Assert.IsTrue(SocialReadContracts.ValidateReplay(
                "{\"replay\":\"" + packed + "\",\"author\":\"\"}"));
            Assert.IsFalse(SocialReadContracts.ValidateReplay(
                "{\"replay\":\"" + packed + "\"}"));
            Assert.IsFalse(SocialReadContracts.ValidateReplay(
                "{\"replay\":\"" + packed + "\",\"author\":null}"));
        }

        [TestCase("{}")]
        [TestCase("{\"replay\":null,\"author\":\"Friend\"}")]
        [TestCase("{\"replay\":true,\"author\":\"Friend\"}")]
        [TestCase("{\"replay\":\"\",\"author\":\"Friend\"}")]
        [TestCase("{\"replay\":\"ABC\",\"author\":\"Friend\"}")]
        [TestCase("{\"replay\":\"AAAA\",\"author\":\"Friend\",\"replay\":\"BBBB\"}")]
        [TestCase("{\"metadata\":{\"replay\":\"AAAA\"},\"author\":\"Friend\"}")]
        public void RejectsMalformedReplayHttpEnvelope(string json)
        {
            Assert.IsFalse(SocialReadContracts.ValidateReplay(json));
        }

        [Test]
        public void RejectsOversizedReplayResponseEnvelope()
        {
            string json = "{\"replay\":\"AAAA\",\"author\":\"" +
                new string('A', 768 * 1024) + "\"}";
            Assert.IsFalse(SocialReadContracts.ValidateReplay(json));
        }
    }
}
