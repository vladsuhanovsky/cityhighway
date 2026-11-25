using System;
using System.Linq;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SocialReadContractsTests
    {
        const string Record = "{\"id\":19,\"playerId\":\"76561198000000001\",\"name\":\"Friend\"," +
            "\"mode\":\"classic\",\"mapPath\":\"Maps/demo\",\"mapName\":\"Demo\"," +
            "\"score\":97,\"won\":true,\"seats\":2,\"date\":\"2026-11-25T08:00:00Z\"}";

        const string ClubData = "{\"id\":\"club-123\",\"name\":\"Road Builders\"," +
            "\"tag\":\"ROAD\",\"code\":\"ABC123\",\"open\":true,\"logo\":\"0.0.0.0\",\"owner\":true}";

        const string Member = "{\"name\":\"Friend\",\"role\":\"owner\",\"me\":true," +
            "\"amount\":4,\"games\":2}";

        const string Week = "{\"id\":\"2026-W48\",\"endsAt\":\"2026-11-30T00:00:00Z\"," +
            "\"kind\":\"contracts\",\"target\":40,\"progress\":4,\"completed\":false}";

        static string Friends(string records) => "{\"records\":[" + records + "]}";
        static string Club(string club, string members = null, string week = null, string records = null) =>
            "{\"club\":" + club +
            ",\"members\":[" + (members ?? Member) + "],\"week\":" + (week ?? Week) +
            ",\"weeksDone\":1,\"logos\":[{\"logo\":\"0.0.0.0\",\"week\":\"\"}]," +
            "\"records\":[" + (records ?? Record) + "]}";

        [Test]
        public void FriendsAcceptEmptyCollection()
        {
            Assert.IsTrue(SocialReadContracts.ValidateFriends("{\"records\":[]}"));
        }

        [Test]
        public void FriendsAcceptValidRecordWithCompleteTypes()
        {
            Assert.IsTrue(SocialReadContracts.ValidateFriends(Friends(Record)));
        }

        [Test]
        public void FriendsAcceptServerMaximumTwoHundredRecords()
        {
            Assert.IsTrue(SocialReadContracts.ValidateFriends(
                Friends(string.Join(",", Enumerable.Repeat(Record, 200)))));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"records\":null}")]
        [TestCase("{\"records\":{}}")]
        [TestCase("{\"records\":[null]}")]
        [TestCase("{\"records\":[{}]}")]
        [TestCase("{\"records\":[7]}")]
        [TestCase("{\"records\":[],\"records\":[]}")]
        [TestCase("{\"other\":{\"records\":[]}}")]
        [TestCase("{\"records\":[]} trailing")]
        public void FriendsRejectMissingMalformedOrSpoofedRecordCollections(string json)
        {
            Assert.IsFalse(SocialReadContracts.ValidateFriends(json), json);
        }

        [Test]
        public void FriendsRejectMissingOrWrongPrimitiveTypes()
        {
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                Friends(Record.Replace("\"won\":true", "\"won\":1"))));
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                Friends(Record.Replace("\"score\":97", "\"score\":97.0"))));
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                Friends(Record.Replace("\"seats\":2", "\"seats\":\"2\""))));
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                Friends(Record.Replace("\"playerId\":\"76561198000000001\"", "\"playerId\":null"))));
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                Friends(Record.Replace("\"id\":19,", ""))));
        }

        [Test]
        public void FriendsRejectExcessiveArrayOrResponseSize()
        {
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                Friends(string.Join(",", Enumerable.Repeat(Record, 201)))));
            Assert.IsFalse(SocialReadContracts.ValidateFriends(
                "{\"records\":[],\"padding\":\"" +
                new string('x', SocialReadContracts.MaxCharacters) + "\"}"));
        }

        [Test]
        public void ClubNullRemainsNullRatherThanUnityPhantomObject()
        {
            Assert.IsTrue(SocialReadContracts.TryParseClub("{\"club\":null}", out var state));
            Assert.IsNotNull(state);
            Assert.IsNull(state.club);
            Assert.IsFalse(state.HasClub);
        }

        [Test]
        public void ClubAcceptsFullMemberWeekLogoAndRecord()
        {
            Assert.IsTrue(SocialReadContracts.TryParseClub(Club(ClubData), out var state));
            Assert.IsTrue(state.HasClub);
            Assert.AreEqual("ROAD", state.club.tag);
            Assert.AreEqual("ABC123", state.club.code);
            Assert.IsTrue(state.club.owner);
            Assert.AreEqual(1, state.members.Length);
            Assert.IsTrue(state.members[0].me);
            Assert.AreEqual(40, state.week.target);
            Assert.AreEqual(1, state.records.Length);
        }

        [TestCase("{}")]
        [TestCase("{\"club\":{}}")]
        [TestCase("{\"club\":false}")]
        [TestCase("{\"club\":\"yes\"}")]
        [TestCase("{\"club\":[],\"members\":[]}")]
        [TestCase("{\"hint\":{\"club\":null}}")]
        [TestCase("{\"club\":null,\"club\":null}")]
        public void ClubRejectsMalformedTopLevelState(string json)
        {
            Assert.IsFalse(SocialReadContracts.TryParseClub(json, out var state));
            Assert.IsNull(state);
        }

        [Test]
        public void ClubRejectsMissingTypedFields()
        {
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData.Replace("\"owner\":true", "\"owner\":1")), out _));
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData).Replace("\"weeksDone\":1", "\"weeksDone\":\"1\""), out _));
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData).Replace("\"logos\":[{\"logo\":\"0.0.0.0\",\"week\":\"\"}]",
                    "\"logos\":null"), out _));
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData).Replace("\"week\":" + Week, "\"week\":null"), out _));
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData).Replace("\"records\":[" + Record + "]", "\"records\":[{}]"), out _));
        }

        [Test]
        public void ClubRejectsBadMemberOrWeeklyProgressData()
        {
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData, "{\"name\":\"Friend\",\"role\":\"owner\",\"me\":\"true\",\"amount\":4,\"games\":2}"), out _));
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData, week: Week.Replace("\"progress\":4", "\"progress\":-1")), out _));
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData, week: Week.Replace("\"completed\":false", "\"completed\":null")), out _));
        }

        [Test]
        public void ClubAcceptsEmptyButCompleteCollections()
        {
            string json = Club(ClubData, members: "", records: "").Replace(
                "\"logos\":[{\"logo\":\"0.0.0.0\",\"week\":\"\"}]",
                "\"logos\":[]");
            Assert.IsTrue(SocialReadContracts.TryParseClub(json, out var state));
            Assert.AreEqual(0, state.members.Length);
            Assert.AreEqual(0, state.records.Length);
        }

        [Test]
        public void ClubListAcceptsEmptyAndCompleteEntries()
        {
            Assert.IsTrue(SocialReadContracts.ValidateClubList("{\"clubs\":[]}"));
            Assert.IsTrue(SocialReadContracts.ValidateClubList(
                "{\"clubs\":[{\"id\":\"c1\",\"name\":\"Road Builders\",\"tag\":\"ROAD\",\"logo\":\"0.0.0.0\",\"members\":4}]}"));
        }

        [TestCase("{}")]
        [TestCase("{\"clubs\":null}")]
        [TestCase("{\"clubs\":[null]}")]
        [TestCase("{\"clubs\":[{}]}")]
        [TestCase("{\"clubs\":[{\"id\":\"c1\",\"name\":\"Road Builders\",\"tag\":\"ROAD\",\"logo\":\"0.0.0.0\"}]}")]
        [TestCase("{\"clubs\":[{\"id\":\"c1\",\"name\":\"Road Builders\",\"tag\":\"ROAD\",\"logo\":\"0.0.0.0\",\"members\":4.5}]}")]
        [TestCase("{\"clubs\":[{\"id\":\"c1\",\"name\":\"Road Builders\",\"tag\":\"ROAD\",\"logo\":\"0.0.0.0\",\"members\":31}]}")]
        [TestCase("{\"clubs\":[],\"clubs\":[]}")]
        public void ClubListRejectsIncompleteOrDuplicatedEntries(string json)
        {
            Assert.IsFalse(SocialReadContracts.ValidateClubList(json), json);
        }

        [Test]
        public void ClubListRejectsMoreThanThirtyItems()
        {
            const string brief = "{\"id\":\"c1\",\"name\":\"Road Builders\",\"tag\":\"ROAD\",\"logo\":null,\"members\":4}";
            Assert.IsFalse(SocialReadContracts.ValidateClubList(
                "{\"clubs\":[" + string.Join(",", Enumerable.Repeat(brief, 31)) + "]}"));
        }

        [Test]
        public void InvalidClubDataNeverProducesPartialResult()
        {
            Assert.IsFalse(SocialReadContracts.TryParseClub(
                Club(ClubData.Replace("\"tag\":\"ROAD\"", "\"tag\":[]")), out var state));
            Assert.IsNull(state);
        }

        [Test]
        public void ReportAckValidatorRemainsCompatibleWithSharedReader()
        {
            Assert.IsTrue(SocialReportAck.TryParse(
                "{\"improved\":true,\"best\":100,\"club\":null}", out var response));
            Assert.IsNull(response.club);
            Assert.IsTrue(response.improved);
        }
    }
}
