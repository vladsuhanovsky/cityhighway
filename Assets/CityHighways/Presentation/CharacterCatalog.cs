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
        /// <summary>Чья вещь: «m» — мужская, «f» — женская, пусто — для всех.</summary>
        public string gender;
        /// <summary>Только у внешности: тон кожи «light», «tan» или «dark» — по нему выбираются варианты слоёв других вещей.</summary>
        public string tone;
        /// <summary>Рамка непрозрачной части на холсте [x, y, ширина, высота] (сверху вниз) — Tools~/wardrobe_layers.py.</summary>
        public int[] bounds;
        /// <summary>Низ, который рисуется поверх верха (комбинезон: нагрудник поверх футболки).</summary>
        public bool overTop;
        /// <summary>Верхняя одежда: остаётся поверх даже «overTop»-низа.</summary>
        public bool outer;
        /// <summary>Описание для гардероба (подсказка при наведении).</summary>
        public string description;
        /// <summary>Редкость вручную: «common», «rare», «epic», «legendary». Пусто — по цене.</summary>
        public string rarity;
        /// <summary>
        /// Что не видно под этой вещью: слоты («hair», «accessory») или id вещей («acc_tie»). Скрытая вещь остаётся
        /// надетой — снимете эту, и она снова появится.
        /// </summary>
        public string[] hides;

        public bool Fits(string g) => string.IsNullOrEmpty(gender) || gender == g;

        /// <summary>Редкость: из поля rarity, а без него — по цене.</summary>
        public Rarity.Level Level
        {
            get
            {
                switch ((rarity ?? "").Trim().ToLowerInvariant())
                {
                    case "common": return Rarity.Level.Common;
                    case "rare": return Rarity.Level.Rare;
                    case "epic": return Rarity.Level.Epic;
                    case "legendary": return Rarity.Level.Legendary;
                    default: return Rarity.Of(price);
                }
            }
        }

        /// <summary>Скрывает ли эта вещь другую (по её слоту или id).</summary>
        public bool Hides(CharacterItem other)
        {
            if (other == null || other == this || hides == null) return false;
            foreach (var target in hides)
                if (target == other.slot || target == other.id) return true;
            return false;
        }
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
        public CharacterDefaults defaultsFemale;
        public CharacterItem[] items;
    }

    /// <summary>
    /// Каталог одежды персонажа: Resources/CityHighways/Character/catalog.json и PNG-слои 512×768 с прозрачным
    /// фоном. Новую вещь добавляют без кода: картинку в папку слота и строчку в catalog.json.
    /// Два пола: мужской («m») и женский («f») — у каждого своя фигура и свой гардероб; пол наряда задаёт внешность.
    /// У вещей, где видна кожа (причёска, футболка), рядом лежат варианты под тон кожи: «…_tan.png», «…_dark.png».
    /// Картинки нарезаны из Scenario скриптом Tools~/wardrobe_extract.py.
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

        public const string Male = "m", Female = "f";

        public static IEnumerable<CharacterItem> ItemsIn(string slot) => Data.items.Where(i => i.slot == slot);

        /// <summary>Вещи слота для пола (мужские, женские или общие).</summary>
        public static IEnumerable<CharacterItem> ItemsIn(string slot, string gender) =>
            Data.items.Where(i => i.slot == slot && i.Fits(gender));

        public static string GenderTitle(string gender) => gender == Female ? "Женщина" : "Мужчина";

        public static CharacterItem Item(string id) =>
            string.IsNullOrEmpty(id) ? null : Data.items.FirstOrDefault(i => i.id == id);

        public static string DefaultFor(string slot) => DefaultFor(slot, Male);

        public static string DefaultFor(string slot, string gender)
        {
            var d = gender == Female && Data.defaultsFemale != null ? Data.defaultsFemale : Data.defaults;
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
        public static Texture2D Texture(CharacterItem item) => Texture(item, null);

        /// <summary>Картинка вещи под тон кожи: сначала «файл_тон», иначе обычный файл.</summary>
        public static Texture2D Texture(CharacterItem item, string tone)
        {
            if (item == null || string.IsNullOrEmpty(item.file)) return null;
            if (!string.IsNullOrEmpty(tone) && tone != "light" && item.slot != "body")
            {
                var toned = Load(item.file + "_" + tone);
                if (toned != null) return toned;
            }
            return Load(item.file);
        }

        static Texture2D Load(string file)
        {
            if (Textures.TryGetValue(file, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Root + file);
            Textures[file] = texture;
            return texture;
        }
    }

    /// <summary>Что надето: слот → id вещи. Хранится строкой «body=body_light;hat=hat_cap;…».</summary>
    public sealed class Outfit
    {
        readonly Dictionary<string, string> _items = new Dictionary<string, string>();

        public string Get(string slot) => _items.TryGetValue(slot, out var id) ? id : "";

        /// <summary>Пол наряда — по внешности («m» или «f»).</summary>
        public string Gender
        {
            get
            {
                var body = CharacterCatalog.Item(Get("body"));
                return body != null && body.gender == CharacterCatalog.Female ? CharacterCatalog.Female : CharacterCatalog.Male;
            }
        }

        /// <summary>Тон кожи — по внешности: под него берутся варианты слоёв.</summary>
        public string Tone => CharacterCatalog.Item(Get("body"))?.tone ?? "light";

        /// <summary>
        /// Картинка слоя под тон кожи. Волосы при надетом головном уборе подрезаются: то, что торчало бы из-под
        /// него (пучок над кепкой, хвост над беретом), не рисуется — см. <see cref="CharacterLayers.HairUnderHat"/>.
        /// </summary>
        public Texture2D TextureFor(string slot)
        {
            if (HiddenBy(slot) != null) return null;
            var texture = CharacterCatalog.Texture(CharacterCatalog.Item(Get(slot)), Tone);
            if (slot != "hair" || texture == null) return texture;
            var hat = CharacterCatalog.Texture(CharacterCatalog.Item(Get("hat")), Tone);
            return hat == null ? texture : CharacterLayers.HairUnderHat(texture, hat);
        }

        /// <summary>Надетая вещь, под которой не видно вещь слота (её «hides»), или null.</summary>
        public CharacterItem HiddenBy(string slot)
        {
            var item = CharacterCatalog.Item(Get(slot));
            if (item == null) return null;
            foreach (var other in CharacterCatalog.Slots)
            {
                if (other == slot) continue;
                var cover = CharacterCatalog.Item(Get(other));
                if (cover != null && cover.Hides(item)) return cover;
            }
            return null;
        }

        /// <summary>Порядок слоёв снизу вверх для этого наряда (комбинезон поверх футболки и т. п.).</summary>
        public List<string> DrawOrder() => CharacterLayers.DrawOrder(this);

        /// <summary>
        /// Наряд с вещью в слоте. Старая вещь слота заменяется целиком (в слоте всегда одна вещь), после чего наряд
        /// проверяется заново, как при загрузке: вещь чужого пола или чужого слота не наденется, обязательный слот не
        /// останется пустым, а при смене внешности на другой пол весь гардероб сменится на вещи этого пола.
        /// </summary>
        public Outfit With(string slot, string id)
        {
            var copy = Parse(Serialize());
            copy._items[slot] = id ?? "";
            return Parse(copy.Serialize());
        }

        public string Serialize() =>
            string.Join(";", CharacterCatalog.Slots.Where(s => !string.IsNullOrEmpty(Get(s))).Select(s => s + "=" + Get(s)));

        /// <summary>
        /// Разобрать строку; неизвестные вещи, вещи другого пола и пустые обязательные слоты заменяются вещами
        /// по умолчанию для пола наряда (пол задаёт внешность; нет её — мужской).
        /// </summary>
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
            string gender = outfit.Gender;
            if (!outfit._items.ContainsKey("body")) outfit._items["body"] = CharacterCatalog.DefaultFor("body", gender) ?? "";
            foreach (var slot in CharacterCatalog.Slots)
            {
                var item = CharacterCatalog.Item(outfit.Get(slot));
                if (item != null && !item.Fits(gender)) outfit._items.Remove(slot);
                if (!outfit._items.ContainsKey(slot) && CharacterCatalog.IsRequired(slot))
                    outfit._items[slot] = CharacterCatalog.DefaultFor(slot, gender) ?? "";
            }
            return outfit;
        }

        public static Outfit Default => Parse("");

        /// <summary>Наряд по умолчанию для пола.</summary>
        public static Outfit DefaultFor(string gender) => Parse("body=" + (CharacterCatalog.DefaultFor("body", gender) ?? ""));

        /// <summary>Наряд бота: случайный, но постоянный для имени — чтобы боты за столом различались.</summary>
        public static Outfit ForBot(string name)
        {
            var random = new System.Random((name ?? "").Aggregate(17, (h, c) => h * 31 + c));
            var outfit = new Outfit();
            string gender = random.NextDouble() < 0.5 ? CharacterCatalog.Female : CharacterCatalog.Male;
            foreach (var slot in CharacterCatalog.Slots)
            {
                var items = CharacterCatalog.ItemsIn(slot, gender).ToList();
                bool skip = !CharacterCatalog.IsRequired(slot) && random.NextDouble() < 0.4;
                outfit._items[slot] = items.Count == 0 || skip ? "" : items[random.Next(items.Count)].id;
            }
            return Parse(outfit.Serialize());
        }
    }
}
