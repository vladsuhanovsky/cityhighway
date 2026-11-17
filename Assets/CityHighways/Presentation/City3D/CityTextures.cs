using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Процедурные текстуры второго шага 3D-города — всё рисуется кодом при первом обращении:
    /// карточки дальних деревьев, атлас декалей (парковки, поля, площадки), карта неровностей земли,
    /// мелкая «деталь» земли (неровности и пятна, повторяются по полю) и рябь воды.
    /// </summary>
    public static class CityTextures
    {
        // ================================================================== Шум

        static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static int Wrap(int v, int period) => period > 0 ? ((v % period) + period) % period : v;

        /// <summary>Сглаженный шум по клеткам; period &gt; 0 — повторяется через period клеток (для плиточных текстур).</summary>
        static float Noise(float x, float y, int period, int salt)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = Wrap(ix, period), x1 = Wrap(ix + 1, period), y0 = Wrap(iy, period), y1 = Wrap(iy + 1, period);
            float a = Hash(x0, y0, salt), b = Hash(x1, y0, salt), c = Hash(x0, y1, salt), d = Hash(x1, y1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int period, int octaves, int salt)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(x, y, period, salt + o * 17) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2f;
                y *= 2f;
                if (period > 0) period *= 2;
            }
            return sum / total;
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Texture2D New(string name, int width, int height, bool linear, TextureWrapMode wrap)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };
        }

        /// <summary>Нормали из поля высот (строка j растёт на север = +v текстуры). Кодируются как RGB, A = 1.</summary>
        static Color32[] NormalsFromHeight(float[] h, int width, int height, float strength, bool tile)
        {
            var pixels = new Color32[width * height];
            int Index(int x, int y)
            {
                if (tile) { x = Wrap(x, width); y = Wrap(y, height); }
                else { x = Mathf.Clamp(x, 0, width - 1); y = Mathf.Clamp(y, 0, height - 1); }
                return y * width + x;
            }
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (h[Index(x + 1, y)] - h[Index(x - 1, y)]) * strength;
                float dy = (h[Index(x, y + 1)] - h[Index(x, y - 1)]) * strength;
                var n = new Vector3(-dx, -dy, 1f).normalized;
                pixels[y * width + x] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }
            return pixels;
        }

        static Color32 ToColor32(Color c) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f));

        // ================================================================== Карточки деревьев

        /// <summary>Плитки карточек 4 × 4 по 64 px: круглые кроны 4 оттенков, тополя 4 оттенков, две ели, осеннее дерево.</summary>
        public const int CardRound = 0, CardPoplar = 4, CardSpruce = 8, CardSpruceDark = 9, CardAutumn = 10;

        // Те же цвета, что у листвы 3D-деревьев в CityAtlas (ряд Nature).
        static readonly Color[] Leaves =
        {
            new Color(0.24f, 0.44f, 0.18f), new Color(0.30f, 0.52f, 0.22f), new Color(0.20f, 0.38f, 0.16f), new Color(0.36f, 0.56f, 0.26f)
        };
        static readonly Color SpruceColor = new Color(0.14f, 0.30f, 0.18f), SpruceDark = new Color(0.14f, 0.26f, 0.12f);
        static readonly Color Autumn = new Color(0.78f, 0.58f, 0.20f), Trunk = new Color(0.36f, 0.26f, 0.18f);

        static Texture2D _cards;

        public static Rect CardUv(int tile)
        {
            const float t = 0.25f, inset = 0.5f / 256f;
            int col = tile % 4, row = tile / 4;
            return new Rect(col * t + inset, row * t + inset, t - 2f * inset, t - 2f * inset);
        }

        public static Texture2D TreeCards
        {
            get
            {
                if (_cards != null) return _cards;
                const int size = 256, tile = 64;
                var pixels = new Color32[size * size];
                for (int index = 0; index < 11; index++)
                {
                    int col = index % 4, row = index / 4;
                    for (int py = 0; py < tile; py++)
                    for (int px = 0; px < tile; px++)
                    {
                        float u = (px + 0.5f) / tile, v = (py + 0.5f) / tile;
                        pixels[(row * tile + py) * size + col * tile + px] = ToColor32(CardPixel(index, u, v));
                    }
                }
                _cards = New("City3D Tree Cards", size, size, false, TextureWrapMode.Clamp);
                _cards.mipMapBias = -0.6f; // дальние карточки не «худеют» на мип-уровнях
                _cards.SetPixels32(pixels);
                _cards.Apply(true, false);
                return _cards;
            }
        }

        /// <summary>Пиксель карточки: силуэт дерева при взгляде сверху-сбоку, свет сверху, тёмные края кроны.</summary>
        static Color CardPixel(int index, float u, float v)
        {
            Color leaf = index < 4 ? Leaves[index] : index < 8 ? Leaves[index - 4]
                : index == CardSpruce ? SpruceColor : index == CardSpruceDark ? SpruceDark : Autumn;
            float lumps = (Noise(u * 9f + index * 3.1f, v * 9f, 0, 91) - 0.5f) * 0.22f;
            float shade = -1f;   // < 0 — не крона
            bool trunk = false;

            if (index == CardSpruce || index == CardSpruceDark)
            {
                trunk = Mathf.Abs(u - 0.5f) < 0.04f && v < 0.2f;
                float[] bases = { 0.15f, 0.42f, 0.68f }, widths = { 0.32f, 0.24f, 0.15f }, apexes = { 0.65f, 0.84f, 0.99f };
                for (int t = 0; t < 3; t++)
                {
                    if (v < bases[t] || v > apexes[t]) continue;
                    float k = (apexes[t] - v) / (apexes[t] - bases[t]);
                    if (Mathf.Abs(u - 0.5f) <= widths[t] * k)
                        shade = 0.72f + 0.38f * (1f - k) + 0.12f * (0.5f - u) + lumps * 0.5f;
                }
            }
            else
            {
                bool poplar = index >= CardPoplar && index < CardSpruce;
                float cy = poplar ? 0.58f : 0.62f, rx = poplar ? 0.16f : 0.31f, ry = poplar ? 0.38f : 0.31f;
                trunk = Mathf.Abs(u - 0.5f) < (poplar ? 0.03f : 0.04f) && v < (poplar ? 0.25f : 0.36f);
                float dx = (u - 0.5f) / rx, dy = (v - cy) / ry;
                float angle = Mathf.Atan2(dy, dx);
                float edge = 1f + 0.07f * Mathf.Sin(angle * 5f + index) + lumps * 0.4f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < edge)
                {
                    float top = Mathf.Clamp01((v - (cy - ry)) / (2f * ry));
                    shade = 0.70f + 0.42f * top - 0.10f * (u - 0.5f) + lumps;
                    shade *= 1f - 0.28f * Smooth(0.65f, 1f, d / edge);
                }
            }

            if (shade >= 0f)
            {
                var c = leaf * Mathf.Clamp(shade, 0.45f, 1.25f);
                c.a = 1f;
                return c;
            }
            if (trunk) return new Color(Trunk.r, Trunk.g, Trunk.b, 1f);
            // Прозрачный край — с цветом листвы, чтобы на мип-уровнях не было тёмной каймы.
            return new Color(leaf.r * 0.8f, leaf.g * 0.8f, leaf.b * 0.8f, 0f);
        }

        // ================================================================== Декали

        static Texture2D _decals;

        public static Rect DecalUv(CityPlanner.DecalKind kind)
        {
            const float t = 0.25f, inset = 2f / 512f;
            int index = (int)kind, col = index % 4, row = index / 4;
            return new Rect(col * t + inset, row * t + inset, t - 2f * inset, t - 2f * inset);
        }

        public static Texture2D Decals
        {
            get
            {
                if (_decals != null) return _decals;
                const int size = 512, tile = 128;
                var pixels = new Color32[size * size];
                int count = System.Enum.GetValues(typeof(CityPlanner.DecalKind)).Length;
                for (int index = 0; index < count; index++)
                {
                    int col = index % 4, row = index / 4;
                    var kind = (CityPlanner.DecalKind)index;
                    for (int py = 0; py < tile; py++)
                    for (int px = 0; px < tile; px++)
                    {
                        float u = (px + 0.5f) / tile, v = (py + 0.5f) / tile;
                        pixels[(row * tile + py) * size + col * tile + px] = ToColor32(DecalPixel(kind, u, v, px, py));
                    }
                }
                _decals = New("City3D Decals", size, size, false, TextureWrapMode.Clamp);
                _decals.SetPixels32(pixels);
                _decals.Apply(true, false);
                return _decals;
            }
        }

        /// <summary>Мягкий край прямоугольной декали: 0 на кромке, 1 — дальше feather от неё.</summary>
        static float Edge(float u, float v, float feather) =>
            Smooth(0f, feather, Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)));

        static bool LineAt(float value, float at, float width) => Mathf.Abs(value - at) < width;

        static readonly Color[] CarColors =
        {
            new Color(0.82f, 0.82f, 0.84f), new Color(0.16f, 0.17f, 0.20f), new Color(0.70f, 0.18f, 0.16f),
            new Color(0.22f, 0.36f, 0.62f), new Color(0.56f, 0.58f, 0.60f), new Color(0.90f, 0.78f, 0.30f)
        };

        static Color DecalPixel(CityPlanner.DecalKind kind, float u, float v, int px, int py)
        {
            float grain = Hash(px, py, 7 + (int)kind) - 0.5f;
            float blotch = Fbm(u * 5f, v * 5f, 0, 3, 31 + (int)kind) - 0.5f;
            var white = new Color(0.92f, 0.92f, 0.88f);
            Color c;
            float alpha;
            switch (kind)
            {
                case CityPlanner.DecalKind.Parking:
                {
                    c = new Color(0.25f, 0.26f, 0.28f) * (1f + grain * 0.10f + blotch * 0.25f);
                    bool upper = v > 0.56f && v < 0.94f, lower = v > 0.06f && v < 0.44f;
                    if ((upper || lower) && (u > 0.04f && u < 0.96f))
                    {
                        float stall = u * 8f;
                        int index = Mathf.FloorToInt(stall);
                        float local = stall - index;
                        if (local < 0.07f || local > 0.93f) c = white;
                        else if (Hash(index, upper ? 1 : 0, 61) < 0.62f)
                        {
                            // Машина в месте: скруглённый прямоугольник цвета кузова, светлое стекло посередине.
                            float sv = upper ? (v - 0.56f) / 0.38f : (v - 0.06f) / 0.38f;
                            if (local > 0.2f && local < 0.8f && sv > 0.12f && sv < 0.88f)
                            {
                                var body = CarColors[Mathf.FloorToInt(Hash(index, upper ? 3 : 2, 62) * CarColors.Length) % CarColors.Length];
                                c = (sv > 0.42f && sv < 0.62f) ? Color.Lerp(body, new Color(0.55f, 0.66f, 0.76f), 0.6f) : body;
                            }
                        }
                    }
                    if (LineAt(v, 0.5f, 0.008f) && Mathf.Repeat(u * 10f, 1f) < 0.5f) c = white;
                    alpha = Edge(u, v, 0.03f);
                    break;
                }
                case CityPlanner.DecalKind.Pitch:
                {
                    var g1 = new Color(0.24f, 0.52f, 0.22f); var g2 = new Color(0.29f, 0.58f, 0.25f);
                    c = (Mathf.FloorToInt(u * 12f) % 2 == 0 ? g1 : g2) * (1f + grain * 0.06f + blotch * 0.12f);
                    bool inField = u > 0.05f && u < 0.95f && v > 0.07f && v < 0.93f;
                    float lw = 0.008f;
                    bool line = (inField && (LineAt(u, 0.05f, lw) || LineAt(u, 0.95f, lw) || LineAt(v, 0.07f, lw * 1.4f) || LineAt(v, 0.93f, lw * 1.4f)))
                                || (inField && LineAt(u, 0.5f, lw))
                                || Mathf.Abs(Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f) * 0.45f) - 0.11f) < lw;
                    for (int s = 0; s < 2; s++)
                    {
                        float x0 = s == 0 ? 0.05f : 0.81f, x1 = s == 0 ? 0.19f : 0.95f;
                        if (u >= x0 && u <= x1 && (LineAt(v, 0.28f, lw * 1.4f) || LineAt(v, 0.72f, lw * 1.4f))) line = true;
                        if (v >= 0.28f && v <= 0.72f && LineAt(u, s == 0 ? x1 : x0, lw)) line = true;
                    }
                    if (line) c = white;
                    alpha = Edge(u, v, 0.02f);
                    break;
                }
                case CityPlanner.DecalKind.Court:
                {
                    bool inner = u > 0.12f && u < 0.88f && v > 0.16f && v < 0.84f;
                    c = inner ? new Color(0.24f, 0.42f, 0.62f) : new Color(0.72f, 0.38f, 0.26f);
                    c *= 1f + grain * 0.05f + blotch * 0.08f;
                    float lw = 0.007f;
                    bool line = inner && (LineAt(u, 0.12f, lw) || LineAt(u, 0.88f, lw) || LineAt(v, 0.16f, lw) || LineAt(v, 0.84f, lw)
                                          || LineAt(u, 0.5f, lw * 1.6f) || LineAt(v, 0.5f, lw)
                                          || (LineAt(u, 0.3f, lw) || LineAt(u, 0.7f, lw)));
                    if (line) c = white;
                    alpha = Edge(u, v, 0.02f);
                    break;
                }
                case CityPlanner.DecalKind.Plaza:
                {
                    int tx = Mathf.FloorToInt(u * 14f), ty = Mathf.FloorToInt(v * 14f);
                    float tint = (Hash(tx, ty, 71) - 0.5f) * 0.08f;
                    c = new Color(0.74f, 0.70f, 0.62f) * (1f + tint + grain * 0.04f + blotch * 0.1f);
                    if (Mathf.Repeat(u * 14f, 1f) < 0.07f || Mathf.Repeat(v * 14f, 1f) < 0.07f) c *= 0.84f;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                    if (Mathf.Abs(r - 0.24f) < 0.025f) c = new Color(0.56f, 0.52f, 0.48f);
                    if (r < 0.08f) c = Color.Lerp(new Color(0.40f, 0.58f, 0.70f), white, Smooth(0.065f, 0.08f, r));
                    alpha = Edge(u, v, 0.04f);
                    break;
                }
                case CityPlanner.DecalKind.Playground:
                {
                    c = new Color(0.84f, 0.76f, 0.56f) * (1f + grain * 0.08f + blotch * 0.12f);
                    Vector2[] centers = { new Vector2(0.3f, 0.32f), new Vector2(0.68f, 0.62f), new Vector2(0.32f, 0.72f) };
                    Color[] rubber = { new Color(0.78f, 0.30f, 0.26f), new Color(0.36f, 0.62f, 0.34f), new Color(0.30f, 0.48f, 0.74f) };
                    float[] radii = { 0.18f, 0.2f, 0.12f };
                    for (int i = 0; i < 3; i++)
                        if ((new Vector2(u, v) - centers[i]).magnitude < radii[i]) c = rubber[i] * (1f + grain * 0.05f);
                    alpha = Edge(u, v, 0.08f);
                    break;
                }
                case CityPlanner.DecalKind.Dirt:
                {
                    c = new Color(0.45f, 0.36f, 0.26f) * (1f + grain * 0.12f + blotch * 0.35f);
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) + blotch * 0.18f;
                    alpha = (1f - Smooth(0.22f, 0.48f, r)) * 0.9f;
                    break;
                }
                case CityPlanner.DecalKind.FlowerBed:
                {
                    c = new Color(0.30f, 0.22f, 0.16f) * (1f + grain * 0.1f);
                    int cx = px / 6, cy = py / 6;
                    float pick = Hash(cx, cy, 81);
                    float lx = (px % 6) - 2.5f, ly = (py % 6) - 2.5f;
                    if (lx * lx + ly * ly < 4.5f)
                    {
                        if (pick < 0.12f) c = new Color(0.86f, 0.20f, 0.22f);
                        else if (pick < 0.24f) c = new Color(0.95f, 0.82f, 0.24f);
                        else if (pick < 0.34f) c = new Color(0.92f, 0.52f, 0.70f);
                        else if (pick < 0.42f) c = new Color(0.94f, 0.94f, 0.92f);
                        else if (pick < 0.50f) c = new Color(0.56f, 0.36f, 0.78f);
                        else if (pick < 0.80f) c = new Color(0.26f, 0.48f, 0.20f);
                    }
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                    alpha = 1f - Smooth(0.40f, 0.47f, r);
                    break;
                }
                case CityPlanner.DecalKind.Wear:
                {
                    c = new Color(0.20f, 0.20f, 0.21f) * (1f + grain * 0.1f);
                    float streak = Noise(u * 3f, v * 40f, 0, 95);
                    alpha = Edge(u, v, 0.25f) * Mathf.Lerp(0.25f, 0.6f, Fbm(u * 4f, v * 4f, 0, 3, 96)) * (0.75f + 0.35f * streak);
                    break;
                }
                case CityPlanner.DecalKind.FieldWheat:
                    c = new Color(0.80f, 0.68f, 0.34f) * (0.92f + 0.10f * Mathf.Sin(v * Mathf.PI * 2f * 16f) + grain * 0.05f + blotch * 0.2f);
                    alpha = Edge(u, v, 0.025f) * 0.95f;
                    break;
                case CityPlanner.DecalKind.FieldSoil:
                    c = new Color(0.46f, 0.34f, 0.24f) * (0.90f + 0.14f * Mathf.Sin(v * Mathf.PI * 2f * 20f) + grain * 0.06f + blotch * 0.2f);
                    alpha = Edge(u, v, 0.025f) * 0.95f;
                    break;
                default: // FieldGreen
                    c = new Color(0.40f, 0.56f, 0.26f) * (0.92f + 0.10f * Mathf.Sin(u * Mathf.PI * 2f * 14f) + grain * 0.05f + blotch * 0.2f);
                    alpha = Edge(u, v, 0.025f) * 0.95f;
                    break;
            }
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        // ================================================================== Неровности земли

        static Texture2D _groundNormal;
        static CityMap _groundNormalMap;

        /// <summary>
        /// Крупные неровности всей доски: пологие холмы, мелкая волна и холмики парков. Та же развёртка, что у
        /// текстуры местности (x и y карты от −Margin до 1 + Margin).
        /// </summary>
        public static Texture2D GroundNormal(CityMap map)
        {
            if (_groundNormal != null && _groundNormalMap == map) return _groundNormal;
            if (_groundNormal != null) Object.Destroy(_groundNormal);
            const float m = TerrainTexture.Margin, span = 1f + 2f * m;
            const int width = 1024, height = 576;

            var parks = new List<Vector3>();
            foreach (var pair in TerrainTexture.Parks)
            {
                var location = map.GetLocation(pair.Key);
                if (location == null) continue;
                parks.Add(new Vector3(location.X, location.Y, pair.Value * 900f / CityPlanner.FieldW));
            }

            var h = new float[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float mx = (x + 0.5f) / width * span - m;
                float my = (1f - (y + 0.5f) / height) * span - m;
                float value = 0.7f * Fbm(mx * 7f, my * 4f, 0, 3, 11) + 0.25f * Fbm(mx * 34f, my * 19f, 0, 2, 23);
                foreach (var park in parks)
                {
                    float dx = (mx - park.x) / park.z, dy = (my - park.y) * (CityPlanner.FieldH / CityPlanner.FieldW) / park.z;
                    value += 0.35f * Mathf.Exp(-(dx * dx + dy * dy) * 2.2f);
                }
                h[y * width + x] = value;
            }
            _groundNormal = New("City3D Ground Normal", width, height, true, TextureWrapMode.Clamp);
            _groundNormal.SetPixels32(NormalsFromHeight(h, width, height, 38f, false));
            _groundNormal.Apply(true, false);
            _groundNormalMap = map;
            return _groundNormal;
        }

        // ================================================================== Окрестности

        /// <summary>Полусторона квадрата окрестностей (м сцены) — текстура Outskirts покрывает [−Extent, Extent]².</summary>
        public const float OutskirtsExtent = 30f;
        static Texture2D _outskirts;
        static CityMap _outskirtsMap;

        /// <summary>
        /// Земля вокруг доски: трава пятнами и лоскуты полей (те же цвета, что у местности доски), тёмная лесная
        /// подстилка там, где CityPlanner сажает леса, светлые пятна у деревень. У края доски всё сходит в
        /// TerrainTexture.SeamColor (туда же сходит и доска) — шва не видно. Вдали цвет ровнеет к дымке.
        /// </summary>
        public static Texture2D Outskirts(CityMap map)
        {
            if (_outskirts != null && _outskirtsMap == map) return _outskirts;
            if (_outskirts != null) Object.Destroy(_outskirts);
            const int size = 1024;
            const float m = TerrainTexture.Margin;
            float boardX = BoardLayout.WorldWidth * (0.5f + m), boardZ = BoardLayout.WorldDepth * (0.5f + m);
            var seam = TerrainTexture.SeamColor;
            var forestFloor = new Color(0.20f, 0.30f, 0.14f);
            var villageGround = new Color(0.52f, 0.54f, 0.40f);
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = ((px + 0.5f) / size * 2f - 1f) * OutskirtsExtent;
                float z = ((py + 0.5f) / size * 2f - 1f) * OutskirtsExtent;
                // Пиксели поля (как у CityPlanner): x вправо, y вниз (на юг).
                float fx = (x + BoardLayout.WorldWidth / 2f) * CityPlanner.PixelsPerUnit;
                float fy = (BoardLayout.WorldDepth / 2f - z) * CityPlanner.PixelsPerUnit;
                var c = Color.Lerp(TerrainTexture.GrassDark, TerrainTexture.GrassLight, Fbm(fx / 90f, fy / 90f, 0, 3, 401));
                float field = Mathf.Clamp01((Noise(fx / 260f, fy / 260f, 0, 402) - 0.6f) / 0.08f);
                c = Color.Lerp(c, TerrainTexture.Field, field * 0.6f);
                float forest = Mathf.Clamp01((CityPlanner.ForestNoise(fx, fy) - (CityPlanner.ForestLevel - 0.05f)) / 0.07f);
                c = Color.Lerp(c, forestFloor, forest * 0.85f);
                float village = Mathf.Clamp01((CityPlanner.VillageNoise(fx, fy) - (CityPlanner.VillageLevel - 0.03f)) / 0.06f);
                c = Color.Lerp(c, villageGround, village * 0.5f * (1f - forest));
                // Шов с доской: в пределах ~0,7 м от края доски — цвет шва.
                float dx = Mathf.Abs(x) - boardX, dz = Mathf.Abs(z) - boardZ;
                float outside = Mathf.Max(dx, dz);
                c = Color.Lerp(seam, c, Mathf.Clamp01((outside + 0.05f) / 0.7f));
                // Вдали — ровнее (дымка тумана всё равно скрывает детали).
                float far = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - 18f) / 12f);
                c = Color.Lerp(c, seam, far * 0.6f);
                float grain = (Hash(px, py, 403) - 0.5f) * 0.03f;
                pixels[py * size + px] = ToColor32(new Color(c.r + grain, c.g + grain, c.b + grain, 1f));
            }
            _outskirts = New("City3D Outskirts", size, size, false, TextureWrapMode.Clamp);
            _outskirts.SetPixels32(pixels);
            _outskirts.Apply(true, false);
            _outskirtsMap = map;
            return _outskirts;
        }

        static Texture2D _detailNormal, _detailAlbedo, _waves;

        /// <summary>Мелкие неровности земли (повторяющаяся плитка).</summary>
        public static Texture2D DetailNormal
        {
            get
            {
                if (_detailNormal != null) return _detailNormal;
                const int size = 256;
                var h = new float[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    h[y * size + x] = Fbm(x / 32f, y / 32f, 8, 4, 41) + (Hash(x, y, 42) - 0.5f) * 0.05f;
                _detailNormal = New("City3D Ground Detail Normal", size, size, true, TextureWrapMode.Repeat);
                _detailNormal.SetPixels32(NormalsFromHeight(h, size, size, 5f, true));
                _detailNormal.Apply(true, false);
                return _detailNormal;
            }
        }

        /// <summary>Пятна земли: серый около 0,5 (множитель ×2 в шейдере Lit) с лёгкими тёплыми и холодными оттенками.</summary>
        public static Texture2D DetailAlbedo
        {
            get
            {
                if (_detailAlbedo != null) return _detailAlbedo;
                const int size = 256;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float value = 0.5f + (Fbm(x / 64f, y / 64f, 4, 3, 51) - 0.5f) * 0.20f + (Hash(x, y, 52) - 0.5f) * 0.05f;
                    float tint = (Fbm(x / 128f, y / 128f, 2, 2, 53) - 0.5f) * 0.12f;
                    pixels[y * size + x] = ToColor32(new Color(value * (1f + tint), value, value * (1f - tint), 1f));
                }
                _detailAlbedo = New("City3D Ground Detail", size, size, false, TextureWrapMode.Repeat);
                _detailAlbedo.SetPixels32(pixels);
                _detailAlbedo.Apply(true, false);
                return _detailAlbedo;
            }
        }

        /// <summary>Рябь воды: волны с целым числом периодов на плитку плюс шум — плитка повторяется без шва.</summary>
        public static Texture2D WaveNormal
        {
            get
            {
                if (_waves != null) return _waves;
                const int size = 256;
                var h = new float[size * size];
                int[,] waves = { { 3, 1 }, { -2, 4 }, { 5, -2 }, { 1, 6 }, { -7, 3 } };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size, value = 0f;
                    for (int i = 0; i < waves.GetLength(0); i++)
                        value += Mathf.Sin(2f * Mathf.PI * (waves[i, 0] * u + waves[i, 1] * v) + i * 1.3f) / (1f + i * 0.6f);
                    value = value * 0.12f + Fbm(u * 8f, v * 8f, 8, 3, 61) * 0.6f;
                    h[y * size + x] = value;
                }
                _waves = New("City3D Water Waves", size, size, true, TextureWrapMode.Repeat);
                _waves.SetPixels32(NormalsFromHeight(h, size, size, 9f, true));
                _waves.Apply(true, false);
                return _waves;
            }
        }
    }
}
