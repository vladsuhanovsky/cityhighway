using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Вещь, которая может выпасть из контейнера: одежда персонажа или предмет оформления.</summary>
    public sealed class LootItem
    {
        public string Id;
        public string Name;
        /// <summary>Ценность в влиянии: по ней редкость и возврат за дубликат.</summary>
        public int Value;
        public CharacterItem Clothing;
        public ShopItem Style;

        /// <summary>Редкость: у одежды — её собственная (может быть задана в catalog.json), у оформления — по цене.</summary>
        public Rarity.Level Level => Clothing != null ? Clothing.Level : Rarity.Of(Value);
        public bool Exclusive => Style != null && Style.Exclusive;

        /// <summary>Что это за вещь, коротко: «Головной убор», «Титул»…</summary>
        public string KindTitle => Clothing != null ? CharacterCatalog.SlotTitle(Clothing.slot)
            : Style.Kind == ShopKind.Emblem ? "Эмблема" : Style.Kind == ShopKind.Title ? "Титул" : "Рамка";
    }

    /// <summary>Вид контейнера: цена и шансы редкостей (обычный, редкий, эпический, легендарный — в сумме 100).</summary>
    public sealed class ContainerKind
    {
        public string Id;
        public string Name;
        public string Description;
        public string DisplayName => LocalizationService.GetOrFallback("container." + Id + ".name", Name);
        public string DisplayDescription => LocalizationService.GetOrFallback("container." + Id + ".description", Description);
        public int Price;
        public int[] Odds;
        public Color Color;
    }

    /// <summary>Итог открытия: что выпало; дубликат — вещь уже была, вместо неё вернули часть ценности.</summary>
    public sealed class ContainerResult
    {
        public ContainerKind Container;
        public LootItem Item;
        public bool Duplicate;
        public int Refund;
    }

    /// <summary>
    /// Контейнеры профиля: открываются за влияние из кошелька, внутри — одежда персонажа (своего пола) и оформление,
    /// в том числе вещи, которых нет в продаже. Сначала по шансам контейнера выбирается редкость, затем случайная вещь
    /// этой редкости, которой у игрока ещё нет. Если всё такой редкости уже собрано — выпадает дубликат, и половина
    /// его ценности возвращается влиянием. Шансы показываются у каждого контейнера.
    /// </summary>
    public static class Containers
    {
        public static readonly ContainerKind[] Kinds =
        {
            new ContainerKind
            {
                Id = "box_foreman", Name = "Ящик прораба", Price = 15, Odds = new[] { 60, 30, 9, 1 },
                Description = "Чаще всего — обычные вещи, иногда редкие.", Color = new Color(0.72f, 0.52f, 0.30f)
            },
            new ContainerKind
            {
                Id = "box_mayor", Name = "Сейф мэрии", Price = 30, Odds = new[] { 25, 45, 24, 6 },
                Description = "В основном редкие и эпические вещи.", Color = new Color(0.36f, 0.50f, 0.66f)
            },
            new ContainerKind
            {
                Id = "box_gold", Name = "Золотой контейнер", Price = 60, Odds = new[] { 0, 30, 50, 20 },
                Description = "Без обычных вещей: эпические и легендарные — чаще всего.", Color = new Color(0.95f, 0.75f, 0.25f)
            }
        };

        public static readonly string[] RarityNames = { "Обычные", "Редкие", "Эпические", "Легендарные" };

        static readonly System.Random Random = new System.Random();

        /// <summary>Всё, что может выпасть этому игроку: платная одежда его пола и всё оформление.</summary>
        public static List<LootItem> Pool(PlayerProfile profile)
        {
            string gender = profile.Outfit.Gender;
            var list = new List<LootItem>();
            foreach (var slot in CharacterCatalog.Slots)
                foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                    if (item.price > 0) list.Add(new LootItem { Id = item.id, Name = item.name, Value = item.price, Clothing = item });
            foreach (var item in Profiles.Catalog.Where(i => !i.Mastery))
                list.Add(new LootItem { Id = item.Id, Name = item.Name, Value = item.Price, Style = item });
            return list;
        }

        public static bool Owns(PlayerProfile profile, LootItem item) =>
            item.Clothing != null ? profile.HasClothing(item.Clothing) : profile.Owns(item.Id);

        /// <summary>Случайная вещь по шансам контейнера (для ленты рулетки — без учёта того, что уже есть).</summary>
        public static LootItem RollAny(List<LootItem> pool, ContainerKind kind, System.Random random)
        {
            var tier = RollTier(pool, kind, random);
            var items = pool.Where(i => (int)i.Level == tier).ToList();
            return items.Count > 0 ? items[random.Next(items.Count)] : pool[random.Next(pool.Count)];
        }

        static int RollTier(List<LootItem> pool, ContainerKind kind, System.Random random)
        {
            int roll = random.Next(100), tier = 0, sum = 0;
            for (int i = 0; i < kind.Odds.Length; i++)
            {
                sum += kind.Odds[i];
                if (roll < sum) { tier = i; break; }
            }
            // Если такой редкости в пуле нет — ближайшая ниже, потом выше.
            for (int step = 0; step < 4; step++)
            {
                foreach (int t in new[] { tier - step, tier + step })
                    if (t >= 0 && t < 4 && pool.Any(i => (int)i.Level == t)) return t;
            }
            return tier;
        }

        /// <summary>Открыть контейнер: списать цену, выдать вещь (или вернуть часть за дубликат), сохранить. Ошибка — текст.</summary>
        public static string Open(PlayerProfile profile, ContainerKind kind, out ContainerResult result)
        {
            result = null;
            if (profile.wallet < kind.Price) return $"Нужно {kind.Price} влияния, в кошельке {profile.wallet}.";
            var pool = Pool(profile);
            if (pool.Count == 0) return "В контейнерах пока пусто.";
            profile.wallet -= kind.Price;
            Profiles.Record(profile, -kind.Price, "container", kind.Name);
            profile.containersOpened++;

            int tier = RollTier(pool, kind, Random);
            var sameTier = pool.Where(i => (int)i.Level == tier).ToList();
            var fresh = sameTier.Where(i => !Owns(profile, i)).ToList();
            result = new ContainerResult { Container = kind };
            if (fresh.Count > 0)
            {
                result.Item = fresh[Random.Next(fresh.Count)];
                profile.owned.Add(result.Item.Id);
            }
            else
            {
                result.Item = sameTier[Random.Next(sameTier.Count)];
                result.Duplicate = true;
                result.Refund = Mathf.Max(2, result.Item.Value / 2);
                profile.wallet += result.Refund;
                Profiles.Record(profile, result.Refund, "container", $"Дубликат: {result.Item.Name}");
            }
            Profiles.Save();
            return null;
        }

        /// <summary>Надеть выпавшую вещь.</summary>
        public static void Equip(PlayerProfile profile, LootItem item)
        {
            if (item.Clothing != null) Profiles.Wear(profile, item.Clothing.slot, item.Id);
            else
            {
                profile.Equip(item.Style.Kind, item.Id);
                Profiles.Save();
            }
        }

        public static bool IsEquipped(PlayerProfile profile, LootItem item) =>
            item.Clothing != null ? profile.Outfit.Get(item.Clothing.slot) == item.Id : profile.Equipped(item.Style.Kind) == item.Id;
    }
}
