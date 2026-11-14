using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Профиль игрока на этом компьютере: кошелёк влияния, статистика и купленное оформление.</summary>
    [Serializable]
    public class PlayerProfile
    {
        public string name = "Игрок";
        /// <summary>Влияние в кошельке: остаток с конца партий минус покупки.</summary>
        public int wallet;
        public int earnedTotal;
        public int games;
        public int wins;
        public List<string> owned = new List<string>();
        /// <summary>Надетые предметы по видам; пусто — ничего.</summary>
        public string emblem = "";
        public string title = "";
        public string frame = "";
        /// <summary>Своя картинка аватара (файл в папке avatars); пусто — нет.</summary>
        public string avatar = "";
        /// <summary>Профиль привязан к аккаунту Steam: ник и аватар берутся оттуда.</summary>
        public string steamId = "";

        public bool IsSteam => !string.IsNullOrEmpty(steamId);
        /// <summary>Наряд персонажа строкой Outfit («body=…;hat=…»); пусто — наряд по умолчанию.</summary>
        public string outfit = "";

        public Outfit Outfit => Outfit.Parse(outfit);

        /// <summary>Вещь персонажа есть: бесплатная (цена 0) или куплена.</summary>
        public bool HasClothing(CharacterItem item) => item != null && (item.price <= 0 || owned.Contains(item.id));

        public bool Owns(string id) => owned.Contains(id);

        public string Equipped(ShopKind kind) => kind == ShopKind.Emblem ? emblem : kind == ShopKind.Title ? title : frame;

        public void Equip(ShopKind kind, string id)
        {
            if (kind == ShopKind.Emblem) emblem = id;
            else if (kind == ShopKind.Title) title = id;
            else frame = id;
        }

        public PlayerLook Look => new PlayerLook(emblem, title, frame, Outfit.Serialize());
    }

    [Serializable]
    public class ProfileBook
    {
        public List<PlayerProfile> profiles = new List<PlayerProfile>();
        public string active = "";
    }

    public enum ShopKind { Emblem, Title, Frame }

    /// <summary>Предмет оформления: эмблема у имени, титул под именем или рамка строки игрока.</summary>
    public sealed class ShopItem
    {
        public string Id { get; }
        public ShopKind Kind { get; }
        public string Name { get; }
        public int Price { get; }

        public ShopItem(string id, ShopKind kind, string name, int price)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Price = price;
        }
    }

    /// <summary>Как выглядит игрок за столом (по его профилю). Пустые поля — без украшений.</summary>
    public readonly struct PlayerLook
    {
        public readonly string Emblem;
        public readonly string Title;
        public readonly string Frame;
        /// <summary>Наряд персонажа (строка Outfit); пусто — по умолчанию.</summary>
        public readonly string Outfit;

        public PlayerLook(string emblem, string title, string frame, string outfit = "")
        {
            Emblem = emblem ?? "";
            Title = title ?? "";
            Frame = frame ?? "";
            Outfit = outfit ?? "";
        }

        public static readonly PlayerLook None = new PlayerLook("", "", "");
    }

    /// <summary>Профили и магазин оформления. Хранятся в JSON рядом со статистикой партий.</summary>
    public static class Profiles
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "cityhighways_profiles.json");

        public static readonly ShopItem[] Catalog =
        {
            new ShopItem("emblem_star", ShopKind.Emblem, "Звезда", 15),
            new ShopItem("emblem_diamond", ShopKind.Emblem, "Ромб", 15),
            new ShopItem("emblem_gear", ShopKind.Emblem, "Шестерня", 20),
            new ShopItem("emblem_flag", ShopKind.Emblem, "Флажок", 20),
            new ShopItem("emblem_bolt", ShopKind.Emblem, "Молния", 25),
            new ShopItem("emblem_crown", ShopKind.Emblem, "Корона", 40),

            new ShopItem("title_foreman", ShopKind.Title, "Прораб года", 10),
            new ShopItem("title_friend", ShopKind.Title, "Друг мэрии", 15),
            new ShopItem("title_bridges", ShopKind.Title, "Мостостроитель", 15),
            new ShopItem("title_asphalt", ShopKind.Title, "Король асфальта", 20),
            new ShopItem("title_honest", ShopKind.Title, "Неподкупный", 25),
            new ShopItem("title_cardinal", ShopKind.Title, "Серый кардинал", 30),

            new ShopItem("frame_steel", ShopKind.Frame, "Стальная рамка", 20),
            new ShopItem("frame_neon", ShopKind.Frame, "Неоновая рамка", 30),
            new ShopItem("frame_gold", ShopKind.Frame, "Золотая рамка", 35)
        };

        public static ShopItem Item(string id) => Catalog.FirstOrDefault(i => i.Id == id);

        public static string TitleText(string id) => Item(id)?.Name ?? "";

        public static Color FrameColor(string id)
        {
            switch (id)
            {
                case "frame_gold": return new Color(0.98f, 0.78f, 0.25f);
                case "frame_neon": return new Color(0.30f, 0.95f, 0.90f);
                case "frame_steel": return new Color(0.70f, 0.76f, 0.84f);
                default: return Color.clear;
            }
        }

        static ProfileBook _book;

        public static ProfileBook Book
        {
            get
            {
                if (_book == null) _book = Load();
                return _book;
            }
        }

        static ProfileBook Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var book = JsonUtility.FromJson<ProfileBook>(File.ReadAllText(FilePath));
                    if (book != null)
                    {
                        book.profiles = book.profiles ?? new List<PlayerProfile>();
                        foreach (var p in book.profiles) p.owned = p.owned ?? new List<string>();
                        return book;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("City Highways: профили не прочитались, начинаем с чистого листа. " + e.Message);
            }
            return new ProfileBook();
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Book, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("City Highways: профили не сохранились. " + e.Message);
            }
        }

        static string Key(string name) => (name ?? "").Trim().ToLowerInvariant();

        public static PlayerProfile Find(string name) =>
            string.IsNullOrWhiteSpace(name) ? null : Book.profiles.FirstOrDefault(p => Key(p.name) == Key(name));

        public static PlayerProfile GetOrCreate(string name)
        {
            var profile = Find(name);
            if (profile != null) return profile;
            profile = new PlayerProfile { name = string.IsNullOrWhiteSpace(name) ? "Игрок" : name.Trim() };
            Book.profiles.Add(profile);
            if (string.IsNullOrEmpty(Book.active)) Book.active = profile.name;
            return profile;
        }

        /// <summary>Активный профиль (для сетевой игры и как имя первого игрока); создаётся при первом обращении.</summary>
        public static PlayerProfile Active
        {
            get
            {
                var profile = Find(Book.active) ?? Book.profiles.FirstOrDefault();
                if (profile == null)
                {
                    profile = GetOrCreate("Игрок");
                    Save();
                }
                Book.active = profile.name;
                return profile;
            }
        }

        public static void SetActive(PlayerProfile profile)
        {
            Book.active = profile.name;
            Save();
        }

        /// <summary>Переименовать профиль (не Steam). Возвращает текст ошибки или null.</summary>
        public static string Rename(PlayerProfile profile, string newName)
        {
            if (profile.IsSteam) return "Имя профиля Steam берётся из Steam.";
            newName = (newName ?? "").Trim();
            if (newName.Length == 0) return "Имя не может быть пустым.";
            if (newName.Length > 16) newName = newName.Substring(0, 16);
            var other = Find(newName);
            if (other != null && other != profile) return "Профиль с таким именем уже есть.";
            bool active = Key(Book.active) == Key(profile.name);
            profile.name = newName;
            if (active) Book.active = newName;
            Save();
            return null;
        }

        public static void Remove(PlayerProfile profile)
        {
            Book.profiles.Remove(profile);
            if (Key(Book.active) == Key(profile.name)) Book.active = Book.profiles.FirstOrDefault()?.name ?? "";
            Save();
        }

        // ================================================================== Аватар

        public static string AvatarFolder => Path.Combine(Application.persistentDataPath, "avatars");
        const int AvatarSize = 256;
        static readonly Dictionary<string, Texture2D> AvatarCache = new Dictionary<string, Texture2D>();

        /// <summary>Картинка аватара: своя загруженная, иначе из Steam (для профиля Steam), иначе null.</summary>
        public static Texture2D AvatarOf(PlayerProfile profile)
        {
            if (profile == null) return null;
            if (!string.IsNullOrEmpty(profile.avatar))
            {
                if (AvatarCache.TryGetValue(profile.avatar, out var cached) && cached != null) return cached;
                string path = Path.Combine(AvatarFolder, profile.avatar);
                if (File.Exists(path))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(File.ReadAllBytes(path)))
                    {
                        texture.wrapMode = TextureWrapMode.Clamp;
                        AvatarCache[profile.avatar] = texture;
                        return texture;
                    }
                }
            }
            var steam = NetPlatforms.Steam;
            if (profile.IsSteam && steam != null && steam.Available) return steam.Avatar;
            return null;
        }

        /// <summary>
        /// Загрузить картинку (PNG/JPG) на аватар: обрезается по центру до квадрата, уменьшается до 256×256
        /// и сохраняется в папку профилей. Возвращает текст ошибки или null.
        /// </summary>
        public static string ImportAvatar(PlayerProfile profile, string sourcePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return "Файл не найден.";
                if (new FileInfo(sourcePath).Length > 20 * 1024 * 1024) return "Файл больше 20 МБ.";
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!source.LoadImage(File.ReadAllBytes(sourcePath))) return "Это не PNG и не JPG.";

                int side = Mathf.Min(source.width, source.height);
                int x0 = (source.width - side) / 2, y0 = (source.height - side) / 2;
                int size = Mathf.Min(AvatarSize, side);
                var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x0 + (x + 0.5f) * side / size) / source.width;
                    float v = (y0 + (y + 0.5f) * side / size) / source.height;
                    pixels[y * size + x] = source.GetPixelBilinear(u, v);
                }
                result.SetPixels(pixels);
                result.Apply();
                UnityEngine.Object.Destroy(source);

                Directory.CreateDirectory(AvatarFolder);
                string file = $"avatar_{Guid.NewGuid():N}.png";
                File.WriteAllBytes(Path.Combine(AvatarFolder, file), result.EncodeToPNG());
                RemoveAvatarFile(profile);
                profile.avatar = file;
                result.wrapMode = TextureWrapMode.Clamp;
                AvatarCache[file] = result;
                Save();
                return null;
            }
            catch (Exception e)
            {
                return "Не удалось загрузить картинку: " + e.Message;
            }
        }

        public static void RemoveAvatar(PlayerProfile profile)
        {
            RemoveAvatarFile(profile);
            profile.avatar = "";
            Save();
        }

        static void RemoveAvatarFile(PlayerProfile profile)
        {
            if (string.IsNullOrEmpty(profile.avatar)) return;
            AvatarCache.Remove(profile.avatar);
            try
            {
                string path = Path.Combine(AvatarFolder, profile.avatar);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // старый файл останется — не страшно
            }
        }

        // ================================================================== Steam

        /// <summary>
        /// Если Steam запущен — активным становится профиль этого аккаунта Steam (создаётся при первом запуске),
        /// и его имя всегда совпадает с ником в Steam.
        /// </summary>
        public static void SyncSteam()
        {
            var steam = NetPlatforms.Steam;
            if (steam == null || !steam.Available || string.IsNullOrEmpty(steam.SteamId)) return;
            var profile = Book.profiles.FirstOrDefault(p => p.steamId == steam.SteamId);
            string persona = string.IsNullOrWhiteSpace(steam.PersonaName) ? "Игрок Steam" : steam.PersonaName.Trim();
            if (profile == null)
            {
                // Профиль с таким именем уже был (до Steam) — привязываем его, иначе создаём новый.
                profile = Find(persona) ?? new PlayerProfile { name = persona };
                if (!Book.profiles.Contains(profile)) Book.profiles.Add(profile);
                profile.steamId = steam.SteamId;
            }
            profile.name = persona;
            Book.active = profile.name;
            Save();
        }

        /// <summary>Как выглядит игрок с таким именем (за одним компьютером); без профиля — без украшений.</summary>
        public static PlayerLook LookFor(string name) => Find(name)?.Look ?? PlayerLook.None;

        public static string Buy(PlayerProfile profile, ShopItem item)
        {
            if (profile.Owns(item.Id)) return "Уже куплено.";
            if (profile.wallet < item.Price) return $"Нужно {item.Price} влияния, в кошельке {profile.wallet}.";
            profile.wallet -= item.Price;
            profile.owned.Add(item.Id);
            profile.Equip(item.Kind, item.Id);
            Save();
            return null;
        }

        // ================================================================== Одежда персонажа

        /// <summary>Купить вещь персонажа и сразу надеть. Возвращает текст ошибки или null.</summary>
        public static string BuyClothing(PlayerProfile profile, CharacterItem item)
        {
            if (item == null) return "Такой вещи нет.";
            if (profile.HasClothing(item)) return "Уже есть.";
            if (profile.wallet < item.price) return $"Нужно {item.price} влияния, в кошельке {profile.wallet}.";
            profile.wallet -= item.price;
            profile.owned.Add(item.id);
            Wear(profile, item.slot, item.id);
            return null;
        }

        /// <summary>Надеть вещь (или снять: id пустой — только для необязательных слотов).</summary>
        public static void Wear(PlayerProfile profile, string slot, string id)
        {
            if (string.IsNullOrEmpty(id) && CharacterCatalog.IsRequired(slot)) return;
            var item = CharacterCatalog.Item(id);
            if (!string.IsNullOrEmpty(id) && !profile.HasClothing(item)) return;
            profile.outfit = profile.Outfit.With(slot, id).Serialize();
            Save();
        }

        /// <summary>Итог зачисления после партии: кому сколько ушло в кошелёк.</summary>
        public struct Credit
        {
            public string Profile;
            public int Amount;
        }

        /// <summary>
        /// Конец партии: остаток влияния людей уходит в их профили (за одним компьютером — по имени игрока,
        /// в сетевой игре — только своё место, в активный профиль), плюс счётчики партий и побед.
        /// </summary>
        public static List<Credit> CreditAfterGame(Game game, GameSetup setup, int? onlineSeat)
        {
            var credits = new List<Credit>();
            if (game.Result == null) return credits;
            var winners = new HashSet<int>(game.Result.Winners.Select(w => w.PlayerIndex));
            for (int i = 0; i < game.Players.Count; i++)
            {
                PlayerProfile profile;
                if (onlineSeat.HasValue)
                {
                    if (i != onlineSeat.Value) continue;
                    profile = Active;
                }
                else
                {
                    if (i >= setup.players.Count || setup.players[i].isBot) continue;
                    profile = GetOrCreate(game.Players[i].Name);
                }
                var player = game.Players[i];
                profile.wallet += Math.Max(0, player.Influence);
                profile.earnedTotal += player.InfluenceEarned;
                profile.games++;
                if (winners.Contains(i)) profile.wins++;
                credits.Add(new Credit { Profile = profile.name, Amount = Math.Max(0, player.Influence) });
            }
            if (credits.Count > 0) Save();
            return credits;
        }
    }
}
