using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class HudTurnClockTests
    {
        [TestCase(false, false, HudRefreshKind.None)]
        [TestCase(false, true, HudRefreshKind.ClockOnly)]
        [TestCase(true, false, HudRefreshKind.Full)]
        [TestCase(true, true, HudRefreshKind.Full)]
        public void StateChangesAlwaysWinOverClockRepaint(bool full, bool clock, HudRefreshKind expected) =>
            Assert.AreEqual(expected, HudTurnClock.SelectRefresh(full, clock));

        [TestCase(60f, 59.9f, false)]
        [TestCase(60f, 59f, true)]
        [TestCase(10.01f, 10f, true)]
        [TestCase(10f, 9.9f, false)]
        [TestCase(0.1f, 0f, true)]
        [TestCase(0f, -0.1f, false)]
        public void RefreshOnlyOnVisibleChange(float before, float after, bool expected) =>
            Assert.AreEqual(expected, HudTurnClock.NeedsRefresh(before, after));

        [TestCase(true, 10f, true)]
        [TestCase(true, 10.01f, false)]
        [TestCase(false, 0f, false)]
        public void WarningRequiresVisibleClock(bool visible, float seconds, bool expected) =>
            Assert.AreEqual(expected, HudTurnClock.IsUrgent(visible, seconds));

        [TestCase(60.1f, true, "  ·  remaining 1:01")]
        [TestCase(59.1f, false, "  ·  осталось 1:00")]
        [TestCase(-1f, true, "  ·  remaining 0:00")]
        public void ClockFormatsLocalizedRoundedSeconds(float seconds, bool english, string expected) =>
            Assert.AreEqual(expected, HudTurnClock.Text(seconds, english));
    }
}
