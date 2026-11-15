using System.Collections.Generic;

namespace CityHighways.Core
{
    public enum ScenarioKind
    {
        /// <summary>Обычная Москва — правила без изменений.</summary>
        Standard,
        /// <summary>Зима: щебёнка всю партию дороже, снегопады чаще и бьют по бетону, асфальта меньше, техники больше.</summary>
        Winter,
        /// <summary>Большая Москва: окраины открыты с начала, участков и стартовых контрактов больше.</summary>
        GreaterMoscow,
        /// <summary>Год ремонта: каждый круг закрыты несколько свободных участков (следующие видны заранее), длинные дороги дают влияние.</summary>
        RepairYear,
        /// <summary>Выборы мэра: два аукциона, влияния больше, взятки дешевле, «друг мэра» в конце.</summary>
        Elections
    }

    /// <summary>
    /// Сценарии: меняют правила на всю партию. Сценарий — это только числа и флаги в GameConfig
    /// (Apply выставляет их на копии), так что он сохраняется вместе с настройками партии и одинаков у всех в сети.
    /// </summary>
    public static class Scenarios
    {
        public static readonly IReadOnlyList<ScenarioKind> All = new[]
        {
            ScenarioKind.Standard, ScenarioKind.Winter, ScenarioKind.GreaterMoscow, ScenarioKind.RepairYear, ScenarioKind.Elections
        };

        public static ScenarioKind Of(GameConfig config) =>
            config != null && config.Scenario >= 0 && config.Scenario < All.Count ? (ScenarioKind)config.Scenario : ScenarioKind.Standard;

        public static string Title(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Winter: return "Зима";
                case ScenarioKind.GreaterMoscow: return "Большая Москва";
                case ScenarioKind.RepairYear: return "Год ремонта";
                case ScenarioKind.Elections: return "Выборы мэра";
                default: return "Обычная Москва";
            }
        }

        /// <summary>Одна строка для меню.</summary>
        public static string Summary(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Winter: return "Щебёнка дороже всю партию, снегопады чаще, асфальта мало.";
                case ScenarioKind.GreaterMoscow: return "Окраины открыты сразу, участков и контрактов больше — длинная партия.";
                case ScenarioKind.RepairYear: return "Каждый круг часть дорог на ремонте; следующие видны заранее.";
                case ScenarioKind.Elections: return "Два аукциона, больше влияния, дешёвые взятки, «друг мэра».";
                default: return "Правила без изменений.";
            }
        }

        /// <summary>Что именно меняется — для меню и страницы правил.</summary>
        public static IReadOnlyList<string> Changes(ScenarioKind kind, GameConfig config = null)
        {
            var c = config ?? Apply(new GameConfig(), kind);
            switch (kind)
            {
                case ScenarioKind.Winter:
                    return new[]
                    {
                        $"Щебёночные дороги всю партию на {c.SnowExtraCost} карту дороже (разрешение мэрии снимает надбавку на ход).",
                        $"Снегопад чаще; в снегопад на {c.SnowExtraCost} карту дороже и бетон.",
                        "Нет парадов и субсидий на мосты.",
                        $"В колоде асфальта {c.CardsPerResource[0]} вместо 26, щебня {c.CardsPerResource[3]} вместо 18, техники {c.MachineryCards} вместо 14."
                    };
                case ScenarioKind.GreaterMoscow:
                    return new[]
                    {
                        "Зеленоград, Некрасовка и Троицк открыты с первого хода, событий «Новый район» нет.",
                        $"+{c.ExtraRoadPieces} участков дороги каждому.",
                        $"В начале — {c.InitialContractsDealt} контракта на выбор (оставить не меньше {c.InitialContractsMinKeep})."
                    };
                case ScenarioKind.RepairYear:
                    return new[]
                    {
                        $"Каждый круг {c.ScheduledRepairs} случайных свободных участка закрыты на ремонт — сверх событий.",
                        "Какие участки закроют в следующем круге, видно на карте заранее.",
                        "Разрешение мэрии пускает строить на ремонтируемом участке.",
                        $"+{c.InfluencePerLongRoad} влияния за каждую свою дорогу длиной {c.LongRoadLength} и больше."
                    };
                case ScenarioKind.Elections:
                    return new[]
                    {
                        $"Два аукциона мэрии: когда кто-то построил треть и {(int)(c.SecondAuctionShare * 100)}% своих участков.",
                        $"Влияние за контракт +{c.InfluencePerContract} (сложный +{c.InfluencePerComplexContract}), за новый район +{c.InfluencePerNewDistrict}.",
                        $"Взятки стоят {c.BribeDiscountCost} влияния.",
                        $"«Друг мэра»: кто за партию потратил больше всех влияния, получает +{c.MayorFriendBonus} очков."
                    };
                default:
                    return new[] { "Правила без изменений." };
            }
        }

        /// <summary>Копия настроек с правилами сценария.</summary>
        public static GameConfig Apply(GameConfig baseConfig, ScenarioKind kind)
        {
            var c = (baseConfig ?? new GameConfig()).Clone();
            c.Scenario = (int)kind;
            switch (kind)
            {
                case ScenarioKind.Winter:
                    c.PermanentSnow = true;
                    c.SnowHitsConcrete = true;
                    c.SnowEvents = 4;
                    c.ParadeEvents = 0;
                    c.SubsidyEvents = 0;
                    // Всего карт столько же: асфальта −6, щебня +2, техники +4.
                    c.CardsPerResource = new[] { 20, 24, 10, 20 };
                    c.MachineryCards = 18;
                    break;
                case ScenarioKind.GreaterMoscow:
                    c.ExpansionsOpen = true;
                    c.ExtraRoadPieces = 8;
                    c.InitialContractsDealt = 4;
                    break;
                case ScenarioKind.RepairYear:
                    c.ScheduledRepairs = 3;
                    c.InfluencePerLongRoad = 1;
                    break;
                case ScenarioKind.Elections:
                    c.AuctionCount = 2;
                    c.InfluencePerContract += 1;
                    c.InfluencePerComplexContract += 1;
                    c.InfluencePerNewDistrict += 1;
                    c.BribeDiscountCost = 1;
                    c.BribeExtraCardCost = 1;
                    c.MayorFriendBonus = 6;
                    break;
            }
            return c;
        }
    }
}
