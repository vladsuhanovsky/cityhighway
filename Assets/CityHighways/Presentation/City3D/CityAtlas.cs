using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Текстурный атлас 3D-города 512 × 512: сетка 8 × 8 плиток по 64 пикселя. Все здания, деревья и мосты
    /// используют один материал — каждая грань берёт свою плитку (фасад с окнами, крышу, листву).
    /// Вторая текстура — карта свечения: ночью горят окна (случайно, тёплым и холодным светом).
    /// Ряды (снизу вверх):
    /// 0 — фасады в 3 этажа, 1 — в 6 этажей, 2 — высотки (12 этажей, стекло), 3 — промышленные,
    /// 4 — крыши, 5 — листва и стволы, 6 — мосты, гранит, шпили, кирпич,
    /// 7 — архитектурные стили карт: китайские стены и черепица (Чунцин), кирпичные фасады каналов (Амстердам).
    /// </summary>
    public static class CityAtlas
    {
        public const int Size = 512, Tile = 64, Columns = 8;

        static Texture2D _albedo, _emission;

        public static Texture2D Albedo { get { Build(); return _albedo; } }
        public static Texture2D Emission { get { Build(); return _emission; } }

        public enum Row { Low = 0, Mid = 1, Tall = 2, Industrial = 3, Roof = 4, Nature = 5, Misc = 6, Style = 7 }

        // Плитки ряда «Природа» и «Разное».
        public const int Leaf0 = 0, Leaf1 = 1, Leaf2 = 2, Leaf3 = 3, LeafAutumn = 4, Spruce = 5, Trunk = 6, LeafDark = 7;
        public const int BridgeConcrete = 0, BridgeSteel = 1, Railing = 2, Granite = 3, Spire = 4, Brick = 5, ChimneyStripe = 6, Asphalt = 7;
        // Крыши.
        public const int RoofDark = 0, RoofGravel = 1, RoofLight = 2, RoofRed = 3, RoofBrown = 4, RoofCopper = 5, RoofMetal = 6, RoofBlack = 7;
        // Ряд стилей: китайская белёная стена с красными решётками, красная стена, серая и зелёная глазурованная
        // черепица; кирпичные фасады каналов — коричневый, красный, тёмный; красный лак (колонны, балки).
        public const int ChinaWhite = 0, ChinaRed = 1, ChinaTileGray = 2, ChinaTileGreen = 3,
            CanalBrown = 4, CanalRed = 5, CanalDark = 6, Lacquer = 7;

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

            ChinaWall(albedo, emission, ChinaWhite, new Color(0.90f, 0.88f, 0.82f), new Color(0.62f, 0.16f, 0.12f), random);
            ChinaWall(albedo, emission, ChinaRed, new Color(0.64f, 0.18f, 0.14f), new Color(0.78f, 0.62f, 0.30f), random);
            ChinaTiles(albedo, ChinaTileGray, new Color(0.30f, 0.32f, 0.34f));
            ChinaTiles(albedo, ChinaTileGreen, new Color(0.22f, 0.46f, 0.40f));
            CanalFacade(albedo, emission, CanalBrown, new Color(0.46f, 0.30f, 0.22f), random);
            CanalFacade(albedo, emission, CanalRed, new Color(0.60f, 0.27f, 0.20f), random);
            CanalFacade(albedo, emission, CanalDark, new Color(0.24f, 0.23f, 0.24f), random);
            Fill(albedo, Row.Style, Lacquer, new Color(0.70f, 0.14f, 0.10f), random, 0.03f);

            ApplyGeneratedTextures(albedo, roofs, misc);

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

        /// <summary>
        /// Текстуры из Scenario (Resources/CityHighways/City3D/Textures), если они есть: черепица — на скатные крыши
        /// (красную, коричневую, медную, светлую), асфальт — на плитку асфальта, брусчатка — на гранит набережных.
        /// Берётся рисунок (яркость относительно средней), а цвет — прежний цвет плитки, поэтому город не меняет
        /// палитру, а получает фактуру. Плитка 64 px, поэтому текстура ужимается до 4 повторов рисунка на плитку.
        /// </summary>
        static void ApplyGeneratedTextures(Color[] albedo, Color[] roofs, Color[] misc)
        {
            var roof = Pattern(GeneratedArt.RoofTexture, 2);
            if (roof != null)
                foreach (int column in new[] { RoofLight, RoofRed, RoofBrown, RoofCopper })
                    Imprint(albedo, Row.Roof, column, roof, roofs[column], 0.9f);
            var asphalt = Pattern(GeneratedArt.AsphaltTexture, 1);
            if (asphalt != null) Imprint(albedo, Row.Misc, Asphalt, asphalt, misc[Asphalt], 0.7f);
            var paving = Pattern(GeneratedArt.PavingTexture, 2);
            if (paving != null) Imprint(albedo, Row.Misc, Granite, paving, misc[Granite], 0.8f);
        }

        /// <summary>Рисунок текстуры в плитку Tile × Tile: repeats × repeats повторов; яркость, нормированная к 1.</summary>
        static float[] Pattern(string path, int repeats)
        {
            var texture = GeneratedArt.Load(path);
            if (texture == null) return null;
            int side = Tile / repeats;
            var pixels = GeneratedArt.ReadPixels(texture, side, side);
            if (pixels == null) return null;
            float mean = 0f;
            var luminance = new float[side * side];
            for (int i = 0; i < pixels.Length; i++)
            {
                luminance[i] = pixels[i].r * 0.3f + pixels[i].g * 0.59f + pixels[i].b * 0.11f;
                mean += luminance[i];
            }
            mean = Mathf.Max(0.01f, mean / pixels.Length);
            var pattern = new float[Tile * Tile];
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
                pattern[y * Tile + x] = luminance[(y % side) * side + x % side] / mean;
            return pattern;
        }

        static void Imprint(Color[] albedo, Row row, int column, float[] pattern, Color color, float strength)
        {
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float k = Mathf.Lerp(1f, pattern[y * Tile + x], strength);
                Set(albedo, row, column, x, y, new Color(Mathf.Clamp01(color.r * k), Mathf.Clamp01(color.g * k), Mathf.Clamp01(color.b * k), 1f));
            }
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

        /// <summary>
        /// Китайская стена: стена, серый цоколь, по краям — колонны цвета lattice, два этажа окон-решёток
        /// (переплёт в клетку), сверху — тёмная балка под карнизом.
        /// </summary>
        static void ChinaWall(Color[] albedo, Color[] emission, int column, Color wall, Color lattice, System.Random random)
        {
            const int floors = 2, windows = 3;
            var lit = new bool[floors, windows];
            for (int f = 0; f < floors; f++)
            for (int w = 0; w < windows; w++) lit[f, w] = random.NextDouble() < 0.5;
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                var color = wall * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(y / 14f));
                var glow = Color.black;
                if (y < 6) color = new Color(0.46f, 0.46f, 0.46f);                       // цоколь
                else if (y >= Tile - 5) color = new Color(0.28f, 0.12f, 0.10f);           // балка под карнизом
                else if (x < 4 || x >= Tile - 4) color = lattice * 0.95f;                 // колонны по краям
                else
                {
                    float floorH = (Tile - 11f) / floors, colW = (Tile - 8f) / windows;
                    int f = Mathf.Min(floors - 1, (int)((y - 6) / floorH)), w = Mathf.Min(windows - 1, (int)((x - 4) / colW));
                    float fy = (y - 6) - f * floorH, fx = (x - 4) - w * colW;
                    if (fy > floorH * 0.2f && fy < floorH * 0.85f && fx > colW * 0.18f && fx < colW * 0.82f)
                    {
                        // Окно-решётка: переплёт в клетку на тёмном стекле.
                        bool bar = ((int)fx % 4 == 0) || ((int)fy % 4 == 0);
                        color = bar ? lattice : new Color(0.20f, 0.17f, 0.15f);
                        if (!bar && lit[f, w]) glow = new Color(1f, 0.62f, 0.32f);
                    }
                }
                color.a = 1f;
                glow.a = 1f;
                Set(albedo, Row.Style, column, x, y, color);
                Set(emission, Row.Style, column, x, y, glow);
            }
        }

        /// <summary>Глазурованная черепица: желобки вдоль ската (полосы по x), блики на гребнях, ряды по y.</summary>
        static void ChinaTiles(Color[] albedo, int column, Color tile)
        {
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float groove = Mathf.Abs(Mathf.Sin(x * Mathf.PI / 4f));                 // полукруглые желобки
                float k = Mathf.Lerp(0.72f, 1.18f, groove) * (y % 8 == 0 ? 0.82f : 1f);
                Set(albedo, Row.Style, column, x, y, new Color(Mathf.Clamp01(tile.r * k), Mathf.Clamp01(tile.g * k), Mathf.Clamp01(tile.b * k), 1f));
            }
        }

        /// <summary>
        /// Фасад дома на канале: кирпич (ряды кладки), четыре этажа по два высоких окна в белых рамах,
        /// внизу — дверь и цокольное окно, белый карниз сверху.
        /// </summary>
        static void CanalFacade(Color[] albedo, Color[] emission, int column, Color brick, System.Random random)
        {
            const int floors = 4, windows = 2;
            var lit = new bool[floors, windows];
            for (int f = 0; f < floors; f++)
            for (int w = 0; w < windows; w++) lit[f, w] = random.NextDouble() < 0.5;
            var frame = new Color(0.94f, 0.93f, 0.90f);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float mortar = (y % 3 == 0 || (x + (y / 3 % 2) * 3) % 6 == 0) ? 0.86f : 1f;
                var color = brick * mortar * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(y / 12f));
                var glow = Color.black;
                if (y >= Tile - 3) color = frame;                                        // белый карниз
                else
                {
                    float floorH = (Tile - 5f) / floors, colW = Tile / (float)windows;
                    int f = Mathf.Min(floors - 1, (int)((y - 2) / floorH)), w = Mathf.Min(windows - 1, (int)(x / colW));
                    float fy = (y - 2) - f * floorH, fx = x - w * colW;
                    bool inside = fy > floorH * 0.14f && fy < floorH * 0.9f && fx > colW * 0.22f && fx < colW * 0.78f;
                    bool rim = fy > floorH * 0.1f && fy < floorH * 0.94f && fx > colW * 0.17f && fx < colW * 0.83f;
                    if (rim && !inside) color = frame;
                    else if (inside)
                    {
                        bool bar = Mathf.Abs(fx - colW / 2f) < 0.6f || Mathf.Abs(fy - floorH * 0.55f) < 0.6f;
                        color = bar ? frame : new Color(0.18f, 0.22f, 0.27f);
                        if (!bar && lit[f, w]) glow = new Color(1f, 0.82f, 0.52f);
                    }
                }
                color.a = 1f;
                glow.a = 1f;
                Set(albedo, Row.Style, column, x, y, color);
                Set(emission, Row.Style, column, x, y, glow);
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
