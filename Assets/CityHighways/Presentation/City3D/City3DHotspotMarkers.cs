using Unity.Profiling;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Sub-stage CPU Profiler scopes for expensive 3D city startup work.
    /// Reusable marker objects are intentionally separate from the sixteen
    /// coarse-grained stages used by the generation timing CSV.
    /// </summary>
    internal static class City3DHotspotMarkers
    {
        internal static readonly ProfilerMarker SetupRendering =
            new ProfilerMarker("City3D.Hotspot/Setup rendering");
        internal static readonly ProfilerMarker CityAtlas =
            new ProfilerMarker("City3D.Hotspot/City atlas");
        internal static readonly ProfilerMarker TerrainAlbedo =
            new ProfilerMarker("City3D.Hotspot/Terrain albedo bake");
        internal static readonly ProfilerMarker GroundNormal =
            new ProfilerMarker("City3D.Hotspot/Ground normal bake");
        internal static readonly ProfilerMarker GroundGeometry =
            new ProfilerMarker("City3D.Hotspot/Ground grid geometry");
        internal static readonly ProfilerMarker GroundMeshUpload =
            new ProfilerMarker("City3D.Hotspot/Ground mesh upload and tangents");
        internal static readonly ProfilerMarker Outskirts =
            new ProfilerMarker("City3D.Hotspot/Outskirts ground");
        internal static readonly ProfilerMarker TerrainPixels =
            new ProfilerMarker("City3D.Hotspot/Terrain base pixel fill");
        internal static readonly ProfilerMarker TerrainFootprints =
            new ProfilerMarker("City3D.Hotspot/Terrain building footprints");
        internal static readonly ProfilerMarker TerrainUpload =
            new ProfilerMarker("City3D.Hotspot/Terrain texture upload");
    }
}
