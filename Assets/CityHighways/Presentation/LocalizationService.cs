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
