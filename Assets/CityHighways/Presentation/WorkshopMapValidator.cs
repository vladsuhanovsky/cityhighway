using System;
using System.Collections.Generic;
using System.Text;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Preflight for untrusted, data-only Classic city JSON. Does not register or launch a map.</summary>
    public static class WorkshopMapValidator
    {
        public const int MaxUtf8Bytes = 256 * 1024;
        public const int MaxLocations = 128, MaxRoads = 384, MaxDistricts = 32;
        public const int MaxContracts = 256, MaxGoals = 64;
        public const int MaxRiverLines = 8, MaxRiverPoints = 128;

        public static bool TryRead(string json, out CityMap map, out string error)
        {
            map = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxUtf8Bytes ||
                Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes)
                return Fail("size", out error);
            CityMapDto dto;
            try { dto = JsonUtility.FromJson<CityMapDto>(json); }
            catch (ArgumentException) { return Fail("json", out error); }
            if (dto == null) return Fail("json", out error);
            var config = new GameConfig();
            if (!Label(dto.name)) return Fail("name", out error);
            if (!Count(dto.districts, 2, MaxDistricts) || !Count(dto.locations, 2, MaxLocations) ||
                !Count(dto.roads, 1, MaxRoads) ||
                !Count(dto.contracts, config.MaxPlayers * config.InitialContractsDealt, MaxContracts) ||
                !Count(dto.goals, config.MayorGoalsDealt, MaxGoals))
                return Fail("counts", out error);
            var openDistricts = new HashSet<string>(StringComparer.Ordinal);
            var districts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in dto.districts)
            {
                if (d == null || !Id(d.id) || !Label(d.name) || !districts.Add(d.id))
                    return Fail("district", out error);
                if (!d.expansion) openDistricts.Add(d.id);
            }
            if (openDistricts.Count < 2) return Fail("open-districts", out error);
            var locations = new HashSet<string>(StringComparer.Ordinal);
            var open = new HashSet<string>(StringComparer.Ordinal);
            var coordinates = new HashSet<Vector2>();
            foreach (var l in dto.locations)
            {
                if (l == null || !Id(l.id) || !Label(l.name) || !districts.Contains(l.district) ||
                    !Unit(l.x) || !Unit(l.y) || !locations.Add(l.id) || !coordinates.Add(new Vector2(l.x, l.y)))
                    return Fail("location", out error);
                if (openDistricts.Contains(l.district)) open.Add(l.id);
            }
            foreach (var r in dto.roads)
                if (r == null || !Id(r.id) || !locations.Contains(r.from) || !locations.Contains(r.to) ||
                    !RoadResource(r.resource) || r.length < 1 || r.length > config.MaxRoadLength)
                    return Fail("road", out error);
            foreach (var c in dto.contracts)
                if (c == null || !Id(c.id) || !districts.Contains(c.from) || !districts.Contains(c.to) ||
                    c.points < 1 || c.points > 100 ||
                    (!string.IsNullOrEmpty(c.kind) && !OneOf(c.kind, "link", "transit", "hub")))
                    return Fail("contract", out error);
            foreach (var g in dto.goals)
                if (g == null || !Id(g.id) || !Label(g.name) || g.count < 1 || g.count > MaxRoads ||
                    g.points < 1 || g.points > 100 || !RoadResource(g.resource) ||
                    g.length < 0 || g.length > config.MaxRoadLength)
                    return Fail("goal", out error);
            if (!River(dto.river) || (dto.rivers != null && dto.rivers.Length > MaxRiverLines))
                return Fail("river", out error);
            if (dto.rivers != null)
                foreach (var r in dto.rivers)
                    if (r == null || !Count(r.points, 2, MaxRiverPoints) || !River(r.points))
                        return Fail("river", out error);
            if (dto.piecesScale != 0 && !(dto.piecesScale > 0.2f && dto.piecesScale <= 2f))
                return Fail("scale", out error);
            // JsonUtility may materialize an absent nested DTO with default fields.
            bool noRing = dto.ring == null || (string.IsNullOrEmpty(dto.ring.name) &&
                dto.ring.cx == 0 && dto.ring.cy == 0 && dto.ring.rx == 0 && dto.ring.ry == 0);
            if (!noRing && (!Label(dto.ring.name) || !Unit(dto.ring.cx) || !Unit(dto.ring.cy) ||
                !Unit(dto.ring.rx) || !Unit(dto.ring.ry) || dto.ring.rx == 0 || dto.ring.ry == 0))
                return Fail("ring", out error);
            CityMap candidate;
            try { candidate = dto.ToMap(); }
            catch (FormatException) { return Fail("format", out error); }
            if (candidate.Validate(config.MaxRoadLength).Count != 0) return Fail("rules", out error);
            if (!Connected(candidate, locations) || !Connected(candidate, open))
                return Fail("connectivity", out error);
            map = candidate;
            return true;
        }

        static bool Connected(CityMap map, HashSet<string> ids)
        {
            if (ids.Count == 0) return false;
            var pending = new Queue<string>();
            foreach (var id in ids) { pending.Enqueue(id); break; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            seen.Add(pending.Peek());
            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                foreach (var r in map.RoadsAt(id))
                {
                    string other = r.OtherEnd(id);
                    if (ids.Contains(other) && seen.Add(other)) pending.Enqueue(other);
                }
            }
            return seen.Count == ids.Count;
        }

        static bool River(PointDto[] points)
        {
            if (points == null || points.Length == 0) return true;
            if (!Count(points, 2, MaxRiverPoints)) return false;
            foreach (var p in points)
                if (p == null || !Unit(p.x) || !Unit(p.y)) return false;
            return true;
        }

        static bool Unit(float value) => value >= 0 && value <= 1;
        static bool Count<T>(T[] values, int min, int max) =>
            values != null && values.Length >= min && values.Length <= max;
        static bool Id(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            return true;
        }
        static bool Label(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96) return false;
            foreach (char c in value) if (char.IsControl(c) || c == '<' || c == '>') return false;
            return true;
        }
        static bool RoadResource(string value) => string.IsNullOrEmpty(value) ||
            OneOf(value, "any", "asphalt", "concrete", "steel", "gravel");
        static bool OneOf(string value, params string[] choices)
        {
            foreach (string choice in choices)
                if (string.Equals(value, choice, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        static bool Fail(string code, out string error) { error = code; return false; }
    }
}
