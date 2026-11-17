using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Текстурный атлас 3D-города 512 × 512: сетка 8 × 8 плиток по 64 пикселя. Все здания, деревья и мосты
    /// используют один материал — каждая грань берёт свою плитку (фасад с окнами, крышу, листву).
    /// Вторая текстура — карта свечения: ночью горят окна (случайно, тёплым и холодным светом).
    /// Ряды (снизу вверх):
    /// 0 — фасады в 3 этажа, 1 — в 6 этажей, 2 — высотки (12 этажей, стекло), 3 — промышленные,
    /// 4 — крыши, 5 — листва и стволы, 6 — мосты, гранит, шпили, кирпич.
    /// </summary>
    public static class CityAtlas
    {
        public const int Size = 512, Tile = 64, Columns = 8;

        static Texture2D _albedo, _emission;

        public static Texture2D Albedo { get { Build(); return _albedo; } }
        public static Texture2D Emission { get { Build(); return _emission; } }

        public enum Row { Low = 0, Mid = 1, Tall = 2, Industrial = 3, Roof = 4, Nature = 5, Misc = 6 }

        // Плитки ряда «Природа» и «Разное».
        public const int Leaf0 = 0, Leaf1 = 1, Leaf2 = 2, Leaf3 = 3, LeafAutumn = 4, Spruce = 5, Trunk = 6, LeafDark = 7;
        public const int BridgeConcrete = 0, BridgeSteel = 1, Railing = 2, Granite = 3, Spire = 4, Brick = 5, ChimneyStripe = 6, Asphalt = 7;
        // Крыши.
        public const int RoofDark = 0, RoofGravel = 1, RoofLight = 2, RoofRed = 3, RoofBrown = 4, RoofCopper = 5, RoofMetal = 6, RoofBlack = 7;

        /// <summary>Прямоугольник плитки в UV (с отступом в 1 пиксель от краёв, чтобы соседние плитки не просвечивали).</summary>
        public static Rect Uv(Row row, int column)
        {
            column = Mathf.Clamp(column, 0, Columns - 1);
            float x = column * Tile + 1f, y = (int)row * Tile + 1f;
            return new Rect(x / Size, y / Size, (Tile - 2f) / Size, (Tile - 2f) / Size);
        }

        /// <summary>Центр плитки сплошного цвета — для мелких граней (кроны, крыши, опоры).</summary>
        public static Rect Solid(Row row, int column)
        {
            var r = Uv(row, column);
            return new Rect(r.center.x - 0.5f / Size, r.center.y - 0.5f / Size, 1f / Size, 1f / Size);
        }

        static void Build()
        {
            if (_albedo != null && _emission != null) return;
            var albedo = new Color[Size * Size];
            var emission = new Color[Size * Size];
            var random = new System.Random(20261117);

            var walls = new[]
            {
                new Color(0.86f, 0.80f, 0.68f), new Color(0.92f, 0.91f, 0.88f), new Color(0.66f, 0.66f, 0.66f),
                new Color(0.66f, 0.34f, 0.26f), new Color(0.82f, 0.72f, 0.52f), new Color(0.90f, 0.86f, 0.62f),
                new Color(0.58f, 0.62f, 0.68f), new Color(0.76f, 0.58f, 0.46f)
            };
            var glass = new[]
            {
                new Color(0.34f, 0.52f, 0.68f), new Color(0.36f, 0.58f, 0.56f), new Color(0.20f, 0.24f, 0.30f),
                new Color(0.70f, 0.70f, 0.68f), new Color(0.82f, 0.76f, 0.60f), new Color(0.90f, 0.90f, 0.88f),
                new Color(0.52f, 0.40f, 0.32f), new Color(0.60f, 0.66f, 0.72f)
            };
            var industrial = new[]
            {
                new Color(0.62f, 0.62f, 0.60f), new Color(0.55f, 0.60f, 0.66f), new Color(0.68f, 0.56f, 0.44f),
                new Color(0.48f, 0.50f, 0.52f), new Color(0.74f, 0.70f, 0.60f), new Color(0.58f, 0.36f, 0.30f),
                new Color(0.40f, 0.52f, 0.46f), new Color(0.80f, 0.80f, 0.78f)
            };
            var roofs = new[]
            {
                new Color(0.28f, 0.29f, 0.31f), new Color(0.50f, 0.50f, 0.48f), new Color(0.70f, 0.70f, 0.68f),
                new Color(0.62f, 0.26f, 0.20f), new Color(0.46f, 0.32f, 0.24f), new Color(0.40f, 0.62f, 0.52f),
                new Color(0.66f, 0.70f, 0.74f), new Color(0.14f, 0.15f, 0.17f)
            };
            var nature = new[]
            {
                new Color(0.24f, 0.44f, 0.18f), new Color(0.30f, 0.52f, 0.22f), new Color(0.20f, 0.38f, 0.16f),
                new Color(0.36f, 0.56f, 0.26f), new Color(0.78f, 0.58f, 0.20f), new Color(0.14f, 0.30f, 0.18f),
                new Color(0.36f, 0.26f, 0.18f), new Color(0.14f, 0.26f, 0.12f)
            };
            var misc = new[]
            {
                new Color(0.70f, 0.69f, 0.66f), new Color(0.38f, 0.48f, 0.60f), new Color(0.90f, 0.90f, 0.88f),
                new Color(0.62f, 0.60f, 0.56f), new Color(0.92f, 0.76f, 0.30f), new Color(0.62f, 0.30f, 0.22f),
                new Color(0.86f, 0.24f, 0.20f), new Color(0.24f, 0.25f, 0.27f)
            };

            for (int c = 0; c < Columns; c++)
            {
                Facade(albedo, emission, Row.Low, c, walls[c], 3, 4, random, false);
                Facade(albedo, emission, Row.Mid, c, walls[(c + 3) % walls.Length] * 0.97f, 6, 4, random, false);
                Facade(albedo, emission, Row.Tall, c, glass[c], 12, 5, random, true);
                IndustrialFacade(albedo, emission, c, industrial[c], random);
                Fill(albedo, Row.Roof, c, roofs[c], random, 0.05f);
                Fill(albedo, Row.Nature, c, nature[c], random, 0.06f);
                Fill(albedo, Row.Misc, c, misc[c], random, 0.03f);
            }
            // Полоски красно-белой трубы.
            for (int y = 0; y < Tile; y++)
            {
                var stripe = (y / 8) % 2 == 0 ? misc[ChimneyStripe] : new Color(0.94f, 0.94f, 0.92f);
                for (int x = 0; x < Tile; x++) Set(albedo, Row.Misc, ChimneyStripe, x, y, stripe);
            }

            _albedo = Make("CityHighways_CityAtlas", albedo);
            _emission = Make("CityHighways_CityAtlasEmission", emission);
        }

        static Texture2D Make(string name, Color[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static void Set(Color[] pixels, Row row, int column, int x, int y, Color color)
        {
            int px = column * Tile + x, py = (int)row * Tile + y;
            pixels[py * Size + px] = color;
        }

        static void Fill(Color[] pixels, Row row, int column, Color color, System.Random random, float grain)
        {
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float g = ((float)random.NextDouble() - 0.5f) * grain;
                Set(pixels, row, column, x, y, new Color(color.r + g, color.g + g, color.b + g, 1f));
            }
        }

        /// <summary>
        /// Фасад: стена с лёгким градиентом (низ темнее — как будто тень у земли), сетка окон, карниз сверху.
        /// Окна в карте свечения горят случайно — тёплым или холодным светом.
        /// </summary>
        static void Facade(Color[] albedo, Color[] emission, Row row, int column, Color wall, int floors, int windows,
            System.Random random, bool curtainWall)
        {
            var glassDay = curtainWall ? Color.Lerp(wall, new Color(0.62f, 0.78f, 0.92f), 0.45f) : new Color(0.24f, 0.30f, 0.38f);
            float floorH = (Tile - 6f) / floors;
            float colW = Tile / (float)windows;
            var lit = new bool[floors, windows];
            var warm = new bool[floors, windows];
            for (int f = 0; f < floors; f++)
            for (int w = 0; w < windows; w++)
            {
                lit[f, w] = random.NextDouble() < (curtainWall ? 0.55 : 0.45);
                warm[f, w] = random.NextDouble() < 0.75;
            }
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(y / 14f));      // тень у основания
                var color = wall * shade;
                var glow = Color.black;
                if (y >= Tile - 4) color = wall * 0.78f;                          // карниз
                else if (y >= 2)
                {
                    int f = Mathf.Min(floors - 1, (int)((y - 2) / floorH));
                    int w = Mathf.Min(windows - 1, (int)(x / colW));
                    float fy = (y - 2) - f * floorH, fx = x - w * colW;
                    bool window = curtainWall
                        ? fy > floorH * 0.22f && fy < floorH * 0.9f
                        : fy > floorH * 0.28f && fy < floorH * 0.82f && fx > colW * 0.22f && fx < colW * 0.78f;
                    if (window)
                    {
                        color = glassDay * (curtainWall ? Mathf.Lerp(0.9f, 1.1f, fx / colW) : 1f);
                        if (lit[f, w]) glow = warm[f, w] ? new Color(1f, 0.80f, 0.48f) : new Color(0.78f, 0.88f, 1f);
                    }
                }
                color.a = 1f;
                glow.a = 1f;
                Set(albedo, row, column, x, y, color);
                Set(emission, row, column, x, y, glow);
            }
        }

        /// <summary>Промышленный фасад: профлист (вертикальные полосы), полоса окон под крышей, ворота внизу.</summary>
        static void IndustrialFacade(Color[] albedo, Color[] emission, int column, Color wall, System.Random random)
        {
            int gateFrom = 10 + random.Next(20), gateTo = gateFrom + 14;
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                var color = wall * (x % 4 == 0 ? 0.86f : 1f) * Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(y / 12f));
                var glow = Color.black;
                if (y > Tile - 16 && y < Tile - 8 && x % 8 > 1)
                {
                    color = new Color(0.30f, 0.36f, 0.42f);
                    if (x % 16 < 8) glow = new Color(0.95f, 0.85f, 0.60f);
                }
                if (y < 22 && x >= gateFrom && x < gateTo) color = wall * 0.55f;
                color.a = 1f;
                glow.a = 1f;
                Set(albedo, Row.Industrial, column, x, y, color);
                Set(emission, Row.Industrial, column, x, y, glow);
            }
        }
    }
}
