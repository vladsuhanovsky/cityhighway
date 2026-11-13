using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Местность под дорогами в духе тактических карт MegaMek, только без шестиугольников: трава с пятнами,
    /// поля за МКАД, кварталы с крышами (вид сверху, с тенью), парки и лесополосы из крон деревьев.
    /// Одна текстура на всё поле, запекается кодом при первом показе и при заметной смене размера окна.
    /// Застройка и деревья обходят дороги, места и реку. Шум — детерминированный хеш: карта всегда одна и та же.
    /// Превью этих же правил: Tools~/preview_terrain.py.
    /// </summary>
    public static class TerrainTexture
    {
        /// <summary>Текстура покрывает координаты карты [-Margin, 1 + Margin] — с запасом под отступы поля.</summary>
        public const float Margin = 0.12f;
        public static readonly Color GrassDark = new Color(0.29f, 0.40f, 0.17f);
        static readonly Color GrassLight = new Color(0.45f, 0.55f, 0.25f);
        static readonly Color Field = new Color(0.62f, 0.60f, 0.34f);
        static readonly Color Pavement = new Color(0.50f, 0.50f, 0.46f);
        static readonly Color[] Roofs =
        {
            new Color(0.60f, 0.60f, 0.58f), new Color(0.52f, 0.33f, 0.27f), new Color(0.46f, 0.40f, 0.33f),
            new Color(0.40f, 0.44f, 0.48f), new Color(0.66f, 0.62f, 0.52f)
        };

        /// <summary>Парки рядом с местами карты: id места → радиус в долях высоты поля.</summary>
        public static readonly Dictionary<string, float> Parks = new Dictionary<string, float>
        {
            { "sokolniki", 0.045f }, { "izmaylovo", 0.055f }, { "kolomenskoe", 0.045f },
            { "mgu", 0.04f }, { "vdnh", 0.04f }, { "kuzminki", 0.045f }, { "luzhniki", 0.03f },
            { "losiny", 0.065f }, { "tsaritsyno", 0.045f }, { "fili", 0.03f }, { "troparevo", 0.04f }
        };

        const int MaxWidth = 2048;
        static Texture2D _cached;
        static CityMap _cachedMap;
        static int _cachedW, _cachedH;

        /// <summary>
        /// Текстура для поля, где карта (0..1) занимает innerWidth × innerHeight экранных пикселей,
        /// а всё поле — fullHeight в высоту. Пересобирается, только если размер изменился больше чем на 12%.
        /// </summary>
        public static Texture2D For(CityMap map, float innerWidth, float innerHeight, float fullHeight)
        {
            float span = 1f + 2f * Margin;
            int w = Mathf.Max(64, Mathf.RoundToInt(innerWidth * span));
            int h = Mathf.Max(64, Mathf.RoundToInt(innerHeight * span));
            if (_cached != null && _cachedMap == map &&
                Mathf.Abs(w - _cachedW) < _cachedW * 0.12f && Mathf.Abs(h - _cachedH) < _cachedH * 0.12f)
                return _cached;

            float k = 1f;                               // пикселей текстуры на экранный пиксель
            if (w > MaxWidth) { k = MaxWidth / (float)w; w = MaxWidth; h = Mathf.RoundToInt(h * k); }
            if (_cached != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_cached);
                else UnityEngine.Object.DestroyImmediate(_cached);
            }
            _cached = Bake(map, w, h, k, fullHeight * k);
            _cachedMap = map;
            _cachedW = Mathf.RoundToInt(w / k);
            _cachedH = Mathf.RoundToInt(h / k);
            return _cached;
        }

        // ================================================================== Шум

        static float Hash(long x, long y, int salt)
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

        static float ValueNoise(float x, float y, float cell, int salt)
        {
            float gx = x / cell, gy = y / cell;
            float ix = Mathf.Floor(gx), iy = Mathf.Floor(gy);
            float fx = gx - ix, fy = gy - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            long lx = (long)ix, ly = (long)iy;
            float a = Hash(lx, ly, salt), b = Hash(lx + 1, ly, salt);
            float c = Hash(lx, ly + 1, salt), d = Hash(lx + 1, ly + 1, salt);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        static float Fbm(float x, float y, int salt) =>
            ValueNoise(x, y, 90f, salt) * 0.5f + ValueNoise(x, y, 34f, salt + 1) * 0.3f + ValueNoise(x, y, 11f, salt + 2) * 0.2f;

        // ================================================================== Запекание

        sealed class Canvas
        {
            public readonly Color[] Pixels;
            public readonly int W, H;
            public Canvas(int w, int h) { W = w; H = h; Pixels = new Color[w * h]; }

            /// <summary>y — вниз, как на экране; в текстуре Unity строки идут снизу вверх.</summary>
            public void Blend(int x, int y, Color c, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
                int i = (H - 1 - y) * W + x;
                var o = Pixels[i];
                Pixels[i] = new Color(o.r + (c.r - o.r) * a, o.g + (c.g - o.g) * a, o.b + (c.b - o.b) * a, 1f);
            }

            public void Rect(float x0, float y0, float x1, float y1, Color c, float a = 1f)
            {
                for (int y = Mathf.RoundToInt(y0); y < Mathf.RoundToInt(y1); y++)
                for (int x = Mathf.RoundToInt(x0); x < Mathf.RoundToInt(x1); x++)
                    Blend(x, y, c, a);
            }

            public void Disc(float cx, float cy, float r, Color c, float a = 1f)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= r + 0.5f) Blend(x, y, c, a * Mathf.Clamp01(r - d + 0.5f));
                }
            }
        }

        struct Seg { public Vector2 A, B; }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        static float Nearest(Vector2 p, List<Seg> segments)
        {
            float best = float.MaxValue;
            foreach (var s in segments) best = Mathf.Min(best, DistanceToSegment(p, s.A, s.B));
            return best;
        }

        static Texture2D Bake(CityMap map, int w, int h, float k, float fullHeight)
        {
            float span = 1f + 2f * Margin;
            Vector2 ToPixel(float x, float y) => new Vector2((x + Margin) / span * w, (y + Margin) / span * h);
            // Положение относительно МКАД: 0 в центре, 1 на кольце (эллипс 0.40 × 0.49 вокруг 0.5, 0.5).
            float Ring(float px, float py)
            {
                float mx = (px / w * span - Margin - 0.5f) / 0.40f, my = (py / h * span - Margin - 0.5f) / 0.49f;
                return Mathf.Sqrt(mx * mx + my * my);
            }
            float Urban(float e) => Mathf.Clamp01((1.08f - e) / 0.55f);

            var canvas = new Canvas(w, h);

            // ---- Земля: трава пятнами, поля за городом, к центру земля сереет.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Urban(Ring(x, y));
                var c = Color.Lerp(GrassDark, GrassLight, Fbm(x, y, 11));
                float field = Mathf.Clamp01((ValueNoise(x, y, 70f, 21) - 0.62f) / 0.08f) * (1f - u);
                c = Color.Lerp(c, Field, field * 0.8f);
                float grey = Mathf.Clamp01(u * 0.85f + (ValueNoise(x, y, 40f, 31) - 0.5f) * 0.5f) * 0.75f;
                c = Color.Lerp(c, Pavement, grey);
                float grain = (Hash(x, y, 5) - 0.5f) * 0.04f;
                canvas.Pixels[(h - 1 - y) * w + x] = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
            }

            // ---- Что обходить: дороги, места, река.
            var points = new Dictionary<string, Vector2>();
            foreach (var l in map.Locations) points[l.Id] = ToPixel(l.X, l.Y);
            var roads = new List<Seg>();
            foreach (var r in map.Roads)
                if (points.TryGetValue(r.A, out var a) && points.TryGetValue(r.B, out var b)) roads.Add(new Seg { A = a, B = b });
            var river = new List<Seg>();
            for (int i = 0; i + 1 < map.River.Count; i++)
                river.Add(new Seg { A = ToPixel(map.River[i].X, map.River[i].Y), B = ToPixel(map.River[i + 1].X, map.River[i + 1].Y) });
            float NearestPlace(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (var q in points.Values) best = Mathf.Min(best, (p - q).magnitude);
                return best;
            }

            // Парки — неровные пятна из трёх кругов, как раньше рисовались зелёные пятна.
            var parks = new List<Vector3[]>();
            foreach (var pair in Parks)
            {
                if (!points.TryGetValue(pair.Key, out var c)) continue;
                float r = pair.Value * fullHeight;
                parks.Add(new[]
                {
                    new Vector3(c.x + r * 0.35f, c.y - r * 0.2f, r),
                    new Vector3(c.x - r * 0.4f, c.y + r * 0.25f, r * 0.8f),
                    new Vector3(c.x + r * 0.1f, c.y + r * 0.45f, r * 0.65f)
                });
            }
            bool InBlob(Vector3[] blob, Vector2 p, float pad)
            {
                foreach (var c in blob)
                    if ((p - new Vector2(c.x, c.y)).magnitude < c.z + pad) return true;
                return false;
            }
            bool InPark(Vector2 p, float pad)
            {
                foreach (var blob in parks)
                    if (InBlob(blob, p, pad)) return true;
                return false;
            }

            // ---- Кварталы: крыши сверху с тенью, улицы между кварталами.
            float cell = 13f * k;
            int line = Mathf.Max(1, Mathf.RoundToInt(k));
            int cols = Mathf.CeilToInt(w / cell), rows = Mathf.CeilToInt(h / cell);
            var shadow = new Color(0.04f, 0.06f, 0.02f);
            for (int iy = 0; iy <= rows; iy++)
            for (int ix = 0; ix <= cols; ix++)
            {
                var p = new Vector2((ix + 0.5f + (Hash(ix, iy, 4) - 0.5f) * 0.15f) * cell,
                                    (iy + 0.5f + (Hash(ix, iy, 5) - 0.5f) * 0.15f) * cell);
                float e = Ring(p.x, p.y), u = Urban(e);
                float density = Mathf.Min(1f, u * 1.1f) + (e > 1.08f ? 0.05f : 0f);
                if (u > 0.15f && (ix % 4 == 0 || iy % 3 == 0)) continue;   // улицы
                if (Hash(ix, iy, 1) > density) continue;
                if (e > 0.97f && e < 1.05f) continue;                         // полоса МКАД
                if (Nearest(p, roads) < 13f * k || NearestPlace(p) < 24f * k || Nearest(p, river) < 22f * k || InPark(p, 4f * k)) continue;

                float bw = cell * (0.55f + Hash(ix, iy, 6) * 0.42f), bh = cell * (0.55f + Hash(ix, iy, 7) * 0.42f);
                var roof = Roofs[Mathf.Min(Roofs.Length - 1, (int)(Hash(ix, iy, 8) * Roofs.Length))] * (0.9f + Hash(ix, iy, 9) * 0.2f);
                float x0 = p.x - bw / 2f, y0 = p.y - bh / 2f, x1 = p.x + bw / 2f, y1 = p.y + bh / 2f;
                canvas.Rect(x0 + 2f * k, y0 + 2f * k, x1 + 2f * k, y1 + 2f * k, shadow, 0.37f);
                canvas.Rect(x0, y0, x1, y1, roof);
                canvas.Rect(x0, y0, x1, y0 + line, Lighten(roof, 1.25f));            // освещённые края
                canvas.Rect(x0, y0, x0 + line, y1, Lighten(roof, 1.18f));
                canvas.Rect(x0, y1 - line, x1, y1, roof * 0.70f);                   // теневые края
                canvas.Rect(x1 - line, y0, x1, y1, roof * 0.75f);
                if (Hash(ix, iy, 10) < 0.45f)                                       // конёк крыши
                {
                    if (bw > bh) canvas.Rect(x0 + 2f * k, p.y, x1 - 2f * k, p.y + line, roof * 0.82f);
                    else canvas.Rect(p.x, y0 + 2f * k, p.x + line, y1 - 2f * k, roof * 0.82f);
                }
            }

            // ---- Парки: тёмная подложка и кроны, сверху вниз (нижние кроны перекрывают верхние).
            var forestFloor = new Color(0.15f, 0.24f, 0.09f);
            var trees = new List<Vector3>();
            for (int pi = 0; pi < parks.Count; pi++)
            {
                var blob = parks[pi];
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var c in blob)
                {
                    canvas.Disc(c.x, c.y, c.z, forestFloor, 0.67f);
                    minX = Mathf.Min(minX, c.x - c.z); maxX = Mathf.Max(maxX, c.x + c.z);
                    minY = Mathf.Min(minY, c.y - c.z); maxY = Mathf.Max(maxY, c.y + c.z);
                }
                for (int j = 0; j < 1400; j++)
                {
                    var p = new Vector2(minX + Hash(pi, j, 40) * (maxX - minX), minY + Hash(pi, j, 41) * (maxY - minY));
                    if (!InBlob(blob, p, -3f * k) || Nearest(p, roads) < 10f * k || NearestPlace(p) < 16f * k || Nearest(p, river) < 16f * k) continue;
                    trees.Add(new Vector3(p.x, p.y, (3.2f + Hash(pi, j, 42) * 2.6f) * k));
                }
            }
            // Лесополосы за МКАД — там, где шум «леса» высок.
            float treeCell = 11f * k;
            for (int iy = 0; iy <= Mathf.CeilToInt(h / treeCell); iy++)
            for (int ix = 0; ix <= Mathf.CeilToInt(w / treeCell); ix++)
            {
                var p = new Vector2((ix + 0.5f + (Hash(ix, iy, 50) - 0.5f) * 0.8f) * treeCell,
                                    (iy + 0.5f + (Hash(ix, iy, 51) - 0.5f) * 0.8f) * treeCell);
                if (Ring(p.x, p.y) < 1.06f) continue;
                if (ValueNoise(p.x, p.y, 60f * k, 52) < 0.55f || Hash(ix, iy, 53) > 0.8f) continue;
                if (Nearest(p, roads) < 12f * k || NearestPlace(p) < 18f * k || Nearest(p, river) < 16f * k) continue;
                trees.Add(new Vector3(p.x, p.y, (3f + Hash(ix, iy, 54) * 2.5f) * k));
            }
            trees.Sort((a, b) => a.y.CompareTo(b.y));
            foreach (var t in trees) Tree(canvas, t.x, t.y, t.z);

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "CityHighways_Terrain",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(canvas.Pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Color Lighten(Color c, float f) => new Color(Mathf.Min(1f, c.r * f), Mathf.Min(1f, c.g * f), Mathf.Min(1f, c.b * f), 1f);

        /// <summary>Крона: тень на юго-восток, тёмная листва, светлая шапка и блик с северо-запада.</summary>
        static void Tree(Canvas canvas, float x, float y, float r)
        {
            float tone = 0.85f + Hash((long)x, (long)y, 43) * 0.3f;
            canvas.Disc(x + r * 0.35f, y + r * 0.45f, r, new Color(0.05f, 0.09f, 0.02f), 0.43f);
            canvas.Disc(x, y, r, new Color(0.16f, 0.31f, 0.12f) * tone);
            canvas.Disc(x - r * 0.2f, y - r * 0.25f, r * 0.58f, new Color(0.24f, 0.41f, 0.16f) * tone);
            canvas.Disc(x - r * 0.28f, y - r * 0.33f, r * 0.24f, new Color(0.35f, 0.51f, 0.22f) * tone, 0.8f);
        }
    }
}
