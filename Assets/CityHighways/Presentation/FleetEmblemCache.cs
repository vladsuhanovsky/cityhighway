using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Bounded scene/preview-owned procedural badge artwork. Only known catalog
    /// IDs can create textures; the caller owns this cache and clears it after its models.</summary>
    public sealed class FleetEmblemCache
    {
        public const int TextureSize = 36;
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, string[]> Patterns = new Dictionary<string, string[]>
        {
            { "emblem_star", new[] { "....#....", "....#....", "...###...", "#########", ".#######.", "..#####..", "..##.##..", ".##...##.", "........." } },
            { "emblem_diamond", new[] { "....#....", "...###...", "..#####..", ".#######.", "#########", ".#######.", "..#####..", "...###...", "....#...." } },
            { "emblem_gear", new[] { "..#.#.#..", ".#######.", "#########", ".##...##.", "###...###", ".##...##.", "#########", ".#######.", "..#.#.#.." } },
            { "emblem_flag", new[] { ".#.......", ".###.....", ".######..", ".########", ".######..", ".###.....", ".#.......", ".#.......", ".#......." } },
            { "emblem_bolt", new[] { ".....##..", "....##...", "...##....", "..##.....", ".######..", "....##...", "...##....", "..##.....", ".##......" } },
            { "emblem_crown", new[] { ".........", "#...#...#", "##..#..##", "##.###.##", "#########", ".#######.", ".#######.", ".#######.", "........." } },
            { "emblem_truck", new[] { ".........", ".........", "#####....", "#####.##.", "#####.###", "#########", "#########", ".##...##.", "........." } },
            { "emblem_cup", new[] { ".........", "#######..", "#.###.#..", "#.###.#..", ".#####...", "..###....", "...#.....", "...#.....", ".#####..." } },
        };
        public int Count => textures.Count;
        public static bool Supports(string id) => !string.IsNullOrEmpty(id) && Patterns.ContainsKey(id);

        public Texture2D Get(string id)
        {
            if (!Supports(id)) return null;
            if (textures.TryGetValue(id, out var cached) && cached != null) return cached;
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
                {
                    name = "Fleet badge " + id, hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
                };
                var pixels = new Color32[TextureSize * TextureSize];
                var background = new Color32(24, 34, 45, 255);
                var ink = new Color32(255, 213, 122, 255);
                var pattern = Patterns[id];
                for (int y = 0; y < TextureSize; y++)
                    for (int x = 0; x < TextureSize; x++)
                    {
                        bool inside = x >= 4 && x < 32 && y >= 4 && y < 32;
                        int column = Mathf.Min(8, (x - 4) * 9 / 28);
                        int row = Mathf.Min(8, (31 - y) * 9 / 28);
                        pixels[y * TextureSize + x] = inside && pattern[row][column] == '#' ? ink : background;
                    }
                texture.SetPixels32(pixels);
                texture.Apply(true, true);
                textures[id] = texture;
                return texture;
            }
            catch
            {
                Remove(texture);
                return null;
            }
        }

        public void Clear()
        {
            foreach (var texture in textures.Values) Remove(texture);
            textures.Clear();
        }

        static void Remove(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture);
        }
    }
}
