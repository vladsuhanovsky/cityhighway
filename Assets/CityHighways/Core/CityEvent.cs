using System.Collections.Generic;

namespace CityHighways.Core
{
    public enum CityEventKind
    {
        /// <summary>Дороги, построенные в этот круг, — загруженные магистрали: проезд по ним дороже.</summary>
        RushHour,
        /// <summary>Несколько свободных участков закрыты для строительства до конца круга.</summary>
        Repair,
        /// <summary>Мосты в этот круг стоят меньше карт.</summary>
        Subsidy,
        /// <summary>На окраине открывается новый район: места, дороги и контракты.</summary>
        NewDistrict,
        /// <summary>Парад: дороги, касающиеся района ParadeDistrict (Центр), закрыты для стройки на круг.</summary>
        Parade,
        /// <summary>Снегопад: щебёночные дороги в этот круг на SnowExtraCost карт дороже.</summary>
        Snow,
        /// <summary>Инвестор: каждая дорога, построенная в этот круг, приносит +InvestorBonus очков.</summary>
        Investor
    }

    /// <summary>Городское событие текущего круга.</summary>
    public sealed class CityEvent
    {
        public CityEventKind Kind { get; }
        /// <summary>Участки на ремонте (для Repair).</summary>
        public IReadOnlyList<string> ClosedRoads { get; }
        /// <summary>Открывшийся район (для NewDistrict).</summary>
        public string DistrictId { get; }

        public CityEvent(CityEventKind kind, IReadOnlyList<string> closedRoads = null, string districtId = null)
        {
            Kind = kind;
            ClosedRoads = closedRoads ?? new string[0];
            DistrictId = districtId;
        }

        public string Title
        {
            get
            {
                switch (Kind)
                {
                    case CityEventKind.RushHour: return "Час пик";
                    case CityEventKind.Repair: return "Ремонт";
                    case CityEventKind.Subsidy: return "Субсидия мэрии";
                    case CityEventKind.Parade: return "Парад";
                    case CityEventKind.Snow: return "Снегопад";
                    case CityEventKind.Investor: return "Инвестор";
                    default: return "Новый район";
                }
            }
        }

        /// <summary>Что событие значит в этом круге — для журнала и интерфейса.</summary>
        public string Describe(Game game)
        {
            var config = game.Config;
            switch (Kind)
            {
                case CityEventKind.RushHour:
                    return $"Дороги, построенные в этот круг, станут загруженными магистралями: проезд по ним — {config.TollPerRoad * config.RushHourTollMultiplier} очк. вместо {config.TollPerRoad}.";
                case CityEventKind.Repair:
                    var names = new List<string>();
                    foreach (var id in ClosedRoads)
                    {
                        var road = game.Map.GetRoad(id);
                        if (road != null) names.Add(game.Map.LocationName(road.A) + " — " + game.Map.LocationName(road.B));
                    }
                    return names.Count == 0
                        ? "Свободных участков для ремонта не нашлось."
                        : "До конца круга нельзя строить: " + string.Join("; ", names) + ".";
                case CityEventKind.Subsidy:
                    return $"Мосты в этот круг дешевле на {config.BridgeSubsidy} карт(у) стали (но не меньше одной).";
                case CityEventKind.Parade:
                    return $"Район «{game.Map.DistrictName(config.ParadeDistrict)}» перекрыт: до конца круга нельзя строить дороги, которые его касаются.";
                case CityEventKind.Snow:
                    return $"Щебёночные дороги в этот круг на {config.SnowExtraCost} карт(у) дороже.";
                case CityEventKind.Investor:
                    return $"Каждая дорога, построенная в этот круг, приносит ещё +{config.InvestorBonus} очка.";
                default:
                    return $"На окраине открылся район «{game.Map.DistrictName(DistrictId)}»: новые места, дороги и контракты.";
            }
        }
    }
}
