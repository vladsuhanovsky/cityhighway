using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public sealed class CitySeedReport
    {
        public int schema = 1;
        public int requestedSeed, actualSeed;
        public bool generated;
        public string unityVersion, utc, cityName;
        public long milliseconds;
        public LocationDto[] locations;
        public RoadDto[] roads;
        public DistrictDto[] districts;
        public RiverDto[] rivers;
        public string[] hubs, publicRoads, issues;
        public City3DLayoutReport layout3D;
        public string generationProfile;
        public CityGenerationAttempt[] attempts;
        public bool Passed => generated && issues != null && issues.Length == 0;
    }

    /// <summary>Bounded diagnostic workload, not a gameplay generation setting.</summary>
    public static class CitySeedBatch
    {
        public const int MaxCount = 512;

        public static bool IsValid(int first, int count) =>
            count >= 1 && count <= MaxCount &&
            (long)first + count - 1 <= int.MaxValue;

        public static int[] Seeds(int first, int count)
        {
            if (!IsValid(first, count))
                throw new ArgumentOutOfRangeException(nameof(count), "Invalid seed range.");
            var seeds = new int[count];
            for (int i = 0; i < count; i++) seeds[i] = first + i;
            return seeds;
        }
    }

    /// <summary>Explicit developer diagnostic, never runs automatically during a match.</summary>
    public static class CitySeedDiagnostics
    {
        public static CitySeedReport Run(int seed, bool include3D = false) => RunInternal(seed, include3D, false);

        public static CitySeedReport RunTutorial(bool include3D = false) =>
            RunInternal(CityGenerator.TutorialSeed, include3D, true);

        static CitySeedReport RunInternal(int seed, bool include3D, bool tutorial)
        {
            var report = new CitySeedReport { requestedSeed = seed, unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O"), generationProfile = tutorial ? "tutorial" : "standard" };
            var attempts = new List<CityGenerationAttempt>();
            var watch = Stopwatch.StartNew();
            var errors = new List<string>();
            try
            {
                var city = tutorial ? CityGenerator.GenerateTutorial(attempts) : CityGenerator.Generate(seed, attempts);
                report.generated = true;
                report.actualSeed = city.Seed;
                var map = city.Map;
                report.cityName = map.Name;
                report.locations = map.Locations.Select(p => new LocationDto { id = p.Id, name = p.Name,
                    district = p.DistrictId, x = p.X, y = p.Y }).ToArray();
                report.roads = map.Roads.Select(r => new RoadDto { id = r.Id, from = r.A, to = r.B,
                    length = r.Length, resource = r.Resource?.ToString() ?? "any" }).ToArray();
                report.districts = map.Districts.Select(d => new DistrictDto { id = d.Id, name = d.Name,
                    expansion = d.IsExpansion }).ToArray();
                report.rivers = map.Rivers.Select(r => new RiverDto { points = r.Select(p =>
                    new PointDto { x = p.X, y = p.Y }).ToArray() }).ToArray();
                report.hubs = city.Hubs.Select(h => h.LocationId + ":" + h.Kind).ToArray();
                report.publicRoads = city.PublicRoads.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                errors.AddRange(map.Validate(6));
                errors.AddRange(CityGraphGeometry.Check(map));
                errors.AddRange(CityGenerator.CheckWater(map));
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var hub in city.Hubs)
                    if (map.GetLocation(hub.LocationId) == null || !seen.Add(hub.LocationId))
                        errors.Add("invalid-hub:" + hub.LocationId);
                foreach (var road in city.PublicRoads)
                    if (map.GetRoad(road) == null) errors.Add("invalid-public-road:" + road);
                if (city.Hubs.Count == 0) errors.Add("no-hubs");
                if (!Connected(map)) errors.Add("disconnected-map");
                if (include3D)
                {
                    report.schema = 2;
                    report.layout3D = City3DLayoutAudit.Capture(map, city.Hubs.Select(h => h.LocationId));
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetType().Name + ": " + ex.Message);
            }
            report.attempts = attempts.ToArray();
            report.milliseconds = watch.ElapsedMilliseconds;
            report.issues = errors.ToArray();
            return report;
        }

        static bool Connected(CityMap map)
        {
            if (map.Locations.Count == 0) return false;
            var queue = new Queue<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(map.Locations[0].Id);
            seen.Add(map.Locations[0].Id);
            while (queue.Count != 0)
            {
                string id = queue.Dequeue();
                foreach (var road in map.RoadsAt(id))
                {
                    string next = road.OtherEnd(id);
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }
            return seen.Count == map.Locations.Count;
        }

        public static string Write(CitySeedReport report, string directory)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Directory.CreateDirectory(directory);
            // Generated filename only. Neither map names nor external data form the path.
            string path = Path.Combine(directory, "city-seed-" + report.requestedSeed + "-" +
                Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            return path;
        }

        public static string LayoutJson(CitySeedReport report)
        {
            // Exclude measurement fields from determinism checks. JsonUtility preserves array order.
            return JsonUtility.ToJson(new CitySeedReport { requestedSeed = report.requestedSeed,
                actualSeed = report.actualSeed, generated = report.generated, cityName = report.cityName,
                locations = report.locations, roads = report.roads, districts = report.districts,
                rivers = report.rivers, hubs = report.hubs, publicRoads = report.publicRoads });
        }
    }
}
