using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Глоссарий правил: термин, значок, короткое определение и формы слова для поиска в тексте.
    /// <see cref="Linkify"/> превращает первое упоминание каждого термина в абзаце в ссылку
    /// (тег &lt;link&gt;) — страница правил показывает по наведению всплывающее определение.
    /// </summary>
    public static class RulesGlossary
    {
        public sealed class Term
        {
            public string Id;
            public string Title;
            public MenuIcon.Glyph Glyph;
            /// <summary>Если задан — вместо значка рисуется карта ресурса.</summary>
            public Resource? Card;
            public string Definition;
            /// <summary>Регулярное выражение для поиска в тексте (начало слова; окончание любое).</summary>
            public string Pattern;
            /// <summary>Раздел правил, где термин раскрыт подробно.</summary>
            public string Section;
            public Color Color;
        }

        static List<Term> _terms;
        static Regex _regex;
        static GameConfig _config;

        /// <summary>Цвет ссылки в тексте правил.</summary>
        public const string LinkColor = "#F6C66B";

        public static IReadOnlyList<Term> All(GameConfig config)
        {
            if (_terms != null && ReferenceEquals(_config, config)) return _terms;
            _config = config;
            var gold = new Color(0.96f, 0.78f, 0.42f);
            var city = new Color(0.55f, 0.75f, 0.95f);
            var danger = new Color(0.92f, 0.42f, 0.40f);
            var good = new Color(0.40f, 0.82f, 0.55f);
            _terms = new List<Term>
            {
                new Term { Id = "contract", Title = "Контракт", Glyph = MenuIcon.Glyph.Document, Color = gold, Section = "Контракты и проезд",
                    Pattern = @"контракт",
                    Definition = "Заказ мэрии: связать два района своей сетью. Выполнен — очки, не выполнен к концу партии — столько же минусом." },
                new Term { Id = "toll", Title = "Проезд", Glyph = MenuIcon.Glyph.Coins, Color = gold, Section = "Контракты и проезд",
                    Pattern = @"проезд",
                    Definition = $"Плата владельцу за чужую дорогу на пути вашего контракта: {config.TollPerRoad} очка за дорогу, не больше {config.MaxForeignRoadsPerContract} чужих на пути." },
                new Term { Id = "highway", Title = "Магистраль", Glyph = MenuIcon.Glyph.Road, Color = new Color(1f, 0.62f, 0.25f), Section = "Сделки и магистрали",
                    Pattern = @"магистрал",
                    Definition = $"Улучшенная своя дорога: +{config.UpgradePoints} очка, проезд по ней в {config.RushHourTollMultiplier} раза дороже, в пробку не встаёт." },
                new Term { Id = "jam", Title = "Пробка", Glyph = MenuIcon.Glyph.Clock, Color = danger, Section = "Сделки и магистрали",
                    Pattern = @"пробк|пробоч",
                    Definition = $"Обычная дорога, по которой едут {config.JamThreshold}+ контракта соперников. Каждый следующий проезд — ещё +{config.JamFee} очка, они сгорают." },
                new Term { Id = "influence", Title = "Влияние", Glyph = MenuIcon.Glyph.Coins, Color = new Color(0.75f, 0.60f, 0.98f), Section = "Влияние",
                    Pattern = @"влияни",
                    Definition = "Валюта мэрии. Тратится раз в ход до основного действия — рынок, разрешения, взятки. В очки не превращается, остаток уходит в профиль." },
                new Term { Id = "bribe", Title = "Взятка", Glyph = MenuIcon.Glyph.Envelope, Color = danger, Section = "Влияние",
                    Pattern = @"взят[кч]",
                    Definition = "Тайное действие за влияние: дорога дешевле на карту или лишняя карта из колоды. Рискованно: соперник может раскрыть — будет штраф." },
                new Term { Id = "deal", Title = "Сделка", Glyph = MenuIcon.Glyph.Swap, Color = good, Section = "Сделки и магистрали",
                    Pattern = @"сделк|сделок",
                    Definition = $"Обмен с одним соперником раз в ход, до действия: 1–{config.TradeMaxCards} карт одного ресурса на карты другого, можно доплатить влиянием. Ход не тратит." },
                new Term { Id = "pass", Title = "Пропуск", Glyph = MenuIcon.Glyph.Document, Color = good, Section = "Сделки и магистрали",
                    Pattern = @"пропуск",
                    Definition = "Продаётся в сделке: до конца партии покупатель ездит по всем вашим дорогам, не платя вам за проезд." },
                new Term { Id = "machinery", Title = "Техника", Card = Resource.Machinery, Color = Palette.Card(Resource.Machinery), Section = "Ресурсы",
                    Pattern = @"техник",
                    Definition = "Джокер: заменяет любую карту при стройке. Открытую технику первой картой — это весь ход." },
                new Term { Id = "resource", Title = "Ресурс", Card = Resource.Asphalt, Color = Palette.Card(Resource.Asphalt), Section = "Ресурсы",
                    Pattern = @"ресурс",
                    Definition = "Карта стройки: асфальт, бетон, сталь или щебень. Цвет дороги — ресурс, которым её строят; длина — сколько карт нужно." },
                new Term { Id = "luck", Title = "Карта удачи", Glyph = MenuIcon.Glyph.Star, Color = gold, Section = "Цели мэрии и удача",
                    Pattern = @"удач",
                    Definition = "Вместо одной из двух карт ресурсов: баф (грант, премия, поставка) или дебаф (штраф, проверка). Не больше одной за ход." },
                new Term { Id = "mayor", Title = "Мэрия", Glyph = MenuIcon.Glyph.Skyline, Color = city, Section = "Цели мэрии и удача",
                    Pattern = @"мэри",
                    Definition = "Заказчик всех строек: выдаёт контракты и общие цели, объявляет события, проводит аукцион и принимает влияние." },
                new Term { Id = "event", Title = "Городское событие", Glyph = MenuIcon.Glyph.Bell, Color = city, Section = "События",
                    Pattern = @"событи",
                    Definition = $"С шансом {Mathf.RoundToInt(config.EventChance * 100)}% в начале круга: час пик, ремонт, снегопад, парад, инвестор… Действует до конца круга." },
                new Term { Id = "auction", Title = "Аукцион мэрии", Glyph = MenuIcon.Glyph.Trophy, Color = gold, Section = "Влияние",
                    Pattern = @"аукцион",
                    Definition = $"Раз за партию: тайные ставки влиянием за дорогой контракт. Выполнен — ещё +{config.AuctionBonus} очков." },
                new Term { Id = "network", Title = "Крупнейшая сеть", Glyph = MenuIcon.Glyph.Pin, Color = good, Section = "Конец партии и очки",
                    Pattern = @"сет(ь|и|ью)\b",
                    Definition = $"Связные свои дороги. За сеть на больше всего районов в конце партии +{config.NetworkBonus} очков." },
                new Term { Id = "piece", Title = "Участок", Glyph = MenuIcon.Glyph.Road, Color = Theme.TextDim, Section = "Конец партии и очки",
                    Pattern = @"участ(ок|ка|ки|ков|ке)\b",
                    Definition = $"Фишки дороги: на дорогу длины 3 уходит 3 участка. Осталось {config.FinalRoundPieces} или меньше — начинается последний круг." },
                new Term { Id = "order", Title = "Заказ города", Glyph = MenuIcon.Glyph.Skyline, Color = new Color(0.40f, 0.82f, 0.55f), Section = "Кооператив",
                    Pattern = @"заказ[а-я]* города",
                    Definition = $"Кооператив: общий контракт команды с таймером ({config.CoopOrderRounds} кругов). Выполнен общей сетью — очки и кризис −{config.CoopCrisisReliefPerOrder}, просрочен — кризис +{config.CoopCrisisPerFailedOrder}." },
                new Term { Id = "crisis", Title = "Кризис", Glyph = MenuIcon.Glyph.Flame, Color = new Color(0.92f, 0.42f, 0.40f), Section = "Кооператив",
                    Pattern = @"кризис",
                    Definition = $"Шкала кооператива от 0 до {config.CoopCrisisMax}: растёт со временем, от просрочек и плохих событий. Дошла до {config.CoopCrisisMax} — город победил команду." },
                new Term { Id = "character", Title = "Персонаж", Glyph = MenuIcon.Glyph.Person, Color = city, Section = "Персонажи",
                    Pattern = @"персонаж",
                    Definition = "Глава компании со своей способностью: Прораб, Сборщик пошлин, Мостовик… Выбирается из двух в начале партии." },
            };
            _regex = new Regex(@"\b(" + string.Join("|", _terms.Select(t => "(?<" + t.Id + ">" + t.Pattern + ")")) + @")[а-яёa-z]*",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return _terms;
        }

        public static Term Find(GameConfig config, string id) => All(config).FirstOrDefault(t => t.Id == id);

        /// <summary>
        /// Первое упоминание каждого термина в абзаце становится ссылкой (подчёркнуто, золотистым).
        /// Внутри тегов (&lt;b&gt; и т. п.) ничего не трогаем. used — термины, уже отмеченные в этом разделе
        /// (каждый термин отмечаем один раз на раздел, чтобы текст не пестрел).
        /// </summary>
        public static string Linkify(GameConfig config, string text, HashSet<string> used)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var terms = All(config);
            var result = new StringBuilder();
            int position = 0;
            foreach (Match match in _regex.Matches(text))
            {
                if (InsideTag(text, match.Index)) continue;
                string id = null;
                foreach (var term in terms)
                    if (match.Groups[term.Id].Success) { id = term.Id; break; }
                if (id == null || used.Contains(id)) continue;
                used.Add(id);
                result.Append(text, position, match.Index - position);
                result.Append("<link=\"").Append(id).Append("\"><color=").Append(LinkColor).Append("><u>")
                      .Append(match.Value).Append("</u></color></link>");
                position = match.Index + match.Length;
            }
            result.Append(text, position, text.Length - position);
            return result.ToString();
        }

        static bool InsideTag(string text, int index)
        {
            int open = text.LastIndexOf('<', index);
            if (open < 0) return false;
            int close = text.LastIndexOf('>', index);
            return close < open;
        }
    }
}
