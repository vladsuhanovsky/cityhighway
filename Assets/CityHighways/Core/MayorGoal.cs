using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Вид общей цели мэрии.</summary>
    public enum GoalKind
    {
        /// <summary>Своя дорога в Count местах района District.</summary>
        Locations,
        /// <summary>Построить Count мостов (стальных дорог).</summary>
        Bridges,
        /// <summary>Связная сеть на Count районов.</summary>
        Network,
        /// <summary>Count участков дорог ресурса Resource.</summary>
        ResourcePieces,
        /// <summary>Count дорог длиной не меньше Length.</summary>
        LongRoads
    }

    /// <summary>
    /// Общая цель мэрии: открыта для всех, награду получает первый, кто её выполнит.
    /// Выполнение проверяется сразу после постройки дороги.
    /// </summary>
    public sealed class MayorGoal
    {
        public string Id { get; }
        public string Name { get; }
        public GoalKind Kind { get; }
        public string District { get; }
        public Resource? Resource { get; }
        public int Count { get; }
        public int Length { get; }
        public int Points { get; }

        public MayorGoal(string id, string name, GoalKind kind, int count, int points,
            string district = null, Resource? resource = null, int length = 0)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Count = count;
            Points = points;
            District = district;
            Resource = resource;
            Length = length;
        }

        /// <summary>Сколько уже сделано: цель выполнена, когда Progress >= Count.</summary>
        public int Progress(CityMap map, IEnumerable<Road> roads)
        {
            var list = roads as IList<Road> ?? roads.ToList();
            switch (Kind)
            {
                case GoalKind.Locations:
                {
                    var touched = new HashSet<string>();
                    foreach (var road in list)
                    {
                        touched.Add(road.A);
                        touched.Add(road.B);
                    }
                    return map.LocationsIn(District).Count(l => touched.Contains(l.Id));
                }
                case GoalKind.Bridges:
                    return list.Count(r => r.IsBridge);
                case GoalKind.Network:
                    return Scoring.NetworkDistricts(map, list);
                case GoalKind.ResourcePieces:
                    return list.Where(r => r.Resource == Resource).Sum(r => r.Length);
                case GoalKind.LongRoads:
                    return list.Count(r => r.Length >= Length);
                default:
                    throw new InvalidOperationException("Неизвестный вид цели " + Kind);
            }
        }

        public bool IsDone(CityMap map, IEnumerable<Road> roads) => Progress(map, roads) >= Count;

        /// <summary>Описание для интерфейса: «Свои дороги в 2 местах района Аэропорты».</summary>
        public string Describe(CityMap map)
        {
            switch (Kind)
            {
                case GoalKind.Locations:
                    return $"Своя дорога в {Count} из {map.LocationsIn(District).Count} мест района «{map.DistrictName(District)}»";
                case GoalKind.Bridges:
                    return $"Построить {Count} моста через реку";
                case GoalKind.Network:
                    return $"Одна связная сеть на {Count} районов";
                case GoalKind.ResourcePieces:
                    return $"{Count} участков дорог «{ResourceInfo.RussianName(Resource ?? Core.Resource.Asphalt)}»";
                case GoalKind.LongRoads:
                    return $"{Count} дороги длиной {Length} и больше";
                default:
                    return Name;
            }
        }

        public override string ToString() => $"{Id} {Kind} {Count} ({Points})";
    }
}
