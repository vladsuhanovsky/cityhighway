using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Цвета ресурсов, дорог, районов и компаний.</summary>
    public static class Palette
    {
        public static readonly Color AnyRoad = new Color(0.97f, 0.97f, 0.95f);

        /// <summary>Цвета компаний — яркие, чтобы не путаться с серыми дорогами и пастельными районами.</summary>
        public static readonly Color[] Players =
        {
            new Color(0.91f, 0.26f, 0.24f), // красный
            new Color(0.16f, 0.66f, 0.36f), // зелёный
            new Color(0.56f, 0.32f, 0.82f), // фиолетовый
            new Color(0.06f, 0.70f, 0.84f), // бирюзовый
            new Color(0.95f, 0.40f, 0.68f)  // розовый
        };

        /// <summary>Пастельные цвета районов для зон на карте.</summary>
        static readonly Color[] Districts =
        {
            new Color(0.98f, 0.80f, 0.42f), new Color(0.62f, 0.80f, 0.98f), new Color(0.78f, 0.66f, 0.96f),
            new Color(0.62f, 0.90f, 0.70f), new Color(0.98f, 0.66f, 0.62f), new Color(0.60f, 0.86f, 0.90f),
            new Color(0.96f, 0.74f, 0.88f), new Color(0.80f, 0.88f, 0.56f), new Color(0.98f, 0.86f, 0.62f),
            new Color(0.70f, 0.76f, 0.96f), new Color(0.90f, 0.78f, 0.66f), new Color(0.74f, 0.74f, 0.80f),
            new Color(0.66f, 0.92f, 0.86f), new Color(0.94f, 0.70f, 0.56f)
        };

        public static Color PlayerColor(int index) => Players[((index % Players.Length) + Players.Length) % Players.Length];

        public static Color DistrictColor(int index) => Districts[((index % Districts.Length) + Districts.Length) % Districts.Length];

        public static Color Card(Resource resource)
        {
            switch (resource)
            {
                case Resource.Asphalt: return new Color(0.28f, 0.30f, 0.34f);
                case Resource.Concrete: return new Color(0.74f, 0.74f, 0.72f);
                case Resource.Steel: return new Color(0.33f, 0.53f, 0.76f);
                case Resource.Gravel: return new Color(0.70f, 0.55f, 0.36f);
                default: return new Color(0.99f, 0.74f, 0.10f); // техника — дорожный жёлтый
            }
        }

        public static Color RoadColor(Road road) => road.Resource.HasValue ? Card(road.Resource.Value) : AnyRoad;

        public static Color TextOn(Color background)
        {
            float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return luminance > 0.6f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        public static string CardTitle(Resource resource)
        {
            string name = ResourceInfo.RussianName(resource);
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
