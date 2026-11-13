using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>Персонаж компании: у каждого своя способность. None — играть без персонажа.</summary>
    public enum CharacterKind
    {
        None,
        /// <summary>Везунчик: открывает 2 карты удачи и оставляет одну.</summary>
        Lucky,
        /// <summary>Прораб: ForemanFreeRoads раз за партию строит дорогу длиной до ForemanMaxLength без карт.</summary>
        Foreman,
        /// <summary>Страховщик: любой дебаф удачи заменяется штрафом −InsurerFine.</summary>
        Insurer,
        /// <summary>Сборщик пошлин: проезд по его дорогам стоит TollmanToll вместо TollPerRoad.</summary>
        Tollman,
        /// <summary>Чиновник: +OfficialBonusPerContract за каждый выполненный контракт.</summary>
        Official,
        /// <summary>Мостовик: мосты на BridgerDiscount карт дешевле и +BridgerPointsPerBridge за каждый свой мост.</summary>
        Bridger
    }

    public static class Characters
    {
        /// <summary>Все персонажи (без None) — для меню выбора и случайной раздачи.</summary>
        public static readonly IReadOnlyList<CharacterKind> All = new[]
        {
            CharacterKind.Lucky, CharacterKind.Foreman, CharacterKind.Insurer,
            CharacterKind.Tollman, CharacterKind.Official, CharacterKind.Bridger
        };

        public static string Title(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Lucky: return "Везунчик";
                case CharacterKind.Foreman: return "Прораб";
                case CharacterKind.Insurer: return "Страховщик";
                case CharacterKind.Tollman: return "Сборщик пошлин";
                case CharacterKind.Official: return "Чиновник";
                case CharacterKind.Bridger: return "Мостовик";
                default: return "Без персонажа";
            }
        }

        /// <summary>Способность одной строкой.</summary>
        public static string Ability(CharacterKind kind, GameConfig config)
        {
            switch (kind)
            {
                case CharacterKind.Lucky:
                    return "Берёт удачу — открывает 2 карты и оставляет лучшую.";
                case CharacterKind.Foreman:
                    return config.ForemanFreeRoads == 1
                        ? $"Один раз за партию строит дорогу длиной до {config.ForemanMaxLength} без карт."
                        : $"{config.ForemanFreeRoads} раза за партию строит дорогу длиной до {config.ForemanMaxLength} без карт.";
                case CharacterKind.Insurer:
                    return $"Дебафы удачи ему не страшны: вместо любого — только −{config.InsurerFine} очко.";
                case CharacterKind.Tollman:
                    return $"Проезд по его дорогам стоит {config.TollmanToll} очка вместо {config.TollPerRoad}.";
                case CharacterKind.Official:
                    return $"Связи в мэрии: +{config.OfficialBonusPerContract} очка за каждый выполненный контракт.";
                case CharacterKind.Bridger:
                    return $"Мосты на {config.BridgerDiscount} карту дешевле и +{config.BridgerPointsPerBridge} очко за каждый свой мост.";
                default:
                    return "Обычная компания без особых способностей.";
            }
        }

        /// <summary>Короткая подсказка, как играть персонажем.</summary>
        public static string Hint(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Lucky: return "Берите удачу смелее: худшую карту вы просто не возьмёте.";
                case CharacterKind.Foreman: return "Берегите бесплатную стройку для дороги, на которую не хватает карт.";
                case CharacterKind.Insurer: return "Удача для вас почти без риска — тяните её, когда нет нужных открытых карт.";
                case CharacterKind.Tollman: return "Стройте в узких местах: соперникам придётся ехать по вашим дорогам.";
                case CharacterKind.Official: return "Каждый выполненный контракт дороже — берите их больше.";
                case CharacterKind.Bridger: return "Река — ваша: мосты дешевле и приносят больше.";
                default: return "";
            }
        }
    }
}
