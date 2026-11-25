namespace CityHighways.Presentation
{
    /// <summary>Read-only availability and labels for replay transport controls.</summary>
    public readonly struct ReplayTransportState
    {
        public readonly bool CanGoBack, CanGoForward, CanPlay, CanSeek;

        ReplayTransportState(bool back, bool forward, bool play, bool seek)
        {
            CanGoBack = back;
            CanGoForward = forward;
            CanPlay = play;
            CanSeek = seek;
        }

        public static ReplayTransportState Read(bool hasGame, bool hasError, int position, int actions)
        {
            bool available = hasGame && actions > 0;
            return new ReplayTransportState(available && position > 0,
                available && !hasError && position < actions,
                available && !hasError, available && !hasError);
        }

        public static string PlayLabel(bool playing, int position, int actions, bool english)
        {
            if (playing) return english ? "⏸ Pause" : "⏸ Пауза";
            if (actions > 0 && position >= actions) return english ? "↺ Restart" : "↺ Сначала";
            return english ? "▶ Play" : "▶ Смотреть";
        }

        public static string HandsLabel(bool shown, bool english) => shown
            ? (english ? "Hide hands" : "Скрыть руки")
            : (english ? "Show hands" : "Показать руки");
    }
}
