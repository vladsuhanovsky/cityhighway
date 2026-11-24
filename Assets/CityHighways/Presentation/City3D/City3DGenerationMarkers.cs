using Unity.Profiling;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Named Unity CPU Profiler samples for real 3D scene startup.
    /// Static markers avoid allocating a new marker for each city build.
    /// </summary>
    internal static class City3DGenerationMarkers
    {
        internal static readonly ProfilerMarker Relief = new ProfilerMarker("City3D.Build/Rivers and relief");
        internal static readonly ProfilerMarker Planning = new ProfilerMarker("City3D.Build/City planning");
        internal static readonly ProfilerMarker Prefabs = new ProfilerMarker("City3D.Build/Prefab replacements");
        internal static readonly ProfilerMarker Materials = new ProfilerMarker("City3D.Build/Rendering and materials");
        internal static readonly ProfilerMarker Ground = new ProfilerMarker("City3D.Build/Ground");
        internal static readonly ProfilerMarker Decals = new ProfilerMarker("City3D.Build/Decals");
        internal static readonly ProfilerMarker Rivers = new ProfilerMarker("City3D.Build/River meshes");
        internal static readonly ProfilerMarker Districts = new ProfilerMarker("City3D.Build/District buildings");
        internal static readonly ProfilerMarker Trees = new ProfilerMarker("City3D.Build/Trees");
        internal static readonly ProfilerMarker Props = new ProfilerMarker("City3D.Build/Props and replacement models");
        internal static readonly ProfilerMarker Roads = new ProfilerMarker("City3D.Build/Road setup");
        internal static readonly ProfilerMarker Junctions = new ProfilerMarker("City3D.Build/Junctions");
        internal static readonly ProfilerMarker Stations = new ProfilerMarker("City3D.Build/Stations");
        internal static readonly ProfilerMarker Bridges = new ProfilerMarker("City3D.Build/Bridges");
        internal static readonly ProfilerMarker Decorations = new ProfilerMarker("City3D.Build/Authored scene decorations");
        internal static readonly ProfilerMarker Reflections = new ProfilerMarker("City3D.Build/Reflections and sampling setup");
    }
}
