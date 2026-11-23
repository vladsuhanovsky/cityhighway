using System;
using System.Globalization;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Advisory validation of manually entered benchmark labels.
    /// Does not inspect the procedural generator, camera transform or real seed.
    /// </summary>
    public static class City3DPerformanceScenarioPreflight
    {
        public struct Result
        {
            public bool HasWarnings;
            public string Warnings;
            public bool HasDeclaredSeed;
            public int DeclaredSeed;
            public bool HasDeclaredViewport;
            public int ViewportWidth;
            public int ViewportHeight;
        }

        public static Result Check(string label, City3DPerformanceEnvironment environment)
        {
            var result = new Result();
            if (string.IsNullOrWhiteSpace(label))
            {
                Add(ref result, "Scenario label is empty.");
                return result;
            }
            bool seenSeed = false, seenViewport = false, seenCamera = false;
            foreach (var raw in label.Split(';'))
            {
                var token = raw.Trim();
                int equals = token.IndexOf('=');
                if (equals < 0) continue;
                string key = token.Substring(0, equals).Trim();
                string value = token.Substring(equals + 1).Trim();
                if (string.Equals(key, "seed", StringComparison.OrdinalIgnoreCase))
                {
                    if (seenSeed) { Add(ref result, "Duplicate seed key."); continue; }
                    seenSeed = true;
                    if (int.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var seed))
                    {
                        result.HasDeclaredSeed = true;
                        result.DeclaredSeed = seed;
                    }
                    else Add(ref result, "Seed must be a signed 32-bit integer.");
                }
                else if (string.Equals(key, "viewport", StringComparison.OrdinalIgnoreCase))
                {
                    if (seenViewport) { Add(ref result, "Duplicate viewport key."); continue; }
                    seenViewport = true;
                    string[] size = value.ToLowerInvariant().Split('x');
                    if (size.Length == 2 &&
                        int.TryParse(size[0], NumberStyles.None,
                            CultureInfo.InvariantCulture, out var width) &&
                        int.TryParse(size[1], NumberStyles.None,
                            CultureInfo.InvariantCulture, out var height) &&
                        width > 0 && height > 0)
                    {
                        result.HasDeclaredViewport = true;
                        result.ViewportWidth = width;
                        result.ViewportHeight = height;
                        if (environment != null && environment.IsValid() &&
                            (environment.width != width || environment.height != height))
                            Add(ref result, "Declared viewport does not match actual capture resolution.");
                    }
                    else Add(ref result, "Viewport must use positive WIDTHxHEIGHT integers.");
                }
                else if (string.Equals(key, "camera", StringComparison.OrdinalIgnoreCase))
                {
                    if (seenCamera) Add(ref result, "Duplicate camera key.");
                    seenCamera = true;
                    if (string.IsNullOrWhiteSpace(value)) Add(ref result, "Camera label is empty.");
                }
            }
            if (!seenSeed) Add(ref result, "No seed key in scenario label; generator seed is unverified.");
            if (!seenCamera) Add(ref result, "No camera key in scenario label.");
            if (!seenViewport) Add(ref result, "No viewport key in scenario label.");
            return result;
        }

        static void Add(ref Result result, string message)
        {
            result.HasWarnings = true;
            result.Warnings = string.IsNullOrEmpty(result.Warnings)
                ? message : result.Warnings + "\n" + message;
        }
    }
}
