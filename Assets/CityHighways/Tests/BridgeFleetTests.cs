using NUnit.Framework;
using UnityEngine;
using CityHighways.Core;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class BridgeFleetTests
    {
        [Test]
        public void BridgeClaimsIgnoreInitialSyncRepeatedFramesAndTransfers()
        {
            var claims = new BridgeFleetClaims();
            Assert.IsFalse(claims.Observe("owned", 0, false, out var changed));
            Assert.IsFalse(changed);
            Assert.IsFalse(claims.Observe("owned", 0, true, out changed));
            Assert.IsFalse(changed);
            Assert.IsFalse(claims.Observe("owned", 1, true, out changed));
            Assert.IsTrue(changed);
            Assert.IsFalse(claims.Observe("free", null, false, out changed));
            Assert.IsTrue(claims.Observe("free", 1, true, out changed));
            Assert.IsTrue(changed);
            Assert.IsFalse(claims.Observe("free", 1, true, out changed));
            Assert.IsFalse(claims.Observe("free", null, true, out changed));
            Assert.IsTrue(changed);
            Assert.IsTrue(claims.Observe("free", 0, true, out changed));
            claims.Clear();
            Assert.IsFalse(claims.Observe("free", 0, true, out changed));
            Assert.IsFalse(changed);
        }

        static BuiltRoad Example()
        {
            var road = new BuiltRoad { Start = new Vector3(1, .1f, 2), End = new Vector3(3, .3f, 2), Length = 2f };
            road.WaterSpans.Add(new WaterSpan(.4f,1.6f));
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Ramp, From = .1f, To = .4f, FromHeight = 0f, ToHeight = .2f });
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Bridge, From = .4f, To = 1.6f, FromHeight = .2f, ToHeight = .2f });
            return road;
        }

        [Test]
        public void CraneUsesDeckHeightAndRejectsShortOrMissingDeck()
        {
            var road = Example();
            Assert.IsTrue(BridgeFleetClaims.TryDeckPosition(road, out var point));
            Assert.That(point.x, Is.EqualTo(2f).Within(.0001f));
            Assert.That(point.y, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(point.z, Is.EqualTo(2f).Within(.0001f));
            road.Pieces.RemoveAt(1);
            Assert.IsFalse(BridgeFleetClaims.TryDeckPosition(road, out _));
            road.Pieces.Add(new RoadPiece { Kind = RoadPieceKind.Bridge, From = .4f, To = .45f });
            Assert.IsFalse(BridgeFleetClaims.TryDeckPosition(road, out _));
            Assert.IsFalse(BridgeFleetClaims.TryDeckPosition(null, out _));
        }

        [Test]
        public void MagnateUsesHumanOwnerAndExcludesPublicBotTutorial()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name = "Alpha" };
            var b = new PlayerProfile { name = "Beta" };
            book.profiles.Add(a); book.profiles.Add(b); book.active = a.name;
            CompanyIdentity.Store(book, b, CompanyIdentity.Create(b, "Beta Co", "", "green", "white"));
            FleetLivery.Store(book, b, "crane", "reverse", false);
            var setup = new GameSetup();
            setup.players.Add(new PlayerSlot("Alpha", false));
            setup.players.Add(new PlayerSlot("Beta", false));
            var livery = ConstructionFleetPolicy.ResolveMagnate(book, setup, 1, "Beta", 2, false, Resource.Steel);
            Assert.IsNotNull(livery);
            Assert.AreEqual("Beta Co", livery.CompanyName);
            Assert.AreEqual("crane", livery.Vehicle);
            Assert.AreEqual(CompanyIdentity.Paint("white"), livery.Primary);
            Assert.IsNull(ConstructionFleetPolicy.ResolveMagnate(book, setup, 1, "Beta", 1, false, Resource.Steel));
            Assert.IsNull(ConstructionFleetPolicy.ResolveMagnate(book, setup, 1, "Beta", 2, true, Resource.Steel));
            setup.magnateTutorialMap = true;
            Assert.IsNull(ConstructionFleetPolicy.ResolveMagnate(book, setup, 1, "Beta", 2, false, Resource.Steel));
        }

        [Test]
        public void BridgeEffectDoesNotAlterExistingGeometryOrMarkRoadUnderConstruction()
        {
            var host = new GameObject("Bridge fleet test host");
            var deck = new GameObject("Existing bridge");
            var set = ScriptableObject.CreateInstance<RoadConstructionSet>();
            try
            {
                var player = host.AddComponent<RoadConstructionPlayer>();
                player.Init(null, set);
                var road = Example();
                road.Root = deck;
                var piece = new GameObject("Existing deck");
                piece.transform.SetParent(deck.transform, false);
                var scale = new Vector3(1,2,3);
                piece.transform.localScale = scale;
                road.Growing.Add((piece.transform, scale, .4f, false));
                bool completed = false;
                player.Completed += _ => completed = true;
                var crane = FleetLivery.Read(null, null, "crane");
                Assert.IsTrue(player.PlayBridgeFleet(road, crane));
                Assert.IsFalse(player.IsBuilding(road));
                Assert.IsFalse(player.PlayBridgeFleet(road, crane));
                Assert.AreEqual(scale, piece.transform.localScale);
                Assert.IsTrue(piece.activeSelf);
                player.Cancel(road);
                Assert.AreEqual(scale, piece.transform.localScale);
                Assert.IsTrue(piece.activeSelf);
                Assert.IsFalse(completed);
                Assert.IsTrue(player.PlayBridgeFleet(road, crane));
                player.Cancel(road);
                deck.SetActive(false);
                Assert.IsFalse(player.PlayBridgeFleet(road, crane));
                Assert.IsFalse(completed);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(deck);
                Object.DestroyImmediate(set);
            }
        }
    }
}
