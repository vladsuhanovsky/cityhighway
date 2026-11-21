using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// First localization slice. Language tables live in Resources/CityHighways/Localization.
    /// UI uses stable keys, not Russian source strings. Unmigrated screens stay unchanged.
    /// </summary>
    public static class LocalizationService
    {
        [Serializable] sealed class Entry { public string key; public string value; }
        [Serializable] sealed class Catalog { public Entry[] entries; }

        const string PrefsKey = "CityHighways.Language";
        const string Root = "CityHighways/Localization/";
        static readonly Dictionary<string, string> Russian = new Dictionary<string, string>();
        static readonly Dictionary<string, string> English = new Dictionary<string, string>();
        static readonly Dictionary<string, string> LegacyEnglish = new Dictionary<string, string>();
        static readonly HashSet<string> AmbiguousLegacy = new HashSet<string>();
        static bool _loaded;
        static string _language;

        public static string Language
        {
            get
            {
                EnsureLoaded();
                return _language;
            }
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            ReadTable("ru", Russian);
            ReadTable("en", English);
            // Allow shared UI controls to reuse existing translations of unmodified legacy labels.
            // A Russian source string may mean different things in different contexts:
            // never guess when its English translations disagree.
            foreach (var entry in Russian)
            {
                if (string.IsNullOrEmpty(entry.Value) || !English.TryGetValue(entry.Key, out var translated))
                    continue;
                if (LegacyEnglish.TryGetValue(entry.Value, out var current) && current != translated)
                {
                    AmbiguousLegacy.Add(entry.Value);
                    LegacyEnglish.Remove(entry.Value);
                }
                else if (!AmbiguousLegacy.Contains(entry.Value))
                    LegacyEnglish[entry.Value] = translated;
            }
            string saved = PlayerPrefs.GetString(PrefsKey, "ru");
            _language = saved == "en" ? "en" : "ru";
        }

        static void ReadTable(string language, Dictionary<string, string> table)
        {
            var asset = Resources.Load<TextAsset>(Root + language);
            if (asset == null)
            {
                Debug.LogWarning("Localization table missing: " + language);
                return;
            }
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog?.entries == null) return;
            foreach (var entry in catalog.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                if (table.ContainsKey(entry.key))
                    Debug.LogWarning("Duplicate localization key: " + language + "/" + entry.key);
                table[entry.key] = entry.value ?? "";
            }
        }

        public static void SetLanguage(string language)
        {
            EnsureLoaded();
            if (language != "ru" && language != "en")
                throw new ArgumentException("Unsupported language: " + language, nameof(language));
            if (_language == language) return;
            _language = language;
            PlayerPrefs.SetString(PrefsKey, language);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Compatibility translation for legacy static text displayed through shared UI helpers.
        /// Ambiguous strings and dynamic messages are deliberately left unchanged.
        /// New code should always use Get with a semantic key.
        /// </summary>
        public static string TranslateLegacy(string text)
        {
            EnsureLoaded();
            if (_language == "en" && text != null && LegacyEnglish.TryGetValue(text, out var english))
                return english;
            return text;
        }

        // Core error strings are translated only when displayed, never in game state.
        static readonly (string Pattern, string Key, bool ResourceName)[] DynamicErrorPatterns =
        {
            (@"^Нужно влияния: (\d+), у вас (\d+)\.$", "game.error.dynamic.001", false),
            (@"^Взятки — только когда игроков (\d+) и больше\.$", "game.error.dynamic.002", false),
            (@"^У вас только (\d+) влияния\.$", "game.error.dynamic.003", false),
            (@"^Нужно оставить хотя бы (\d+) шт\.$", "game.error.dynamic.004", false),
            (@"^В сделке до (\d+) карт с каждой стороны\.$", "game.error.dynamic.005", false),
            (@"^В сделке до (\d+) влияния с каждой стороны\.$", "game.error.dynamic.006", false),
            (@"^Сейчас ходит не игрок (\d+)\.$", "game.error.dynamic.007", false),
            (@"^У (.+) уже есть ваш пропуск\.$", "game.error.dynamic.008", false),
            (@"^У вас уже есть пропуск (.+)\.$", "game.error.dynamic.009", false),
            (@"^Нужно (\d+) карт\(ы\) одного ресурса \(техника подходит вместо любой карты\)\.$", "game.error.dynamic.010", false),
            (@"^Нужно (\d+) карт\(ы\) «([^»]+)» \(техника подходит вместо любой карты\)\.$", "game.error.dynamic.011", true),
            (@"^Эту дорогу строят из ресурса «([^»]+)»\.$", "game.error.dynamic.012", true),
            (@"^Не хватает карт «([^»]+)»\.$", "game.error.dynamic.013", true),
            (@"^Дорогу уже построил (.+)\.$", "game.error.dynamic.014", false),
            (@"^(.+) уже выбрал контракты\.$", "game.error.dynamic.015", false),
            (@"^Бесплатно — только дороги длиной до (\d+)\.$", "game.error.dynamic.016", false),
            (@"^Контракта (\d+) нет среди предложенных\.$", "game.error.dynamic.017", false),
            (@"^Контракт (\d+) указан дважды\.$", "game.error.dynamic.018", false),
        };

        static string LocalizedErrorResource(string name)
        {
            switch (name)
            {
                case "асфальт": return Get("game.error.resource.asphalt");
                case "бетон": return Get("game.error.resource.concrete");
                case "сталь": return Get("game.error.resource.steel");
                case "щебень": return Get("game.error.resource.gravel");
                case "техника": return Get("game.error.resource.machinery");
                default: return null;
            }
        }

        /// <summary>Translate recognized parameterized errors; keep unknown text intact.</summary>
        public static string TranslateGameError(string message)
        {
            if (string.IsNullOrEmpty(message) || Language != "en") return message;
            string translated = TranslateLegacy(message);
            if (translated != message) return translated;
            foreach (var item in DynamicErrorPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    message, item.Pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (!match.Success) continue;
                object[] arguments = new object[match.Groups.Count - 1];
                for (int index = 0; index < arguments.Length; index++)
                    arguments[index] = match.Groups[index + 1].Value;
                if (item.ResourceName)
                {
                    int index = arguments.Length - 1;
                    string resource = LocalizedErrorResource((string)arguments[index]);
                    if (resource == null) return message;
                    arguments[index] = resource;
                }
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(item.Key), arguments);
            }
            return message;
        }

        /// <summary>
        /// Localize self-contained game journal entries. Player-supplied names and bid details
        /// remain unchanged. Returns null for formats the presenter does not yet know.
        /// </summary>
        public static string TranslateSpecialJournal(string message)
        {
            if (string.IsNullOrEmpty(message) || Language != "en") return null;

            var match = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) делает тайную ставку\.$");
            if (match.Success) return match.Groups[1].Value + " places a secret bid.";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^Аукцион выигрывает (.+) за (\d+) влияния \(ставки: (.*)\)\.$");
            if (match.Success)
                return $"Auction winner: {match.Groups[1].Value} for {match.Groups[2].Value} influence (bids: {match.Groups[3].Value}).";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^На аукционе никто не поставил — контракт уходит под колоду \(ставки: (.*)\)\.$");
            if (match.Success)
                return $"No auction bids were made; the contract returns to the bottom of the deck (bids: {match.Groups[1].Value}).";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^В прошлом круге в мэрии шептались: взяток — (\d+)\. Кто — неизвестно; раскрыть можно до конца этого круга\.$");
            if (match.Success)
                return $"There were {match.Groups[1].Value} bribes last round. Their authors remain unknown and can be exposed before this round ends.";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^У (.+) не нашлось нужного — сделки нет\.$");
            if (match.Success)
                return $"{match.Groups[1].Value} lacks the required resources; the trade is canceled.";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) соглашается на сделку\.$");
            if (match.Success) return $"{match.Groups[1].Value} accepts the trade.";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) отказывается от сделки\.$");
            if (match.Success) return $"{match.Groups[1].Value} rejects the trade.";

            match = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) теперь ездит по дорогам (.+) бесплатно\.$");
            if (match.Success)
                return $"{match.Groups[1].Value} now travels free on roads owned by {match.Groups[2].Value}.";

            return null;
        }

        /// <summary>
        /// Translate construction-adjacent journal entries. Keep player names and numeric
        /// values as stored by the core; localize city locations through the presenter.
        /// Unknown formats return null instead of producing a misleading translation.
        /// </summary>
        public static string TranslateProgressJournal(string message, Func<string, string> placeName = null)
        {
            if (string.IsNullOrEmpty(message) || Language != "en") return null;
            Func<string, string> place = placeName ?? (name => name);
            System.Text.RegularExpressions.Match m;

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^Пробка на дороге (.*?) — (.*?): дальше проезд по ней ещё \+(\d+) очк\. городу\. Владелец может улучшить её до магистрали\.$");
            if (m.Success)
                return $"Traffic jam on {place(m.Groups[1].Value)} — {place(m.Groups[2].Value)}: future trips pay an extra {m.Groups[3].Value} pts to the city. The owner can upgrade the road to a highway.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) платит за проезд: (.+) очк\.(?:, пробка: ещё (\d+) городу)?\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} pays road tolls: {m.Groups[2].Value} pts" +
                    (m.Groups[3].Success ? $", plus {m.Groups[3].Value} pts to the city for traffic jams." : ".");

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) улучшает дорогу (.*?) — (.*?) до магистрали \(\+(\d+)\)\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} upgrades {place(m.Groups[2].Value)} — {place(m.Groups[3].Value)} to a highway (+{m.Groups[4].Value} pts).";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) тратит (\d+) влияния: рынок выложен заново \(следующее обновление — (\d+)\)\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} spends {m.Groups[2].Value} influence to refresh the market (next refresh costs {m.Groups[3].Value}).";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) тратит (\d+) влияния на выбор контрактов\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} spends {m.Groups[2].Value} influence to select contracts.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) получает разрешение мэрии за (\d+) влияния: в этот ход ему не мешают снег и ремонт\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} buys a City Hall permit for {m.Groups[2].Value} influence: snow and repairs do not restrict this turn.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) получает разрешение мэрии за (\d+) влияния: «(.+)» в этот ход ему не мешает\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} buys a City Hall permit for {m.Groups[2].Value} influence: {TranslateCityEventName(m.Groups[3].Value)} does not restrict this turn.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) тратит (\d+) влияния и отменяет событие «(.+)»\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} spends {m.Groups[2].Value} influence to cancel the event “{TranslateCityEventName(m.Groups[3].Value)}”.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+): \+(\d+) влияния \((.+)\)\.$");
            if (m.Success)
            {
                string reason = TranslateInfluenceReason(m.Groups[3].Value, place);
                if (reason == null) return null;
                return $"{m.Groups[1].Value}: +{m.Groups[2].Value} influence ({reason}).";
            }

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^Скоро финал: у компании (.+) осталось участков — (\d+)\. Когда у кого-то останется (\d+) или меньше, начнётся последний круг\.$");
            if (m.Success)
                return $"Final round approaching: {m.Groups[1].Value} has {m.Groups[2].Value} road pieces left. The final round begins when a player has {m.Groups[3].Value} or fewer.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^Финальный заказ мэрии: дорога (.*?) — (.*?)\. Кто построит её в последнем круге, получит \+(\d+) очков\.$");
            if (m.Success)
                return $"Final City Hall order: build {place(m.Groups[1].Value)} — {place(m.Groups[2].Value)} during the final round for +{m.Groups[3].Value} pts.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^(.+) выполняет финальный заказ мэрии: \+(\d+) очков\.$");
            if (m.Success)
                return $"{m.Groups[1].Value} completes the final City Hall order: +{m.Groups[2].Value} pts.";

            if (message == "Проверка перед итогами: взяток не нашли.")
                return "Final audit: no bribes were found.";

            m = System.Text.RegularExpressions.Regex.Match(message,
                @"^Проверка перед итогами: нашли взяток — (\d+) \((.*)\)\.$");
            if (m.Success)
                return $"Final audit: {m.Groups[1].Value} bribes exposed ({m.Groups[2].Value}).";

            return null;
        }

        static string TranslateCityEventName(string name)
        {
            switch (name)
            {
                case "Час пик": return "Rush Hour";
                case "Ремонт": return "Roadworks";
                case "Субсидия мэрии": return "City Hall Subsidy";
                case "Парад": return "Parade";
                case "Снегопад": return "Snowfall";
                case "Инвестор": return "Investor";
                case "Новый район": return "New District";
                default: return name;
            }
        }

        static string TranslateInfluenceReason(string reason, Func<string, string> place)
        {
            if (reason == "новый район") return "new district";
            if (reason == "поддержка отстающих") return "catch-up support";
            var m = System.Text.RegularExpressions.Regex.Match(reason, @"^дорога длиной (\d+)$");
            if (m.Success) return $"road of length {m.Groups[1].Value}";
            m = System.Text.RegularExpressions.Regex.Match(reason, @"^контракт (.*?) — (.*?) выполнен$");
            if (m.Success) return $"contract {place(m.Groups[1].Value)} — {place(m.Groups[2].Value)} completed";
            return null;
        }

        /// <summary>Look up translated UI data by stable ID, preserving its Russian source as fallback.</summary>
        public static string GetOrFallback(string key, string fallback)
        {
            EnsureLoaded();
            if (_language == "en" && English.TryGetValue(key, out var english)) return english;
            if (Russian.TryGetValue(key, out var russian)) return russian;
            return fallback ?? "";
        }

        public static string Get(string key)
        {
            EnsureLoaded();
            if (key == null) return "";
            if (_language == "en" && English.TryGetValue(key, out var english)) return english;
            return Russian.TryGetValue(key, out var russian) ? russian : key;
        }
    }
}
