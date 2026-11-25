using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SocialReportAckTests
    {
        [Test]
        public void AcceptsRecordOnlyResponseWithNoClub()
        {
            Assert.IsTrue(SocialReportAck.TryParse(
                "{\"improved\":false,\"best\":0,\"club\":null}", out var response));
            Assert.IsNotNull(response);
            Assert.IsFalse(response.improved);
            Assert.AreEqual(0, response.best);
            Assert.IsNull(response.club);
        }

        [Test]
        public void AcceptsImprovedRecordAndClubContribution()
        {
            Assert.IsTrue(SocialReportAck.TryParse(
                "{\"improved\":true,\"best\":87,\"club\":{\"kind\":\"roads\",\"amount\":3,\"closed\":false,\"logo\":null}}",
                out var response));
            Assert.IsTrue(response.improved);
            Assert.AreEqual(87, response.best);
            Assert.AreEqual("roads", response.club.kind);
            Assert.AreEqual(3, response.club.amount);
        }

        [Test]
        public void AcceptsCompletedClubGoalWithLogo()
        {
            Assert.IsTrue(SocialReportAck.TryParse(
                "{\"improved\":false,\"best\":-100,\"club\":{\"kind\":\"wins\",\"amount\":1,\"closed\":true,\"logo\":\"star.sun.gold.2\"}}",
                out var response));
            Assert.IsTrue(response.club.closed);
            Assert.AreEqual("star.sun.gold.2", response.club.logo);
        }

        [Test]
        public void AcceptsHarmlessUnknownFieldsWithoutConfusingNestedKeys()
        {
            string json = "{\"metadata\":{\"best\":99999,\"array\":[true,null,{\"detail\":\"hi\"}]},\"improved\":false,\"best\":50,\"club\":null}";
            Assert.IsTrue(SocialReportAck.TryParse(json, out var response));
            Assert.AreEqual(50, response.best);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("not json")]
        [TestCase("{\"improved\":false,\"best\":50}")]
        [TestCase("{\"improved\":false,\"club\":null}")]
        [TestCase("{\"best\":50,\"club\":null}")]
        [TestCase("{\"improved\":\"false\",\"best\":50,\"club\":null}")]
        [TestCase("{\"improved\":0,\"best\":50,\"club\":null}")]
        [TestCase("{\"improved\":false,\"best\":\"50\",\"club\":null}")]
        [TestCase("{\"improved\":false,\"best\":50.0,\"club\":null}")]
        [TestCase("{\"improved\":false,\"best\":1e2,\"club\":null}")]
        [TestCase("{\"improved\":false,\"best\":1001,\"club\":null}")]
        [TestCase("{\"improved\":false,\"best\":-101,\"club\":null}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":false}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":{}}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":{\"kind\":\"roads\",\"amount\":3,\"closed\":false}}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":{\"kind\":\"roads\",\"amount\":3,\"closed\":\"false\",\"logo\":null}}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":{\"kind\":\"roads\",\"amount\":-1,\"closed\":false,\"logo\":null}}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":{\"kind\":\"roads\",\"amount\":3.5,\"closed\":false,\"logo\":null}}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":{\"kind\":\"roads\",\"amount\":3,\"closed\":false,\"logo\":99}}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":null,\"best\":99}")]
        [TestCase("{\"improved\":false,\"best\":50,\"club\":null} trailing")]
        [TestCase("{\"note\":\"\\\"improved\\\":true,\\\"best\\\":55,\\\"club\\\":null\"}")]
        [TestCase("{\"improved\":false,\"best\":01,\"club\":null}")]
        public void RejectsIncompleteMalformedOrSpoofedAcknowledgement(string json)
        {
            Assert.IsFalse(SocialReportAck.TryParse(json, out var response), json);
            Assert.IsNull(response);
        }

        [Test]
        public void RejectsOversizedOrDeeplyNestedAcknowledgement()
        {
            string large = "{\"improved\":false,\"best\":0,\"club\":null,\"padding\":\"" +
                new string('x', SocialReportAck.MaxCharacters) + "\"}";
            Assert.IsFalse(SocialReportAck.TryParse(large, out _));
            string deeplyNested = "{\"improved\":false,\"best\":0,\"club\":null,\"extra\":" +
                new string('[', 20) + "0" + new string(']', 20) + "}";
            Assert.IsFalse(SocialReportAck.TryParse(deeplyNested, out _));
        }

        [Test]
        public void EscapedFieldsCannotImpersonateMissingRootAcknowledgement()
        {
            string json = "{\"log\":\"a \\\"best\\\":9 \\\"improved\\\":true \\\"club\\\":null\"}";
            Assert.IsFalse(SocialReportAck.TryParse(json, out _));
        }
    }
}
