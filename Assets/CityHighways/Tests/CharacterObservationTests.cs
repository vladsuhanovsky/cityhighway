using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;

namespace CityHighways.Tests
{
    public sealed class CharacterObservationTests
    {
        [Test]
        public void ReturnRestoresBookmarkOnlyOnce()
        {
            var session = new CharacterObservation();
            var pan = new Vector2(30f, -45f);
            Assert.IsTrue(session.Begin(2.5f, pan));
            Assert.IsTrue(session.Active);
            Assert.IsFalse(session.Begin(8f, Vector2.zero));
            Assert.IsTrue(session.End(true, out var zoom, out var returnedPan));
            Assert.AreEqual(2.5f, zoom);
            Assert.AreEqual(pan, returnedPan);
            Assert.IsFalse(session.Active);
            Assert.IsFalse(session.End(true, out _, out _));
        }

        [Test]
        public void ManualTakeoverAbandonsOldBookmarkAndNextSessionStartsFresh()
        {
            var session = new CharacterObservation();
            Assert.IsTrue(session.Begin(1f, Vector2.zero));
            Assert.IsFalse(session.End(false, out _, out _));
            Assert.IsFalse(session.Active);
            Assert.IsFalse(session.End(true, out _, out _));
            Assert.IsTrue(session.Begin(5f, new Vector2(100f, 20f)));
            Assert.IsTrue(session.End(true, out var zoom, out var pan));
            Assert.AreEqual(5f, zoom);
            Assert.AreEqual(new Vector2(100f, 20f), pan);
        }

        [Test]
        public void InvalidCoordinatesDoNotStartObservation()
        {
            var session = new CharacterObservation();
            Assert.IsFalse(session.Begin(float.NaN, Vector2.zero));
            Assert.IsFalse(session.Begin(2f, new Vector2(float.PositiveInfinity, 0f)));
            Assert.IsFalse(session.Active);
            Assert.IsFalse(CharacterObservation.IsFinite(new Vector3(0f, float.NaN, 0f)));
            Assert.IsTrue(CharacterObservation.IsFinite(Vector3.zero));
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(135f)]
        public void WorldFocusUsesSameProjectionAsRenderedCity(float yaw)
        {
            var layout = new BoardLayout(TestCities.Small());
            var area = new Rect(0f, 0f, 1000f, 700f);
            layout.SetPerspective(true, 38f, yaw);
            layout.Fit(area, 1f, 2f, new Vector2(20f,-20f));
            var position = new Vector3(.1f, .05f, -.1f);
            var pan = layout.PanToCenterWorld(position, 8f);
            layout.Fit(area, 1f, 8f, pan);
            Assert.That(Vector2.Distance(area.center, layout.WorldToScreen(position)), Is.LessThan(.05f));
            Assert.AreEqual(Vector2.zero, layout.PanToCenterWorld(position, 1f));
            var edge = layout.PanToCenterWorld(new Vector3(100f,0f,100f), 8f);
            Assert.That(Mathf.Abs(edge.x), Is.LessThanOrEqualTo(3500f));
            Assert.That(Mathf.Abs(edge.y), Is.LessThanOrEqualTo(2450f));
        }

        [Test]
        public void FlatMapCannotStartObservationAndNoTargetControlStaysHidden()
        {
            var map = new MapView();
            var control = new CharacterObservationControl(map);
            Assert.IsFalse(map.CanObserveCharacter);
            Assert.IsFalse(map.BeginCharacterObservation(Vector3.zero));
            control.Sync(null);
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, control.style.display.value);
            Assert.IsFalse(map.IsObservingCharacter);
            map.EndCharacterObservation(true);
        }
    }
}
