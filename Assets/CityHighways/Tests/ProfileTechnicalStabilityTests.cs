using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class ProfileTechnicalStabilityTests
    {
        static PlayerProfile Create(ProfileBook book, string name)
        {
            var profile = new PlayerProfile { name = name, wallet = 25 };
            book.profiles.Add(profile);
            return profile;
        }

        [Test]
        public void FailedSaveRestoresCompanyFleetAndExhibitionTogether()
        {
            var book = new ProfileBook();
            var alpha = Create(book, "Alpha");
            var beta = Create(book, "Beta");
            CompanyIdentity.Store(book, alpha, CompanyIdentity.Create(alpha,"Roads","","green","white"));
            FleetLivery.Store(book, alpha,"mixer","reverse",true);
            ProfileExhibition.Store(book,alpha,0,"paver");
            CompanyIdentity.Store(book,beta,CompanyIdentity.Create(beta,"Other","","red","blue"));
            string before = JsonUtility.ToJson(book);
            var original = book.companies.Find(r=>r.owner==CompanyIdentity.OwnerKey(alpha));
            int writes = 0;
            Assert.IsFalse(ProfileCosmeticSave.TryCommit(book,alpha,()=>
            {
                CompanyIdentity.Store(book,alpha,CompanyIdentity.Create(alpha,"Changed","","blue","steel"));
                FleetLivery.Store(book,alpha,"mixer","solid",false);
                ProfileExhibition.Store(book,alpha,2,"paver");
                return true;
            },()=>{ writes++; return false; }));
            Assert.AreEqual(1,writes);
            Assert.AreEqual(before,JsonUtility.ToJson(book));
            Assert.AreSame(original,book.companies.Find(r=>r.owner==CompanyIdentity.OwnerKey(alpha)));
        }

        [Test]
        public void SuccessfulSaveAndRejectedChangeUseExactlyOnePersistenceBoundary()
        {
            var book = new ProfileBook();
            var alpha = Create(book,"Alpha");
            int writes=0;
            bool Persist() { writes++; return true; }
            Assert.IsTrue(ProfileCosmeticSave.TryCommit(book,alpha,
                ()=>ProfileExhibition.Store(book,alpha,0,"paver"),Persist));
            Assert.AreEqual(1,writes);
            var before=JsonUtility.ToJson(book);
            Assert.IsFalse(ProfileCosmeticSave.TryCommit(book,alpha,()=>
            {
                ProfileExhibition.Store(book,alpha,2,"paver");
                return false;
            },Persist));
            Assert.AreEqual(1,writes);
            Assert.AreEqual(before,JsonUtility.ToJson(book));
            Assert.IsFalse(ProfileCosmeticSave.TryCommit(book,new PlayerProfile{name="Alpha"},
                ()=>throw new InvalidOperationException("Must not execute"),Persist));
            Assert.AreEqual(1,writes);
        }

        [Test]
        public void ExceptionDuringSaveRestoresLegacyNullList()
        {
            var book = new ProfileBook { companies = null };
            var alpha = Create(book,"Alpha");
            Assert.IsFalse(ProfileCosmeticSave.TryCommit(book,alpha,
                ()=>ProfileExhibition.Store(book,alpha,0,"paver"),
                ()=>throw new InvalidOperationException("Simulated storage failure")));
            Assert.IsNull(book.companies);
            Assert.AreEqual(25,alpha.wallet);
        }

        [Test]
        public void MigrationDoesNotMutatePriorSnapshotAndKeepsDestinationOnCollision()
        {
            var book=new ProfileBook();
            var profile=Create(book,"Old");
            ProfileExhibition.Store(book,profile,0,"paver");
            FleetLivery.Store(book,profile,"crane","reverse",true);
            string oldKey=CompanyIdentity.OwnerKey(profile);
            var old=book.companies[0];
            profile.name="New";
            CompanyIdentity.Move(book,oldKey,CompanyIdentity.OwnerKey(profile));
            Assert.AreEqual(oldKey,old.owner);
            Assert.AreEqual("paver",ProfileExhibition.Read(book,profile)[0]);
            Assert.AreEqual("reverse",FleetLivery.Read(book,profile,"crane").Scheme);
            var other=Create(book,"Other");
            CompanyIdentity.Store(book,other,CompanyIdentity.Create(other,"Destination","","red","white"));
            CompanyIdentity.Move(book,CompanyIdentity.OwnerKey(profile),CompanyIdentity.OwnerKey(other));
            Assert.AreEqual("Destination",CompanyIdentity.Read(book,other).Name);
            Assert.AreEqual("",ProfileExhibition.Read(book,other)[0]);
        }

        [Test]
        public void DestroyedFleetRootsArePrunedBeforeAcquiringReplacements()
        {
            var host=new GameObject("Fleet lifecycle test");
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader);
            var material=new Material(shader);
            var pool=new ConstructionFleetPool(host.transform,material);
            try
            {
                var value=FleetLivery.Read(null,null,"paver");
                var keep=pool.Acquire(value);
                Assert.IsNotNull(keep);
                for(int i=0;i<5;i++)
                {
                    var lost=pool.Acquire(value);
                    Assert.IsNotNull(lost);
                    Assert.AreEqual(2,pool.Count);
                    Object.DestroyImmediate(lost.Root);
                    // The next acquire must prune this dead reference, even if it
                    // was never released through the ordinary construction path.
                }
                var replacement=pool.Acquire(value);
                Assert.IsNotNull(replacement);
                Assert.AreEqual(2,pool.Count);
                Assert.AreEqual(2,pool.ActiveCount);
                Assert.IsNull(pool.Acquire(value));
                pool.Release(keep);
                pool.Release(replacement);
                Assert.AreEqual(0,pool.ActiveCount);
                pool.Clear();
                pool.Clear();
                Assert.AreEqual(0,host.transform.childCount);
                Assert.AreEqual(0,pool.Count);
            }
            finally {pool.Clear();Object.DestroyImmediate(host);Object.DestroyImmediate(material);}
        }

        [Test]
        public void DetachedRenderControlsAreUnavailableAndCanBeReleasedRepeatedly()
        {
            var parent=new VisualElement();
            var map=new MapView();
            var control=new CharacterObservationControl(map);
            parent.Add(control);
            Assert.IsFalse(ProfileRenderVisibility.IsVisible(parent));
            Assert.IsFalse(ProfileRenderVisibility.IsParentVisible(control));
            Assert.IsFalse(ProfileRenderVisibility.IsVisible(null));
            control.Sync(null);
            control.Sync(null);
            Assert.IsFalse(map.IsObservingCharacter);
            Assert.AreEqual(DisplayStyle.None,control.style.display.value);
            var stage=new FleetGarageStage();
            parent.Add(stage);
            stage.Show(FleetLivery.Read(null,null,"crane"));
            Assert.IsFalse(stage.IsShowing3D);
            stage.Release();
            stage.Release();
        }
    }
}
