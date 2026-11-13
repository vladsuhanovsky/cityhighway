using System;
using System.Collections.Generic;

namespace CityHighways.Core
{
    // Классы для чтения карты из JSON. Поля публичные и в нижнем регистре, чтобы их понимал JsonUtility.

    [Serializable]
    public class CityMapDto
    {
        public string name;
        public DistrictDto[] districts;
        public LocationDto[] locations;
        public RoadDto[] roads;
        public ContractDto[] contracts;
        public PointDto[] river;
        public GoalDto[] goals;

        public CityMap ToMap()
        {
            var districtList = new List<District>();
            foreach (var d in districts ?? new DistrictDto[0]) districtList.Add(new District(d.id, d.name, d.expansion));

            var locationList = new List<Location>();
            foreach (var l in locations ?? new LocationDto[0]) locationList.Add(new Location(l.id, l.name, l.district, l.x, l.y));

            var roadList = new List<Road>();
            foreach (var r in roads ?? new RoadDto[0]) roadList.Add(new Road(r.id, r.from, r.to, r.length, ParseResource(r.resource, r.id)));

            var contractList = new List<Contract>();
            foreach (var c in contracts ?? new ContractDto[0])
                contractList.Add(new Contract(c.id, c.from, c.to, c.points, ParseKind(c.kind, c.id), c.third));

            var riverList = new List<MapPoint>();
            foreach (var p in river ?? new PointDto[0]) riverList.Add(new MapPoint(p.x, p.y));

            var goalList = new List<MayorGoal>();
            foreach (var g in goals ?? new GoalDto[0])
                goalList.Add(new MayorGoal(g.id, g.name, ParseGoalKind(g.kind, g.id), g.count, g.points, g.district,
                    string.IsNullOrEmpty(g.resource) ? (Resource?)null : ParseResource(g.resource, g.id), g.length));

            return new CityMap(name, districtList, locationList, roadList, contractList, riverList, goalList);
        }

        static GoalKind ParseGoalKind(string value, string goalId)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "locations": return GoalKind.Locations;
                case "bridges": return GoalKind.Bridges;
                case "network": return GoalKind.Network;
                case "resource_pieces": return GoalKind.ResourcePieces;
                case "long_roads": return GoalKind.LongRoads;
                default: throw new FormatException($"Цель {goalId}: неизвестный вид \"{value}\".");
            }
        }

        static ContractKind ParseKind(string value, string contractId)
        {
            if (string.IsNullOrEmpty(value) || value.Equals("link", StringComparison.OrdinalIgnoreCase)) return ContractKind.Link;
            if (value.Equals("transit", StringComparison.OrdinalIgnoreCase)) return ContractKind.Transit;
            if (value.Equals("hub", StringComparison.OrdinalIgnoreCase)) return ContractKind.Hub;
            throw new FormatException($"Контракт {contractId}: неизвестный вид \"{value}\".");
        }

        static Resource? ParseResource(string value, string roadId)
        {
            if (string.IsNullOrEmpty(value) || value.Equals("any", StringComparison.OrdinalIgnoreCase)) return null;
            if (Enum.TryParse(value, true, out Resource resource) && resource != Resource.Machinery) return resource;
            throw new FormatException($"Дорога {roadId}: неизвестный ресурс \"{value}\".");
        }
    }

    [Serializable]
    public class DistrictDto
    {
        public string id;
        public string name;
        /// <summary>true — район открывается событием «Новый район».</summary>
        public bool expansion;
    }

    [Serializable]
    public class LocationDto
    {
        public string id;
        public string name;
        public string district;
        public float x;
        public float y;
    }

    [Serializable]
    public class RoadDto
    {
        public string id;
        public string from;
        public string to;
        public int length;
        /// <summary>asphalt, concrete, steel, gravel или any.</summary>
        public string resource;
    }

    [Serializable]
    public class ContractDto
    {
        public string id;
        /// <summary>Id района.</summary>
        public string from;
        public string to;
        public int points;
        /// <summary>link (по умолчанию), transit — через район third, hub — from связать с to и с third.</summary>
        public string kind;
        public string third;
    }

    [Serializable]
    public class GoalDto
    {
        public string id;
        public string name;
        /// <summary>locations, bridges, network, resource_pieces или long_roads.</summary>
        public string kind;
        public string district;
        public string resource;
        public int count;
        public int length;
        public int points;
    }

    [Serializable]
    public class PointDto
    {
        public float x;
        public float y;
    }
}
