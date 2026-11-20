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

        public static string Get(string key)
        {
            EnsureLoaded();
            if (key == null) return "";
            if (_language == "en" && English.TryGetValue(key, out var english)) return english;
            return Russian.TryGetValue(key, out var russian) ? russian : key;
        }
    }
}
