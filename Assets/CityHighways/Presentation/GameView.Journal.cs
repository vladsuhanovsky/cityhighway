using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Журнал событий, переводы старых записей и управление прокруткой.</summary>
    public sealed partial class GameView
    {
        /// <summary>
        /// Журнал: новые записи дописываются в конец, а не пересобирают весь список, — поэтому
        /// прокрутка не прыгает, пока ходят боты. Внизу журнал держится, только если игрок там и был.
        /// </summary>
        void RefreshLog(bool rebuild = false)
        {
            if (_sheet != SheetKind.Log) return; // журнал собираем, только когда его видно
            int fresh = _logTotal - _logShownTotal;
            if (!rebuild && _logShownTotal >= 0 && fresh == 0) return; // ничего нового — прокрутку не трогаем

            if (rebuild || _logShownTotal < 0 || fresh >= _log.Count || fresh < 0)
            {
                _logBox.Clear();
                foreach (var entry in _log) _logBox.Add(LogLine(entry));
            }
            else
            {
                for (int i = _log.Count - fresh; i < _log.Count; i++) _logBox.Add(LogLine(_log[i]));
                while (_logBox.childCount > _log.Count) _logBox.RemoveAt(0);
            }
            // Последняя запись — ярче остальных.
            for (int i = Math.Max(0, _logBox.childCount - fresh - 1); i < _logBox.childCount; i++)
                _logBox[i].style.color = i == _logBox.childCount - 1 ? Theme.Text : Theme.TextDim;

            bool hadShown = _logShownTotal >= 0;
            _logShownTotal = _logTotal;
            if (rebuild) _logStick = true;
            if (_logStick) ScrollLogToEnd();
            else if (hadShown && fresh > 0) Ui.Show(_logNewButton, true);
        }

        static string CityOrderLogRoute(string route)
        {
            if (string.IsNullOrEmpty(route)) return route;
            // The route uses display names, not IDs; preserve punctuation and order.
            var segments = System.Text.RegularExpressions.Regex.Split(route, @"( — | через | и )");
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == " через ") segments[i] = " via ";
                else if (segments[i] == " и ") segments[i] = " and ";
                else if (segments[i] != " — ") segments[i] = DisplayPlace(segments[i]);
            }
            return string.Concat(segments);
        }

        static string CoopJournalText(string message)
        {
            var order = System.Text.RegularExpressions.Regex.Match(message,
                @"^Новый заказ города: (.*?) — (\d+) очк\., срок (\d+) кругов\.$");
            if (order.Success)
                return $"New city order: {CityOrderLogRoute(order.Groups[1].Value)} — {order.Groups[2].Value} pts, due in {order.Groups[3].Value} rounds.";
            var complete = System.Text.RegularExpressions.Regex.Match(message,
                @"^Заказ города выполнен: (.*?) \(\+(\d+) очк\. — (.*?)\)\. Кризис (\d+) → (\d+)\. Выполнено (\d+) из (\d+)\.$");
            if (complete.Success)
                return $"City order completed: {CityOrderLogRoute(complete.Groups[1].Value)} (+{complete.Groups[2].Value} pts — {complete.Groups[3].Value}). Crisis {complete.Groups[4].Value} → {complete.Groups[5].Value}. Completed {complete.Groups[6].Value} of {complete.Groups[7].Value}.";
            var failed = System.Text.RegularExpressions.Regex.Match(message,
                @"^Заказ города просрочен: (.*?)\. Кризис \+(\d+) → (\d+) из (\d+)\.$");
            if (failed.Success)
                return $"City order expired: {CityOrderLogRoute(failed.Groups[1].Value)}. Crisis +{failed.Groups[2].Value} → {failed.Groups[3].Value} of {failed.Groups[4].Value}.";
            var saved = System.Text.RegularExpressions.Regex.Match(message,
                @"^Город спасён: выполнено (\d+) заказов из (\d+)(?:, кризис (\d+) из (\d+))?\.$");
            if (saved.Success)
                return $"City saved: completed {saved.Groups[1].Value} of {saved.Groups[2].Value} orders" +
                    (saved.Groups[3].Success ? $", crisis {saved.Groups[3].Value} of {saved.Groups[4].Value}." : ".");
            var lost = System.Text.RegularExpressions.Regex.Match(message,
                @"^Город встал: кризис дошёл до (\d+)\. Выполнено заказов: (\d+) из (\d+)\.$");
            if (lost.Success)
                return $"City overwhelmed: crisis reached {lost.Groups[1].Value}. Completed {lost.Groups[2].Value} of {lost.Groups[3].Value} orders.";
            var outOfRoads = System.Text.RegularExpressions.Regex.Match(message,
                @"^Участки кончились раньше, чем город получил свои заказы: выполнено (\d+) из (\d+)\.$");
            if (outOfRoads.Success)
                return $"Road pieces ran out before the city received its orders: completed {outOfRoads.Groups[1].Value} of {outOfRoads.Groups[2].Value}.";
            return null;
        }

        static string LuckResultText(string result)
        {
            var insurance = System.Text.RegularExpressions.Regex.Match(result, @"^страховка: вместо дебафа −(\d+) очко\.$");
            if (insurance.Success) return $"Insurance: −{insurance.Groups[1].Value} pt instead of the penalty.";
            var influence = System.Text.RegularExpressions.Regex.Match(result, @"^\+(\d+) влияния\.$");
            if (influence.Success) return $"+{influence.Groups[1].Value} influence.";
            var points = System.Text.RegularExpressions.Regex.Match(result, @"^\+(\d+) очков\.$");
            if (points.Success) return $"+{points.Groups[1].Value} pts.";
            var cards = System.Text.RegularExpressions.Regex.Match(result, @"^\+(\d+) карт ресурсов\.$");
            if (cards.Success) return $"+{cards.Groups[1].Value} resource cards.";
            var premium = System.Text.RegularExpressions.Regex.Match(result, @"^выполнено контрактов: (\d+), \+(\d+) очков\.$");
            if (premium.Success) return $"{premium.Groups[1].Value} completed contracts, +{premium.Groups[2].Value} pts.";
            var fine = System.Text.RegularExpressions.Regex.Match(result, @"^−(\d+) очка\.$");
            if (fine.Success) return $"−{fine.Groups[1].Value} pts.";
            var stolen = System.Text.RegularExpressions.Regex.Match(result, @"^−(\d+) карта с руки\.$");
            if (stolen.Success) return $"Discard {stolen.Groups[1].Value} cards from hand.";
            if (result == "рука пуста, красть нечего.") return "Hand is empty; nothing to steal.";
            var recallFine = System.Text.RegularExpressions.Regex.Match(result, @"^невыполненных контрактов нет, −(\d+) очка\.$");
            if (recallFine.Success) return $"No unfinished contracts; −{recallFine.Groups[1].Value} pts.";
            var recall = System.Text.RegularExpressions.Regex.Match(result, @"^контракт (.*?) — (.*?) \((\d+)\) уходит под колоду\.$");
            if (recall.Success)
                return $"Contract {DisplayPlace(recall.Groups[1].Value)} — {DisplayPlace(recall.Groups[2].Value)} ({recall.Groups[3].Value}) goes to the bottom of the deck.";
            return LocalizationService.TranslateLegacy(result);
        }

        static string LuckTitleFromRussian(string name)
        {
            foreach (LuckCardKind kind in Enum.GetValues(typeof(LuckCardKind)))
                if (LuckInfo.Title(kind) == name) return LuckCardTitle(kind);
            return name;
        }

        static string ArchivedCityEventText(string message)
        {
            var line = System.Text.RegularExpressions.Regex.Match(message, @"^Событие: (.*?)\. (.*)$");
            if (!line.Success) return null;
            var title = line.Groups[1].Value;
            var body = line.Groups[2].Value;
            if (title == "Час пик")
            {
                var toll = System.Text.RegularExpressions.Regex.Match(body,
                    @"^Дороги, построенные в этот круг, станут загруженными магистралями: проезд по ним — (\d+) очк\. вместо (\d+)\.$");
                if (toll.Success)
                    return $"Event: Rush Hour. Roads built this round become busy highways; tolls are {toll.Groups[1].Value} pts instead of {toll.Groups[2].Value}.";
            }
            if (title == "Ремонт")
            {
                if (body == "Свободных участков для ремонта не нашлось.")
                    return "Event: Roadworks. No free roads were available for repairs.";
                var closed = System.Text.RegularExpressions.Regex.Match(body,
                    @"^До конца круга нельзя строить: (.*)\.$");
                if (closed.Success)
                {
                    var roads = closed.Groups[1].Value.Split(new[] { "; " }, StringSplitOptions.None);
                    for (int i = 0; i < roads.Length; i++)
                        roads[i] = CityOrderLogRoute(roads[i]);
                    return "Event: Roadworks. Construction blocked this round: " + string.Join("; ", roads) + ".";
                }
            }
            if (title == "Субсидия мэрии")
            {
                var subsidy = System.Text.RegularExpressions.Regex.Match(body,
                    @"^Мосты в этот круг дешевле на (\d+) карт\(у\) стали \(но не меньше одной\)\.$");
                if (subsidy.Success)
                    return $"Event: City Hall Subsidy. Bridges cost {subsidy.Groups[1].Value} fewer steel cards this round (minimum one).";
            }
            if (title == "Парад")
            {
                var parade = System.Text.RegularExpressions.Regex.Match(body,
                    @"^Район «(.*)» перекрыт: до конца круга нельзя строить дороги, которые его касаются\.$");
                if (parade.Success)
                    return $"Event: Parade. District {DisplayPlace(parade.Groups[1].Value)} is closed to construction this round.";
            }
            if (title == "Снегопад")
            {
                var snow = System.Text.RegularExpressions.Regex.Match(body,
                    @"^Щебёночные дороги в этот круг на (\d+) карт\(у\) дороже\.$");
                if (snow.Success)
                    return $"Event: Snowfall. Gravel roads cost {snow.Groups[1].Value} extra cards this round.";
            }
            if (title == "Инвестор")
            {
                var investor = System.Text.RegularExpressions.Regex.Match(body,
                    @"^Каждая дорога, построенная в этот круг, приносит ещё \+(\d+) очка\.$");
                if (investor.Success)
                    return $"Event: Investor. Each road built this round earns +{investor.Groups[1].Value} pts.";
            }
            if (title == "Новый район")
            {
                var district = System.Text.RegularExpressions.Regex.Match(body,
                    @"^На окраине открылся район «(.*)»: новые места, дороги и контракты\.$");
                if (district.Success)
                    return $"Event: New District. {DisplayPlace(district.Groups[1].Value)} opened on the outskirts, bringing locations, roads and contracts.";
            }
            return null; // Unknown formats remain unchanged rather than inventing a translation.
        }

        string JournalDisplayText(string message) => FormatJournalMessage(_game, message);

        public static string FormatJournalMessage(Game game, string message)
        {
            if (!EnglishUi || string.IsNullOrEmpty(message)) return message;
            switch (message)
            {
                case "Ходов больше нет ни у кого.": return "No player has any turns left.";
                case "Открыто слишком много техники — ряд выложен заново.":
                    return "Too many face-up machinery cards: the market has been refreshed.";
                case "Победа команды!": return "Team victory!";
            }
            if (message.StartsWith("Победа команды! ", StringComparison.Ordinal))
            {
                string reason = message.Substring("Победа команды! ".Length);
                return "Team victory! " + (CoopJournalText(reason) ?? reason);
            }
            if (message.StartsWith("Город победил. ", StringComparison.Ordinal))
            {
                string reason = message.Substring("Город победил. ".Length);
                return "The city won. " + (CoopJournalText(reason) ?? reason);
            }
            var finalRound = System.Text.RegularExpressions.Regex.Match(message,
                @"^У компании (.*?) осталось участков: (\d+)\. Начинается последний круг: у каждого ещё один ход\.$");
            if (finalRound.Success)
                return $"{finalRound.Groups[1].Value} has {finalRound.Groups[2].Value} road pieces left. Final round begins: every player gets one more turn.";
            var starting = System.Text.RegularExpressions.Regex.Match(message, @"^Игра началась\. Ходит (.*)\.$");
            if (starting.Success) return $"Game started. {starting.Groups[1].Value} plays first.";
            var defeat = System.Text.RegularExpressions.Regex.Match(message, @"^Игра окончена\. Победа: (.*)\.$");
            if (defeat.Success) return $"Game over. Winner: {defeat.Groups[1].Value}.";
            var crisis = System.Text.RegularExpressions.Regex.Match(message, @"^Город растёт: кризис \+1 → (\d+) из (\d+)\.$");
            if (crisis.Success) return $"The city grows: crisis +1 → {crisis.Groups[1].Value} of {crisis.Groups[2].Value}.";
            var eventCrisis = System.Text.RegularExpressions.Regex.Match(message, @"^Событие давит на город: кризис \+(\d+) → (\d+) из (\d+)\.$");
            if (eventCrisis.Success) return $"A city event increases the crisis by {eventCrisis.Groups[1].Value} → {eventCrisis.Groups[2].Value} of {eventCrisis.Groups[3].Value}.";
            string scenario = game == null ? null :
                ScenarioMenuLocalization.JournalAnnouncement(message, game.Scenario, game.Config);
            if (scenario != null) return scenario;
            string trade = LocalizationService.TranslateTradeJournal(message);
            if (trade != null) return trade;
            string progress = LocalizationService.TranslateProgressJournal(message, DisplayPlace);
            if (progress != null) return progress;
            string special = LocalizationService.TranslateSpecialJournal(message, DisplayPlace);
            if (special != null) return special;
            string coop = CoopJournalText(message);
            if (coop != null) return coop;
            string cityEvent = ArchivedCityEventText(message);
            if (cityEvent != null) return cityEvent;
            if (game == null) return message;
            foreach (var player in game.Players)
            {
                string prefix = player.Name + " ";
                if (!message.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string action = message.Substring(prefix.Length);
                var initial = System.Text.RegularExpressions.Regex.Match(action, @"^берёт стартовых контрактов: (\d+)\.$");
                if (initial.Success) return $"{player.Name} keeps {initial.Groups[1].Value} starting contracts.";
                var faceup = System.Text.RegularExpressions.Regex.Match(action, @"^берёт открытую карту: (.*)\.$");
                if (faceup.Success)
                    return $"{player.Name} draws a face-up {EnglishResourceName(faceup.Groups[1].Value)} card.";
                if (action == "берёт карту из колоды.") return $"{player.Name} draws a card from the deck.";
                var choice = System.Text.RegularExpressions.Regex.Match(action, @"^берёт контракты: (\d+) шт\.$");
                if (choice.Success) return $"{player.Name} draws {choice.Groups[1].Value} contracts.";
                var kept = System.Text.RegularExpressions.Regex.Match(action, @"^оставляет контрактов: (\d+)\.$");
                if (kept.Success) return $"{player.Name} keeps {kept.Groups[1].Value} contracts.";
                if (action == "пропускает ход.") return $"{player.Name} skips a turn.";
                var luckyChoice = System.Text.RegularExpressions.Regex.Match(action,
                    @"^открывает две карты удачи: «(.*?)» и «(.*?)»\.$");
                if (luckyChoice.Success)
                    return $"{player.Name} reveals two luck cards: “{LuckTitleFromRussian(luckyChoice.Groups[1].Value)}” and “{LuckTitleFromRussian(luckyChoice.Groups[2].Value)}”.";
                var luck = System.Text.RegularExpressions.Regex.Match(action,
                    @"^берёт карту удачи «(.*?)»: (.*)$");
                if (luck.Success)
                    return $"{player.Name} draws luck card “{LuckTitleFromRussian(luck.Groups[1].Value)}”: {LuckResultText(luck.Groups[2].Value)}";
                var goalClaim = System.Text.RegularExpressions.Regex.Match(action,
                    @"^первым выполняет цель мэрии «(.*?)» \(\+(\d+)\)\.$");
                if (goalClaim.Success)
                {
                    string goalName = goalClaim.Groups[1].Value;
                    var matchedGoal = game.ActiveGoals.FirstOrDefault(g => g.Name == goalName);
                    return $"{player.Name} claims City Hall goal “{(matchedGoal != null ? MayorGoalName(matchedGoal) : goalName)}” (+{goalClaim.Groups[2].Value} pts).";
                }

                var road = System.Text.RegularExpressions.Regex.Match(action,
                    @"^строит( бесплатно \(Прораб\))? (улица \(любой ресурс\)|загородная дорога|улица|проспект|мост|дорога) (.*?) — (.*?) \(\+(\d+)\)\.$");
                if (road.Success)
                    return $"{player.Name} builds {(road.Groups[1].Success ? "a free " : "")}{EnglishRoadType(road.Groups[2].Value)} {DisplayPlace(road.Groups[3].Value)} — {DisplayPlace(road.Groups[4].Value)} (+{road.Groups[5].Value} pts).";
            }
            return LocalizationService.TranslateLegacy(message);
        }

        static string EnglishResourceName(string name)
        {
            switch (name)
            {
                case "асфальт": return "asphalt";
                case "бетон": return "concrete";
                case "сталь": return "steel";
                case "щебень": return "gravel";
                case "техника": return "machinery";
                default: return name;
            }
        }

        static string EnglishRoadType(string name)
        {
            switch (name)
            {
                case "улица": return "street";
                case "проспект": return "avenue";
                case "мост": return "bridge";
                case "загородная дорога": return "country road";
                case "улица (любой ресурс)": return "street";
                case "дорога": return "road";
                default: return name;
            }
        }

        Label LogLine(string text)
        {
            var line = Ui.Text(JournalDisplayText(text), 12f, Theme.TextDim);
            line.style.whiteSpace = WhiteSpace.Normal;
            line.style.marginBottom = 4f;
            line.style.flexShrink = 0f;
            return line;
        }

        /// <summary>Содержимое журнала поменяло размер — если он был внизу, остаёмся внизу.</summary>
        void KeepLogAtEnd()
        {
            if (_logStick && _sheet == SheetKind.Log) ScrollLogToEnd();
        }

        void ScrollLogToEnd()
        {
            var scroller = _logPage.verticalScroller;
            scroller.value = scroller.highValue;
            // Размер прокрутки обновляется после раскладки — повторяем в следующем кадре.
            _logPage.schedule.Execute(() =>
            {
                if (_logStick) scroller.value = scroller.highValue;
            });
        }

    }
}
