namespace CityHighways.Core
{
    /// <summary>
    /// Карта удачи. Берётся вместо одной из двух карт ресурсов, не больше одной за ход.
    /// Первые три — бафы, остальные — дебафы.
    /// </summary>
    public enum LuckCardKind
    {
        /// <summary>Грант мэрии: +LuckGrant очков.</summary>
        Grant,
        /// <summary>Поставка: LuckSupply карт из колоды ресурсов.</summary>
        Supply,
        /// <summary>Премия: +LuckPremiumPerContract за каждый уже выполненный контракт (не больше LuckPremiumMax).</summary>
        Premium,
        /// <summary>Проверка: −LuckFine очков.</summary>
        Fine,
        /// <summary>Кража со склада: LuckTheft случайных карт с руки уходят в сброс.</summary>
        Theft,
        /// <summary>Отзыв контракта: самый дешёвый невыполненный контракт уходит под колоду; если таких нет — штраф как у проверки.</summary>
        Recall
    }

    public static class LuckInfo
    {
        public const int KindCount = 6;

        public static bool IsBuff(LuckCardKind kind) => kind <= LuckCardKind.Premium;

        public static string Title(LuckCardKind kind)
        {
            switch (kind)
            {
                case LuckCardKind.Grant: return "Грант мэрии";
                case LuckCardKind.Supply: return "Внеплановая поставка";
                case LuckCardKind.Premium: return "Премия за сдачу";
                case LuckCardKind.Fine: return "Проверка инспекции";
                case LuckCardKind.Theft: return "Кража со склада";
                default: return "Отзыв контракта";
            }
        }

        public static string Describe(LuckCardKind kind, GameConfig config)
        {
            switch (kind)
            {
                case LuckCardKind.Grant: return $"+{config.LuckGrant} очков.";
                case LuckCardKind.Supply: return $"+{config.LuckSupply} карты ресурсов из колоды.";
                case LuckCardKind.Premium:
                    return $"+{config.LuckPremiumPerContract} очка за каждый выполненный контракт (не больше {config.LuckPremiumMax}).";
                case LuckCardKind.Fine: return $"−{config.LuckFine} очка.";
                case LuckCardKind.Theft: return $"Случайная карта с руки уходит в сброс.";
                default: return $"Самый дешёвый невыполненный контракт уходит под колоду (без штрафа). Нет таких — −{config.LuckFine} очка.";
            }
        }

        public static int CountIn(GameConfig config, LuckCardKind kind)
        {
            switch (kind)
            {
                case LuckCardKind.Grant: return config.LuckGrantCards;
                case LuckCardKind.Supply: return config.LuckSupplyCards;
                case LuckCardKind.Premium: return config.LuckPremiumCards;
                case LuckCardKind.Fine: return config.LuckFineCards;
                case LuckCardKind.Theft: return config.LuckTheftCards;
                default: return config.LuckRecallCards;
            }
        }
    }
}
