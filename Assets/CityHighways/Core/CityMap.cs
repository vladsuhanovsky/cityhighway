using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Район города. Контракты связывают районы, а не отдельные точки.</summary>
    public sealed class District
    {
        public string Id { get; }
        public string Name { get; }
        /// <summary>Район-расширение: закрыт в начале партии, открывается событием «Новый район».</summary>
        public bool IsExpansion { get; }

        public District(string id, string name, bool isExpansion = false)
        {
            Id = id;
            Name = name;
            IsExpansion = isExpansion;
        }

        public override string ToString() => Name;
    }

    /// <summary>Место на карте (площадь, станция, перекрёсток). X/Y — от 0 до 1, Y растёт вниз.</summary>
    public sealed class Location
    {
        public string Id { get; }
        public string Name { get; }
        public string DistrictId { get; }
        public float X { get; }
        public float Y { get; }

        public Location(string id, string name, string districtId, float x, float y)
        {
            Id = id;
            Name = name;
            DistrictId = districtId;
            X = x;
            Y = y;
        }

        public override string ToString() => Name;
    }

    /// <summary>Участок дороги между двумя местами.</summary>
    public sealed class Road
    {
        public string Id { get; }
        public string A { get; }
        public string B { get; }
        public int Length { get; }
        /// <summary>Чем строить; null — любым одним ресурсом.</summary>
        public Resource? Resource { get; }

        public bool IsBridge => Resource == Core.Resource.Steel;

        public Road(string id, string a, string b, int length, Resource? resource)
        {
            Id = id;
            A = a;
            B = b;
            Length = length;
            Resource = resource;
        }

        public bool Connects(string locationId) => A == locationId || B == locationId;

        public string OtherEnd(string locationId) => locationId == A ? B : A;

        public override string ToString() => $"{A}-{B} ({Length}, {(Resource.HasValue ? Resource.Value.ToString() : "Any")})";
    }

    /// <summary>Вид контракта мэрии.</summary>
    public enum ContractKind
    {
        /// <summary>Соединить район A с районом B.</summary>
        Link,
        /// <summary>Транзит: один путь из A в B, который проходит через район C.</summary>
        Transit,
        /// <summary>Узел: район A соединить и с B, и с C.</summary>
        Hub
    }

    /// <summary>Плечо контракта: путь из района From в район To (через район Via, если он задан).</summary>
    public struct ContractLeg
    {
        public string From;
        public string To;
        public string Via;

        public ContractLeg(string from, string to, string via = null)
        {
            From = from;
            To = to;
            Via = via;
        }
    }

    /// <summary>Контракт мэрии: обычный (A–B), транзит (A–B через C) или узел (A с B и с C).</summary>
    public sealed class Contract
    {
        public string Id { get; }
        public ContractKind Kind { get; }
        public string DistrictA { get; }
        public string DistrictB { get; }
        /// <summary>Третий район: «через» для транзита, второе плечо для узла; null у обычного контракта.</summary>
        public string DistrictC { get; }
        public int Points { get; }
        /// <summary>Все районы контракта: A, B и C (если есть).</summary>
        public IReadOnlyList<string> Districts { get; }
        /// <summary>Плечи: каждое проверяется как отдельный путь со своими правилами проезда.</summary>
        public IReadOnlyList<ContractLeg> Legs { get; }

        public bool IsComplex => Kind != ContractKind.Link;

        public Contract(string id, string districtA, string districtB, int points,
            ContractKind kind = ContractKind.Link, string districtC = null)
        {
            Id = id;
            Kind = kind;
            DistrictA = districtA;
            DistrictB = districtB;
            DistrictC = kind == ContractKind.Link ? null : districtC;
            Points = points;
            Districts = DistrictC == null ? new[] { districtA, districtB } : new[] { districtA, districtB, DistrictC };
            switch (kind)
            {
                case ContractKind.Transit:
                    Legs = new[] { new ContractLeg(districtA, districtB, DistrictC) };
                    break;
                case ContractKind.Hub:
                    Legs = new[] { new ContractLeg(districtA, districtB), new ContractLeg(districtA, DistrictC) };
                    break;
                default:
                    Legs = new[] { new ContractLeg(districtA, districtB) };
                    break;
            }
        }

        public override string ToString() =>
            Kind == ContractKind.Link ? $"{DistrictA}-{DistrictB} ({Points})" : $"{Kind} {DistrictA}-{DistrictB}+{DistrictC} ({Points})";
    }

    public struct MapPoint
    {
        public float X;
        public float Y;

        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>Неизменяемое описание города: районы, места, дороги, контракты и река для отрисовки.</summary>
    public sealed class CityMap
    {
        readonly Dictionary<string, District> _districts = new Dictionary<string, District>();
        readonly Dictionary<string, Location> _locations = new Dictionary<string, Location>();
        readonly Dictionary<string, Road> _roads = new Dictionary<string, Road>();
        readonly Dictionary<string, Contract> _contracts = new Dictionary<string, Contract>();
        readonly Dictionary<string, MayorGoal> _goals = new Dictionary<string, MayorGoal>();
        readonly Dictionary<string, List<Road>> _roadsAt = new Dictionary<string, List<Road>>();
        readonly Dictionary<string, List<Location>> _locationsIn = new Dictionary<string, List<Location>>();

        public string Name { get; }
        public IReadOnlyList<District> Districts { get; }
        public IReadOnlyList<Location> Locations { get; }
        public IReadOnlyList<Road> Roads { get; }
        public IReadOnlyList<Contract> Contracts { get; }
        public IReadOnlyList<MapPoint> River { get; }
        /// <summary>Пул общих целей мэрии; в партии открывается GameConfig.MayorGoalsDealt из них.</summary>
        public IReadOnlyList<MayorGoal> Goals { get; }

        public CityMap(string name, IEnumerable<District> districts, IEnumerable<Location> locations,
            IEnumerable<Road> roads, IEnumerable<Contract> contracts, IEnumerable<MapPoint> river = null,
            IEnumerable<MayorGoal> goals = null)
        {
            Name = name;
            Districts = new List<District>(districts);
            Locations = new List<Location>(locations);
            Roads = new List<Road>(roads);
            Contracts = new List<Contract>(contracts);
            River = new List<MapPoint>(river ?? new MapPoint[0]);
            Goals = new List<MayorGoal>(goals ?? new MayorGoal[0]);

            // Повторы id не ломают конструктор — их найдёт Validate().
            foreach (var d in Districts) _districts[d.Id] = d;
            foreach (var l in Locations)
            {
                _locations[l.Id] = l;
                if (l.DistrictId == null) continue;
                if (!_locationsIn.TryGetValue(l.DistrictId, out var list)) _locationsIn[l.DistrictId] = list = new List<Location>();
                list.Add(l);
            }
            foreach (var r in Roads)
            {
                _roads[r.Id] = r;
                AddRoadAt(r.A, r);
                AddRoadAt(r.B, r);
            }
            foreach (var c in Contracts) _contracts[c.Id] = c;
            foreach (var g in Goals) _goals[g.Id] = g;
        }

        void AddRoadAt(string locationId, Road road)
        {
            if (locationId == null) return;
            if (!_roadsAt.TryGetValue(locationId, out var list)) _roadsAt[locationId] = list = new List<Road>();
            list.Add(road);
        }

        public District GetDistrict(string id) => id != null && _districts.TryGetValue(id, out var d) ? d : null;
        public Location GetLocation(string id) => id != null && _locations.TryGetValue(id, out var l) ? l : null;
        public Road GetRoad(string id) => id != null && _roads.TryGetValue(id, out var r) ? r : null;
        public Contract GetContract(string id) => id != null && _contracts.TryGetValue(id, out var c) ? c : null;
        public MayorGoal GetGoal(string id) => id != null && _goals.TryGetValue(id, out var g) ? g : null;

        public IReadOnlyList<Road> RoadsAt(string locationId) =>
            locationId != null && _roadsAt.TryGetValue(locationId, out var list) ? list : (IReadOnlyList<Road>)new Road[0];

        public IReadOnlyList<Location> LocationsIn(string districtId) =>
            districtId != null && _locationsIn.TryGetValue(districtId, out var list) ? list : (IReadOnlyList<Location>)new Location[0];

        public string DistrictOf(string locationId) => GetLocation(locationId)?.DistrictId;

        public string DistrictName(string districtId) => GetDistrict(districtId)?.Name ?? districtId;

        public string LocationName(string locationId) => GetLocation(locationId)?.Name ?? locationId;

        /// <summary>Проверяет карту; пустой список — всё в порядке.</summary>
        public List<string> Validate(int maxRoadLength)
        {
            var errors = new List<string>();

            CheckUnique(errors, Districts.Select(d => d.Id), "района");
            CheckUnique(errors, Locations.Select(l => l.Id), "места");
            CheckUnique(errors, Roads.Select(r => r.Id), "дороги");
            CheckUnique(errors, Contracts.Select(c => c.Id), "контракта");
            CheckUnique(errors, Goals.Select(g => g.Id), "цели");

            foreach (var g in Goals)
            {
                if (g.Count <= 0) errors.Add($"Цель {g.Id}: count должен быть больше нуля.");
                if (g.Points <= 0) errors.Add($"Цель {g.Id}: очков должно быть больше нуля.");
                if (g.Kind == GoalKind.Locations && GetDistrict(g.District) == null) errors.Add($"Цель {g.Id}: нет района {g.District}.");
                if (g.Kind == GoalKind.Locations && LocationsIn(g.District).Count < g.Count) errors.Add($"Цель {g.Id}: в районе меньше {g.Count} мест.");
                if (g.Kind == GoalKind.ResourcePieces && (!g.Resource.HasValue || g.Resource == Resource.Machinery))
                    errors.Add($"Цель {g.Id}: нужен ресурс дороги.");
                if (g.Kind == GoalKind.LongRoads && g.Length <= 0) errors.Add($"Цель {g.Id}: нужна длина дороги.");
            }

            foreach (var l in Locations)
                if (GetDistrict(l.DistrictId) == null) errors.Add($"Место {l.Id}: нет района {l.DistrictId}.");

            foreach (var d in Districts)
                if (LocationsIn(d.Id).Count == 0) errors.Add($"В районе {d.Id} нет ни одного места.");

            var pairs = new HashSet<string>();
            foreach (var r in Roads)
            {
                if (GetLocation(r.A) == null) errors.Add($"Дорога {r.Id}: нет места {r.A}.");
                if (GetLocation(r.B) == null) errors.Add($"Дорога {r.Id}: нет места {r.B}.");
                if (r.A == r.B) errors.Add($"Дорога {r.Id} ведёт из места в него же.");
                if (r.Length < 1 || r.Length > maxRoadLength)
                    errors.Add($"Дорога {r.Id}: длина {r.Length} вне диапазона 1..{maxRoadLength}.");
                if (r.Resource == Resource.Machinery) errors.Add($"Дорога {r.Id}: техника — джокер, а не тип дороги.");
                string key = string.CompareOrdinal(r.A, r.B) < 0 ? r.A + "|" + r.B : r.B + "|" + r.A;
                if (!pairs.Add(key)) errors.Add($"Дорога {r.Id}: между {r.A} и {r.B} уже есть дорога.");
            }

            foreach (var c in Contracts)
            {
                if (GetDistrict(c.DistrictA) == null) errors.Add($"Контракт {c.Id}: нет района {c.DistrictA}.");
                if (GetDistrict(c.DistrictB) == null) errors.Add($"Контракт {c.Id}: нет района {c.DistrictB}.");
                if (c.DistrictA == c.DistrictB) errors.Add($"Контракт {c.Id}: районы совпадают.");
                if (c.IsComplex)
                {
                    if (GetDistrict(c.DistrictC) == null) errors.Add($"Контракт {c.Id}: нет третьего района {c.DistrictC}.");
                    if (c.DistrictC == c.DistrictA || c.DistrictC == c.DistrictB) errors.Add($"Контракт {c.Id}: третий район совпадает с другим.");
                }
                if (c.Points <= 0) errors.Add($"Контракт {c.Id}: очков должно быть больше нуля.");
            }
            return errors;
        }

        static void CheckUnique(List<string> errors, IEnumerable<string> ids, string what)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) errors.Add($"Есть объект {what} без id.");
                else if (!seen.Add(id)) errors.Add($"Повторяется id {what}: {id}.");
            }
        }
    }
}
