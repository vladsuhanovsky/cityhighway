using System;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class WorkshopMapValidatorTests
    {
        static string Sample() => Resources.Load<TextAsset>("CityHighways/Maps/paris").text;
        static CityMapDto Draft() => JsonUtility.FromJson<CityMapDto>(Sample());

        [TestCase("paris")]
        [TestCase("berlin")]
        public void AcceptsKnownCity(string key)
        {
            string json = Resources.Load<TextAsset>("CityHighways/Maps/" + key).text;
            Assert.IsTrue(WorkshopMapValidator.TryRead(json, out var map, out var error), error);
            Assert.AreEqual(42, map.Locations.Count);
            Assert.IsNull(error);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{")]
        [TestCase("{}")]
        public void RejectsInvalidPayload(string json)
        {
            Assert.IsFalse(WorkshopMapValidator.TryRead(json, out var map, out var error));
            Assert.IsNull(map);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void EmptyOptionalRingIsAbsentButMalformedRingIsRejected()
        {
            var dto = Draft();
            dto.ring = new RingDto();
            Assert.IsTrue(WorkshopMapValidator.TryRead(JsonUtility.ToJson(dto), out var map, out var error), error);
            Assert.IsNull(map.Ring);
            dto.ring.rx = 0.2f;
            Assert.IsFalse(WorkshopMapValidator.TryRead(JsonUtility.ToJson(dto), out map, out error));
            Assert.AreEqual("ring", error);
            Assert.IsNull(map);
        }

        [Test]
        public void BoundsUtf8Payload()
        {
            string json = new string('я', WorkshopMapValidator.MaxUtf8Bytes / 2 + 1);
            Assert.IsFalse(WorkshopMapValidator.TryRead(json, out var map, out var error));
            Assert.IsNull(map);
            Assert.AreEqual("size", error);
        }

        [TestCase("numeric-resource", "road")]
        [TestCase("missing-endpoint", "road")]
        [TestCase("duplicate-id", "location")]
        [TestCase("coordinate", "location")]
        [TestCase("label", "name")]
        [TestCase("few-contracts", "counts")]
        [TestCase("duplicate-road", "rules")]
        [TestCase("isolated-location", "connectivity")]
        [TestCase("all-closed", "open-districts")]
        public void RejectsInvalidMap(string defect, string expected)
        {
            var dto = Draft();
            switch (defect)
            {
                case "numeric-resource": dto.roads[0].resource = "999"; break;
                case "missing-endpoint": dto.roads[0].from = "absent"; break;
                case "duplicate-id": dto.locations[1].id = dto.locations[0].id; break;
                case "coordinate": dto.locations[0].x = 1.1f; break;
                case "label": dto.name = "<b>City</b>"; break;
                case "few-contracts": dto.contracts = new ContractDto[0]; break;
                case "duplicate-road": dto.roads[1].id = dto.roads[0].id; break;
                case "isolated-location":
                    string id = dto.locations[0].id;
                    dto.roads = dto.roads.Where(r => r.from != id && r.to != id).ToArray();
                    break;
                case "all-closed":
                    foreach (var district in dto.districts) district.expansion = true;
                    break;
            }
            Assert.IsFalse(WorkshopMapValidator.TryRead(JsonUtility.ToJson(dto), out var map, out var error));
            Assert.IsNull(map);
            Assert.AreEqual(expected, error);
        }
    }
}
