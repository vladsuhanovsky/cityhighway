using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ProfileExhibitionTests
    {
        [Test]
        public void CatalogUsesExistingProgressAndKnownVehicleIds()
        {
            Assert.AreEqual(10, ProfileExhibition.All.Count);
            Assert.AreEqual(10, ProfileExhibition.All.Select(e=>e.Id).Distinct().Count());
            var profile = new PlayerProfile { name = "Alpha" };
            Assert.AreEqual(1, ProfileExhibition.All.Count(e=>e.IsUnlocked(profile)));
            foreach (var e in ProfileExhibition.All)
            {
                if (e.Achievement != null) Assert.IsTrue(Achievements.All.Any(a=>a.Id==e.Achievement));
                if (e.Vehicle != null) CollectionAssert.Contains(FleetLivery.VehicleIds,e.Vehicle);
                if (e.Achievement != null) profile.achievements.Add(e.Achievement);
                if (e.Map != null) profile.AddMark("map:"+e.Map);
            }
            Assert.AreEqual(10, ProfileExhibition.All.Count(e=>e.IsUnlocked(profile)));
        }

        [Test]
        public void LockedUnknownDuplicateAndOversizedSelectionsAreNormalized()
        {
            var profile = new PlayerProfile { name = "Alpha" };
            CollectionAssert.AreEqual(new[]{"paver","",""}, ProfileExhibition.Normalize(profile,
                new[]{"paver","paver","crane","unknown"}));
            Assert.IsFalse(ProfileExhibition.Store(new ProfileBook(),profile,0,"crane"));
            Assert.IsFalse(ProfileExhibition.Store(new ProfileBook(),profile,3,"paver"));
            Assert.IsFalse(ProfileExhibition.Store(new ProfileBook(),profile,0,"unknown"));
            CollectionAssert.AreEqual(new[]{"","",""}, ProfileExhibition.Normalize(null,new[]{"paver"}));
        }

        [Test]
        public void MovingExhibitsAndSavingCompanyOrFleetPreservesOtherLocalSettings()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name="Alpha", wallet=25 };
            var b = new PlayerProfile { name="Beta" };
            a.achievements.Add("first_win");
            book.profiles.Add(a); book.profiles.Add(b);
            string before = JsonUtility.ToJson(a);
            Assert.IsTrue(ProfileExhibition.Store(book,a,0,"paver"));
            Assert.IsTrue(ProfileExhibition.Store(book,a,2,"paver"));
            Assert.IsTrue(ProfileExhibition.Store(book,a,1,"first_win"));
            var old = book.companies[0];
            Assert.IsTrue(ProfileExhibition.Store(book,a,0,"first_win"));
            CollectionAssert.AreEqual(new[]{"","first_win","paver"},old.exhibits);
            CompanyIdentity.Store(book,a,CompanyIdentity.Create(a,"Roads","","green","white"));
            FleetLivery.Store(book,a,"mixer","reverse",true);
            Assert.IsTrue(ProfileExhibition.Store(book,b,0,"paver"));
            var loaded = JsonUtility.FromJson<ProfileBook>(JsonUtility.ToJson(book));
            CollectionAssert.AreEqual(new[]{"first_win","","paver"},ProfileExhibition.Read(loaded,loaded.profiles[0]));
            CollectionAssert.AreEqual(new[]{"paver","",""},ProfileExhibition.Read(loaded,loaded.profiles[1]));
            Assert.AreEqual("reverse",FleetLivery.Read(loaded,loaded.profiles[0],"mixer").Scheme);
            Assert.AreEqual("Roads",CompanyIdentity.Read(loaded,loaded.profiles[0]).Name);
            Assert.AreEqual(before,JsonUtility.ToJson(a));
            loaded.profiles[0].achievements.Clear();
            CollectionAssert.AreEqual(new[]{"","","paver"},ProfileExhibition.Read(loaded,loaded.profiles[0]));
        }

        [Test]
        public void LegacyBookAndProfileRenameRetainSafeDefaultsAndExhibits()
        {
            var book=JsonUtility.FromJson<ProfileBook>("{\"profiles\":[],\"active\":\"\"}");
            var profile=new PlayerProfile {name="Old"};
            CollectionAssert.AreEqual(new[]{"","",""},ProfileExhibition.Read(book,profile));
            ProfileExhibition.Store(book,profile,0,"paver");
            var oldKey=CompanyIdentity.OwnerKey(profile);
            profile.name="New";
            CompanyIdentity.Move(book,oldKey,CompanyIdentity.OwnerKey(profile));
            CollectionAssert.AreEqual(new[]{"paver","",""},ProfileExhibition.Read(book,profile));
            CompanyIdentity.Remove(book,CompanyIdentity.OwnerKey(profile));
            CollectionAssert.AreEqual(new[]{"","",""},ProfileExhibition.Read(book,profile));
        }

        [Test]
        public void PhotoWritesUniquePngFilesAndRejectsInvalidInput()
        {
            string folder=Path.Combine(Path.GetTempPath(),"CityHighways-photo-test-"+Guid.NewGuid().ToString("N"));
            var source=new Texture2D(2,2,TextureFormat.RGB24,false);
            try
            {
                Assert.Throws<ArgumentException>(()=>CharacterPhoto.WritePng(folder,new byte[]{1,2,3}));
                Assert.IsFalse(Directory.Exists(folder));
                source.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});
                source.Apply();
                var png=source.EncodeToPNG();
                string first=CharacterPhoto.WritePng(folder,png);
                string second=CharacterPhoto.WritePng(folder,png);
                Assert.AreNotEqual(first,second);
                Assert.AreEqual(".png",Path.GetExtension(first));
                CollectionAssert.AreEqual(png,File.ReadAllBytes(first));
                CollectionAssert.AreEqual(png,File.ReadAllBytes(second));
                Assert.AreEqual(2,Directory.GetFiles(folder).Length);
            }
            finally
            {
                Object.DestroyImmediate(source);
                if(Directory.Exists(folder)) Directory.Delete(folder,true);
            }
        }

        [Test]
        public void PhotoRejectsMissingAndUncreatedTextures()
        {
            var rt=new RenderTexture(16,16,0);
            try
            {
                Assert.IsFalse(CharacterPhoto.TrySave(null,out var path));
                Assert.IsNull(path);
                Assert.IsFalse(CharacterPhoto.TrySave(rt,out path));
                Assert.IsNull(path);
            }
            finally { Object.DestroyImmediate(rt); }
        }
    }
}
