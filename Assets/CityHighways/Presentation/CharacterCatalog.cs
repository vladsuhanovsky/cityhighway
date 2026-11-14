using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public class CharacterItem
    {
        public string id;
        public string slot;
        public string name;
        public int price;
        /// <summary>Путь картинки внутри Resources/CityHighways/Character без расширения, например «hat/hat_cap».</summary>
        public string file;
    }

    [Serializable]
    public class CharacterDefaults
    {
        public string body, hair, top, bottom, shoes, hat, accessory;
    }

    [Serializable]
    class CharacterCatalogData
    {
        public int[] canvas;
        public string[] order;
        public CharacterDefaults defaults;
        public CharacterItem[] items;
    }

    /// <summary>
    /// Каталог одежды персонажа: Resources/CityHighways/Character/catalog.json и PNG-слои 512×768 с прозрачным
    /// фоном. Новую вещь добавляют без кода: картинку в папку слота и строчку в catalog.json.
    /// Заглушки и шаблон для художника — Tools~/character_placeholders.py, Tools~/character_template.png.
    /// </summary>
    public static class CharacterCatalog
    {
        const string Root = "CityHighways/Character/";

        /// <summary>Слоты в порядке показа в магазине.</summary>
        public static readonly string[] Slots = { "body", "hair", "hat", "top", "bottom", "shoes", "accessory" };
        /// <summary>Слоты, которые нельзя оставить пустыми.</summary>
        static readonly HashSet<string> Required = new HashSet<string> { "body", "hair", "top", "bottom", "shoes" };

        public static string SlotTitle(string slot)
        {
            switch (slot)
            {
                case "body": return "Внешность";
                case "hair": return "Причёска";
                case "hat": return "Головной убор";
                case "top": return "Верх";
                case "bottom": return "Низ";
                case "shoes": return "Обувь";
                default: return "Аксессуар";
            }
        }

        public static bool IsRequired(string slot) => Required.Contains(slot);

        static CharacterCatalogData _data;
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        static CharacterCatalogData Data
        {
            get
            {
                if (_data != null) return _data;
                var asset = Resources.Load<TextAsset>(Root + "catalog");
                try
                {
                    _data = asset != null ? JsonUtility.FromJson<CharacterCatalogData>(asset.text) : null;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("City Highways: catalog.json персонажа не прочитался: " + e.Message);
                }
                _data = _data ?? new CharacterCatalogData();
                _data.items = _data.items ?? new CharacterItem[0];
                _data.order = _data.order ?? new[] { "body", "bottom", "shoes", "top", "hair", "accessory", "hat" };
                _data.defaults = _data.defaults ?? new CharacterDefaults();
                return _data;
            }
        }

        /// <summary>Порядок слоёв при рисовании: снизу вверх.</summary>
        public static IReadOnlyList<string> DrawOrder => Data.order;

        public static IEnumerable<CharacterItem> ItemsIn(string slot) => Data.items.Where(i => i.slot == slot);

        public static CharacterItem Item(string id) =>
            string.IsNullOrEmpty(id) ? null : Data.items.FirstOrDefault(i => i.id == id);

        public static string DefaultFor(string slot)
        {
            var d = Data.defaults;
            switch (slot)
            {
                case "body": return d.body;
                case "hair": return d.hair;
                case "top": return d.top;
                case "bottom": return d.bottom;
                case "shoes": return d.shoes;
                case "hat": return d.hat;
                default: return d.accessory;
            }
        }

        /// <summary>Картинка вещи (кэш); нет файла — null, слой просто не рисуется.</summary>
        public static Texture2D Texture(CharacterItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.file)) return null;
            if (Textures.TryGetValue(item.file, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Root + item.file);
            Textures[item.file] = texture;
            return texture;
        }
    }

    /// <summary>Что надето: слот → id вещи. Хранится строкой «body=body_light;hat=hat_cap;…».</summary>
    public sealed class Outfit
    {
        readonly Dictionary<string, string> _items = new Dictionary<string, string>();

        public string Get(string slot) => _items.TryGetValue(slot, out var id) ? id : "";

        public Outfit With(string slot, string id)
        {
            var copy = Parse(Serialize());
            copy._items[slot] = id ?? "";
            return copy;
        }

        public string Serialize() =>
            string.Join(";", CharacterCatalog.Slots.Where(s => !string.IsNullOrEmpty(Get(s))).Select(s => s + "=" + Get(s)));

        /// <summary>Разобрать строку; неизвестные вещи и пустые обязательные слоты заменяются вещами по умолчанию.</summary>
        public static Outfit Parse(string text)
        {
            var outfit = new Outfit();
            foreach (var part in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string slot = part.Substring(0, eq).Trim(), id = part.Substring(eq + 1).Trim();
                var item = CharacterCatalog.Item(id);
                if (item != null && item.slot == slot) outfit._items[slot] = id;
            }
            foreach (var slot in CharacterCatalog.Slots)
                if (!outfit._items.ContainsKey(slot) && CharacterCatalog.IsRequired(slot))
                    outfit._items[slot] = CharacterCatalog.DefaultFor(slot) ?? "";
            return outfit;
        }

        public static Outfit Default => Parse("");

        /// <summary>Наряд бота: случайный, но постоянный для имени — чтобы боты за столом различались.</summary>
        public static Outfit ForBot(string name)
        {
            var random = new System.Random((name ?? "").Aggregate(17, (h, c) => h * 31 + c));
            var outfit = new Outfit();
            foreach (var slot in CharacterCatalog.Slots)
            {
                var items = CharacterCatalog.ItemsIn(slot).ToList();
                bool skip = !CharacterCatalog.IsRequired(slot) && random.NextDouble() < 0.4;
                outfit._items[slot] = items.Count == 0 || skip ? "" : items[random.Next(items.Count)].id;
            }
            return Parse(outfit.Serialize());
        }
    }
}
