using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Счётчик партий»: сводка, игроки (люди по именам, боты по уровню), персонажи
    /// и последние партии. Данные — из cityhighways_stats.jsonl (StatsLog).
    /// </summary>
    public static class MenuStatsPage
    {
        public static VisualElement Build(Action back, Action show, Action rebuild)
        {
            var games = StatsLog.LoadAll();
            var page = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 18f;
            var backButton = Ui.Button("← Назад", back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            var title = Ui.Text("Счётчик партий", 34f, Theme.Accent, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            if (games.Count > 0) header.Add(ResetButton(rebuild));
            page.Add(header);

            if (games.Count == 0)
            {
                var empty = Ui.Card(30f);
                empty.style.backgroundColor = Theme.Panel;
                empty.style.alignItems = Align.Center;
                empty.Add(Ui.Text("Пока ни одной сыгранной партии", 22f, Theme.Text, true));
                var hint = Ui.Text("Доиграйте партию до конца — здесь появятся победы, рекорды и любимые персонажи.", 14f, Theme.TextDim);
                hint.style.marginTop = 8f;
                empty.Add(hint);
                page.Add(empty);
                return page;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.height = 660f;
            page.Add(scroll);

            scroll.Add(Totals(games));
            var columns = Ui.Row();
            columns.style.alignItems = Align.FlexStart;
            columns.style.marginTop = 14f;
            var left = Players(games);
            left.style.flexGrow = 1f;
            left.style.marginRight = 14f;
            columns.Add(left);
            var right = CharactersBlock(games);
            right.style.width = 380f;
            columns.Add(right);
            scroll.Add(columns);
            scroll.Add(Recent(games));
            return page;
        }

        // ================================================================== Блоки

        static VisualElement Totals(List<GameStat> games)
        {
            var row = Ui.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            int seconds = games.Sum(g => g.seconds);
            var best = games.SelectMany(g => g.results.Select(r => (game: g, result: r)))
                .OrderByDescending(x => x.result.total).First();
            int humanGames = games.Count(g => g.results.Any(r => r.kind == "human"));
            int humanWins = games.Count(g => g.results.Any(r => r.kind == "human" && r.winner));

            row.Add(Stat("Сыграно партий", games.Count.ToString()));
            row.Add(Stat("Время в игре", Duration(seconds)));
            row.Add(Stat("Средняя партия", Duration(seconds / Math.Max(1, games.Count))));
            row.Add(Stat("Победы людей", humanGames > 0 ? $"{humanWins} из {humanGames}" : "—"));
            row.Add(Stat("Рекорд", $"{best.result.total}", $"{best.result.name}, {best.game.date}"));
            return row;
        }

        static VisualElement Stat(string caption, string value, string note = null)
        {
            var card = Ui.Card(14f);
            card.style.backgroundColor = Theme.Panel;
            card.style.width = 210f;
            Ui.Radius(card, 10f);
            card.Add(Ui.Text(caption, 13f, Theme.TextDim));
            card.Add(Ui.Text(value, 28f, Theme.Accent, true));
            if (note != null) card.Add(Ui.Text(note, 12f, Theme.TextDim));
            return card;
        }

        /// <summary>Люди — по именам, боты — «Умный бот» / «Случайный бот».</summary>
        static VisualElement Players(List<GameStat> games)
        {
            var card = Block("Игроки");
            var rows = games
                .SelectMany(g => g.results)
                .GroupBy(r => r.kind == "human" ? r.name : r.kind == "random" ? "Случайный бот" : "Умный бот")
                .Select(group => new
                {
                    Name = group.Key,
                    Human = group.Any(r => r.kind == "human"),
                    Games = group.Count(),
                    Wins = group.Count(r => r.winner),
                    Average = group.Average(r => r.total),
                    Best = group.Max(r => r.total),
                    Contracts = group.Sum(r => r.completedContracts),
                    Failed = group.Sum(r => r.failedContracts)
                })
                .OrderByDescending(x => x.Human)
                .ThenByDescending(x => x.Games)
                .ToList();

            float[] widths = { 200f, 70f, 90f, 90f, 80f, 120f };
            card.Add(TableRow(new[] { "Игрок", "Партий", "Побед", "Ср. очки", "Лучший", "Контракты" }, widths, Theme.TextDim, true));
            foreach (var r in rows)
            {
                int percent = Mathf.RoundToInt(100f * r.Wins / Math.Max(1, r.Games));
                card.Add(TableRow(new[]
                {
                    r.Name, r.Games.ToString(), $"{r.Wins} ({percent}%)", Mathf.RoundToInt((float)r.Average).ToString(),
                    r.Best.ToString(), $"{r.Contracts} / −{r.Failed}"
                }, widths, r.Human ? Theme.Text : Theme.TextDim, false));
            }
            return card;
        }

        static VisualElement CharactersBlock(List<GameStat> games)
        {
            var card = Block("Персонажи");
            var rows = games
                .SelectMany(g => g.results)
                .Where(r => !string.IsNullOrEmpty(r.character))
                .GroupBy(r => r.character)
                .Select(group => new { Name = group.Key, Games = group.Count(), Wins = group.Count(r => r.winner) })
                .OrderByDescending(x => (float)x.Wins / Math.Max(1, x.Games))
                .ThenByDescending(x => x.Games)
                .ToList();
            if (rows.Count == 0)
            {
                card.Add(Ui.Text("Партий с персонажами пока не было.", 13f, Theme.TextDim));
                return card;
            }
            float[] widths = { 170f, 70f, 110f };
            card.Add(TableRow(new[] { "Персонаж", "Партий", "Побед" }, widths, Theme.TextDim, true));
            foreach (var r in rows)
            {
                int percent = Mathf.RoundToInt(100f * r.Wins / Math.Max(1, r.Games));
                card.Add(TableRow(new[] { r.Name, r.Games.ToString(), $"{r.Wins} ({percent}%)" }, widths, Theme.Text, false));
            }
            var note = Ui.Text("Считаются все компании, включая ботов.", 12f, Theme.TextDim);
            note.style.marginTop = 6f;
            card.Add(note);
            return card;
        }

        static VisualElement Recent(List<GameStat> games)
        {
            var card = Block("Последние партии");
            card.style.marginTop = 14f;
            float[] widths = { 150f, 260f, 420f, 100f };
            card.Add(TableRow(new[] { "Когда", "Победитель", "Остальные", "Длилась" }, widths, Theme.TextDim, true));
            foreach (var game in Enumerable.Reverse(games).Take(10))
            {
                var ordered = game.results.OrderByDescending(r => r.total).ToList();
                var winner = ordered.First();
                string others = string.Join(", ", ordered.Skip(1).Select(r => $"{r.name} {r.total}"));
                string winnerText = $"{winner.name} — {winner.total}" + (string.IsNullOrEmpty(winner.character) ? "" : $" ({winner.character})");
                card.Add(TableRow(new[] { game.date, winnerText, others, Duration(game.seconds) }, widths, Theme.Text, false));
            }
            return card;
        }

        // ================================================================== Мелочи

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
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            if (!header)
            {
                row.style.borderTopWidth = 1f;
                row.style.borderTopColor = Theme.Line;
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var label = Ui.Text(cells[i], header ? 12f : 14f, color, header);
                label.style.width = widths[i];
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(label);
            }
            return row;
        }

        static string Duration(int seconds)
        {
            if (seconds <= 0) return "—";
            int minutes = seconds / 60;
            return minutes >= 60 ? $"{minutes / 60} ч {minutes % 60} мин" : $"{Math.Max(1, minutes)} мин";
        }

        /// <summary>Сброс в два нажатия: первое просит подтверждения.</summary>
        static UnityEngine.UIElements.Button ResetButton(Action rebuild)
        {
            bool armed = false;
            UnityEngine.UIElements.Button button = null;
            button = Ui.Button("Сбросить статистику", () =>
            {
                if (!armed)
                {
                    armed = true;
                    button.text = "Точно сбросить? Нажмите ещё раз";
                    return;
                }
                StatsLog.Clear();
                rebuild();
            }, ButtonKind.Ghost, 14f);
            return button;
        }
    }
}
