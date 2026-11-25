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

        // Alternative company colors inspired by a color-vision-friendly
        // categorical palette; not a guarantee for every display or vision type.
        static readonly Color[] AccessiblePlayers =
        {
            new Color32(0, 114, 178, 255),   // blue
            new Color32(230, 159, 0, 255),   // orange
            new Color32(0, 158, 115, 255),   // bluish green
            new Color32(204, 121, 167, 255), // reddish purple
            new Color32(240, 228, 66, 255)   // yellow
        };

        public static Color PlayerColor(int index, bool accessible)
        {
            var colors = accessible ? AccessiblePlayers : Players;
            return colors[((index % colors.Length) + colors.Length) % colors.Length];
        }

        public static Color PlayerColor(int index) =>
            PlayerColor(index, DisplaySettings.Active?.accessiblePlayerColors == true);

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

        // Relative luminance of opaque sRGB colors, not gamma-space brightness.
        static float LinearChannel(float channel)
        {
            channel = Mathf.Clamp01(channel);
            return channel <= 0.04045f ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        public static float RelativeLuminance(Color color) =>
            0.2126f * LinearChannel(color.r) + 0.7152f * LinearChannel(color.g) + 0.0722f * LinearChannel(color.b);

        public static float ContrastRatio(Color first, Color second)
        {
            float a = RelativeLuminance(first), b = RelativeLuminance(second);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }

        public static Color TextOn(Color background)
        {
            var dark = new Color(0.1f, 0.1f, 0.1f);
            if (ContrastRatio(background, Color.white) >= ContrastRatio(background, Color.black))
                return Color.white;
            return ContrastRatio(background, dark) >= 4.5f ? dark : Color.black;
        }

        public static string CardTitle(Resource resource)
        {
            if (LocalizationService.Language == "en")
            {
                switch (resource)
                {
                    case Resource.Asphalt: return "Asphalt";
                    case Resource.Concrete: return "Concrete";
                    case Resource.Steel: return "Steel";
                    case Resource.Gravel: return "Gravel";
                    case Resource.Machinery: return "Machinery";
                }
            }
            string name = ResourceInfo.RussianName(resource);
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
