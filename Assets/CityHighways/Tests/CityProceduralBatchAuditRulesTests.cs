using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class CityProceduralBatchAuditRulesTests
    {
        const string City = "Москва";
        const string Id = "B:12:300:400";

        [Test]
        public void OtherCityIsSkippedRatherThanReportedAsBroken()
        {
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.SkippedOtherCity,
                CityProceduralBatchAuditRules.Classify(City, "Чунцин", "", false, false, false, false));
        }

        [Test]
        public void MissingIdIsDetectedBeforePrefabCheck()
        {
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.MissingObjectId,
                CityProceduralBatchAuditRules.Classify(City, City, "", false, false, false, false));
        }

        [Test]
        public void MissingPrefabIsReported()
        {
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.MissingPrefab,
                CityProceduralBatchAuditRules.Classify(City, City, Id, false, true, true, true));
        }

        [Test]
        public void MissingIdMatchAndKindMismatchAreDistinct()
        {
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.MissingCatalogObject,
                CityProceduralBatchAuditRules.Classify(City, City, Id, true, false, false, false));
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.WrongObjectKind,
                CityProceduralBatchAuditRules.Classify(City, City, Id, true, true, false, true));
        }

        [Test]
        public void ValidIdWithNonMeshPrefabCannotBeMeasured()
        {
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.InvalidGeometry,
                CityProceduralBatchAuditRules.Classify(City, City, Id, true, true, true, false));
        }

        [Test]
        public void MatchingCompleteReplacementIsReady()
        {
            Assert.AreEqual(CityProceduralBatchAuditRules.Status.Ready,
                CityProceduralBatchAuditRules.Classify(City, City, Id, true, true, true, true));
        }
    }
}
