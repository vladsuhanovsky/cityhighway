using System;
using System.IO;
using System.Linq;
using System.Text;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ReplayStorageIOTests
    {
        static string TempFolder()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "cityhighways-replay-io-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [TestCase("20261125-120000-a1b2c3d4e5f6")]
        [TestCase("game-123")]
        [TestCase("ABC_def-123")]
        [TestCase("X")]
        public void AcceptsPortableReplayIds(string id)
        {
            Assert.IsTrue(ReplayStorageIO.IsSafeId(id));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("../escape")]
        [TestCase("..")]
        [TestCase("a/b")]
        [TestCase(@"a\b")]
        [TestCase("a.json")]
        [TestCase(" a")]
        [TestCase("-dash")]
        [TestCase("_underscore")]
        [TestCase("index")]
        [TestCase("INDEX")]
        [TestCase("CON")]
        [TestCase("com1")]
        [TestCase("LPT9")]
        [TestCase("тест")]
        public void RejectsUnsafeOrReservedReplayIds(string id)
        {
            Assert.IsFalse(ReplayStorageIO.IsSafeId(id));
            Assert.Throws<ArgumentException>(() => ReplayStorageIO.PathFor("folder", id));
            Assert.IsFalse(Replays.Exists(id));
            Assert.IsNull(Replays.Read(id));
            Assert.IsFalse(Replays.Store(new ReplayFile { id = id }));
        }

        [Test]
        public void RejectsOverlongIdentifier()
        {
            string id = new string('A', ReplayStorageIO.MaxIdLength + 1);
            Assert.IsFalse(ReplayStorageIO.IsSafeId(id));
        }

        [Test]
        public void ValidIdStaysInsideFolder()
        {
            string folder = TempFolder();
            try
            {
                string path = ReplayStorageIO.PathFor(folder, "20261125-123456-abcd");
                Assert.AreEqual(Path.Combine(folder, "20261125-123456-abcd.json"), path);
                Assert.AreEqual(folder, Path.GetDirectoryName(path));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void AtomicallyCreatesAndReplacesExistingReplay()
        {
            string folder = TempFolder();
            try
            {
                string path = ReplayStorageIO.PathFor(folder, "test");
                ReplayStorageIO.WriteAtomic(path, "old");
                Assert.AreEqual("old", ReplayStorageIO.ReadBoundedUtf8(path, 10));
                ReplayStorageIO.WriteAtomic(path, "new");
                Assert.AreEqual("new", ReplayStorageIO.ReadBoundedUtf8(path, 10));
                Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void BoundedReadRejectsFileOverLimit()
        {
            string folder = TempFolder();
            try
            {
                string path = Path.Combine(folder, "large.json");
                File.WriteAllText(path, new string('x', 1025));
                Assert.Throws<InvalidDataException>(() =>
                    ReplayStorageIO.ReadBoundedUtf8(path, 1024));
                Assert.AreEqual(1025, ReplayStorageIO.ReadBoundedUtf8(path, 1025).Length);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void BoundedReadRejectsInvalidUtf8()
        {
            string folder = TempFolder();
            try
            {
                string path = Path.Combine(folder, "utf8.json");
                File.WriteAllBytes(path, new byte[] { 0xff, 0xfe, 0xfd });
                Assert.Throws<DecoderFallbackException>(() =>
                    ReplayStorageIO.ReadBoundedUtf8(path, 10));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void BoundedReadAcceptsBomAndUnicode()
        {
            string folder = TempFolder();
            try
            {
                string path = Path.Combine(folder, "bom.json");
                byte[] prefix = { 0xef, 0xbb, 0xbf };
                byte[] encoded = Encoding.UTF8.GetBytes("{\"name\":\"Москва\"}");
                File.WriteAllBytes(path, prefix.Concat(encoded).ToArray());
                Assert.AreEqual("{\"name\":\"Москва\"}",
                    ReplayStorageIO.ReadBoundedUtf8(path, 1024));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void DownloadedCloudReplayCannotUseTraversalId()
        {
            var replay = new ReplayFile
            {
                id = "../danger",
                mode = "classic",
                mapPath = "Maps/demo",
                players = new[] { "A" },
                scores = new[] { 1 },
                humans = new[] { true },
                authorSeat = 0,
                authorScore = 1,
                start = new GameStartInfo { players = new[] { "A" } },
                history = new[] { new GameCommand { kind = GameCommand.PassKind, player = 0 } }
            };
            Assert.IsNull(Replays.UnpackCloud(Replays.Pack(replay)));
        }

        [Test]
        public void AtomicWriterDoesNotCreateDestinationOnInvalidPath()
        {
            Assert.Throws<ArgumentException>(() => ReplayStorageIO.WriteAtomic("", "test"));
        }

        [Test]
        public void BoundedReaderRequiresPositiveLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReplayStorageIO.ReadBoundedUtf8("never-opened.json", 0));
        }
    }
}
