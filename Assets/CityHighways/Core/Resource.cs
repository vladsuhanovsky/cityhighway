namespace CityHighways.Core
{
    /// <summary>
    /// Карта ресурса. Асфальт — обычные улицы, бетон — проспекты, сталь — мосты,
    /// щебень — окраины и промзоны. Дорожная техника — джокер, подходит к любой дороге.
    /// </summary>
    public enum Resource
    {
        Asphalt,
        Concrete,
        Steel,
        Gravel,
        Machinery
    }

    public static class ResourceInfo
    {
        /// <summary>Сколько всего видов карт (вместе с техникой).</summary>
        public const int Count = 5;

        /// <summary>Строительные ресурсы — все, кроме техники.</summary>
        public static readonly Resource[] Building = { Resource.Asphalt, Resource.Concrete, Resource.Steel, Resource.Gravel };

        public static string RussianName(Resource resource)
        {
            switch (resource)
            {
                case Resource.Asphalt: return "асфальт";
                case Resource.Concrete: return "бетон";
                case Resource.Steel: return "сталь";
                case Resource.Gravel: return "щебень";
                default: return "техника";
            }
        }

        /// <summary>Как называется дорога, построенная из этого ресурса.</summary>
        public static string RoadKind(Resource? resource)
        {
            if (!resource.HasValue) return "улица (любой ресурс)";
            switch (resource.Value)
            {
                case Resource.Asphalt: return "улица";
                case Resource.Concrete: return "проспект";
                case Resource.Steel: return "мост";
                case Resource.Gravel: return "загородная дорога";
                default: return "дорога";
            }
        }
    }
}
