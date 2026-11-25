using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class CampaignTests
    {
        const string Owner = "local:12345678901234567890123456789012";

        [Test]
        public void CatalogHasTwelveUniqueStableMissionsAndSeeds()
        {
            Assert.AreEqual(12, CampaignCatalog.All.Count);
            Assert.AreEqual(12, CampaignCatalog.All.Select(m => m.Id).Distinct().Count());
            Assert.AreEqual(12, CampaignCatalog.All.Select(m => m.Seed).Distinct().Count());
            Assert.AreEqual(3, CampaignCatalog.All.Select(m => m.Map).Distinct().Count());
            Assert.IsTrue(CampaignCatalog.All.Any(m => m.Scenario == ScenarioKind.SupplySeason));
            Assert.IsTrue(CampaignCatalog.All.Any(m => m.Coop && m.Difficulty == CoopDifficultyKind.Hard));
        }

        [Test]
        public void UnlocksAreSequentialAndIdempotent()
        {
            int completed = 0;
            Assert.IsFalse(CampaignCatalog.Unlocked("mission-02", completed));
            Assert.AreEqual(0, CampaignCatalog.Advance("mission-02", completed));
            foreach (var mission in CampaignCatalog.All)
            {
                Assert.IsTrue(CampaignCatalog.Unlocked(mission.Id, completed));
                int next = CampaignCatalog.Advance(mission.Id, completed);
                Assert.AreEqual(completed + 1, next);
                Assert.AreEqual(next, CampaignCatalog.Advance(mission.Id, next));
                completed = next;
            }
            Assert.AreEqual(12, completed);
            Assert.IsFalse(CampaignCatalog.Unlocked("missing", completed));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        public void MissionSetupRoundTripsWithFixedRulesAndMap(int index)
        {
            var mission = CampaignCatalog.All[index];
            var setup = mission.CreateSetup("Player", Owner, false);
            var restored = JsonUtility.FromJson<GameSetup>(JsonUtility.ToJson(setup));
            Assert.IsTrue(mission.Matches(restored));
            Assert.AreEqual(Owner, restored.campaignOwner);
            Assert.AreEqual(mission.Coop, restored.CreateConfig().Cooperative);
            Assert.AreEqual(mission.Pieces, restored.CreateConfig().RoadPiecesOverride);
            var map = CityHighwaysApp.LoadMap(mission.Map);
            Assert.IsEmpty(map.Validate(restored.CreateConfig().MaxRoadLength));
            var game = new Game(map, restored.PlayerNames(), restored.CreateConfig(), mission.Seed, restored.ResolveCharacters(new System.Random(mission.Seed)));
            Assert.IsFalse(mission.Passed(game));
            restored.roadPieces++;
            Assert.IsFalse(mission.Matches(restored));
        }

        [Test]
        public void CampaignOwnerIsStableAndPathsAreScoped()
        {
            var profile = new PlayerProfile { name = "Old", campaignId = Owner.Substring(6) };
            Assert.AreEqual(Owner, CampaignStorage.Owner(profile));
            profile.name = "New";
            Assert.AreEqual(Owner, CampaignStorage.Owner(profile));
            Assert.IsFalse(CampaignStorage.ValidOwner("local:../bad"));
            Assert.AreNotEqual(CampaignStorage.CheckpointPath(Owner), CampaignStorage.CheckpointPath("local:abcdefabcdefabcdefabcdefabcdefab"));
            Assert.IsFalse(new GameSetup().IsCampaign);
        }

        [Test]
        public void GoalTextChangesLanguage()
        {
            string old = LocalizationService.Language;
            try
            {
                LocalizationService.SetLanguage("en");
                StringAssert.Contains("bridges", CampaignText.Goal(CampaignCatalog.All[1]));
                LocalizationService.SetLanguage("ru");
                StringAssert.Contains("мостов", CampaignText.Goal(CampaignCatalog.All[1]));
            }
            finally { LocalizationService.SetLanguage(old); }
        }
    }
}
