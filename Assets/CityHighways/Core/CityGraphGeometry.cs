using System;
using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>Planar straight-line graph checks in normalized map coordinates.
    /// Independent of rendering; does not certify road width or 3D meshes.</summary>
    public static class CityGraphGeometry
    {
        const double Epsilon = 1e-7;
        public static List<string> Check(CityMap map)
        {
            var issues = new List<string>();
            if (map == null) { issues.Add("map-null"); return issues; }
            foreach (var road in map.Roads)
            {
                var a = map.GetLocation(road.A);
                var b = map.GetLocation(road.B);
                if (a == null || b == null) { issues.Add("missing-endpoint:" + road.Id); continue; }
                if (Math.Abs(a.X - b.X) <= Epsilon && Math.Abs(a.Y - b.Y) <= Epsilon)
                    issues.Add("zero-length:" + road.Id);
                foreach (var point in map.Locations)
                    if (point.Id != a.Id && point.Id != b.Id && OnSegment(a, b, point))
                        issues.Add("road-through-location:" + road.Id + ":" + point.Id);
            }
            for (int i = 0; i < map.Roads.Count; i++)
            {
                var r = map.Roads[i];
                var a = map.GetLocation(r.A); var b = map.GetLocation(r.B);
                if (a == null || b == null) continue;
                for (int j = i + 1; j < map.Roads.Count; j++)
                {
                    var s = map.Roads[j];
                    var c = map.GetLocation(s.A); var d = map.GetLocation(s.B);
                    if (c == null || d == null) continue;
                    // Shared endpoints are legitimate junctions. Collinear overlap
                    // through a distinct endpoint is caught by the location check above.
                    if (r.Connects(s.A) || r.Connects(s.B)) continue;
                    if (Intersects(a, b, c, d)) issues.Add("road-crossing:" + r.Id + ":" + s.Id);
                }
            }
            return issues;
        }

        static double Cross(Location a, Location b, Location c) =>
            ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
        static int Side(double value) => value > Epsilon ? 1 : value < -Epsilon ? -1 : 0;
        static bool OnSegment(Location a, Location b, Location p) =>
            Side(Cross(a, b, p)) == 0 &&
            p.X >= Math.Min(a.X, b.X) - Epsilon && p.X <= Math.Max(a.X, b.X) + Epsilon &&
            p.Y >= Math.Min(a.Y, b.Y) - Epsilon && p.Y <= Math.Max(a.Y, b.Y) + Epsilon;
        static bool Intersects(Location a, Location b, Location c, Location d)
        {
            int x = Side(Cross(a, b, c)), y = Side(Cross(a, b, d));
            int u = Side(Cross(c, d, a)), v = Side(Cross(c, d, b));
            return (x * y < 0 && u * v < 0) || (x == 0 && OnSegment(a, b, c)) ||
                (y == 0 && OnSegment(a, b, d)) || (u == 0 && OnSegment(c, d, a)) ||
                (v == 0 && OnSegment(c, d, b));
        }
    }
}
