using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Быстрый «цветной меш»: прямоугольники, треугольники и полосы с цветом в каждой вершине — для градиентов
    /// и сотен окон без тесселяции Painter2D. Сбрасывается в MeshGenerationContext кусками до 60 000 вершин.
    /// </summary>
    public sealed class ColorMesh
    {
        readonly List<Vertex> _vertices = new List<Vertex>();
        readonly List<ushort> _indices = new List<ushort>();
        readonly List<(Vertex[] v, ushort[] i)> _chunks = new List<(Vertex[], ushort[])>();

        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
            _chunks.Clear();
        }

        void Room(int vertices)
        {
            if (_vertices.Count + vertices <= 60000) return;
            _chunks.Add((_vertices.ToArray(), _indices.ToArray()));
            _vertices.Clear();
            _indices.Clear();
        }

        static Vertex V(Vector2 p, Color c) => new Vertex { position = new Vector3(p.x, p.y, Vertex.nearZ), tint = c };

        /// <summary>Четырёхугольник a-b-c-d (по часовой) со своими цветами вершин.</summary>
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            Room(4);
            ushort s = (ushort)_vertices.Count;
            _vertices.Add(V(a, ca));
            _vertices.Add(V(b, cb));
            _vertices.Add(V(c, cc));
            _vertices.Add(V(d, cd));
            _indices.Add(s);
            _indices.Add((ushort)(s + 1));
            _indices.Add((ushort)(s + 2));
            _indices.Add(s);
            _indices.Add((ushort)(s + 2));
            _indices.Add((ushort)(s + 3));
        }

        public void Rect(Rect r, Color color) =>
            Quad(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), color, color, color, color);

        /// <summary>Вертикальный градиент: top — цвет сверху, bottom — снизу.</summary>
        public void VerticalGradient(Rect r, Color top, Color bottom) =>
            Quad(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), top, top, bottom, bottom);

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            Room(3);
            ushort s = (ushort)_vertices.Count;
            _vertices.Add(V(a, color));
            _vertices.Add(V(b, color));
            _vertices.Add(V(c, color));
            _indices.Add(s);
            _indices.Add((ushort)(s + 1));
            _indices.Add((ushort)(s + 2));
        }

        /// <summary>
        /// Скруглённый прямоугольник с горизонтальным градиентом: colorAt(t) даёт цвет для доли ширины t (0..1).
        /// Строится вертикальными полосками; у скруглений полоски чаще, чтобы край был гладким.
        /// </summary>
        public void RoundRectGradient(Rect r, float radius, Func<float, Color> colorAt)
        {
            radius = Mathf.Min(radius, r.width / 2f, r.height / 2f);
            var xs = new List<float>();
            const int cornerSteps = 8, middleSteps = 18;
            for (int i = 0; i <= cornerSteps; i++) xs.Add(r.xMin + radius * i / cornerSteps);
            for (int i = 1; i < middleSteps; i++) xs.Add(Mathf.Lerp(r.xMin + radius, r.xMax - radius, i / (float)middleSteps));
            for (int i = 0; i <= cornerSteps; i++) xs.Add(r.xMax - radius + radius * i / cornerSteps);
            float Inset(float x)
            {
                float d = x < r.xMin + radius ? r.xMin + radius - x : x > r.xMax - radius ? x - (r.xMax - radius) : 0f;
                if (d <= 0f || radius <= 0f) return 0f;
                return radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - d * d));
            }
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                if (x1 - x0 < 0.01f) continue;
                float i0 = Inset(x0), i1 = Inset(x1);
                var c0 = colorAt((x0 - r.xMin) / r.width);
                var c1 = colorAt((x1 - r.xMin) / r.width);
                Quad(new Vector2(x0, r.yMin + i0), new Vector2(x1, r.yMin + i1), new Vector2(x1, r.yMax - i1), new Vector2(x0, r.yMax - i0), c0, c1, c1, c0);
            }
        }

        public void Flush(MeshGenerationContext context)
        {
            if (_vertices.Count > 0) _chunks.Add((_vertices.ToArray(), _indices.ToArray()));
            foreach (var (v, i) in _chunks)
            {
                if (v.Length == 0 || i.Length == 0) continue;
                var mesh = context.Allocate(v.Length, i.Length);
                mesh.SetAllVertices(v);
                mesh.SetAllIndices(i);
            }
            _vertices.Clear();
            _indices.Clear();
            _chunks.Clear();
        }
    }

    /// <summary>
    /// Живой фон главного меню: небо, солнце или луна со звёздами, три слоя московских небоскрёбов
    /// (дальние силуэты, сталинские высотки со шпилями, стеклянные башни Сити), горящие окна, эстакада и шоссе
    /// с бегущими огнями машин. Слои сдвигаются за мышью (параллакс), освещение плавно меняется: день, закат, ночь.
    /// </summary>
    public sealed class MenuBackground : VisualElement
    {
        /// <summary>Режим света: по часам компьютера, всегда день или всегда ночь.</summary>
        public enum LightMode { Auto, Day, Night }

        sealed class Building
        {
            public float X, Width, Height;   // в долях ширины и высоты окна
            public int Kind;                 // 0 — коробка, 1 — высотка со шпилем, 2 — стеклянная башня
            public int Seed;
        }

        struct Palette3
        {
            public Color SkyDayTop, SkyDayBottom, SkyNightTop, SkyNightBottom, Window, WindowAlt, Glass;
        }

        static readonly Palette3[] Palettes =
        {
            // «Синий час» — по умолчанию.
            new Palette3
            {
                SkyDayTop = new Color(0.29f, 0.56f, 0.76f), SkyDayBottom = new Color(0.66f, 0.83f, 0.93f),
                SkyNightTop = new Color(0.04f, 0.08f, 0.15f), SkyNightBottom = new Color(0.12f, 0.21f, 0.31f),
                Window = new Color(1f, 0.82f, 0.48f), WindowAlt = new Color(0.98f, 0.93f, 0.75f), Glass = new Color(0.55f, 0.75f, 0.90f)
            },
            // «Закат».
            new Palette3
            {
                SkyDayTop = new Color(0.95f, 0.61f, 0.42f), SkyDayBottom = new Color(1f, 0.85f, 0.66f),
                SkyNightTop = new Color(0.17f, 0.06f, 0.21f), SkyNightBottom = new Color(0.36f, 0.16f, 0.27f),
                Window = new Color(1f, 0.70f, 0.36f), WindowAlt = new Color(1f, 0.86f, 0.62f), Glass = new Color(0.98f, 0.70f, 0.55f)
            },
            // «Неон».
            new Palette3
            {
                SkyDayTop = new Color(0.23f, 0.25f, 0.48f), SkyDayBottom = new Color(0.56f, 0.49f, 0.76f),
                SkyNightTop = new Color(0.03f, 0.03f, 0.07f), SkyNightBottom = new Color(0.11f, 0.06f, 0.21f),
                Window = new Color(0.35f, 0.95f, 0.95f), WindowAlt = new Color(1f, 0.40f, 0.85f), Glass = new Color(0.55f, 0.50f, 0.95f)
            }
        };

        public static readonly string[] PaletteNames = { "Синий час", "Закат", "Неон" };

        /// <summary>Цвета палитры для превью в настройках: небо сверху и снизу, окна.</summary>
        public static (Color top, Color bottom, Color window) PreviewColors(int palette, bool night)
        {
            var p = Palettes[Mathf.Clamp(palette, 0, Palettes.Length - 1)];
            return night ? (p.SkyNightTop, p.SkyNightBottom, p.Window) : (p.SkyDayTop, p.SkyDayBottom, p.WindowAlt);
        }

        /// <summary>Ночь ли сейчас для режима «по часам» (тот же порог, что у фона).</summary>
        public static bool IsNightNow() => DateTime.Now.Hour < 7 || DateTime.Now.Hour >= 20;

        readonly List<Building>[] _layers = { new List<Building>(), new List<Building>(), new List<Building>() };
        readonly ColorMesh _mesh = new ColorMesh();
        float _time;
        float _hour = 13f;
        Vector2 _pointer = new Vector2(0.5f, 0.5f), _smoothPointer = new Vector2(0.5f, 0.5f);

        public LightMode Mode = LightMode.Auto;
        public int PaletteIndex;
        /// <summary>Затемнение поверх фона (на страницах, где важнее читать, чем любоваться).</summary>
        public float Dim;
        float _dim;

        public MenuBackground()
        {
            pickingMode = PickingMode.Ignore;
            Ui.Fill(this);
            generateVisualContent += Draw;
            Generate();
            _hour = TargetHour();
        }

        /// <summary>Положение мыши в долях окна (0..1) — для параллакса.</summary>
        public void SetPointer(Vector2 normalized) => _pointer = normalized;

        /// <summary>Сейчас ночь (больше половины)? Для подписи режима.</summary>
        public bool IsNight => Night > 0.5f;

        float TargetHour()
        {
            switch (Mode)
            {
                case LightMode.Day: return 13f;
                case LightMode.Night: return 23f;
                default:
                    var now = DateTime.Now;
                    return now.Hour + now.Minute / 60f;
            }
        }

        public void Tick(float dt)
        {
            _time += dt;
            // Час плавно догоняет цель: переключили день/ночь — небо перетекает за пару секунд.
            float target = TargetHour();
            if (Mode == LightMode.Auto && Mathf.Abs(target - _hour) > 12f) _hour = target; // переход через полночь
            _hour = Mathf.MoveTowards(_hour, target, dt * 6f);
            _smoothPointer = Vector2.Lerp(_smoothPointer, _pointer, 1f - Mathf.Exp(-dt * 4f));
            _dim = Mathf.MoveTowards(_dim, Dim, dt * 2f);
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ Свет

        float Altitude => Mathf.Sin(Mathf.PI * (_hour - 6f) / 14f);   // солнце над горизонтом с 6 до 20
        float Night => Smooth(0.15f, -0.12f, Altitude);
        float Dusk => Smooth(0.40f, 0.05f, Altitude) * (1f - Night);
        float SunX => Mathf.Cos(Mathf.PI * (_hour - 6f) / 14f);       // +1 утром (справа), −1 вечером (слева)

        static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(from, to, x));
            return t * t * (3f - 2f * t);
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // ------------------------------------------------------------------ Город

        void Generate()
        {
            var random = new System.Random(2026);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            // Дальний слой — сплошная гряда невысоких силуэтов.
            for (float x = -0.12f; x < 1.12f;)
            {
                float w = Range(0.025f, 0.06f);
                _layers[0].Add(new Building { X = x, Width = w, Height = Range(0.16f, 0.32f), Kind = 0, Seed = random.Next() });
                x += w * Range(0.7f, 1.0f);
            }
            // Средний слой — жилые дома и две сталинские высотки со шпилями.
            for (float x = -0.12f; x < 1.12f;)
            {
                float w = Range(0.035f, 0.075f);
                _layers[1].Add(new Building { X = x, Width = w, Height = Range(0.12f, 0.26f), Kind = 0, Seed = random.Next() });
                x += w + Range(0.004f, 0.02f);
            }
            // Высотки и Сити — по сторонам, чтобы их не закрывало меню посередине.
            _layers[1].Add(new Building { X = 0.10f, Width = 0.07f, Height = 0.44f, Kind = 1, Seed = 11 });
            _layers[1].Add(new Building { X = 0.27f, Width = 0.06f, Height = 0.38f, Kind = 1, Seed = 23 });
            // Ближний слой — кластер стеклянных башен Сити справа и дома пониже.
            for (float x = -0.12f; x < 1.12f;)
            {
                float w = Range(0.045f, 0.09f);
                bool city = x > 0.68f && x < 0.86f;
                _layers[2].Add(new Building
                {
                    X = x, Width = w, Height = city ? Range(0.38f, 0.55f) : Range(0.12f, 0.24f),
                    Kind = city ? 2 : 0, Seed = random.Next()
                });
                x += w + Range(0.006f, 0.03f);
            }
            // Высокие — позади низких в своём слое.
            foreach (var layer in _layers) layer.Sort((a, b) => b.Height.CompareTo(a.Height));
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (float.IsNaN(r.width) || r.width < 50f || r.height < 50f) return;
            var pal = Palettes[Mathf.Clamp(PaletteIndex, 0, Palettes.Length - 1)];
            float night = Night, dusk = Dusk;
            var parallax = (_smoothPointer - new Vector2(0.5f, 0.5f)) * 2f; // −1..1
            float horizon = r.height * 0.78f;

            _mesh.Clear();
            // Небо: день ↔ ночь, у горизонта к закату — тёплая полоса.
            var top = Color.Lerp(pal.SkyDayTop, pal.SkyNightTop, night);
            var bottom = Color.Lerp(pal.SkyDayBottom, pal.SkyNightBottom, night);
            bottom = Color.Lerp(bottom, new Color(1f, 0.55f, 0.30f), dusk * 0.55f);
            _mesh.VerticalGradient(new Rect(0f, 0f, r.width, horizon), top, bottom);
            _mesh.Rect(new Rect(0f, horizon, r.width, r.height - horizon), Color.Lerp(new Color(0.18f, 0.22f, 0.26f), new Color(0.05f, 0.06f, 0.09f), night));
            _mesh.Flush(context);

            var p = context.painter2D;
            // Звёзды ночью — мерцают, чуть сдвигаются за мышью.
            if (night > 0.05f)
            {
                for (int i = 0; i < 90; i++)
                {
                    float sx = Hash(i, 1) * r.width - parallax.x * 4f;
                    float sy = Hash(i, 2) * horizon * 0.75f - parallax.y * 3f;
                    float twinkle = 0.6f + 0.4f * Mathf.Sin(_time * (1.5f + Hash(i, 3) * 2f) + i);
                    p.fillColor = new Color(1f, 1f, 1f, 0.75f * night * twinkle);
                    p.BeginPath();
                    p.Arc(new Vector2(sx, sy), 0.8f + Hash(i, 4) * 1.2f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                }
            }
            // Солнце или луна — по дуге над городом.
            float arc = Mathf.Clamp01((_hour - 6f) / 14f);
            if (night < 0.95f)
            {
                var sun = new Vector2(Mathf.Lerp(r.width * 0.88f, r.width * 0.12f, arc), horizon - Mathf.Max(0f, Altitude) * horizon * 0.62f + 30f)
                          - parallax * 6f;
                var sunColor = Color.Lerp(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.62f, 0.32f), dusk);
                for (int ring = 4; ring >= 1; ring--)
                {
                    p.fillColor = new Color(sunColor.r, sunColor.g, sunColor.b, 0.08f * (1f - night));
                    p.BeginPath();
                    p.Arc(sun, 34f + ring * 16f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                }
                p.fillColor = new Color(sunColor.r, sunColor.g, sunColor.b, 1f - night);
                p.BeginPath();
                p.Arc(sun, 34f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
            if (night > 0.05f)
            {
                var moon = new Vector2(r.width * 0.18f, horizon * 0.22f) - parallax * 6f;
                p.fillColor = new Color(0.95f, 0.95f, 0.88f, 0.10f * night);
                p.BeginPath();
                p.Arc(moon, 60f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                p.fillColor = new Color(0.96f, 0.95f, 0.88f, night);
                p.BeginPath();
                p.Arc(moon, 26f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                p.fillColor = Color.Lerp(top, bottom, 0.3f);
                p.BeginPath();
                p.Arc(moon + new Vector2(11f, -6f), 22f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            // Три слоя домов: дальний бледнее и сдвигается меньше всего.
            float[] shifts = { 8f, 18f, 32f };
            for (int layer = 0; layer < 3; layer++)
            {
                float k = layer / 2f;
                var dayColor = Color.Lerp(Color.Lerp(bottom, new Color(0.45f, 0.52f, 0.60f), 0.55f), new Color(0.32f, 0.38f, 0.45f), k);
                var nightColor = Color.Lerp(new Color(0.10f, 0.14f, 0.21f), new Color(0.05f, 0.07f, 0.11f), k);
                var color = Color.Lerp(dayColor, nightColor, night);
                var offset = new Vector2(-parallax.x * shifts[layer], -parallax.y * shifts[layer] * 0.35f);
                foreach (var b in _layers[layer]) AddBuilding(b, r, horizon, offset, color, layer, pal, night);
            }
            // Эстакада между средним и ближним слоем — тонкая полоса с огоньками.
            float overpassY = horizon - r.height * 0.12f;
            var overpassOffset = new Vector2(-parallax.x * 24f, -parallax.y * 6f);
            _mesh.Rect(new Rect(-40f + overpassOffset.x, overpassY + overpassOffset.y, r.width + 80f, 6f),
                Color.Lerp(new Color(0.30f, 0.33f, 0.37f), new Color(0.08f, 0.09f, 0.12f), night));
            for (int i = 0; i < 9; i++)
            {
                float x = -40f + i * (r.width + 80f) / 8f + overpassOffset.x;
                _mesh.Rect(new Rect(x - 3f, overpassY + 6f + overpassOffset.y, 6f, horizon - overpassY - 6f), Color.Lerp(new Color(0.28f, 0.31f, 0.35f), new Color(0.07f, 0.08f, 0.11f), night));
            }
            _mesh.Flush(context);
            DrawTraffic(p, r, overpassY + overpassOffset.y - 1f, overpassOffset.x, 0.6f, 9, night);

            // Шоссе у нижнего края: две полосы, огни бегут навстречу друг другу.
            var roadOffset = new Vector2(-parallax.x * 40f, 0f);
            float roadTop = horizon + 6f;
            float roadHeight = r.height - roadTop;
            _mesh.Rect(new Rect(0f, roadTop, r.width, roadHeight), Color.Lerp(new Color(0.22f, 0.24f, 0.27f), new Color(0.06f, 0.07f, 0.09f), night));
            _mesh.Rect(new Rect(0f, roadTop, r.width, 3f), Color.Lerp(new Color(0.75f, 0.75f, 0.72f), new Color(0.30f, 0.30f, 0.32f), night));
            float dashY = roadTop + roadHeight * 0.5f;
            for (float x = Mathf.Repeat(roadOffset.x, 60f) - 60f; x < r.width; x += 60f)
                _mesh.Rect(new Rect(x, dashY - 1f, 30f, 2f), new Color(0.95f, 0.85f, 0.40f, 0.65f));
            _mesh.Flush(context);
            DrawTraffic(p, r, roadTop + roadHeight * 0.30f, roadOffset.x, 1f, 14, night, reverse: true);
            DrawTraffic(p, r, roadTop + roadHeight * 0.72f, roadOffset.x, 1.2f, 16, night);

            // Тёплый свет заката поверх и затемнение (если просили).
            var p2 = context.painter2D;
            if (dusk > 0.02f)
            {
                p2.fillColor = new Color(1f, 0.55f, 0.25f, 0.10f * dusk);
                p2.BeginPath();
                p2.MoveTo(Vector2.zero);
                p2.LineTo(new Vector2(r.width, 0f));
                p2.LineTo(new Vector2(r.width, r.height));
                p2.LineTo(new Vector2(0f, r.height));
                p2.ClosePath();
                p2.Fill();
            }
            float darken = 0.25f + 0.45f * _dim;
            p2.fillColor = new Color(0.02f, 0.03f, 0.05f, darken);
            p2.BeginPath();
            p2.MoveTo(Vector2.zero);
            p2.LineTo(new Vector2(r.width, 0f));
            p2.LineTo(new Vector2(r.width, r.height));
            p2.LineTo(new Vector2(0f, r.height));
            p2.ClosePath();
            p2.Fill();
        }

        void AddBuilding(Building b, Rect r, float horizon, Vector2 offset, Color color, int layer, Palette3 pal, float night)
        {
            float x = b.X * r.width + offset.x;
            float w = b.Width * r.width;
            float h = b.Height * r.height;
            float y = horizon - h + offset.y;
            var body = new Rect(x, y, w, horizon - y + 4f);
            // Сторона к солнцу светлее, другая — в тени (днём заметно, ночью почти нет).
            float light = 0.10f * (1f - night);
            var lit = new Color(color.r + light, color.g + light, color.b + light);
            var shade = new Color(color.r * (1f - light), color.g * (1f - light), color.b * (1f - light));
            bool sunRight = SunX > 0f;

            if (b.Kind == 2)
            {
                // Стеклянная башня: вертикальный градиент цвета стекла, скошенный верх.
                var glassTop = Color.Lerp(Color.Lerp(pal.Glass, color, 0.35f), color, night * 0.7f);
                _mesh.Quad(new Vector2(x, y + w * 0.25f), new Vector2(x + w, y), new Vector2(x + w, horizon + 4f), new Vector2(x, horizon + 4f),
                    glassTop, glassTop, color, color);
            }
            else if (b.Kind == 1)
            {
                // Сталинская высотка: ступенчатый силуэт и шпиль со звездой.
                _mesh.Rect(new Rect(x, y + h * 0.35f, w, horizon - y - h * 0.35f + 4f), color);
                _mesh.Rect(new Rect(x + w * 0.18f, y + h * 0.18f, w * 0.64f, h * 0.2f), color);
                _mesh.Rect(new Rect(x + w * 0.32f, y + h * 0.06f, w * 0.36f, h * 0.14f), color);
                _mesh.Triangle(new Vector2(x + w * 0.42f, y + h * 0.07f), new Vector2(x + w * 0.58f, y + h * 0.07f), new Vector2(x + w * 0.5f, y - h * 0.10f), color);
                var star = Color.Lerp(new Color(0.85f, 0.25f, 0.20f), new Color(1f, 0.30f, 0.25f), night);
                _mesh.Rect(new Rect(x + w * 0.5f - 2.5f, y - h * 0.10f - 4f, 5f, 5f), star);
            }
            else
            {
                _mesh.Rect(body, color);
            }
            // Боковая грань — светлая или тёмная полоска.
            float edge = Mathf.Max(2f, w * 0.12f);
            var edgeRect = sunRight ? new Rect(x + w - edge, y, edge, horizon - y + 4f) : new Rect(x, y, edge, horizon - y + 4f);
            if (b.Kind != 1) _mesh.Rect(edgeRect, layer == 0 ? color : lit);
            var shadeRect = sunRight ? new Rect(x, y, edge * 0.6f, horizon - y + 4f) : new Rect(x + w - edge * 0.6f, y, edge * 0.6f, horizon - y + 4f);
            if (b.Kind != 1 && layer > 0) _mesh.Rect(shadeRect, shade);

            // Окна: ночью часть горит, некоторые иногда гаснут и загораются.
            if (layer == 0 || (night < 0.05f && layer == 1)) return;
            float cell = layer == 1 ? 7f : 9f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((w - 6f) / cell));
            int rows = Mathf.Max(1, Mathf.FloorToInt((horizon - y - 12f) / (cell * 1.3f)));
            float startY = y + (b.Kind == 1 ? h * 0.4f : 8f);
            for (int row = 0; row < rows; row++)
            {
                float wy = startY + row * cell * 1.3f;
                if (wy > horizon - 6f) break;
                for (int col = 0; col < cols; col++)
                {
                    float hash = Hash(b.Seed + col * 31, row);
                    float wx = x + 4f + col * cell;
                    // Днём окна — блики стекла, ночью — тёплые огни.
                    if (night > 0.05f)
                    {
                        float flicker = Hash(b.Seed + col, row + Mathf.FloorToInt(_time * 0.2f + hash * 10f));
                        if (hash > 0.55f || flicker < 0.08f) continue;
                        var lamp = hash < 0.12f ? pal.WindowAlt : pal.Window;
                        _mesh.Rect(new Rect(wx, wy, cell * 0.5f, cell * 0.7f), new Color(lamp.r, lamp.g, lamp.b, 0.85f * night));
                    }
                    else if (hash < 0.35f)
                        _mesh.Rect(new Rect(wx, wy, cell * 0.5f, cell * 0.7f), new Color(1f, 1f, 1f, 0.10f));
                }
            }
        }

        /// <summary>Огни машин по прямой: фары (белые) или стоп-сигналы (красные), с мягким ореолом ночью.</summary>
        void DrawTraffic(Painter2D p, Rect r, float y, float offsetX, float speed, int cars, float night, bool reverse = false)
        {
            float span = r.width + 200f;
            for (int i = 0; i < cars; i++)
            {
                float seed = Hash(i, reverse ? 7 : 5);
                float v = (60f + seed * 70f) * speed;
                float t = Mathf.Repeat(_time * v + seed * span, span);
                float x = (reverse ? span - t : t) - 100f + offsetX;
                var color = reverse ? new Color(1f, 0.25f, 0.20f) : new Color(1f, 0.95f, 0.78f);
                float glow = 0.15f + 0.45f * night;
                p.fillColor = new Color(color.r, color.g, color.b, glow * 0.5f);
                p.BeginPath();
                p.Arc(new Vector2(x, y), 7f + 4f * night, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                // Пара огней одной машины.
                p.fillColor = new Color(color.r, color.g, color.b, 0.55f + 0.45f * night);
                p.BeginPath();
                p.Arc(new Vector2(x, y - 1.5f), 1.6f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                p.BeginPath();
                p.Arc(new Vector2(x, y + 1.5f), 1.6f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }
    }
}
