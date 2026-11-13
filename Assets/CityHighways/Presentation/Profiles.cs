using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityHighways.Core;
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

        public bool Owns(string id) => owned.Contains(id);

        public string Equipped(ShopKind kind) => kind == ShopKind.Emblem ? emblem : kind == ShopKind.Title ? title : frame;

        public void Equip(ShopKind kind, string id)
        {
            if (kind == ShopKind.Emblem) emblem = id;
            else if (kind == ShopKind.Title) title = id;
            else frame = id;
        }

        public PlayerLook Look => new PlayerLook(emblem, title, frame);
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

        public PlayerLook(string emblem, string title, string frame)
        {
            Emblem = emblem ?? "";
            Title = title ?? "";
            Frame = frame ?? "";
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

        public static void Remove(PlayerProfile profile)
        {
            Book.profiles.Remove(profile);
            if (Key(Book.active) == Key(profile.name)) Book.active = Book.profiles.FirstOrDefault()?.name ?? "";
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
