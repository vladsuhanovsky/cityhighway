using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Вид хаба — точки, где появляются грузы.</summary>
    public enum HubKind { Airport, Station, Port }

    public sealed class Hub
    {
        public string LocationId { get; }
        public HubKind Kind { get; }
        /// <summary>Уровень хаба 1–5: склады, краны, территория, комплекс. Растёт за очки стройки.</summary>
        public int Level { get; internal set; } = 1;
        /// <summary>Компания, развивающая хаб (-1 — городской). Владелец грузит первым и берёт плату с чужих погрузок.</summary>
        public int Owner { get; internal set; } = -1;
        public Hub(string locationId, HubKind kind) { LocationId = locationId; Kind = kind; }
    }

    /// <summary>Сгенерированный город: карта, хабы и городская (общая) сеть дорог.</summary>
    public sealed class GeneratedCity
    {
        public CityMap Map;
        public List<Hub> Hubs = new List<Hub>();
        /// <summary>Дороги, которые есть с начала партии: общая городская сеть (остовное дерево — связывает все места).</summary>
        public HashSet<string> PublicRoads = new HashSet<string>();
        public int Seed;
    }

    /// <summary>
    /// Генератор города для «Логистического магната»: каждый запуск — новая карта.
    ///   1. Центр города и реки: главная река с запада на восток с изгибами, иногда приток с севера или юга,
    ///      впадающий в главную (как Амстел в Эй). Русла сглаживаются так же, как в 3D (Катмулл — Ром).
    ///   2. Места: аэропорты на окраинах, вокзалы вокруг центра, речные порты у воды, центр, затем остальные —
    ///      случайно, гуще к центру. Места не ближе 80 px друг к другу и 36 px к воде, подписи не наезжают.
    ///   3. Районы: центр, аэропорты, вокзалы, порт и шесть секторов по сторонам света.
    ///   4. Дороги — граф Габриэля (планарный: дороги не пересекаются) длиной до 300 px; дорога не идёт
    ///      вплотную к чужому месту и через подписи; пересекает реку — мост (сталь), но только под углом от 40°
    ///      и не длиннее 230 px; вдоль воды (ближе 22 px) — не строится.
    ///   5. Магистрали (бетон) — кратчайшие пути от центра к хабам; окраины — щебень, остальное — асфальт.
    ///   6. Городская сеть — минимальное остовное дерево: с первого хода можно доехать куда угодно, но тесно.
    /// Попытка не удалась (места не встали, граф несвязен) — следующая с другим зерном.
    /// Логика проверена на Python-модели (300 зёрен: 99% попыток удачны, 40–45 мест, 70–100 дорог, 4–20 мостов).
    /// </summary>
    public static class CityGenerator
    {
        const float W = 1400f, H = 780f;

        static readonly string[] Names =
        {
            "Заречье", "Нагорная", "Сосновка", "Липки", "Ключи", "Озёрки", "Слобода", "Кирпичный", "Мельница", "Ярмарка",
            "Луговая", "Садовая", "Берёзки", "Ольховка", "Песчаная", "Рябиновая", "Каменка", "Дубки", "Овражки", "Крутой Яр",
            "Посад", "Гончары", "Ткачи", "Литейная", "Затон", "Пристань", "Маяк", "Рощино", "Холмы", "Заводская", "Новосёлки",
            "Высоты", "Кленовая", "Тополя", "Сенная", "Торговая", "Солнечная", "Полевая", "Лесная", "Ивушки", "Камыши",
            "Мостовая", "Университет", "Стадион", "Парк Победы", "Звёздная", "Обсерватория", "Ботсад", "Арсенал", "Депо",
            "Элеватор", "Ипподром", "Цирк", "Гостиный двор", "Набережная", "Октябрьская", "Пионерская", "Студенческая",
            "Монетный двор", "Слободка", "Хутор", "Яблоневая", "Вишнёвая", "Гранитная", "Медовая"
        };
        static readonly string[] CenterNames = { "Ратуша", "Соборная", "Главная площадь", "Театральная", "Купеческая" };
        static readonly string[] StationNames = { "Главный вокзал", "Северный вокзал", "Товарная", "Сортировочная", "Южный вокзал" };
        static readonly string[] PortNames = { "Речной порт", "Грузовой порт" };
        static readonly string[] SectorNames = { "Север", "Северо-Восток", "Юго-Восток", "Юг", "Юго-Запад", "Северо-Запад" };
        static readonly string[] CityFirst = { "Ново", "Верх", "Красно", "Бело", "Яро", "Зелено", "Светло", "Старо", "Речно", "Дубро" };
        static readonly string[] CityLast = { "градск", "речье", "горск", "мостье", "славль", "полье", "бережск", "озёрск" };

        struct P
        {
            public float X, Y;
            public P(float x, float y) { X = x; Y = y; }
            public static P operator +(P a, P b) => new P(a.X + b.X, a.Y + b.Y);
            public static P operator -(P a, P b) => new P(a.X - b.X, a.Y - b.Y);
            public static P operator *(P a, float k) => new P(a.X * k, a.Y * k);
        }

        sealed class Spot
        {
            public P At;
            public string Name, District;
            public HubKind? Hub;
        }

        public static GeneratedCity Generate(int seed)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var city = TryGenerate(seed + attempt * 7919);
                if (city != null) return city;
            }
            throw new InvalidOperationException("Не удалось сгенерировать город.");
        }

        // ================================================================== Геометрия

        static float Dist(P a, P b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        static float SegDist(P p, P a, P b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float t = Math.Max(0f, Math.Min(1f, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / Math.Max(1e-9f, dx * dx + dy * dy)));
            return Dist(p, new P(a.X + t * dx, a.Y + t * dy));
        }

        static float Cross(P a, P b, P c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        static bool Intersect(P p1, P p2, P p3, P p4) =>
            Cross(p3, p4, p1) * Cross(p3, p4, p2) < 0 && Cross(p1, p2, p3) * Cross(p1, p2, p4) < 0;

        static List<P> Smooth(List<P> pts, int steps = 5)
        {
            var result = new List<P>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var p0 = pts[Math.Max(0, i - 1)]; var p1 = pts[i]; var p2 = pts[i + 1]; var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add((p1 * 2f + (p2 - p0) * t + (p0 * 2f - p1 * 5f + p2 * 4f - p3) * t2 + (p1 * 3f - p0 - p2 * 3f + p3) * t3) * 0.5f);
                }
            }
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        /// <summary>Рамка подписи под местом (как проверяет сборщик карт): ширина по числу букв.</summary>
        static void Box(P p, string name, out float x0, out float y0, out float x1, out float y1)
        {
            float w = name.Length * 7.2f + 10f;
            x0 = p.X - w / 2f; x1 = p.X + w / 2f; y0 = p.Y + 13f; y1 = p.Y + 31f;
        }

        static bool BoxesOverlap(P a, string an, P b, string bn)
        {
            Box(a, an, out var ax0, out var ay0, out var ax1, out var ay1);
            Box(b, bn, out var bx0, out var by0, out var bx1, out var by1);
            return ax0 < bx1 && bx0 < ax1 && ay0 < by1 && by0 < ay1;
        }

        static bool SegmentHitsBox(P a, P b, P at, string name)
        {
            Box(at, name, out var x0, out var y0, out var x1, out var y1);
            if (a.X >= x0 && a.X <= x1 && a.Y >= y0 && a.Y <= y1) return true;
            var c00 = new P(x0, y0); var c10 = new P(x1, y0); var c01 = new P(x0, y1); var c11 = new P(x1, y1);
            return Intersect(a, b, c00, c10) || Intersect(a, b, c01, c11) || Intersect(a, b, c00, c01) || Intersect(a, b, c10, c11);
        }

        // ================================================================== Генерация

        static GeneratedCity TryGenerate(int seed)
        {
            var rng = new Random(seed);
            float R() => (float)rng.NextDouble();
            float cx = W * (0.44f + 0.12f * R()), cy = H * (0.44f + 0.12f * R());

            // ---- Реки.
            float side = R() < 0.5f ? 1f : -1f;
            float y0 = cy + side * (55f + 70f * R()), amp = 35f + 50f * R(), freq = 2f + 2.5f * R(), phase = R() * 6.28f;
            var main = new List<P>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float y = y0 + amp * (float)Math.Sin(phase + t * freq * Math.PI) + (R() - 0.5f) * 20f;
                main.Add(new P(t * W, Math.Min(Math.Max(y, 0.14f * H), 0.86f * H)));
            }
            var rivers = new List<List<P>> { main };
            if (R() < 0.55f)
            {
                bool top = R() < 0.5f;
                float jx = cx + (R() - 0.5f) * 260f;
                var smoothMain = Smooth(main);
                var join = smoothMain.OrderBy(p => Math.Abs(p.X - jx)).First();
                float ex = jx + (R() - 0.5f) * 300f, ey = top ? 0f : H;
                var tributary = new List<P>();
                for (int i = 0; i <= 5; i++)
                {
                    float t = i / 5f;
                    tributary.Add(new P(ex + (join.X - ex) * t + (float)Math.Sin(t * Math.PI) * (R() - 0.5f) * 80f, ey + (join.Y - ey) * t));
                }
                tributary[tributary.Count - 1] = join;
                rivers.Add(tributary);
            }
            var lines = rivers.Select(r => Smooth(r)).ToList();
            float RiverDist(P p)
            {
                float best = float.MaxValue;
                foreach (var l in lines)
                    for (int i = 0; i + 1 < l.Count; i++) best = Math.Min(best, SegDist(p, l[i], l[i + 1]));
                return best;
            }

            // ---- Места.
            var spots = new List<Spot>();
            var used = new HashSet<string>();
            bool Free(P p, string name, float minDistance)
            {
                if (p.X < 45f || p.X > W - 45f || p.Y < 40f || p.Y > H - 50f) return false;
                if (RiverDist(p) < 36f) return false;
                Box(p, name, out var bx0, out _, out var bx1, out _);
                if (bx0 < 4f || bx1 > W - 4f) return false;
                foreach (var q in spots)
                {
                    if (Dist(p, q.At) < minDistance) return false;
                    if (BoxesOverlap(p, name, q.At, q.Name)) return false;
                }
                return true;
            }
            void Add(P p, string name, string district, HubKind? hub)
            {
                spots.Add(new Spot { At = p, Name = name, District = district, Hub = hub });
                used.Add(name);
            }

            // Аэропорты — по разные стороны города, на окраине.
            float a0 = R() * 6.2832f;
            for (int k = 0; k < 2; k++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 60 && !placed; attempt++)
                {
                    float a = a0 + k * 3.1416f + (R() - 0.5f) * 0.9f, rad = 0.40f + 0.06f * R();
                    var p = new P(W / 2f + (float)Math.Cos(a) * rad * W, H / 2f + (float)Math.Sin(a) * rad * H * 1.05f);
                    p = new P(Math.Min(Math.Max(p.X, 60f), W - 60f), Math.Min(Math.Max(p.Y, 50f), H - 60f));
                    float dx = p.X - W / 2f, dy = p.Y - H / 2f;
                    string name = Math.Abs(dx) / W > Math.Abs(dy) / H ? (dx > 0 ? "Аэропорт-Восток" : "Аэропорт-Запад") : (dy > 0 ? "Аэропорт-Юг" : "Аэропорт-Север");
                    if (used.Contains(name)) name = name.Replace("Аэропорт", "Аэропорт-2");
                    if (Free(p, name, 120f)) { Add(p, name, "airports", HubKind.Airport); placed = true; }
                }
                if (!placed) return null;
            }

            // Вокзалы — кольцом вокруг центра.
            var stations = StationNames.OrderBy(_ => rng.Next()).ToList();
            float s0 = R() * 6.2832f;
            for (int k = 0; k < 3; k++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 60 && !placed; attempt++)
                {
                    float a = s0 + k * 6.2832f / 3f + (R() - 0.5f) * 0.8f, rad = 0.15f + 0.10f * R();
                    var p = new P(cx + (float)Math.Cos(a) * rad * W, cy + (float)Math.Sin(a) * rad * H * 1.4f);
                    if (Free(p, stations[k], 80f)) { Add(p, stations[k], "vokzaly", HubKind.Station); placed = true; }
                }
                if (!placed) return null;
            }

            // Речные порты — у главной реки, не в самом центре.
            int ports = R() < 0.5f ? 1 : 2;
            var mainLine = lines[0];
            for (int k = 0; k < ports; k++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 80 && !placed; attempt++)
                {
                    int i = rng.Next(mainLine.Count - 1);
                    var a = mainLine[i]; var b = mainLine[i + 1];
                    float dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Max(1e-5f, (float)Math.Sqrt(dx * dx + dy * dy));
                    float s = R() < 0.5f ? 1f : -1f, offset = 48f + 12f * R();
                    var p = new P(a.X - dy / len * offset * s, a.Y + dx / len * offset * s);
                    if (Math.Sqrt(Math.Pow((p.X - cx) / W, 2) + Math.Pow((p.Y - cy) / H, 2)) < 0.12) continue;
                    if (Free(p, PortNames[k], 80f)) { Add(p, PortNames[k], "port", HubKind.Port); placed = true; }
                }
                if (!placed) return null;
            }

            // Центр.
            var centers = CenterNames.OrderBy(_ => rng.Next()).ToList();
            int centerCount = 0;
            for (int attempt = 0; attempt < 300 && centerCount < 3; attempt++)
            {
                float a = R() * 6.2832f, rad = R() * 0.085f;
                var p = new P(cx + (float)Math.Cos(a) * rad * W, cy + (float)Math.Sin(a) * rad * H * 1.5f);
                if (Free(p, centers[centerCount], 78f)) { Add(p, centers[centerCount], "center", null); centerCount++; }
            }
            if (centerCount < 2) return null;

            // Остальные — гуще к центру.
            var pool = Names.OrderBy(_ => rng.Next()).ToList();
            int target = 40 + rng.Next(6);
            for (int attempt = 0; attempt < 6000 && spots.Count < target && pool.Count > 0; attempt++)
            {
                var p = new P(45f + R() * (W - 90f), 40f + R() * (H - 90f));
                float e = (float)Math.Sqrt(Math.Pow((p.X - cx) / (0.42f * W), 2) + Math.Pow((p.Y - cy) / (0.46f * H), 2));
                float keep = e < 0.6f ? 1f : e < 1f ? 0.7f : 0.3f;
                if (R() > keep) continue;
                string name = pool[pool.Count - 1];
                if (Free(p, name, e < 0.7f ? 84f : 96f)) { Add(p, name, null, null); pool.RemoveAt(pool.Count - 1); }
            }
            if (spots.Count < 34) return null;

            // ---- Районы: центр, хабы, шесть секторов по сторонам света.
            foreach (var spot in spots)
            {
                if (spot.District != null) continue;
                if (Math.Sqrt(Math.Pow((spot.At.X - cx) / W, 2) + Math.Pow((spot.At.Y - cy) / H, 2)) < 0.10) spot.District = "center";
                else
                {
                    double angle = (Math.Atan2(spot.At.Y - cy, spot.At.X - cx) * 180.0 / Math.PI + 90.0 + 30.0) % 360.0;
                    if (angle < 0) angle += 360.0;
                    spot.District = "sector" + Math.Min(5, (int)(angle / 60.0));
                }
            }

            // ---- Дороги: граф Габриэля.
            int n = spots.Count;
            var candidates = new List<(float d, int i, int j)>();
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float d = Dist(spots[i].At, spots[j].At);
                if (d > 300f) continue;
                var m = (spots[i].At + spots[j].At) * 0.5f;
                float r = d / 2f * 0.98f;
                bool empty = true;
                for (int k = 0; k < n && empty; k++)
                    if (k != i && k != j && Dist(spots[k].At, m) < r) empty = false;
                if (empty) candidates.Add((d, i, j));
            }
            candidates.Sort((a, b) => a.d.CompareTo(b.d));

            (int count, float angle) RiverCross(P a, P b)
            {
                int count = 0;
                float worst = 90f;
                foreach (var l in lines)
                    for (int k = 0; k + 1 < l.Count; k++)
                    {
                        if (!Intersect(a, b, l[k], l[k + 1])) continue;
                        count++;
                        float ux = b.X - a.X, uy = b.Y - a.Y, vx = l[k + 1].X - l[k].X, vy = l[k + 1].Y - l[k].Y;
                        double cos = Math.Abs(ux * vx + uy * vy) / (Math.Sqrt(ux * ux + uy * uy) * Math.Sqrt(vx * vx + vy * vy) + 1e-9);
                        worst = Math.Min(worst, (float)(Math.Acos(Math.Min(1.0, cos)) * 180.0 / Math.PI));
                    }
                return (count, worst);
            }

            var edges = new List<(int i, int j, bool bridge, float d)>();
            foreach (var (d, i, j) in candidates)
            {
                var a = spots[i].At; var b = spots[j].At;
                bool bad = false;
                for (int k = 0; k < n && !bad; k++)
                {
                    if (k == i || k == j) continue;
                    if (SegDist(spots[k].At, a, b) < 24f || SegmentHitsBox(a, b, spots[k].At, spots[k].Name)) bad = true;
                }
                if (bad) continue;
                var (count, angle) = RiverCross(a, b);
                if (count == 0)
                {
                    float near = float.MaxValue;
                    for (int t = 0; t <= 40; t++) near = Math.Min(near, RiverDist(a + (b - a) * (t / 40f)));
                    if (near < 22f) continue;
                    edges.Add((i, j, false, d));
                }
                else if (count == 1 && angle >= 40f && d <= 230f) edges.Add((i, j, true, d));
            }

            // Связность.
            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            foreach (var e in edges) parent[Find(e.i)] = Find(e.j);
            if (Enumerable.Range(0, n).Select(Find).Distinct().Count() > 1) return null;

            // ---- Сборка карты.
            var ids = new string[n];
            for (int i = 0; i < n; i++) ids[i] = "g" + i.ToString("00");
            var districtIds = spots.Select(s => s.District).Distinct().ToList();
            var districts = new List<District>();
            foreach (var id in districtIds)
            {
                string name = id == "center" ? "Центр" : id == "airports" ? "Аэропорты" : id == "vokzaly" ? "Вокзалы"
                    : id == "port" ? "Порт" : SectorNames[int.Parse(id.Substring(6))];
                districts.Add(new District(id, name));
            }
            var locations = new List<Location>();
            for (int i = 0; i < n; i++)
                locations.Add(new Location(ids[i], spots[i].Name, spots[i].District, spots[i].At.X / W, spots[i].At.Y / H));

            // Магистрали: кратчайшие (по длине) пути от центра к каждому хабу — бетон.
            int centerIndex = Enumerable.Range(0, n).OrderBy(i => Dist(spots[i].At, new P(cx, cy))).First();
            var adjacency = new List<(int to, int edge)>[n];
            for (int i = 0; i < n; i++) adjacency[i] = new List<(int, int)>();
            for (int e = 0; e < edges.Count; e++)
            {
                adjacency[edges[e].i].Add((edges[e].j, e));
                adjacency[edges[e].j].Add((edges[e].i, e));
            }
            var distance = Enumerable.Repeat(float.MaxValue, n).ToArray();
            var via = Enumerable.Repeat(-1, n).ToArray();
            distance[centerIndex] = 0f;
            var open = new SortedSet<(float, int)> { (0f, centerIndex) };
            while (open.Count > 0)
            {
                var (du, u) = open.Min;
                open.Remove(open.Min);
                foreach (var (v, e) in adjacency[u])
                {
                    float nd = du + edges[e].d;
                    if (nd >= distance[v]) continue;
                    if (distance[v] < float.MaxValue) open.Remove((distance[v], v));
                    distance[v] = nd;
                    via[v] = e;
                    open.Add((nd, v));
                }
            }
            var arterial = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                if (!spots[i].Hub.HasValue) continue;
                int at = i;
                while (at != centerIndex && via[at] >= 0)
                {
                    int e = via[at];
                    arterial.Add(e);
                    at = edges[e].i == at ? edges[e].j : edges[e].i;
                }
            }

            var roads = new List<Road>();
            for (int e = 0; e < edges.Count; e++)
            {
                var (i, j, bridge, d) = edges[e];
                int length = d < 95f ? 1 : d < 150f ? 2 : d < 210f ? 3 : d < 300f ? 4 : 5;
                Resource? resource;
                if (bridge) resource = Resource.Steel;
                else if (arterial.Contains(e)) resource = Resource.Concrete;
                else
                {
                    var mid = (spots[i].At + spots[j].At) * 0.5f;
                    float far = (float)Math.Sqrt(Math.Pow((mid.X - cx) / W, 2) + Math.Pow((mid.Y - cy) / H, 2));
                    resource = far > 0.34f ? Resource.Gravel : (e % 5 == 0 ? (Resource?)null : Resource.Asphalt);
                }
                roads.Add(new Road("r" + (e + 1).ToString("000"), ids[i], ids[j], length, resource));
            }

            var map = new CityMap(CityFirst[rng.Next(CityFirst.Length)] + CityLast[rng.Next(CityLast.Length)],
                districts, locations, roads, new Contract[0]);
            map.SetExtras(rivers.Select(r => (IReadOnlyList<MapPoint>)r.Select(p => new MapPoint(Math.Min(1f, Math.Max(0f, p.X / W)), Math.Min(1f, Math.Max(0f, p.Y / H)))).ToList()), 1f);
            if (map.Validate(6).Count > 0) return null;

            // Городская сеть: минимальное остовное дерево (мосты чуть «дороже» — их в общей сети меньше).
            var city = new GeneratedCity { Map = map, Seed = seed };
            for (int i = 0; i < n; i++) parent[i] = i;
            foreach (var e in Enumerable.Range(0, edges.Count).OrderBy(e => edges[e].d * (edges[e].bridge ? 1.3f : 1f)))
            {
                int a = Find(edges[e].i), b = Find(edges[e].j);
                if (a == b) continue;
                parent[a] = b;
                city.PublicRoads.Add(roads[e].Id);
            }
            for (int i = 0; i < n; i++)
                if (spots[i].Hub.HasValue) city.Hubs.Add(new Hub(ids[i], spots[i].Hub.Value));
            return city;
        }
    }
}
