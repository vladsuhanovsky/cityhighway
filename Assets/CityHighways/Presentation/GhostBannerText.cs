using System.Collections.Generic;

namespace CityHighways.Presentation
{
    public static class GhostBannerText
    {
        public static string Watch(bool english) => english ? "▶ Replay" : "▶ Повтор";
        public static string Raid(bool self, bool english) => self
            ? (english ? "Beat your record" : "Побить свой рекорд")
            : (english ? "Challenge" : "Бросить вызов");
        public static string Tooltip(bool english) => english
            ? "Raid: same map, deck, bot opponents and companies as the record-holder's match. Take their seat and make your own moves."
            : "Рейд: те же карта, колода, соперники (боты) и компании, что в партии рекордсмена. Вы садитесь на его место — дальше решают ваши ходы.";

        public static string Records(IReadOnlyList<string> others, int? own, bool english)
        {
            string line = others.Count > 0 ? (english ? "Other records: " : "Ещё рекорды: ") +
                string.Join(", ", others) + "." : "";
            if (own.HasValue)
                line += (line.Length > 0 ? " " : "") + (english ? $"Your record: {own.Value}." : $"Ваш рекорд: {own.Value}.");
            return line.Length > 0 ? line : english
                ? "Raid — same starting deal and opponents as the record holder."
                : "Рейд — та же раздача и те же соперники, что у рекордсмена.";
        }
    }
}
