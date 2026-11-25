using System;
using System.IO;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class SaveFileStorageTests
    {
        string folder, path;
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "city-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "save.json");
        }
        [TearDown]
        public void TearDown() { Directory.Delete(folder, true); }

        static SaveFile Fixture(float seconds)
        {
            var setup = new GameSetup { magnate = false };
            setup.players.Add(new PlayerSlot("A", false));
            setup.players.Add(new PlayerSlot("B", true));
            var map = CityHighwaysApp.LoadMap(setup.mapResourcePath);
            return new SaveFile { setup = setup, game = new Game(map, new[] { "A", "B" },
                new GameConfig(), 42).CreateSave(), playSeconds = seconds };
        }

        [Test]
        public void FirstWriteAndRotationPreservePreviousSnapshot()
        {
            SaveFileStorage.Write(path, Fixture(10));
            Assert.IsFalse(File.Exists(SaveFileStorage.BackupPath(path)));
            SaveFileStorage.Write(path, Fixture(20));
            Assert.AreEqual(20, SaveFileStorage.Read(path, null).playSeconds);
            Assert.AreEqual(10, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), null).playSeconds);
            SaveFileStorage.Write(path, Fixture(30));
            Assert.AreEqual(20, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), null).playSeconds);
        }

        [Test]
        public void MalformedPrimaryDoesNotReplaceExistingBackup()
        {
            SaveFileStorage.Write(path, Fixture(10));
            SaveFileStorage.Write(path, Fixture(20));
            File.WriteAllText(path, "{");
            SaveFileStorage.Write(path, Fixture(30));
            Assert.AreEqual(10, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), null).playSeconds);
        }

        [Test]
        public void FuturePrimaryDoesNotReplaceExistingBackup()
        {
            SaveFileStorage.Write(path, Fixture(10));
            SaveFileStorage.Write(path, Fixture(20));
            var future = Fixture(50);
            future.game.version = GameSaveData.CurrentVersion + 1;
            File.WriteAllText(path, JsonUtility.ToJson(future));
            SaveFileStorage.Write(path, Fixture(30));
            Assert.AreEqual(10, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), null).playSeconds);
        }

        [Test]
        public void OversizedReadLeavesFileUntouched()
        {
            using (var stream = File.Create(path)) stream.SetLength(SaveFileStorage.MaxSaveBytes + 1L);
            Assert.Throws<InvalidDataException>(() => SaveFileStorage.Read(path, null));
            Assert.AreEqual(SaveFileStorage.MaxSaveBytes + 1L, new FileInfo(path).Length);
        }

        [Test]
        public void ExplicitDiscardRemovesPrimaryAndBackup()
        {
            SaveFileStorage.Write(path, Fixture(10));
            SaveFileStorage.Write(path, Fixture(20));
            SaveFileStorage.Delete(path);
            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(SaveFileStorage.BackupPath(path)));
            SaveFileStorage.Delete(path);
        }

        [Test]
        public void NormalSaveCannotBeReadAsCampaign()
        {
            SaveFileStorage.Write(path, Fixture(10));
            Assert.IsNull(SaveFileStorage.Read(path, "local:abcdefabcdefabcdefabcdefabcdefab"));
        }

        [Test]
        public void EmptyShapeCannotRotateIntoBackup()
        {
            SaveFileStorage.Write(path, Fixture(10));
            SaveFileStorage.Write(path, Fixture(20));
            File.WriteAllText(path, "{}");
            Assert.IsNull(SaveFileStorage.Read(path, null));
            SaveFileStorage.Write(path, Fixture(30));
            Assert.AreEqual(10, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), null).playSeconds);
        }

        [Test]
        public void CampaignBackupRemainsScopedToOwner()
        {
            const string owner = "local:abcdefabcdefabcdefabcdefabcdefab";
            const string other = "local:12345678901234567890123456789012";
            var file = Fixture(10);
            file.setup = CampaignCatalog.All[0].CreateSetup("A", owner, false);
            SaveFileStorage.Write(path, file);
            file.playSeconds = 20;
            SaveFileStorage.Write(path, file);
            Assert.IsNull(SaveFileStorage.Read(path, null));
            Assert.IsNull(SaveFileStorage.Read(SaveFileStorage.BackupPath(path), other));
            Assert.AreEqual(10, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), owner).playSeconds);
        }

        [Test]
        public void OversizedWriteLeavesBothSnapshotsUntouched()
        {
            SaveFileStorage.Write(path, Fixture(10));
            SaveFileStorage.Write(path, Fixture(20));
            var bad = Fixture(30);
            bad.savedAt = new string('x', SaveFileStorage.MaxSaveBytes);
            Assert.Throws<InvalidDataException>(() => SaveFileStorage.Write(path, bad));
            Assert.AreEqual(20, SaveFileStorage.Read(path, null).playSeconds);
            Assert.AreEqual(10, SaveFileStorage.Read(SaveFileStorage.BackupPath(path), null).playSeconds);
        }
    }
}
