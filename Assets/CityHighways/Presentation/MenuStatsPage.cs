using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Счётчик партий» — дашборд: карточка игрока с сериями, график очков за 10 партий, кольцо побед,
    /// тепловая карта дорог, сравнение с соперниками, история партий (цветная полоска, трофеи, фильтры,
    /// обзор партии по клику), игроки и персонажи с аватарами. Данные — cityhighways_stats.jsonl (StatsLog).
    /// </summary>
    public static class MenuStatsPage
    {
        public static VisualElement Build(Action back, Action show, Action rebuild) => new StatsView(back, rebuild).Root;
    }

    sealed class StatsView
    {
        static bool En => LocalizationService.Language == "en";
        // Names stored inside map assets remain in their original language; localize only UI display.
        static string PlaceLabel(string name)
        {
            if (!En || string.IsNullOrEmpty(name)) return name;
            switch (name)
            {
                case "Москва": return "Moscow";
                case "Амстердам": return "Amsterdam";
                case "Чунцин": return "Chongqing";
                case "Центр": return "Centre";
                case "Восток": return "East";
                case "Запад": return "West";
                case "Север": return "North";
                case "Юг": return "South";
                case "Музейный квартал": return "Museum Quarter";
                case "Юго-Запад": return "Southwest";
                case "Замоскворечье": return "Zamoskvorechye";
            }
            const string ru = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            string[] latin = { "a", "b", "v", "g", "d", "e", "yo", "zh", "z", "i", "y", "k", "l", "m", "n", "o", "p", "r", "s", "t", "u", "f", "kh", "ts", "ch", "sh", "shch", "", "y", "", "e", "yu", "ya" };
            var result = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                int index = ru.IndexOf(char.ToLowerInvariant(c));
                if (index < 0) { result.Append(c); continue; }
                string part = latin[index];
                result.Append(char.IsUpper(c) && part.Length > 0
                    ? char.ToUpperInvariant(part[0]) + part.Substring(1) : part);
            }
            return result.ToString();
        }

        enum HistoryFilter { All, Wins, Online, Week }

        readonly Action _rebuild;
        /// <summary>Все партии журнала и партии выбранного режима (по ним строится весь дашборд).</summary>
        readonly List<GameStat> _all;
        List<GameStat> _games;
        readonly VisualElement _body, _tabs;
        List<string> _humans;
        /// <summary>Выбранная вкладка режима — запоминается, пока игра открыта.</summary>
        static GameMode s_mode = GameMode.Magnate;
        string _player;
        HistoryFilter _filter = HistoryFilter.All;
        bool _pieByCharacter;
        VisualElement _overlay;
        static readonly Dictionary<string, CityMap> Maps = new Dictionary<string, CityMap>();

        public VisualElement Root { get; }

        public StatsView(Action back, Action rebuild)
        {
            _rebuild = rebuild;
            _all = StatsLog.LoadAll();
            Root = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14f;
            var backButton = Ui.Button(LocalizationService.Get("menu.stats.001"), back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            var title = Ui.Text(LocalizationService.Get("menu.stats.002"), 34f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            if (_all.Count > 0) header.Add(ResetButton());
            Root.Add(header);

            // Вкладки режимов: у каждого режима — своя статистика.
            _tabs = Ui.Column();
            Root.Add(_tabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.height = 600f;
            Ui.StyleScroller(scroll);
            Root.Add(scroll);
            _body = Ui.Column();
            scroll.Add(_body);
            ApplyMode();
            Rebuild();
        }

        /// <summary>Партии и игроки выбранного режима; игрок по умолчанию — активный профиль.</summary>
        void ApplyMode()
        {
            _games = _all.Where(g => ModeTabs.Of(g) == s_mode).ToList();
            _humans = _games.SelectMany(g => g.results).Where(r => r.kind == "human").Select(r => r.name)
                .GroupBy(n => n, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).Select(g => g.First()).ToList();
            string keep = _player ?? Profiles.Active?.name;
            _player = _humans.FirstOrDefault(n => string.Equals(n, keep, StringComparison.OrdinalIgnoreCase))
                      ?? _humans.FirstOrDefault(n => string.Equals(n, Profiles.Active?.name, StringComparison.OrdinalIgnoreCase))
                      ?? _humans.FirstOrDefault();
        }

        string ModeNote(GameMode mode)
        {
            int count = _all.Count(g => ModeTabs.Of(g) == mode);
            return count == 0 ? LocalizationService.Get("menu.stats.003") : $"{count} {Plural(count, LocalizationService.Get("menu.stats.004"), LocalizationService.Get("menu.stats.005"), LocalizationService.Get("menu.stats.006"))}";
        }

        VisualElement EmptyCard()
        {
            var empty = Ui.Card(30f);
            empty.style.backgroundColor = Theme.Panel;
            empty.style.alignItems = Align.Center;
            empty.Add(new MenuIcon(ModeTabs.Glyph(s_mode), 48f, Theme.Gold));
            empty.Add(Ui.Text((En ? $"No games played in {ModeTabs.Title(s_mode)} mode yet" : $"В режиме «{ModeTabs.Title(s_mode)}» ещё ни одной партии"), 22f, Theme.Text, true));
            string hint = s_mode == GameMode.Magnate
                ? LocalizationService.Get("menu.stats.007")
                : s_mode == GameMode.Coop
                ? LocalizationService.Get("menu.stats.008")
                : LocalizationService.Get("menu.stats.009");
            var label = Ui.Text(hint, 14f, Theme.TextDim);
            label.style.marginTop = 8f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.Add(label);
            return empty;
        }

        void Rebuild()
        {
            _tabs.Clear();
            _tabs.Add(ModeTabs.Build(s_mode, mode =>
            {
                s_mode = mode;
                _filter = HistoryFilter.All;
                _pieByCharacter = false;
                ApplyMode();
                Rebuild();
            }, ModeNote));
            _body.Clear();
            if (_games.Count == 0)
            {
                _body.Add(EmptyCard());
                Ui.Appear(_body);
                return;
            }
            bool magnate = s_mode == GameMode.Magnate;
            if (_humans.Count > 1)
            {
                var chooser = Ui.Row();
                chooser.style.alignItems = Align.Center;
                chooser.style.flexWrap = Wrap.Wrap;
                chooser.style.marginBottom = 10f;
                chooser.Add(Ui.Text(LocalizationService.Get("menu.stats.010"), 14f, Theme.TextDim, true));
                foreach (var name in _humans.Take(8))
                {
                    string chosen = name;
                    var chip = Ui.Button(name, () => { _player = chosen; Rebuild(); },
                        string.Equals(name, _player, StringComparison.OrdinalIgnoreCase) ? ButtonKind.Primary : ButtonKind.Secondary, 13f);
                    chip.style.marginLeft = 8f;
                    chip.style.minHeight = 32f;
                    chooser.Add(chip);
                }
                _body.Add(chooser);
            }

            var mine = MyGames();
            var top = Ui.Row();
            top.style.alignItems = Align.Stretch;
            top.style.marginBottom = 12f;
            top.Add(PlayerCard(mine));
            top.Add(ScoreCard(mine));
            top.Add(PieCard(mine));
            _body.Add(top);

            var middle = Ui.Row();
            middle.style.alignItems = Align.Stretch;
            middle.style.marginBottom = 12f;
            // У магната город каждый раз новый — вместо тепловой карты дорог итоги доставок.
            middle.Add(magnate ? MagnateCard(mine) : HeatCard(mine));
            middle.Add(CompareCard(mine));
            _body.Add(middle);

            _body.Add(TotalsRow());
            _body.Add(History());

            var bottom = Ui.Row();
            bottom.style.alignItems = Align.FlexStart;
            bottom.style.marginTop = 12f;
            var players = Players();
            players.style.flexGrow = 1f;
            bottom.Add(players);
            if (!magnate)
            {
                players.style.marginRight = 12f;
                var characters = CharactersBlock();
                characters.style.width = 400f;
                bottom.Add(characters);
            }
            _body.Add(bottom);
            Ui.Appear(_body);
        }

        /// <summary>Партии выбранного игрока: (партия, его строка), от старых к новым.</summary>
        List<(GameStat game, PlayerStat me)> MyGames() =>
            _games.Select(g => (game: g, me: g.results.FirstOrDefault(r => r.kind == "human" && string.Equals(r.name, _player, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.me != null).ToList();

        // ================================================================== Дашборд

        static VisualElement Widget(string title, float width = 0f)
        {
            var card = Ui.Card(16f);
            card.style.backgroundColor = Theme.Panel;
            Ui.Radius(card, 12f);
            if (width > 0f) card.style.width = width;
            else
            {
                card.style.flexGrow = 1f;
                card.style.flexBasis = 0f;
            }
            card.Add(Ui.SectionTitle(title));
            return card;
        }

        VisualElement PlayerCard(List<(GameStat game, PlayerStat me)> mine)
        {
            var card = Widget(LocalizationService.Get("menu.stats.011"), 300f);
            card.style.marginRight = 12f;
            card.Add(Ui.Text(_player ?? "—", 24f, Theme.Text, true));
            int wins = mine.Count(x => x.me.winner);
            int percent = mine.Count > 0 ? Mathf.RoundToInt(100f * wins / mine.Count) : 0;
            card.Add(Caption((En ? $"Games: {mine.Count} · wins: {wins} ({percent}%)" : $"Партий: {mine.Count} · побед: {wins} ({percent}%)"), 14f, Theme.TextDim, 2f));

            // Серии: текущая (победы или поражения подряд) и лучшая серия побед.
            int current = 0;
            bool currentWins = mine.Count > 0 && mine[mine.Count - 1].me.winner;
            for (int i = mine.Count - 1; i >= 0 && mine[i].me.winner == currentWins; i--) current++;
            int best = 0, run = 0;
            foreach (var x in mine)
            {
                run = x.me.winner ? run + 1 : 0;
                best = Mathf.Max(best, run);
            }
            var streak = Ui.Row();
            streak.style.alignItems = Align.Center;
            streak.style.marginTop = 14f;
            streak.style.backgroundColor = currentWins ? new Color(0.95f, 0.45f, 0.15f, 0.16f) : new Color(0.35f, 0.65f, 0.95f, 0.16f);
            Ui.Radius(streak, 10f);
            Ui.Padding(streak, 10f, 12f);
            var icon = new MenuIcon(currentWins ? MenuIcon.Glyph.Flame : MenuIcon.Glyph.Snow, 34f,
                currentWins ? new Color(1f, 0.55f, 0.20f) : new Color(0.55f, 0.80f, 1f));
            icon.style.marginRight = 12f;
            streak.Add(icon);
            var texts = Ui.Column();
            if (mine.Count == 0) texts.Add(Ui.Text(LocalizationService.Get("menu.stats.012"), 16f, Theme.Text, true));
            else if (currentWins)
            {
                texts.Add(Ui.Text((En ? $"Winning streak: {current}" : $"Серия побед: {current}"), 18f, new Color(1f, 0.65f, 0.30f), true));
                texts.Add(Ui.Text(current >= 3 ? LocalizationService.Get("menu.stats.013") : LocalizationService.Get("menu.stats.014"), 12f, Theme.TextDim));
            }
            else
            {
                texts.Add(Ui.Text((En ? $"Losing streak: {current} losses" : $"Холодная серия: {current} {Plural(current, "поражение", LocalizationService.Get("menu.stats.015"), LocalizationService.Get("menu.stats.016"))}"), 16f, new Color(0.60f, 0.82f, 1f), true));
                texts.Add(Ui.Text(LocalizationService.Get("menu.stats.017"), 12f, Theme.TextDim));
            }
            streak.Add(texts);
            card.Add(streak);
            card.Add(Caption((En ? $"Best winning streak: {best}" : $"Лучшая серия побед: {best}"), 13f, Theme.Text, 10f));
            if (mine.Count > 0)
            {
                card.Add(Caption((En ? $"Best score: {mine.Max(x => x.me.total)} · average: {mine.Average(x => x.me.total):0}" : $"Лучший счёт: {mine.Max(x => x.me.total)} · средний: {mine.Average(x => x.me.total):0}"), 13f, Theme.Text, 4f));
                int achievements = mine.Sum(x => x.me.achievements?.Length ?? 0);
                if (achievements > 0) card.Add(Caption((En ? $"Achievements earned in games: {achievements}" : $"Достижений за партии: {achievements}"), 13f, Theme.Gold, 4f));
            }
            return card;
        }

        VisualElement ScoreCard(List<(GameStat game, PlayerStat me)> mine)
        {
            var card = Widget(LocalizationService.Get("menu.stats.018"));
            card.style.marginRight = 12f;
            var last = mine.Skip(Mathf.Max(0, mine.Count - 10)).ToList();
            if (last.Count == 0)
            {
                card.Add(Caption(LocalizationService.Get("menu.stats.019"), 13f, Theme.TextDim, 4f));
                return card;
            }
            var chart = new ScoreLineChart(170f);
            chart.SetPoints(last.Select(x => (ShortDate(x.game.date), x.me.total, x.me.winner)));
            card.Add(chart);
            if (last.Count >= 4)
            {
                int half = last.Count / 2;
                float early = (float)last.Take(half).Average(x => x.me.total);
                float late = (float)last.Skip(half).Average(x => x.me.total);
                float delta = late - early;
                card.Add(Caption(Mathf.Abs(delta) < 2f ? LocalizationService.Get("menu.stats.020")
                    : delta > 0 ? (En ? $"Improving: recent games average {delta:0} points higher than earlier games." : $"Растёте: последние партии в среднем на {delta:0} очк. выше прежних.")
                    : (En ? $"Declining: recent games average {-delta:0} points lower than earlier games." : $"Спад: последние партии в среднем на {-delta:0} очк. ниже прежних."),
                    13f, Mathf.Abs(delta) < 2f ? Theme.TextDim : delta > 0 ? GameColors.Success : GameColors.Danger, 6f));
            }
            card.Add(Caption(LocalizationService.Get("menu.stats.021"), 11f, Theme.TextDim, 4f));
            return card;
        }

        VisualElement PieCard(List<(GameStat game, PlayerStat me)> mine)
        {
            var card = Widget(_pieByCharacter ? LocalizationService.Get("menu.stats.022") : LocalizationService.Get("menu.stats.023"), 300f);
            card.style.alignItems = Align.Center;
            if (s_mode != GameMode.Magnate)
            {
                var toggle = Ui.Row();
                toggle.style.marginBottom = 8f;
                toggle.Add(Chip(LocalizationService.Get("menu.stats.024"), !_pieByCharacter, () => { _pieByCharacter = false; Rebuild(); }));
                toggle.Add(Chip(LocalizationService.Get("menu.stats.025"), _pieByCharacter, () => { _pieByCharacter = true; Rebuild(); }));
                card.Add(toggle);
            }
            else _pieByCharacter = false;
            var donut = new DonutChart(150f);
            card.Add(donut);
            var legend = Ui.Column();
            legend.style.marginTop = 8f;
            legend.style.alignSelf = Align.Stretch;
            int wins = mine.Count(x => x.me.winner);
            if (!_pieByCharacter)
            {
                donut.Set(new[] { ((float)wins, GameColors.Success), ((float)(mine.Count - wins), GameColors.Danger) },
                    mine.Count > 0 ? $"{Mathf.RoundToInt(100f * wins / mine.Count)}%" : "—", LocalizationService.Get("menu.stats.026"));
                legend.Add(LegendLine(GameColors.Success, LocalizationService.Get("menu.stats.027"), wins.ToString()));
                legend.Add(LegendLine(GameColors.Danger, LocalizationService.Get("menu.stats.028"), (mine.Count - wins).ToString()));
            }
            else
            {
                var byCharacter = mine.Where(x => x.me.winner && !string.IsNullOrEmpty(x.me.character))
                    .GroupBy(x => x.me.character).Select(g => (name: g.Key, count: g.Count())).OrderByDescending(x => x.count).ToList();
                var slices = new List<(float, Color)>();
                for (int i = 0; i < byCharacter.Count; i++)
                {
                    var color = Palette.PlayerColor(i);
                    slices.Add((byCharacter[i].count, color));
                    legend.Add(LegendLine(color, byCharacter[i].name, byCharacter[i].count.ToString()));
                }
                donut.Set(slices, wins.ToString(), LocalizationService.Get("menu.stats.026"));
                if (byCharacter.Count == 0) legend.Add(Caption(LocalizationService.Get("menu.stats.029"), 12f, Theme.TextDim, 0f));
            }
            card.Add(legend);
            return card;
        }

        static VisualElement LegendLine(Color color, string name, string value)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3f;
            var dot = Ui.Box();
            dot.style.width = 10f;
            dot.style.height = 10f;
            dot.style.marginRight = 8f;
            dot.style.backgroundColor = color;
            Ui.Radius(dot, 5f);
            row.Add(dot);
            var label = Ui.Text(name, 13f, Theme.Text);
            label.style.flexGrow = 1f;
            row.Add(label);
            row.Add(Ui.Number(value, 13f, Theme.Text, true));
            return row;
        }

        static readonly string[] ReputationOrder = { LocalizationService.Get("menu.stats.030"), LocalizationService.Get("menu.stats.031"), LocalizationService.Get("menu.stats.032"), LocalizationService.Get("menu.stats.033"), LocalizationService.Get("menu.stats.034") };
        static readonly string[] DifficultyOrder = { LocalizationService.Get("menu.stats.035"), LocalizationService.Get("menu.stats.036"), LocalizationService.Get("menu.stats.037"), LocalizationService.Get("menu.stats.038") };

        /// <summary>Магнат: средние за партию (доставки, заказы мэрии, пробки, проезд, хабы, потери), лучшая репутация, победы по сложности.</summary>
        VisualElement MagnateCard(List<(GameStat game, PlayerStat me)> mine)
        {
            var card = Widget(LocalizationService.Get("menu.stats.039"), 520f);
            card.style.marginRight = 12f;
            if (mine.Count == 0)
            {
                card.Add(Caption(LocalizationService.Get("menu.stats.040"), 13f, Theme.TextDim, 4f));
                return card;
            }
            var grid = Ui.Row();
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 4f;
            void Stat(string caption, string value, string note, Color color)
            {
                var tile = Ui.Column();
                tile.style.width = 154f;
                tile.style.marginRight = 6f;
                tile.style.marginBottom = 6f;
                tile.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(tile, 10f);
                Ui.Padding(tile, 8f, 10f);
                tile.Add(Ui.Text(caption, 11f, Theme.TextDim));
                tile.Add(Ui.Number(value, 20f, color, true));
                if (note != null) tile.Add(Ui.Text(note, 10f, Theme.TextDim));
                grid.Add(tile);
            }
            float Avg(Func<PlayerStat, int> f) => (float)mine.Average(x => f(x.me));
            Stat(LocalizationService.Get("menu.stats.041"), $"{Avg(r => r.delivered):0.#}", (En ? $"total {mine.Sum(x => x.me.delivered)}" : $"всего {mine.Sum(x => x.me.delivered)}"), Theme.Text);
            Stat(LocalizationService.Get("menu.stats.042"), $"{Avg(r => r.mayorOrders):0.#}", (En ? $"total {mine.Sum(x => x.me.mayorOrders)}" : $"всего {mine.Sum(x => x.me.mayorOrders)}"), Theme.Gold);
            Stat(LocalizationService.Get("menu.stats.043"), $"{Avg(r => r.jams):0.#}", null, Avg(r => r.jams) > 3f ? GameColors.Danger : Theme.Text);
            Stat(LocalizationService.Get("menu.stats.044"), $"{Avg(r => r.tollsEarned - r.tollsPaid):+0.#;−0.#;0}", $"+{mine.Sum(x => x.me.tollsEarned)} / −{mine.Sum(x => x.me.tollsPaid)}", Theme.Text);
            Stat(LocalizationService.Get("menu.stats.045"), $"{Avg(r => r.hubFees):0.#}", LocalizationService.Get("menu.stats.046"), Theme.Text);
            Stat(LocalizationService.Get("menu.stats.047"), $"{mine.Sum(x => x.me.perishableOnTime)}", (En ? $"lost cargo: {mine.Sum(x => x.me.losses)}" : $"потерь грузов: {mine.Sum(x => x.me.losses)}"), GameColors.Success);
            card.Add(grid);

            int bestRank = mine.Select(x => Array.IndexOf(ReputationOrder, x.me.reputation ?? "")).DefaultIfEmpty(-1).Max();
            if (bestRank >= 0)
                card.Add(Caption((En ? $"Best reputation: {ReputationOrder[bestRank]} {new string('★', bestRank + 1)}" : $"Лучшая репутация: {ReputationOrder[bestRank]} {new string('★', bestRank + 1)}"), 14f, Theme.Gold, 6f));

            var byDifficulty = Ui.Column();
            byDifficulty.style.marginTop = 8f;
            byDifficulty.Add(Ui.Text(LocalizationService.Get("menu.stats.048"), 12f, Theme.TextDim, true));
            foreach (var level in DifficultyOrder)
            {
                var games = mine.Where(x => x.game.difficulty == level).ToList();
                if (games.Count == 0) continue;
                int wins = games.Count(x => x.me.winner);
                int percent = Mathf.RoundToInt(100f * wins / games.Count);
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4f;
                var name = Ui.Text(level, 13f, Theme.Text);
                name.style.width = 90f;
                row.Add(name);
                row.Add(WinBar(percent, $"{wins} из {games.Count} ({percent}%)"));
                byDifficulty.Add(row);
            }
            card.Add(byDifficulty);
            return card;
        }

        VisualElement HeatCard(List<(GameStat game, PlayerStat me)> mine)
        {
            var card = Widget(LocalizationService.Get("menu.stats.049"), 520f);
            card.style.marginRight = 12f;
            // Карта — последней партии с записанными дорогами.
            var withRoads = mine.Where(x => x.me.roads != null && x.me.roads.Length > 0).ToList();
            if (withRoads.Count == 0)
            {
                card.Add(Caption(LocalizationService.Get("menu.stats.050"), 13f, Theme.TextDim, 4f));
                return card;
            }
            string path = withRoads.Last().game.mapPath;
            if (string.IsNullOrEmpty(path)) path = "CityHighways/Maps/moscow";
            var map = MapFor(path);
            if (map == null)
            {
                card.Add(Caption(LocalizationService.Get("menu.stats.051"), 13f, Theme.TextDim, 4f));
                return card;
            }
            var counts = new Dictionary<string, int>();
            int games = 0;
            foreach (var x in withRoads.Where(x => (string.IsNullOrEmpty(x.game.mapPath) ? "CityHighways/Maps/moscow" : x.game.mapPath) == path))
            {
                games++;
                foreach (var id in x.me.roads) counts[id] = (counts.TryGetValue(id, out var n) ? n : 0) + 1;
            }
            var heat = new RoadHeatMap(488f, 250f);
            heat.Set(map, counts);
            card.Add(heat);
            var legend = Ui.Row();
            legend.style.alignItems = Align.Center;
            legend.style.marginTop = 8f;
            legend.Add(Ui.Text(LocalizationService.Get("menu.stats.052"), 11f, Theme.TextDim));
            for (int i = 0; i <= 8; i++)
            {
                var swatch = Ui.Box();
                swatch.style.width = 18f;
                swatch.style.height = 8f;
                swatch.style.marginLeft = 2f;
                swatch.style.backgroundColor = RoadHeatMap.Heat(i / 8f);
                legend.Add(swatch);
            }
            var often = Ui.Text(LocalizationService.Get("menu.stats.053"), 11f, Theme.TextDim);
            often.style.marginLeft = 6f;
            legend.Add(often);
            var space = Ui.Box();
            space.style.flexGrow = 1f;
            legend.Add(space);
            legend.Add(Ui.Text((En ? $"{PlaceLabel(map.Name)} · games: {games}" : $"{map.Name} · партий: {games}"), 11f, Theme.TextDim));
            card.Add(legend);
            // Любимые районы — по концам самых частых дорог.
            var districts = counts.OrderByDescending(p => p.Value).Take(12)
                .SelectMany(p => { var road = map.Roads.FirstOrDefault(r => r.Id == p.Key); return road == null ? new string[0] : new[] { road.A, road.B }; })
                .Select(id => map.Locations.FirstOrDefault(l => l.Id == id)?.DistrictId).Where(d => d != null)
                .GroupBy(d => d).OrderByDescending(g => g.Count()).Take(3)
                .Select(g => PlaceLabel(map.Districts.FirstOrDefault(d => d.Id == g.Key)?.Name)).Where(n => n != null).ToList();
            if (districts.Count > 0) card.Add(Caption(LocalizationService.Get("menu.stats.054") + string.Join(", ", districts), 13f, Theme.Text, 6f));
            return card;
        }

        static CityMap MapFor(string path)
        {
            if (Maps.TryGetValue(path, out var map)) return map;
            try { map = CityHighwaysApp.LoadMap(path); }
            catch { map = null; }
            Maps[path] = map;
            return map;
        }

        VisualElement CompareCard(List<(GameStat game, PlayerStat me)> mine)
        {
            var card = Widget(LocalizationService.Get("menu.stats.055"));
            if (mine.Count == 0)
            {
                card.Add(Caption(LocalizationService.Get("menu.stats.056"), 13f, Theme.TextDim, 4f));
                return card;
            }
            float myAverage = (float)mine.Average(x => x.me.total);
            var others = _games.SelectMany(g => g.results)
                .Where(r => !(r.kind == "human" && string.Equals(r.name, _player, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(Key).Select(g => (name: g.Key, average: (float)g.Average(r => r.total), games: g.Count()))
                .Where(x => x.games > 0).OrderByDescending(x => x.games).ToList();
            if (others.Count > 0)
            {
                int below = others.Count(x => x.average < myAverage);
                int percent = Mathf.RoundToInt(100f * below / others.Count);
                var head = Ui.Row();
                head.style.alignItems = Align.Center;
                var big = Ui.Number($"{percent}%", 40f, percent >= 50 ? GameColors.Success : Theme.Gold, true);
                big.style.marginRight = 12f;
                head.Add(big);
                head.Add(Caption((En ? $"Your average score is higher than {percent}% of recorded opponents ({below} of {others.Count})." : $"Вы набираете в среднем больше, чем {percent}% соперников из журнала ({below} из {others.Count})."), 14f, Theme.Text, 0f));
                card.Add(head);
            }
            // Соперники, с которыми чаще всего играли: на сколько ваш средний счёт выше или ниже.
            foreach (var other in others.Take(6))
            {
                float delta = other.average <= 0f ? 0f : (myAverage - other.average) / other.average * 100f;
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 8f;
                var name = Ui.Text(other.name, 14f, Theme.Text, true);
                name.style.width = 150f;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(name);
                // Полоска: середина — равенство, вправо — вы сильнее, влево — слабее.
                var track = Ui.Box();
                track.style.width = 180f;
                track.style.height = 8f;
                track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
                Ui.Radius(track, 4f);
                var fill = Ui.Box();
                fill.style.position = Position.Absolute;
                fill.style.top = 0f;
                fill.style.bottom = 0f;
                float part = Mathf.Clamp(Mathf.Abs(delta) / 60f, 0.03f, 0.5f) * 180f;
                if (delta >= 0f) fill.style.left = 90f;
                else fill.style.left = 90f - part;
                fill.style.width = part;
                fill.style.backgroundColor = delta >= 0f ? GameColors.Success : GameColors.Danger;
                Ui.Radius(fill, 4f);
                track.Add(fill);
                var middle = Ui.Box();
                middle.style.position = Position.Absolute;
                middle.style.left = 89f;
                middle.style.width = 2f;
                middle.style.top = -2f;
                middle.style.bottom = -2f;
                middle.style.backgroundColor = Theme.TextDim;
                track.Add(middle);
                row.Add(track);
                var text = Ui.Text(Mathf.Abs(delta) < 1f ? LocalizationService.Get("menu.stats.057")
                    : delta > 0 ? (En ? $"you lead by {delta:0}%" : $"вы выше на {delta:0}%") : (En ? $"you trail by {-delta:0}%" : $"вы ниже на {-delta:0}%"), 13f, delta >= 0 ? GameColors.Success : GameColors.Danger, true);
                text.style.marginLeft = 10f;
                row.Add(text);
                card.Add(row);
            }
            card.Add(Caption(LocalizationService.Get("menu.stats.058"), 11f, Theme.TextDim, 10f));
            return card;
        }

        VisualElement TotalsRow()
        {
            var row = Ui.Row();
            row.style.marginBottom = 12f;
            int seconds = _games.Sum(g => g.seconds);
            var best = _games.SelectMany(g => g.results.Select(r => (game: g, result: r))).OrderByDescending(x => x.result.total).First();
            void Tile(MenuIcon.Glyph glyph, string caption, string value, string note = null)
            {
                var tile = Ui.Row();
                tile.style.flexGrow = 1f;
                tile.style.flexBasis = 0f;
                tile.style.alignItems = Align.Center;
                tile.style.marginRight = 10f;
                tile.style.backgroundColor = Theme.Panel;
                Ui.Radius(tile, 12f);
                Ui.Padding(tile, 12f, 14f);
                var icon = new MenuIcon(glyph, 26f, Theme.Gold);
                icon.style.marginRight = 12f;
                tile.Add(icon);
                var texts = Ui.Column();
                texts.Add(Ui.Text(caption, 12f, Theme.TextDim));
                texts.Add(Ui.Number(value, 22f, Theme.Text, true));
                if (note != null) texts.Add(Ui.Text(note, 11f, Theme.TextDim));
                tile.Add(texts);
                row.Add(tile);
            }
            Tile(MenuIcon.Glyph.Gamepad, LocalizationService.Get("menu.stats.059"), _games.Count.ToString(), _games.Any(g => g.online) ? (En ? $"online: {_games.Count(g => g.online)}" : $"сетевых: {_games.Count(g => g.online)}") : null);
            Tile(MenuIcon.Glyph.Clock, LocalizationService.Get("menu.stats.060"), Duration(seconds), (En ? $"average {Duration(seconds / Math.Max(1, _games.Count))}" : $"в среднем {Duration(seconds / Math.Max(1, _games.Count))}"));
            Tile(MenuIcon.Glyph.Trophy, LocalizationService.Get("menu.stats.061"), best.result.total.ToString(), $"{best.result.name}, {ShortDate(best.game.date)}");
            return row;
        }

        // ================================================================== История

        VisualElement History()
        {
            var card = Ui.Card(16f);
            card.style.backgroundColor = Theme.Panel;
            Ui.Radius(card, 12f);
            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 8f;
            var title = Ui.SectionTitle(LocalizationService.Get("menu.stats.062"));
            title.style.flexGrow = 1f;
            title.style.marginBottom = 0f;
            head.Add(title);
            head.Add(Chip(LocalizationService.Get("menu.stats.063"), _filter == HistoryFilter.All, () => { _filter = HistoryFilter.All; Rebuild(); }));
            head.Add(Chip(LocalizationService.Get("menu.stats.064"), _filter == HistoryFilter.Wins, () => { _filter = HistoryFilter.Wins; Rebuild(); }));
            head.Add(Chip(LocalizationService.Get("menu.stats.065"), _filter == HistoryFilter.Online, () => { _filter = HistoryFilter.Online; Rebuild(); }));
            head.Add(Chip(LocalizationService.Get("menu.stats.066"), _filter == HistoryFilter.Week, () => { _filter = HistoryFilter.Week; Rebuild(); }));
            card.Add(head);

            // Повтор лучшей партии выбранного игрока в этом режиме (если повтор сохранился).
            var best = _games.Where(g => Replays.Exists(g.replay) && Mine(g) != null)
                .OrderByDescending(g => Mine(g).total).FirstOrDefault();
            if (best != null)
            {
                var bestRow = Ui.Row();
                bestRow.style.alignItems = Align.Center;
                bestRow.style.marginBottom = 4f;
                var bestText = Ui.Text((En ? $"Best game: {Mine(best).total} pts · {best.map} · {ShortDate(best.date)}" : $"Лучшая партия: {Mine(best).total} очк. · {best.map} · {ShortDate(best.date)}"), 13f, Theme.Gold, true);
                bestText.style.flexGrow = 1f;
                bestRow.Add(bestText);
                var watch = Ui.Button(LocalizationService.Get("menu.stats.067"), () => Replays.Open(best.replay), ButtonKind.Secondary, 13f);
                watch.style.minWidth = 170f;
                bestRow.Add(watch);
                card.Add(bestRow);
            }

            var weekAgo = DateTime.Now.AddDays(-7);
            var list = Enumerable.Reverse(_games).Where(g =>
            {
                var me = Mine(g);
                switch (_filter)
                {
                    case HistoryFilter.Wins: return me != null && me.winner;
                    case HistoryFilter.Online: return g.online;
                    case HistoryFilter.Week: return ParseDate(g.date) is DateTime d && d >= weekAgo;
                    default: return true;
                }
            }).Take(15).ToList();
            if (list.Count == 0)
                card.Add(Caption(_filter == HistoryFilter.Online ? LocalizationService.Get("menu.stats.068") : LocalizationService.Get("menu.stats.069"), 13f, Theme.TextDim, 4f));
            foreach (var game in list) card.Add(HistoryRow(game));
            card.Add(Caption(LocalizationService.Get("menu.stats.070") +
                             LocalizationService.Get("menu.stats.071"), 11f, Theme.TextDim, 8f));
            return card;
        }

        PlayerStat Mine(GameStat game) =>
            game.results.FirstOrDefault(r => r.kind == "human" && string.Equals(r.name, _player, StringComparison.OrdinalIgnoreCase));

        VisualElement HistoryRow(GameStat game)
        {
            var me = Mine(game);
            var ordered = game.results.OrderByDescending(r => r.total).ToList();
            var winner = ordered.First();
            var stripe = me == null ? Theme.TextDim : me.winner ? GameColors.Success : GameColors.Danger;

            var row = Ui.Row();
            row.pickingMode = PickingMode.Position;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6f;
            row.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(row, 10f);
            Ui.Padding(row, 8f, 12f);
            row.style.borderLeftWidth = 6f;
            row.style.borderLeftColor = stripe;
            Ui.Transitions(row, "background-color", "translate");

            var when = Ui.Column();
            when.style.width = 130f;
            when.Add(Ui.Number(ShortDate(game.date), 13f, Theme.Text, true));
            var tags = new List<string>();
            if (game.online) tags.Add(LocalizationService.Get("menu.stats.072"));
            if (!string.IsNullOrEmpty(game.difficulty)) tags.Add(game.difficulty);
            if (!string.IsNullOrEmpty(game.scenario) && game.scenario != Scenarios.Title(ScenarioKind.Standard)) tags.Add(game.scenario);
            when.Add(Ui.Text(tags.Count > 0 ? string.Join(" · ", tags) : Duration(game.seconds), 11f, Theme.TextDim));
            row.Add(when);

            // Трофей — если партия открыла достижение.
            var trophySlot = Ui.Box();
            trophySlot.style.width = 26f;
            if (me?.achievements != null && me.achievements.Length > 0)
            {
                var trophy = new MenuIcon(MenuIcon.Glyph.Trophy, 20f, Theme.Gold);
                trophySlot.Add(trophy);
            }
            row.Add(trophySlot);

            var result = Ui.Column();
            result.style.flexGrow = 1f;
            result.style.flexShrink = 1f;
            if (me != null)
            {
                int place = game.results.Count(r => r.total > me.total) + 1;
                result.Add(Ui.Text(En ? (me.winner ? $"Victory · {me.total} pts" : $"Place {place} of {game.results.Length} · {me.total} pts") : (me.winner ? $"Победа · {me.total} очк." : $"{place}-е место из {game.results.Length} · {me.total} очк."), 14f,
                    me.winner ? GameColors.Success : Theme.Text, true));
            }
            else result.Add(Ui.Text((En ? $"{winner.name} won · {winner.total} pts" : $"Победил {winner.name} · {winner.total} очк."), 14f, Theme.Text, true));
            var others = Ui.Text(string.Join(", ", ordered.Select(r => $"{r.name} {r.total}")), 12f, Theme.TextDim);
            others.style.overflow = Overflow.Hidden;
            others.style.textOverflow = TextOverflow.Ellipsis;
            result.Add(others);
            row.Add(result);

            if (!string.IsNullOrEmpty(winner.character) && CharacterOf(winner.character) is CharacterKind kind)
            {
                var icon = new CharacterIcon(30f, kind, Palette.PlayerColor(0));
                icon.style.marginLeft = 8f;
                row.Add(icon);
            }
            if (Replays.Exists(game.replay))
            {
                var play = new MenuIcon(MenuIcon.Glyph.Play, 16f, Theme.Gold);
                play.style.marginLeft = 10f;
                play.tooltip = LocalizationService.Get("menu.stats.073");
                row.Add(play);
            }
            var arrow = new MenuIcon(MenuIcon.Glyph.Chevron, 16f, Theme.TextDim);
            arrow.style.marginLeft = 10f;
            row.Add(arrow);

            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                row.style.backgroundColor = Theme.PanelHover;
                row.style.translate = new Translate(new Length(3f), new Length(0f));
                Ui.Hovered?.Invoke();
            });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                row.style.backgroundColor = Theme.PanelLight;
                row.style.translate = new Translate(new Length(0f), new Length(0f));
            });
            row.RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                OpenSummary(game);
            });
            return row;
        }

        // ================================================================== Обзор партии

        void OpenSummary(GameStat game)
        {
            CloseSummary();
            _overlay = Ui.Box();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = -20f;
            _overlay.style.top = -20f;
            _overlay.style.right = -20f;
            _overlay.style.bottom = -20f;
            _overlay.style.backgroundColor = Theme.Shade;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.RegisterCallback<ClickEvent>(e => { if (e.target == _overlay) CloseSummary(); });

            var window = Ui.Card(20f);
            window.style.width = 820f;
            window.style.backgroundColor = Theme.Panel;
            Ui.Shadow(window, 16f);
            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            var icon = new MenuIcon(MenuIcon.Glyph.Flag, 22f, Theme.Gold);
            icon.style.marginRight = 10f;
            head.Add(icon);
            var title = Ui.Text((En ? $"Game {ShortDate(game.date)}" : $"Партия {ShortDate(game.date)}"), 22f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            head.Add(title);
            if (Replays.Exists(game.replay))
            {
                var watch = Ui.Button(LocalizationService.Get("menu.stats.067"), () =>
                {
                    CloseSummary();
                    Replays.Open(game.replay);
                }, ButtonKind.Primary, 14f);
                watch.style.marginRight = 8f;
                head.Add(watch);
            }
            head.Add(Ui.Button(LocalizationService.Get("menu.stats.074"), CloseSummary, ButtonKind.Ghost, 14f));
            window.Add(head);
            var facts = new List<string>();
            if (!string.IsNullOrEmpty(game.map)) facts.Add(game.map);
            if (!string.IsNullOrEmpty(game.scenario)) facts.Add(game.scenario);
            bool magnateGame = ModeTabs.Of(game) == GameMode.Magnate;
            if (!string.IsNullOrEmpty(game.difficulty)) facts.Add((En ? $"difficulty: {game.difficulty}" : $"сложность «{game.difficulty}»"));
            if (game.turns > 0) facts.Add(En ? (magnateGame ? $"{game.turns} rounds" : $"{game.turns} turns") : (magnateGame ? $"{game.turns} раундов" : $"{game.turns} ходов"));
            if (game.seconds > 0) facts.Add(Duration(game.seconds));
            if (game.online) facts.Add(LocalizationService.Get("menu.stats.075"));
            if (game.coop) facts.Add(LocalizationService.Get("menu.stats.076"));
            window.Add(Caption(string.Join(" · ", facts), 13f, Theme.TextDim, 2f));

            var ordered = game.results.OrderByDescending(r => r.total).ToList();
            var winner = ordered.First();
            var hero = Ui.Row();
            hero.style.alignItems = Align.Center;
            hero.style.marginTop = 12f;
            hero.style.backgroundColor = new Color(GameColors.Success.r, GameColors.Success.g, GameColors.Success.b, 0.14f);
            Ui.Radius(hero, 10f);
            Ui.Padding(hero, 10f, 14f);
            var trophy = new MenuIcon(MenuIcon.Glyph.Trophy, 34f, Theme.Gold);
            trophy.style.marginRight = 12f;
            hero.Add(trophy);
            var heroTexts = Ui.Column();
            bool teamWon = game.results.Any(r => r.winner);
            heroTexts.Add(Ui.Text(game.coop
                ? (teamWon ? (En ? $"The team saved the city — {game.results.Sum(r => r.total)} total points" : $"Команда спасла город — {game.results.Sum(r => r.total)} очк. на всех") : LocalizationService.Get("menu.stats.077"))
                : (En ? $"{winner.name} won — {winner.total} pts" : $"Победил {winner.name} — {winner.total} очк."), 18f, Theme.Text, true));
            if (ordered.Count > 1 && !game.coop) heroTexts.Add(Ui.Text((En ? $"Lead over second place: {winner.total - ordered[1].total} pts." : $"Отрыв от второго места: {winner.total - ordered[1].total} очк.") +
                                                        (string.IsNullOrEmpty(winner.character) ? "" : (En ? $" Character: {winner.character}." : $" Персонаж: {winner.character}.")), 13f, Theme.TextDim));
            hero.Add(heroTexts);
            window.Add(hero);

            // Итог по категориям: у каждого игрока строка цветных столбиков.
            var categories = magnateGame ? new (string title, Func<PlayerStat, int> value, Color color)[]
            {
                (LocalizationService.Get("menu.stats.078"), r => r.delivered, new Color(0.45f, 0.70f, 1f)),
                (LocalizationService.Get("menu.stats.079"), r => r.mayorOrders, Theme.Gold),
                (LocalizationService.Get("menu.stats.080"), r => r.tollsEarned - r.tollsPaid, new Color(0.75f, 0.60f, 0.98f)),
                (LocalizationService.Get("menu.stats.081"), r => r.hubFees, GameColors.Success),
                (LocalizationService.Get("menu.stats.082"), r => -r.jams, new Color(0.95f, 0.75f, 0.30f)),
                (LocalizationService.Get("menu.stats.083"), r => -r.losses, new Color(0.55f, 0.80f, 0.85f))
            } : new (string title, Func<PlayerStat, int> value, Color color)[]
            {
                (LocalizationService.Get("menu.stats.084"), r => r.roadPoints, new Color(0.45f, 0.70f, 1f)),
                (LocalizationService.Get("menu.stats.085"), r => r.contractPoints, Theme.Gold),
                (LocalizationService.Get("menu.stats.080"), r => r.tollsEarned - r.tollsPaid - r.jamFees, new Color(0.75f, 0.60f, 0.98f)),
                (LocalizationService.Get("menu.stats.086"), r => r.goalPoints, GameColors.Success),
                (LocalizationService.Get("menu.stats.087"), r => r.luckPoints, new Color(0.95f, 0.75f, 0.30f)),
                (LocalizationService.Get("menu.stats.088"), r => r.characterPoints, new Color(0.55f, 0.80f, 0.85f))
            };
            var table = Ui.Column();
            table.style.marginTop = 12f;
            var headRow = Ui.Row();
            var spacer = Ui.Box();
            spacer.style.width = 170f;
            headRow.Add(spacer);
            foreach (var c in categories)
            {
                var cell = Ui.Text(c.title, 11f, Theme.TextDim, true);
                cell.style.width = 92f;
                headRow.Add(cell);
            }
            var totalHead = Ui.Text(LocalizationService.Get("menu.stats.089"), 11f, Theme.TextDim, true);
            totalHead.style.width = 60f;
            headRow.Add(totalHead);
            table.Add(headRow);
            int maxAbs = Mathf.Max(1, ordered.SelectMany(r => categories.Select(c => Mathf.Abs(c.value(r)))).Max());
            foreach (var r in ordered)
            {
                var line = Ui.Row();
                line.style.alignItems = Align.Center;
                line.style.marginTop = 6f;
                var name = Ui.Text((r.winner ? "1. " : "") + r.name, 13f, r.winner ? Theme.Gold : Theme.Text, true);
                name.style.width = 170f;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                line.Add(name);
                foreach (var c in categories)
                {
                    int value = c.value(r);
                    var cell = Ui.Row();
                    cell.style.width = 92f;
                    cell.style.alignItems = Align.Center;
                    var bar = Ui.Box();
                    bar.style.height = 8f;
                    bar.style.width = Mathf.Max(2f, 46f * Mathf.Abs(value) / maxAbs);
                    bar.style.backgroundColor = value >= 0 ? c.color : GameColors.Danger;
                    Ui.Radius(bar, 4f);
                    cell.Add(bar);
                    var number = Ui.Number(value > 0 ? $"+{value}" : value.ToString(), 12f, value >= 0 ? Theme.Text : GameColors.Danger);
                    number.style.marginLeft = 4f;
                    cell.Add(number);
                    line.Add(cell);
                }
                line.Add(Ui.Number(r.total.ToString(), 15f, Theme.Gold, true));
                table.Add(line);
            }
            window.Add(table);

            // Ключевые моменты — из записанных чисел партии.
            var moments = new List<string>();
            if (magnateGame)
            {
                var courier = game.results.OrderByDescending(r => r.delivered).First();
                if (courier.delivered > 0) moments.Add((En ? $"Most deliveries: {courier.name} ({courier.delivered})." : $"Больше всех доставок у {courier.name}: {courier.delivered}."));
                var mayor = game.results.OrderByDescending(r => r.mayorOrders).First();
                if (mayor.mayorOrders > 0) moments.Add((En ? $"Most city orders: {mayor.name} ({mayor.mayorOrders})." : $"Заказы мэрии первым выполнял {mayor.name}: {mayor.mayorOrders}."));
                var famous = game.results.OrderByDescending(r => Array.IndexOf(ReputationOrder, r.reputation ?? "")).First();
                if (!string.IsNullOrEmpty(famous.reputation)) moments.Add((En ? $"Best reputation: {famous.name} ({famous.reputation})." : $"Лучшая репутация — у {famous.name}: {famous.reputation}."));
                var stuck = game.results.Where(r => r.jams > 0).OrderByDescending(r => r.jams).FirstOrDefault();
                if (stuck != null) moments.Add((En ? $"Most traffic jams: {stuck.name} ({stuck.jams})." : $"Чаще всех стоял в пробках {stuck.name}: {stuck.jams} {Plural(stuck.jams, LocalizationService.Get("menu.stats.091"), LocalizationService.Get("menu.stats.090"), LocalizationService.Get("menu.stats.091"))}."));
            }
            var tollKing = game.results.OrderByDescending(r => r.tollsEarned).First();
            if (tollKing.tollsEarned > 0) moments.Add((En ? $"Most tolls earned: {tollKing.name} (+{tollKing.tollsEarned})." : $"Больше всех за проезд получил {tollKing.name}: +{tollKing.tollsEarned}."));
            var network = game.results.OrderByDescending(r => r.networkDistricts).First();
            if (network.networkDistricts > 0) moments.Add((En ? $"Largest network: {network.name} ({network.networkDistricts} districts)." : $"Самая большая сеть — у {network.name}: {network.networkDistricts} {Plural(network.networkDistricts, "район", LocalizationService.Get("menu.stats.092"), LocalizationService.Get("menu.stats.093"))}."));
            var contracts = game.results.OrderByDescending(r => r.completedContracts).First();
            if (contracts.completedContracts > 0) moments.Add((En ? $"Most contracts completed: {contracts.name} ({contracts.completedContracts})." : $"Больше всех контрактов выполнил {contracts.name}: {contracts.completedContracts}."));
            var failed = game.results.Where(r => r.failedContracts > 0).OrderByDescending(r => r.failedContracts).FirstOrDefault();
            if (failed != null) moments.Add((En ? $"Most failed contracts: {failed.name} ({failed.failedContracts})." : $"Провалено контрактов у {failed.name}: {failed.failedContracts}."));
            var jam = game.results.Where(r => r.jamFees > 0).OrderByDescending(r => r.jamFees).FirstOrDefault();
            if (jam != null) moments.Add((En ? $"Most points lost to jams: {jam.name} (−{jam.jamFees})." : $"В пробках больше всех потерял {jam.name}: −{jam.jamFees}."));
            foreach (var r in game.results.Where(r => r.achievements != null && r.achievements.Length > 0))
                moments.Add((En ? $"{r.name} unlocked achievements: {string.Join(", ", r.achievements)}." : $"{r.name} открыл достижени{(r.achievements.Length == 1 ? "е" : "я")}: {string.Join(", ", r.achievements)}."));
            if (moments.Count > 0)
            {
                var momentsTitle = Ui.SectionTitle(LocalizationService.Get("menu.stats.094"));
                momentsTitle.style.marginTop = 14f;
                window.Add(momentsTitle);
                foreach (var moment in moments)
                {
                    var line = Ui.Row();
                    line.style.marginTop = 3f;
                    var dot = new MenuIcon(MenuIcon.Glyph.Star, 12f, Theme.Gold);
                    dot.style.marginRight = 8f;
                    dot.style.marginTop = 2f;
                    line.Add(dot);
                    line.Add(Caption(moment, 13f, Theme.Text, 0f));
                    window.Add(line);
                }
            }
            _overlay.Add(window);
            Root.Add(_overlay);
            Ui.Appear(window);
        }

        void CloseSummary()
        {
            _overlay?.RemoveFromHierarchy();
            _overlay = null;
        }

        // ================================================================== Таблицы

        static string Key(PlayerStat r) => r.kind == "human" ? r.name : r.kind == "random" ? LocalizationService.Get("menu.stats.095") : LocalizationService.Get("menu.stats.096");

        VisualElement Players()
        {
            var card = Block(LocalizationService.Get("menu.stats.097"));
            var rows = _games.SelectMany(g => g.results).GroupBy(Key)
                .Select(group => new
                {
                    Name = group.Key,
                    Human = group.Any(r => r.kind == "human"),
                    Games = group.Count(),
                    Wins = group.Count(r => r.winner),
                    Average = group.Average(r => r.total),
                    Best = group.Max(r => r.total),
                    Contracts = group.Sum(r => r.completedContracts),
                    Failed = group.Sum(r => r.failedContracts),
                    Delivered = group.Sum(r => r.delivered),
                    Orders = group.Sum(r => r.mayorOrders)
                })
                .OrderByDescending(x => x.Human).ThenByDescending(x => x.Games).ToList();
            bool magnate = s_mode == GameMode.Magnate;
            float[] widths = { 170f, 60f, 130f, 70f, 70f, 120f };
            card.Add(TableRow(new[] { LocalizationService.Get("menu.stats.011"), LocalizationService.Get("menu.stats.098"), (En ? "Wins" : "Побед"), LocalizationService.Get("menu.stats.099"), LocalizationService.Get("menu.stats.100"), magnate ? LocalizationService.Get("menu.stats.101") : LocalizationService.Get("menu.stats.085") }, widths, Theme.TextDim, true));
            foreach (var r in rows)
            {
                int percent = Mathf.RoundToInt(100f * r.Wins / Math.Max(1, r.Games));
                string last = magnate ? $"{r.Delivered} / {r.Orders}" : $"{r.Contracts} / −{r.Failed}";
                var row = TableRow(new[] { r.Name, r.Games.ToString(), "", Mathf.RoundToInt((float)r.Average).ToString(), r.Best.ToString(), last },
                    widths, r.Human ? Theme.Text : Theme.TextDim, false);
                // Колонка «Побед» — с полоской процента.
                var winCell = row[2];
                winCell.Clear();
                winCell.Add(WinBar(percent, $"{r.Wins} ({percent}%)"));
                if (string.Equals(r.Name, _player, StringComparison.OrdinalIgnoreCase)) row.style.backgroundColor = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.10f);
                card.Add(row);
            }
            return card;
        }

        VisualElement CharactersBlock()
        {
            var card = Block(LocalizationService.Get("menu.stats.025"));
            var rows = _games.SelectMany(g => g.results).Where(r => !string.IsNullOrEmpty(r.character)).GroupBy(r => r.character)
                .Select(group => new { Name = group.Key, Games = group.Count(), Wins = group.Count(r => r.winner), Average = group.Average(r => r.total) })
                .OrderByDescending(x => (float)x.Wins / Math.Max(1, x.Games)).ThenByDescending(x => x.Games).ToList();
            if (rows.Count == 0)
            {
                card.Add(Caption(LocalizationService.Get("menu.stats.102"), 13f, Theme.TextDim, 0f));
                return card;
            }
            int index = 0;
            foreach (var r in rows)
            {
                int percent = Mathf.RoundToInt(100f * r.Wins / Math.Max(1, r.Games));
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 6f;
                row.style.paddingBottom = 6f;
                row.style.borderBottomWidth = 1f;
                row.style.borderBottomColor = Theme.Line;
                var kind = CharacterOf(r.Name);
                var avatar = new CharacterIcon(40f, kind ?? CharacterKind.None, Palette.PlayerColor(index));
                avatar.style.marginRight = 10f;
                row.Add(avatar);
                var texts = Ui.Column();
                texts.style.flexGrow = 1f;
                texts.Add(Ui.Text(r.Name, 14f, Theme.Text, true));
                texts.Add(Ui.Text((En ? $"{r.Games} games · average score {r.Average:0}" : $"{r.Games} {Plural(r.Games, LocalizationService.Get("menu.stats.004"), LocalizationService.Get("menu.stats.005"), LocalizationService.Get("menu.stats.006"))} · средний счёт {r.Average:0}"), 11f, Theme.TextDim));
                var bar = new ProgressBar(180f, 6f, percent >= 50 ? GameColors.Success : Theme.Gold);
                bar.Set(percent / 100f);
                bar.style.marginTop = 4f;
                texts.Add(bar);
                row.Add(texts);
                var rate = Ui.Column();
                rate.style.alignItems = Align.FlexEnd;
                rate.Add(Ui.Number($"{percent}%", 18f, percent >= 50 ? GameColors.Success : Theme.Text, true));
                rate.Add(Ui.Text(LocalizationService.Get("menu.stats.026"), 10f, Theme.TextDim));
                row.Add(rate);
                card.Add(row);
                index++;
            }
            card.Add(Caption(LocalizationService.Get("menu.stats.103"), 11f, Theme.TextDim, 6f));
            return card;
        }

        static VisualElement WinBar(int percent, string text)
        {
            var cell = Ui.Row();
            cell.style.alignItems = Align.Center;
            var bar = new ProgressBar(46f, 6f, percent >= 50 ? GameColors.Success : Theme.Gold);
            bar.Set(percent / 100f);
            bar.style.marginRight = 6f;
            cell.Add(bar);
            cell.Add(Ui.Text(text, 13f, Theme.Text));
            return cell;
        }

        // ================================================================== Мелочи

        static CharacterKind? CharacterOf(string title)
        {
            foreach (var kind in Characters.All)
                if (Characters.Title(kind) == title) return kind;
            return null;
        }

        static DateTime? ParseDate(string date) =>
            DateTime.TryParseExact(date ?? "", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;

        static string ShortDate(string date) => ParseDate(date) is DateTime d ? d.ToString("dd.MM HH:mm") : date ?? "";

        static UnityEngine.UIElements.Button Chip(string text, bool active, Action onClick)
        {
            var chip = Ui.Button(text, onClick, active ? ButtonKind.Primary : ButtonKind.Secondary, 12f);
            chip.style.minHeight = 30f;
            chip.style.marginLeft = 6f;
            Ui.Padding(chip, 3f, 10f);
            return chip;
        }

        static Label Caption(string text, float size, Color color, float marginTop)
        {
            var label = Ui.Text(text, size, color);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = marginTop;
            label.style.flexShrink = 1f;
            return label;
        }

        static VisualElement Block(string title)
        {
            var card = Ui.Card(18f);
            card.style.backgroundColor = Theme.Panel;
            Ui.Radius(card, 12f);
            card.Add(Ui.SectionTitle(title));
            return card;
        }

        static VisualElement TableRow(string[] cells, float[] widths, Color color, bool header)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 5f;
            if (!header)
            {
                row.style.borderTopWidth = 1f;
                row.style.borderTopColor = Theme.Line;
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = Ui.Row();
                cell.style.width = widths[i];
                var label = Ui.Text(cells[i], header ? 12f : 14f, color, header);
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                cell.Add(label);
                row.Add(cell);
            }
            return row;
        }

        static string Duration(int seconds)
        {
            if (seconds <= 0) return "—";
            int minutes = seconds / 60;
            return En
                ? (minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{Math.Max(1, minutes)}m")
                : (minutes >= 60 ? $"{minutes / 60} ч {minutes % 60} мин" : $"{Math.Max(1, minutes)} мин");
        }

        static string Plural(int n, string one, string few, string many)
        {
            int last = n % 10, lastTwo = n % 100;
            if (last == 1 && lastTwo != 11) return one;
            if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few;
            return many;
        }

        /// <summary>Сброс в два нажатия: первое просит подтверждения.</summary>
        UnityEngine.UIElements.Button ResetButton()
        {
            bool armed = false;
            UnityEngine.UIElements.Button button = null;
            button = Ui.Button(LocalizationService.Get("menu.stats.104"), () =>
            {
                if (!armed)
                {
                    armed = true;
                    button.text = LocalizationService.Get("menu.stats.105");
                    return;
                }
                StatsLog.Clear();
                _rebuild();
            }, ButtonKind.Ghost, 14f);
            return button;
        }
    }
}
