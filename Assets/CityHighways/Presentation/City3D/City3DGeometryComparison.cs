using System;
using System.Collections.Generic;

namespace CityHighways.Presentation
{
    /// <summary>Pure budget diagnostics and comparison of captured generated-mesh snapshots.</summary>
    public static class City3DGeometryComparison
    {
        public struct Delta
        {
            public long Triangles;
            public long Vertices;
            public int Meshes;
            public long BuildingTriangles;
            public long TreeTriangles;
            public long AuthoredTriangles;
            public long OtherTriangles;
        }

        public static bool ExceedsBudget(long triangles, long budget) =>
            budget > 0 && triangles > budget;

        public static Delta Compare(City3DGeometryAudit.Totals baseline,
            City3DGeometryAudit.Totals current) => new Delta
        {
            Triangles = current.Triangles - baseline.Triangles,
            Vertices = current.Vertices - baseline.Vertices,
            Meshes = current.Meshes - baseline.Meshes,
            BuildingTriangles = current.BuildingTriangles - baseline.BuildingTriangles,
            TreeTriangles = current.TreeTriangles - baseline.TreeTriangles,
            AuthoredTriangles = current.AuthoredTriangles - baseline.AuthoredTriangles,
            OtherTriangles = current.OtherTriangles - baseline.OtherTriangles
        };

        /// <summary>All matching names are compared by sum, avoiding false unique-name assumptions.</summary>
        public static long DeltaForGroup(IReadOnlyList<City3DGeometryAudit.Entry> baseline,
            IReadOnlyList<City3DGeometryAudit.Entry> current, string name)
        {
            long previous = 0, latest = 0;
            if (baseline != null)
                foreach (var item in baseline)
                    if (string.Equals(item.Name, name, StringComparison.Ordinal))
                        previous += item.Triangles;
            if (current != null)
                foreach (var item in current)
                    if (string.Equals(item.Name, name, StringComparison.Ordinal))
                        latest += item.Triangles;
            return latest - previous;
        }
    }
}
