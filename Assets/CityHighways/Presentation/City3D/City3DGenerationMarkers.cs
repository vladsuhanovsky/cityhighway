using Unity.Profiling;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Named Unity CPU Profiler samples for real 3D scene startup.
    /// Static markers avoid allocating a new marker for each city build.
    /// </summary>
    internal static class City3DGenerationMarkers
    {
        internal const string ReliefName = "Rivers and relief";
        internal const string PlanningName = "City planning";
        internal const string PrefabsName = "Prefab replacements";
        internal const string MaterialsName = "Rendering and materials";
        internal const string GroundName = "Ground";
        internal const string DecalsName = "Decals";
        internal const string RiversName = "River meshes";
        internal const string DistrictsName = "District buildings";
        internal const string TreesName = "Trees";
        internal const string PropsName = "Props and replacement models";
        internal const string RoadsName = "Road setup";
        internal const string JunctionsName = "Junctions";
        internal const string StationsName = "Stations";
        internal const string BridgesName = "Bridges";
        internal const string DecorationsName = "Authored scene decorations";
        internal const string ReflectionsName = "Reflections and sampling setup";

        internal static readonly ProfilerMarker Relief = new ProfilerMarker("City3D.Build/" + ReliefName);
        internal static readonly ProfilerMarker Planning = new ProfilerMarker("City3D.Build/" + PlanningName);
        internal static readonly ProfilerMarker Prefabs = new ProfilerMarker("City3D.Build/" + PrefabsName);
        internal static readonly ProfilerMarker Materials = new ProfilerMarker("City3D.Build/" + MaterialsName);
        internal static readonly ProfilerMarker Ground = new ProfilerMarker("City3D.Build/" + GroundName);
        internal static readonly ProfilerMarker Decals = new ProfilerMarker("City3D.Build/" + DecalsName);
        internal static readonly ProfilerMarker Rivers = new ProfilerMarker("City3D.Build/" + RiversName);
        internal static readonly ProfilerMarker Districts = new ProfilerMarker("City3D.Build/" + DistrictsName);
        internal static readonly ProfilerMarker Trees = new ProfilerMarker("City3D.Build/" + TreesName);
        internal static readonly ProfilerMarker Props = new ProfilerMarker("City3D.Build/" + PropsName);
        internal static readonly ProfilerMarker Roads = new ProfilerMarker("City3D.Build/" + RoadsName);
        internal static readonly ProfilerMarker Junctions = new ProfilerMarker("City3D.Build/" + JunctionsName);
        internal static readonly ProfilerMarker Stations = new ProfilerMarker("City3D.Build/" + StationsName);
        internal static readonly ProfilerMarker Bridges = new ProfilerMarker("City3D.Build/" + BridgesName);
        internal static readonly ProfilerMarker Decorations = new ProfilerMarker("City3D.Build/" + DecorationsName);
        internal static readonly ProfilerMarker Reflections = new ProfilerMarker("City3D.Build/" + ReflectionsName);
    }
}
