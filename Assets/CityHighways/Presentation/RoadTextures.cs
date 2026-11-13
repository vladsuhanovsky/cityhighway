using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Текстуры покрытий, сгенерированные кодом при первом обращении (художника нет): асфальт с зерном,
    /// бетонные плиты со швами, щебень с камешками, рифлёный стальной настил моста с заклёпками,
    /// брусчатка для «любых» дорог и вода с волнами. Детерминированный шум — каждый запуск одинаков.
    /// Все текстуры квадратные (кроме воды 2:1) и рисуются «плиткой» — по одной на ширину дороги.
    /// </summary>
    public static class RoadTextures
    {
        const int Size = 64;
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D For(Road road)
        {
            if (!road.Resource.HasValue) return Cobble;
            switch (road.Resource.Value)
            {
                case Resource.Asphalt: return Asphalt;
                case Resource.Concrete: return Concrete;
                case Resource.Steel: return SteelDeck;
                case Resource.Gravel: return Gravel;
                default: return Asphalt;
            }
        }

        public static Texture2D Asphalt => Get("asphalt", Size, Size, MakeAsphalt);
        public static Texture2D Concrete => Get("concrete", Size, Size, MakeConcrete);
        public static Texture2D Gravel => Get("gravel", Size, Size, MakeGravel);
        public static Texture2D SteelDeck => Get("steel", Size, Size, MakeSteel);
        public static Texture2D Cobble => Get("cobble", Size, Size, MakeCobble);
        public static Texture2D Water => Get("water", 128, 64, MakeWater);

        static Texture2D Get(string key, int w, int h, Action<Color[], int, int, System.Random> fill)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var pixels = new Color[w * h];
            fill(pixels, w, h, new System.Random(key.GetHashCode() & 0x7fffffff ^ 1234567));
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "CityHighways_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        // ================================================================== Генераторы

        static float Noise(System.Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;

        static void Blend(Color[] px, int w, int h, int x, int y, Color c, float a)
        {
            x = ((x % w) + w) % w;
            y = ((y % h) + h) % h;
            int i = y * w + x;
            px[i] = Color.Lerp(px[i], c, a);
        }

        static void Disc(Color[] px, int w, int h, float cx, float cy, float r, Color c, float alpha = 1f)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
            for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r) Blend(px, w, h, x, y, c, alpha * Mathf.Clamp01(r - d + 0.5f));
            }
        }

        /// <summary>Асфальт: тёмное зерно, светлые вкрапления, следы колёс чуть темнее.</summary>
        static void MakeAsphalt(Color[] px, int w, int h, System.Random rng)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float track = (Mathf.Abs(y - h * 0.3f) < 5 || Mathf.Abs(y - h * 0.7f) < 5) ? -0.025f : 0f;
                float v = 0.20f + track + Noise(rng, 0.035f);
                px[y * w + x] = new Color(v, v + 0.005f, v + 0.02f, 1f);
            }
            for (int i = 0; i < 90; i++)
            {
                float g = 0.32f + (float)rng.NextDouble() * 0.18f;
                Blend(px, w, h, rng.Next(w), rng.Next(h), new Color(g, g, g), 0.8f);
            }
            // Тонкая трещина.
            float cy = rng.Next(h), dir = Noise(rng, 0.6f);
            for (int x = 0; x < w; x += 1)
            {
                Blend(px, w, h, x, Mathf.RoundToInt(cy), new Color(0.08f, 0.08f, 0.09f), 0.5f);
                cy += dir + Noise(rng, 0.5f);
            }
        }

        /// <summary>Бетон: светло-серые плиты, тёмный шов поперёк дороги, пятна.</summary>
        static void MakeConcrete(Color[] px, int w, int h, System.Random rng)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0.62f + Noise(rng, 0.03f);
                px[y * w + x] = new Color(v, v, v - 0.02f, 1f);
            }
            for (int i = 0; i < 5; i++)
                Disc(px, w, h, rng.Next(w), rng.Next(h), 4f + (float)rng.NextDouble() * 6f, new Color(0.50f, 0.50f, 0.48f), 0.18f);
            for (int y = 0; y < h; y++)
            {
                Blend(px, w, h, 0, y, new Color(0.32f, 0.32f, 0.31f), 0.9f);
                Blend(px, w, h, 1, y, new Color(0.40f, 0.40f, 0.39f), 0.6f);
                Blend(px, w, h, w - 1, y, new Color(0.78f, 0.78f, 0.76f), 0.5f);
            }
            for (int x = 0; x < w; x++) Blend(px, w, h, x, h / 2, new Color(0.45f, 0.45f, 0.44f), 0.35f);
        }

        /// <summary>Щебень: бурая подложка и сотни камешков разного тона с бликом.</summary>
        static void MakeGravel(Color[] px, int w, int h, System.Random rng)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = Noise(rng, 0.04f);
                px[y * w + x] = new Color(0.36f + v, 0.29f + v, 0.21f + v, 1f);
            }
            for (int i = 0; i < 260; i++)
            {
                float r = 0.9f + (float)rng.NextDouble() * 1.9f;
                float tone = (float)rng.NextDouble();
                var c = Color.Lerp(new Color(0.55f, 0.47f, 0.36f), new Color(0.66f, 0.64f, 0.60f), tone) * (0.8f + 0.35f * (float)rng.NextDouble());
                c.a = 1f;
                float x = rng.Next(w), y = rng.Next(h);
                Disc(px, w, h, x + 0.6f, y + 0.8f, r, new Color(0.15f, 0.12f, 0.09f), 0.5f);
                Disc(px, w, h, x, y, r, c);
                Blend(px, w, h, Mathf.RoundToInt(x - r * 0.4f), Mathf.RoundToInt(y - r * 0.4f), Color.white, 0.35f);
            }
        }

        /// <summary>Стальной настил: сине-серые листы, рифление «ёлочкой», заклёпки у краёв.</summary>
        static void MakeSteel(Color[] px, int w, int h, System.Random rng)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = Noise(rng, 0.02f);
                px[y * w + x] = new Color(0.30f + v, 0.36f + v, 0.44f + v, 1f);
            }
            for (int gy = 4; gy < h; gy += 8)
            for (int gx = 4; gx < w; gx += 8)
            {
                bool flip = ((gx / 8) + (gy / 8)) % 2 == 0;
                for (int k = -2; k <= 2; k++)
                {
                    int x = gx + k, y = gy + (flip ? k : -k);
                    Blend(px, w, h, x, y, new Color(0.62f, 0.70f, 0.80f), 0.7f);
                    Blend(px, w, h, x + 1, y + 1, new Color(0.14f, 0.17f, 0.22f), 0.5f);
                }
            }
            for (int x = 0; x < w; x++)
            {
                Blend(px, w, h, x, 0, new Color(0.12f, 0.15f, 0.20f), 0.9f);
                Blend(px, w, h, x, h - 1, new Color(0.12f, 0.15f, 0.20f), 0.9f);
            }
            for (int x = 6; x < w; x += 12)
            {
                Disc(px, w, h, x, 3.5f, 1.4f, new Color(0.75f, 0.80f, 0.88f));
                Disc(px, w, h, x, h - 4.5f, 1.4f, new Color(0.75f, 0.80f, 0.88f));
            }
            for (int y = 0; y < h; y++) Blend(px, w, h, 0, y, new Color(0.12f, 0.15f, 0.20f), 0.8f);
        }

        /// <summary>Брусчатка: светлые камни вразбежку, тёмные швы.</summary>
        static void MakeCobble(Color[] px, int w, int h, System.Random rng)
        {
            const int stoneW = 16, stoneH = 11;
            for (int y = 0; y < h; y++)
            {
                int row = y / stoneH;
                int shift = row % 2 == 0 ? 0 : stoneW / 2;
                for (int x = 0; x < w; x++)
                {
                    int sx = (x + shift) % stoneW, sy = y % stoneH;
                    bool seam = sx == 0 || sy == 0;
                    int stone = (x + shift) / stoneW + row * 13;
                    float tone = 0.70f + ((stone * 37) % 11) / 11f * 0.12f + Noise(rng, 0.02f);
                    var c = seam ? new Color(0.30f, 0.30f, 0.32f) : new Color(tone, tone, tone - 0.03f);
                    if (!seam && (sx == 1 || sy == 1)) c *= 1.08f;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
        }

        /// <summary>Вода: ясная синева, темнее к берегам, светлые гребни волн и блики.</summary>
        static void MakeWater(Color[] px, int w, int h, System.Random rng)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = Mathf.Abs(y / (float)(h - 1) - 0.5f) * 2f;      // 0 в центре, 1 у берега
                float wave = Mathf.Sin(x / (float)w * Mathf.PI * 4f + Mathf.Sin(y * 0.35f) * 1.5f) * 0.5f + 0.5f;
                float ripple = Mathf.Sin(x / (float)w * Mathf.PI * 10f + y * 0.9f) * 0.5f + 0.5f;
                // Дневная вода в духе MegaMek: ясная синева, темнее у берегов, светлые гребни.
                var deep = new Color(0.17f, 0.38f, 0.62f);
                var bank = new Color(0.10f, 0.25f, 0.45f);
                var c = Color.Lerp(deep, bank, across * across);
                c += new Color(0.08f, 0.11f, 0.12f) * Mathf.Pow(wave, 6f) * (1f - across);
                c += new Color(0.05f, 0.07f, 0.08f) * Mathf.Pow(ripple, 10f);
                c += new Color(1f, 1f, 1f) * Noise(rng, 0.008f);
                c.a = 1f;
                px[y * w + x] = c;
            }
            for (int i = 0; i < 26; i++)
            {
                int x = rng.Next(w), y = rng.Next(h / 5, h - h / 5);
                int length = 3 + rng.Next(6);
                for (int k = 0; k < length; k++) Blend(px, w, h, x + k, y, new Color(0.80f, 0.90f, 1f), 0.45f * (1f - Mathf.Abs(k - length / 2f) / length * 2f));
            }
        }
    }

    /// <summary>
    /// Сборщик текстурированных четырёхугольников для UI Toolkit: копит вершины по текстурам и отдаёт их
    /// одним MeshGenerationContext.Allocate на текстуру. Учитывает uvRegion (текстура может лечь в атлас),
    /// поэтому UV каждого четырёхугольника — в пределах 0..1, без повтора.
    /// </summary>
    public sealed class TexturedQuads
    {
        sealed class Batch
        {
            public readonly List<Vertex> Vertices = new List<Vertex>();
            public readonly List<ushort> Indices = new List<ushort>();
        }

        readonly Dictionary<Texture2D, Batch> _batches = new Dictionary<Texture2D, Batch>();

        public void Clear() => _batches.Clear();

        /// <summary>Углы по часовой стрелке на экране (y вниз): a — левый верхний, b, c, d.</summary>
        public void Add(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Color tint)
        {
            if (!_batches.TryGetValue(texture, out var batch))
            {
                batch = new Batch();
                _batches[texture] = batch;
            }
            if (batch.Vertices.Count > 60000) return; // предел ushort — на нашей карте недостижим
            var start = (ushort)batch.Vertices.Count;
            Color32 color = tint;
            batch.Vertices.Add(new Vertex { position = new Vector3(a.x, a.y, Vertex.nearZ), uv = uvA, tint = color });
            batch.Vertices.Add(new Vertex { position = new Vector3(b.x, b.y, Vertex.nearZ), uv = uvB, tint = color });
            batch.Vertices.Add(new Vertex { position = new Vector3(c.x, c.y, Vertex.nearZ), uv = uvC, tint = color });
            batch.Vertices.Add(new Vertex { position = new Vector3(d.x, d.y, Vertex.nearZ), uv = uvD, tint = color });
            batch.Indices.Add(start);
            batch.Indices.Add((ushort)(start + 1));
            batch.Indices.Add((ushort)(start + 2));
            batch.Indices.Add(start);
            batch.Indices.Add((ushort)(start + 2));
            batch.Indices.Add((ushort)(start + 3));
        }

        /// <summary>Прямоугольник вдоль направления dir: центр, длина, толщина, UV по длине u0..u1.</summary>
        public void AddStrip(Texture2D texture, Vector2 center, Vector2 dir, float length, float thickness, float u0, float u1, Color tint)
        {
            var normal = new Vector2(-dir.y, dir.x);
            var half = dir * length / 2f;
            var side = normal * thickness / 2f;
            Add(texture, center - half - side, center + half - side, center + half + side, center - half + side,
                new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f), new Vector2(u0, 0f), tint);
        }

        public void Flush(MeshGenerationContext context)
        {
            foreach (var pair in _batches)
            {
                var batch = pair.Value;
                if (batch.Vertices.Count == 0) continue;
                var mesh = context.Allocate(batch.Vertices.Count, batch.Indices.Count, pair.Key);
                var region = mesh.uvRegion;
                var vertices = batch.Vertices.ToArray();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var uv = vertices[i].uv;
                    vertices[i].uv = new Vector2(region.x + uv.x * region.width, region.y + uv.y * region.height);
                }
                mesh.SetAllVertices(vertices);
                mesh.SetAllIndices(batch.Indices.ToArray());
            }
        }
    }
}
