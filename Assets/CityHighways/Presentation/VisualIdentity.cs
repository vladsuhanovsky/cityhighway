using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Stable presentation identities independent of current colors.
    /// Future accessible themes can map these to glyphs and patterns.
    /// No gameplay or serialization values are changed.
    /// </summary>
    public static class VisualIdentity
    {
        public enum Pattern { Solid, Stripes, Dots, Crosshatch, Zigzag }

        public static string ResourceId(Resource resource)
        {
            switch (resource)
            {
                case Resource.Asphalt: return "resource.asphalt";
                case Resource.Concrete: return "resource.concrete";
                case Resource.Steel: return "resource.steel";
                case Resource.Gravel: return "resource.gravel";
                case Resource.Machinery: return "resource.machinery";
                default: return "resource.unknown";
            }
        }

        public static Pattern ResourcePattern(Resource resource)
        {
            switch (resource)
            {
                case Resource.Asphalt: return Pattern.Solid;
                case Resource.Concrete: return Pattern.Stripes;
                case Resource.Steel: return Pattern.Dots;
                case Resource.Gravel: return Pattern.Crosshatch;
                default: return Pattern.Zigzag;
            }
        }

        public static string PlayerId(int index) => "player." + (index + 1);

        public static Pattern PlayerPattern(int index)
        {
            Pattern[] patterns = { Pattern.Solid, Pattern.Stripes, Pattern.Dots, Pattern.Crosshatch, Pattern.Zigzag };
            return patterns[((index % patterns.Length) + patterns.Length) % patterns.Length];
        }
    }
}
