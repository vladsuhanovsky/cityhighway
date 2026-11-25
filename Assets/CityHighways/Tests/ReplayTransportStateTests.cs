using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class ReplayTransportStateTests
    {
        [TestCase(false, false, 0, 10, false, false, false, false)]
        [TestCase(true, false, 0, 0, false, false, false, false)]
        [TestCase(true, false, 0, 10, false, true, true, true)]
        [TestCase(true, false, 5, 10, true, true, true, true)]
        [TestCase(true, false, 10, 10, true, false, true, true)]
        [TestCase(true, true, 5, 10, true, false, false, false)]
        [TestCase(true, true, 0, 10, false, false, false, false)]
        public void AvailabilityRespectsPlaybackBoundariesAndErrors(bool game, bool error, int position,
            int actions, bool back, bool forward, bool play, bool seek)
        {
            var state = ReplayTransportState.Read(game, error, position, actions);
            Assert.AreEqual(back, state.CanGoBack);
            Assert.AreEqual(forward, state.CanGoForward);
            Assert.AreEqual(play, state.CanPlay);
            Assert.AreEqual(seek, state.CanSeek);
        }

        [TestCase(true, "▶ Play", "⏸ Pause", "↺ Restart", "Show hands", "Hide hands")]
        [TestCase(false, "▶ Смотреть", "⏸ Пауза", "↺ Сначала", "Показать руки", "Скрыть руки")]
        public void TransportLabelsReflectStateAndLanguage(bool english, string play, string pause,
            string restart, string show, string hide)
        {
            Assert.AreEqual(play, ReplayTransportState.PlayLabel(false, 0, 10, english));
            Assert.AreEqual(pause, ReplayTransportState.PlayLabel(true, 3, 10, english));
            Assert.AreEqual(restart, ReplayTransportState.PlayLabel(false, 10, 10, english));
            Assert.AreEqual(play, ReplayTransportState.PlayLabel(false, 0, 0, english));
            Assert.AreEqual(show, ReplayTransportState.HandsLabel(false, english));
            Assert.AreEqual(hide, ReplayTransportState.HandsLabel(true, english));
        }
    }
}
