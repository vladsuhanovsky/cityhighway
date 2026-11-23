using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Раскладка 3D-города по карте. Работает в пикселях поля 1400 × 780 (как рельеф TerrainTexture): сетка улиц,
    /// застройка плотнее и выше к центру, отступы от дорог, мест, рек и парков. Высокие здания держатся дальше от
    /// дорог — особенно от тех, что за ними (севернее): так здание не встаёт перед дорогой на наклонном поле.
    /// Всё детерминировано: один и тот же город при каждом запуске.
    /// </summary>
    public static class CityPlanner
    {
        public const float FieldW = 1400f, FieldH = 780f;
        /// <summary>Пикселей поля на метр сцены (1400 / 14 = 780 / 7.8 = 100).</summary>
        public const float PixelsPerUnit = 100f;
        /// <summary>Радиус (пиксели поля) свободной земли вокруг хаба магната: там встаёт вокзал или аэропорт.</summary>
        public const float HubClearance = 52f;
        /// <summary>Радиус свободного места вокруг центра комплекса хаба (пятно 0,4 × 0,27 м плюс запас).</summary>
        const float ComplexClearance = 32f;

        public struct Building
        {
            public Vector2 Center;     // пиксели поля
            public float Width, Depth; // пиксели поля
            public float Height;       // метры сцены
            public float Angle;        // градусы вокруг вертикали
            public CityShapes.Kind Kind;
            public string District;
            public int Seed;
        }

        public struct TreeSpot
        {
            public Vector2 At;         // пиксели поля
            public float Size;         // метры сцены
            public CityShapes.Tree Kind;
            public string District;
            public int Seed;
            /// <summary>Дальние окрестности: дерево видно только карточкой (объёмный меш не строится).</summary>
            public bool CardOnly;
        }

        /// <summary>Декали на земле — порядок совпадает с плитками атласа CityTextures.Decals.</summary>
        public enum DecalKind { Parking, Pitch, Court, Plaza, Playground, Dirt, FlowerBed, Wear, FieldWheat, FieldSoil, FieldGreen }

        public struct Decal
        {
            public Vector2 Center;     // пиксели поля
            public float Width, Depth; // пиксели поля
            public float Angle;        // градусы вокруг вертикали
            public DecalKind Kind;
            public string District;
        }

        /// <summary>Мелкие предметы города: фонари вдоль дорог, кусты во дворах, лодки на реке.</summary>
        public enum PropKind { Lamp, Bush, Boat }

        public struct Prop
        {
            public Vector2 At;         // пиксели поля
            public float Angle;        // градусы вокруг вертикали (у фонаря — куда смотрит плечо, у лодки — нос)
            public float Size;         // метры сцены (куст)
            public PropKind Kind;
            public string District;
            public int Seed;
        }

        /// <summary>
        /// Архитектурный стиль карты: обычный (Москва и города магната), китайский (Чунцин — пагоды, залы и дома под
        /// крышами с загнутыми углами, высотки с павильонами, красные фонари) или европейский (Амстердам — узкие
        /// кирпичные дома на каналах с фронтонами, церкви со шпилями, мансарды, без небоскрёбов).
        /// </summary>
        public enum CityStyle { Standard, Chinese, European }

        public static CityStyle StyleOf(CityMap map)
        {
            string name = map?.Name ?? "";
            if (name == "Чунцин") return CityStyle.Chinese;
            if (name == "Амстердам") return CityStyle.European;
            return CityStyle.Standard;
        }

        /// <summary>
        /// Тип здания в стиле карты: обычный тип (по характеру района) заменяется на свой для Китая или Европы.
        /// Промзоны и аэропорты не меняются. Размеры и высота подгоняются под новый тип.
        /// </summary>
        static CityShapes.Kind Restyle(CityStyle style, CityShapes.Kind kind, bool center, float pick, float cell,
            ref float w, ref float d, ref float h)
        {
            if (style == CityStyle.Standard) return kind;
            switch (kind)
            {
                case CityShapes.Kind.Hangar:
                case CityShapes.Kind.Warehouse:
                case CityShapes.Kind.Sawtooth:
                case CityShapes.Kind.Factory:
                    return kind;
            }
            if (style == CityStyle.Chinese)
            {
                switch (kind)
                {
                    case CityShapes.Kind.Stepped:
                    case CityShapes.Kind.Round:
                        if (!center) { h = Mathf.Min(h, 0.2f); return CityShapes.Kind.Pagoda; }
                        return CityShapes.Kind.ChineseTower;
                    case CityShapes.Kind.Spire:
                    case CityShapes.Kind.Landmark:
                        w = d = cell * 0.72f;
                        h = Mathf.Clamp(h, 0.16f, 0.3f);
                        return CityShapes.Kind.Pagoda;
                    case CityShapes.Kind.Gable:
                    case CityShapes.Kind.Hip:
                        return CityShapes.Kind.ChineseHouse;
                    case CityShapes.Kind.LBlock:
                        h = Mathf.Clamp(h * 0.6f, 0.05f, 0.09f);
                        w = cell * 0.85f;
                        return CityShapes.Kind.ChineseHall;
                    default:
                        // Плоские коробки: в центре — половина залов, остальное — жилые дома с китайской крышей.
                        if (center && pick < 0.35f) { h = Mathf.Clamp(h * 0.6f, 0.06f, 0.1f); w = cell * 0.85f; return CityShapes.Kind.ChineseHall; }
                        return CityShapes.Kind.ChineseBlock;
                }
            }
            // Европейский стиль: без небоскрёбов, дома на каналах и мансарды, церкви вместо шпилей.
            switch (kind)
            {
                case CityShapes.Kind.Spire:
                case CityShapes.Kind.Landmark:
                    if (pick < 0.45f)
                    {
                        w = cell * 0.7f; d = cell * 0.9f; h = 0.07f;
                        return CityShapes.Kind.Church;
                    }
                    break;
                case CityShapes.Kind.Gable:
                case CityShapes.Kind.Hip:
                    return kind;
            }
            h = Mathf.Clamp(h, 0.07f, 0.13f);
            if (kind == CityShapes.Kind.Panel || (!center && pick < 0.4f)) return CityShapes.Kind.EuroBlock;
            w = cell * (0.75f + pick * 0.2f);
            d = cell * (0.55f + pick * 0.15f);
            return CityShapes.Kind.CanalRow;
        }

        /// <summary>Circle in field pixels that suppresses visual procedural objects only.</summary>
        public struct ExclusionZone
        {
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly bool Buildings;
            public readonly bool Trees;

            public ExclusionZone(Vector2 center, float radius, bool buildings, bool trees)
            {
                Center = center;
                Radius = Mathf.Max(0f, radius);
                Buildings = buildings;
                Trees = trees;
            }

            public bool Contains(Vector2 point, bool building, float objectRadius = 0f)
            {
                if (building ? !Buildings : !Trees) return false;
                float reach = Radius + Mathf.Max(0f, objectRadius);
                return (point - Center).sqrMagnitude < reach * reach;
            }
        }

        public sealed class Plan
        {
            /// <summary>Архитектурный стиль города (фонари, деревья и здания — в его духе).</summary>
            public CityStyle Style;
            public readonly List<Building> Buildings = new List<Building>();
            public readonly List<Prop> Props = new List<Prop>();
            public readonly List<TreeSpot> Trees = new List<TreeSpot>();
            public readonly List<Decal> Decals = new List<Decal>();
            /// <summary>Основания для затемнения на земле: центр и полуразмеры в координатах карты.</summary>
            public readonly List<Vector4> Footprints = new List<Vector4>();
        }

        struct Seg { public Vector2 A, B; }

        /// <summary>Convert field pixels to flat-world X/Z without sampling runtime terrain.</summary>
        public static Vector3 ToFlatWorld(Vector2 field, float preservedY = 0f)
        {
            return new Vector3(
                field.x / PixelsPerUnit - BoardLayout.WorldWidth * 0.5f,
                preservedY,
                BoardLayout.WorldDepth * 0.5f - field.y / PixelsPerUnit);
        }

        /// <summary>Точка поля в мире; y — высота над землёй (с рельефом 3D-города).</summary>
        public static Vector3 ToWorld(Vector2 field, float y = 0f)
        {
            float x = field.x / PixelsPerUnit - BoardLayout.WorldWidth / 2f, z = BoardLayout.WorldDepth / 2f - field.y / PixelsPerUnit;
            return new Vector3(x, y + Relief.At(x, z), z);
        }

        public static float Hash(int x, int y, int salt)
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

        public static float Noise(float x, float y, float cell, int salt)
        {
            float gx = x / cell, gy = y / cell;
            int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
            float fx = gx - ix, fy = gy - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, salt), b = Hash(ix + 1, iy, salt), c = Hash(ix, iy + 1, salt), d = Hash(ix + 1, iy + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

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

        /// <summary>Характер района по id и названию: центр, аэропорт, порт и промзона — или обычный.</summary>
        enum Character { Center, Airport, Industrial, Regular }

        static Character CharacterOf(District district)
        {
            if (district == null) return Character.Regular;
            string id = district.Id.ToLowerInvariant(), name = (district.Name ?? "").ToLowerInvariant();
            if (id == "center") return Character.Center;
            if (id == "airports" || name.Contains("аэропорт")) return Character.Airport;
            if (id.Contains("port") || id.Contains("dock") || id.Contains("industr") || name.Contains("порт") ||
                name.Contains("док") || name.Contains("промзон") || name.Contains("завод")) return Character.Industrial;
            return Character.Regular;
        }

        /// <summary>Слой декали: поля — внизу, пятна — посередине, площадки — сверху.</summary>
        static int Layer(DecalKind kind) =>
            kind == DecalKind.FieldWheat || kind == DecalKind.FieldGreen || kind == DecalKind.FieldSoil ? 0
            : kind == DecalKind.Dirt || kind == DecalKind.Wear ? 1 : 2;

        /// <param name="density">0.4–1: сколько зданий оставить (качество графики).</param>
        /// <param name="hubSites">Станции с комплексами хабов: в радиусе HubClearance вокруг них — ни домов, ни деревьев, ни площадок.</param>
        /// <param name="hubCenters">Центры комплексов хабов в мире (x, z) — вокруг них тоже свободно.</param>
        public static Plan Build(CityMap map, float density = 1f, IEnumerable<string> hubSites = null,
            IEnumerable<Vector2> hubCenters = null, float buildingDensityMultiplier = 1f, float treeDensityMultiplier = 1f,
            IEnumerable<ExclusionZone> exclusionZones = null)
        {
            var plan = new Plan { Style = StyleOf(map) };
            // Default multipliers keep the original deterministic layout exactly intact.
            float buildingDensity = density * Mathf.Clamp(buildingDensityMultiplier, 0.5f, 1.5f);
            float treeDensity = density * Mathf.Clamp(treeDensityMultiplier, 0.5f, 1.5f);
            var zones = exclusionZones == null ? new List<ExclusionZone>() : exclusionZones.ToList();
            bool IsExcluded(Vector2 point, bool building, float radius = 0f)
            {
                foreach (var zone in zones)
                    if (zone.Contains(point, building, radius)) return true;
                return false;
            }
            Vector2 Field(float x, float y) => new Vector2(x * FieldW, y * FieldH);

            var points = map.Locations.ToDictionary(l => l.Id, l => Field(l.X, l.Y));
            var hubs = hubSites == null ? new List<Vector2>()
                : hubSites.Where(points.ContainsKey).Select(id => points[id]).ToList();
            // Центр комплекса в координатах поля: 1 м мира = 100 единиц поля.
            var complexes = hubCenters == null ? new List<Vector2>()
                : hubCenters.Select(c => new Vector2(c.x / BoardLayout.WorldWidth * FieldW + FieldW / 2f,
                    FieldH / 2f - c.y / BoardLayout.WorldDepth * FieldH)).ToList();
            bool NearHub(Vector2 p)
            {
                foreach (var h in hubs)
                    if ((p - h).sqrMagnitude < HubClearance * HubClearance) return true;
                foreach (var c in complexes)
                    if ((p - c).sqrMagnitude < ComplexClearance * ComplexClearance) return true;
                return false;
            }
            var roads = new List<Seg>();
            foreach (var r in map.Roads)
                if (points.TryGetValue(r.A, out var a) && points.TryGetValue(r.B, out var b)) roads.Add(new Seg { A = a, B = b });
            var river = new List<Seg>();
            foreach (var line in map.Rivers)
                for (int i = 0; i + 1 < line.Count; i++)
                    river.Add(new Seg { A = Field(line[i].X, line[i].Y), B = Field(line[i + 1].X, line[i + 1].Y) });

            // Парки — те же пятна, что на рельефе (радиус от высоты поля 900).
            var parks = new List<Vector3>();
            foreach (var pair in TerrainTexture.Parks)
            {
                if (!points.TryGetValue(pair.Key, out var c)) continue;
                float r = pair.Value * 900f;
                parks.Add(new Vector3(c.x + r * 0.35f, c.y - r * 0.2f, r));
                parks.Add(new Vector3(c.x - r * 0.4f, c.y + r * 0.25f, r * 0.8f));
                parks.Add(new Vector3(c.x + r * 0.1f, c.y + r * 0.45f, r * 0.65f));
            }
            bool InPark(Vector2 p, float pad) => parks.Any(c => (p - new Vector2(c.x, c.y)).magnitude < c.z + pad);

            // Граница «город — пригород»: эллипс МКАД у Москвы, у других карт — такой же эллипс вокруг центра.
            float Ring(Vector2 p)
            {
                float mx = (p.x / FieldW - 0.5f) / 0.40f, my = (p.y / FieldH - 0.5f) / 0.49f;
                return Mathf.Sqrt(mx * mx + my * my);
            }
            float Urban(float e) => Mathf.Clamp01((1.08f - e) / 0.55f);
            bool ringRoad = map.Ring != null && map.Ring.Name == "МКАД";

            Location NearestLocation(Vector2 p)
            {
                Location best = null;
                float distance = float.MaxValue;
                foreach (var l in map.Locations)
                {
                    float d = (points[l.Id] - p).sqrMagnitude;
                    if (d < distance) { distance = d; best = l; }
                }
                return best;
            }
            float NearestPlace(Vector2 p) => points.Values.Min(q => (p - q).magnitude);

            // ---- Здания по сетке кварталов.
            const float cell = 16f;
            int cols = Mathf.CeilToInt(FieldW / cell), rows = Mathf.CeilToInt(FieldH / cell);
            for (int iy = -1; iy <= rows; iy++)
            for (int ix = -1; ix <= cols; ix++)
            {
                var p = new Vector2((ix + 0.5f + (Hash(ix, iy, 4) - 0.5f) * 0.2f) * cell, (iy + 0.5f + (Hash(ix, iy, 5) - 0.5f) * 0.2f) * cell);
                float e = Ring(p), u = Urban(e);
                if (u > 0.15f && (ix % 4 == 0 || iy % 3 == 0)) continue;                  // улицы
                float keep = (Mathf.Min(1f, u * 1.1f) + (e > 1.08f ? 0.06f : 0f)) * buildingDensity;
                if (Hash(ix, iy, 1) > keep) continue;
                if (ringRoad && e > 0.97f && e < 1.05f) continue;                          // полоса МКАД
                var location = NearestLocation(p);
                var district = location == null ? null : map.GetDistrict(location.DistrictId);
                var character = CharacterOf(district);

                // Высота: центр — башни, город — дома в 5–10 этажей, пригород — низкие.
                float h = Mathf.Lerp(0.05f, 0.16f, u) * (0.75f + Hash(ix, iy, 11) * 0.6f);
                var kind = CityShapes.Kind.Block;
                float roll = Hash(ix, iy, 12);
                float size = 0.62f + Hash(ix, iy, 6) * 0.3f;
                float w = cell * size, d = cell * (0.62f + Hash(ix, iy, 7) * 0.3f);
                switch (character)
                {
                    case Character.Center:
                        if (roll < 0.22f) { kind = CityShapes.Kind.Stepped; h = 0.24f + Hash(ix, iy, 13) * 0.22f; }
                        else if (roll < 0.34f) { kind = CityShapes.Kind.Spire; h = 0.22f + Hash(ix, iy, 13) * 0.2f; }
                        else if (roll < 0.40f) { kind = CityShapes.Kind.Round; h = 0.2f + Hash(ix, iy, 13) * 0.18f; }
                        else if (roll < 0.44f && map.Name == "Москва") { kind = CityShapes.Kind.Landmark; h = 0.4f; w = d = cell * 0.95f; }
                        else if (roll < 0.75f) { kind = CityShapes.Kind.Parapet; h = Mathf.Max(h, 0.12f); }
                        else kind = CityShapes.Kind.LBlock;
                        break;
                    case Character.Airport:
                        kind = roll < 0.5f ? CityShapes.Kind.Hangar : CityShapes.Kind.Warehouse;
                        h = kind == CityShapes.Kind.Hangar ? 0.07f : 0.05f;
                        w = cell * 0.9f; d = cell * 0.7f;
                        break;
                    case Character.Industrial:
                        kind = roll < 0.35f ? CityShapes.Kind.Sawtooth : roll < 0.55f ? CityShapes.Kind.Factory : roll < 0.85f ? CityShapes.Kind.Warehouse : CityShapes.Kind.Block;
                        h = 0.05f + Hash(ix, iy, 13) * 0.04f;
                        w = cell * 0.9f; d = cell * (0.6f + Hash(ix, iy, 7) * 0.3f);
                        break;
                    default:
                        if (u < 0.25f) { kind = roll < 0.55f ? CityShapes.Kind.Gable : CityShapes.Kind.Hip; h = 0.035f + Hash(ix, iy, 13) * 0.025f; w *= 0.8f; d *= 0.75f; }
                        else if (roll < 0.30f) { kind = CityShapes.Kind.Panel; w = cell * 0.95f; d = cell * 0.5f; }
                        else if (roll < 0.45f) kind = CityShapes.Kind.LBlock;
                        else if (roll < 0.62f) kind = CityShapes.Kind.Parapet;
                        else if (roll < 0.68f && u > 0.6f) { kind = CityShapes.Kind.Round; h = Mathf.Max(h, 0.18f); }
                        else if (roll < 0.74f) { kind = CityShapes.Kind.Gable; h = Mathf.Min(h, 0.07f); }
                        break;
                }
                // Стиль карты: Чунцин — китайская архитектура, Амстердам — европейская.
                kind = Restyle(plan.Style, kind, character == Character.Center, Hash(ix, iy, 16), cell, ref w, ref d, ref h);
                if (IsExcluded(p, true, Mathf.Max(w, d) * 0.5f)) continue;

                // Отступы: чем выше здание, тем дальше от дорог; севернее здания (за ним на экране) — ещё дальше.
                float heightPx = h * PixelsPerUnit;
                float clearance = 14f + Mathf.Max(w, d) * 0.45f;
                var top = p + new Vector2(0f, -heightPx * 0.75f);   // где на экране окажется крыша
                if (Nearest(p, roads) < clearance || Nearest(top, roads) < 10f + heightPx * 0.15f) continue;
                if (NearestPlace(p) < 26f + heightPx * 0.2f || NearestPlace(top) < 22f) continue;
                if (Nearest(p, river) < 24f + Mathf.Max(w, d) * 0.4f || InPark(p, 6f)) continue;
                if (NearHub(p)) continue;

                float angle = (Hash(ix, iy, 14) < 0.5f ? 0f : 90f) + (Hash(ix, iy, 15) - 0.5f) * 8f;
                plan.Buildings.Add(new Building
                {
                    Center = p, Width = w, Depth = d, Height = h, Angle = angle, Kind = kind,
                    District = district?.Id, Seed = ix * 7919 + iy * 104729
                });
                float rx = (Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad)) * w + Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) * d) / 2f;
                float rz = (Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) * w + Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad)) * d) / 2f;
                plan.Footprints.Add(new Vector4(p.x / FieldW, p.y / FieldH, rx / FieldW, rz / FieldH));
            }

            var occupied = plan.Buildings.Select(b => b.Center).ToList();
            bool FreeOfBuildings(Vector2 p, float radius)
            {
                foreach (var c in occupied)
                    if ((p - c).sqrMagnitude < radius * radius) return false;
                return true;
            }

            // ---- Декали: парковки, площади, спортплощадки, клумбы, пустыри; за городом — поля.
            // Твёрдые покрытия и поля запоминаем, чтобы на них не выросли деревья.
            var paved = new List<Vector3>();
            const float decalCell = 30f;
            for (int iy = -1; iy <= Mathf.CeilToInt(FieldH / decalCell); iy++)
            for (int ix = -1; ix <= Mathf.CeilToInt(FieldW / decalCell); ix++)
            {
                var p = new Vector2((ix + 0.5f + (Hash(ix, iy, 70) - 0.5f) * 0.6f) * decalCell, (iy + 0.5f + (Hash(ix, iy, 71) - 0.5f) * 0.6f) * decalCell);
                float e = Ring(p), u = Urban(e);
                float roll = Hash(ix, iy, 72) / Mathf.Max(0.4f, density);
                float sizeRoll = Hash(ix, iy, 73);
                bool park = InPark(p, 0f);
                DecalKind kind;
                float w, d;
                if (park)
                {
                    if (roll < 0.22f) { kind = DecalKind.FlowerBed; w = d = 11f + sizeRoll * 6f; }
                    else if (roll < 0.27f) { kind = DecalKind.Pitch; w = 30f; d = 20f; }
                    else continue;
                }
                else if (e > 1.1f)
                {
                    if (roll < 0.30f)
                    {
                        float pick = Hash(ix, iy, 74);
                        kind = pick < 0.4f ? DecalKind.FieldWheat : pick < 0.7f ? DecalKind.FieldGreen : DecalKind.FieldSoil;
                        w = 44f + sizeRoll * 30f; d = 30f + Hash(ix, iy, 75) * 22f;
                    }
                    else if (roll < 0.35f) { kind = DecalKind.Dirt; w = d = 18f + sizeRoll * 10f; }
                    else continue;
                }
                else
                {
                    var location = NearestLocation(p);
                    var character = CharacterOf(location == null ? null : map.GetDistrict(location.DistrictId));
                    switch (character)
                    {
                        case Character.Center:
                            if (roll < 0.22f) { kind = DecalKind.Plaza; w = d = 22f + sizeRoll * 10f; }
                            else if (roll < 0.32f) { kind = DecalKind.Parking; w = 22f; d = 14f; }
                            else continue;
                            break;
                        case Character.Industrial:
                            if (roll < 0.28f) { kind = DecalKind.Parking; w = 26f; d = 16f; }
                            else if (roll < 0.52f) { kind = DecalKind.Dirt; w = d = 20f + sizeRoll * 10f; }
                            else if (roll < 0.62f) { kind = DecalKind.Wear; w = 26f; d = 18f; }
                            else continue;
                            break;
                        case Character.Airport:
                            if (roll < 0.35f) { kind = DecalKind.Wear; w = 34f; d = 22f; }
                            else if (roll < 0.55f) { kind = DecalKind.Parking; w = 26f; d = 16f; }
                            else continue;
                            break;
                        default:
                            if (u > 0.25f)
                            {
                                if (roll < 0.10f) { kind = DecalKind.Playground; w = d = 14f + sizeRoll * 4f; }
                                else if (roll < 0.16f) { kind = DecalKind.Court; w = 13f; d = 19f; }
                                else if (roll < 0.20f) { kind = DecalKind.Pitch; w = 28f; d = 18f; }
                                else if (roll < 0.32f) { kind = DecalKind.Parking; w = 20f; d = 13f; }
                                else if (roll < 0.36f) { kind = DecalKind.FlowerBed; w = d = 10f + sizeRoll * 4f; }
                                else if (roll < 0.41f) { kind = DecalKind.Dirt; w = d = 14f + sizeRoll * 8f; }
                                else continue;
                            }
                            else
                            {
                                if (roll < 0.08f) { kind = DecalKind.Pitch; w = 28f; d = 18f; }
                                else if (roll < 0.16f) { kind = DecalKind.Dirt; w = d = 16f + sizeRoll * 8f; }
                                else if (roll < 0.20f) { kind = DecalKind.FlowerBed; w = d = 10f + sizeRoll * 4f; }
                                else continue;
                            }
                            break;
                    }
                }

                float radius = 0.5f * Mathf.Sqrt(w * w + d * d);
                bool field = kind == DecalKind.FieldWheat || kind == DecalKind.FieldGreen || kind == DecalKind.FieldSoil;
                if (Nearest(p, roads) < radius * 0.75f + 8f || NearestPlace(p) < radius + 12f) continue;
                if (Nearest(p, river) < radius + 16f || !FreeOfBuildings(p, radius * 0.7f + 10f)) continue;
                if (field && InPark(p, radius)) continue;
                if (ringRoad && e > 0.95f && e < 1.07f) continue;
                if (NearHub(p)) continue;

                var owner = NearestLocation(p);
                float angle = (Hash(ix, iy, 76) < 0.5f ? 0f : 90f) + (Hash(ix, iy, 77) - 0.5f) * (field ? 16f : 8f);
                plan.Decals.Add(new Decal
                {
                    Center = p, Width = w, Depth = d, Angle = angle, Kind = kind,
                    District = owner == null ? null : map.DistrictOf(owner.Id)
                });
                if (kind != DecalKind.Dirt && kind != DecalKind.Wear) paved.Add(new Vector3(p.x, p.y, radius * 0.8f));
            }
            // Поля под низом, площадки поверх — декали прозрачные, порядок отрисовки внутри меша = порядок в списке.
            plan.Decals.Sort((a, b) => Layer(a.Kind).CompareTo(Layer(b.Kind)));

            // ---- Деревья: парки (густо), лесополосы за городом, редкие деревья во дворах и вдоль рек.
            bool OnPaved(Vector2 p)
            {
                foreach (var s in paved)
                    if ((p - new Vector2(s.x, s.y)).sqrMagnitude < s.z * s.z) return true;
                return false;
            }
            void AddTree(Vector2 p, float size, int salt, bool forest)
            {
                if (Nearest(p, roads) < 11f || NearestPlace(p) < 17f || Nearest(p, river) < 17f || !FreeOfBuildings(p, 11f) || OnPaved(p)) return;
                if (NearHub(p) || IsExcluded(p, false)) return;
                var location = NearestLocation(p);
                float pick = Hash((int)p.x, (int)p.y, salt);
                // На вершинах холмов — ельники.
                bool hill = ToWorld(p).y > ReliefField.Amplitude * 0.55f;
                // Амстердам — вязы и тополя вдоль каналов (тополей больше, елей в городе нет).
                float poplars = plan.Style == CityStyle.European ? 0.35f : 0.15f;
                var kind = (forest || hill) && plan.Style != CityStyle.European
                    ? (pick < (hill ? 0.8f : 0.45f) ? CityShapes.Tree.Spruce : CityShapes.Tree.Round)
                    : pick < poplars ? CityShapes.Tree.Poplar : CityShapes.Tree.Round;
                plan.Trees.Add(new TreeSpot { At = p, Size = size, Kind = kind, District = location == null ? null : map.DistrictOf(location.Id), Seed = (int)(p.x * 31 + p.y * 17) });
            }
            for (int pi = 0; pi < parks.Count; pi++)
            {
                var c = parks[pi];
                int count = Mathf.RoundToInt(c.z * c.z / 55f * treeDensity);
                for (int j = 0; j < count; j++)
                {
                    float angle = Hash(pi, j, 40) * Mathf.PI * 2f, radius = Mathf.Sqrt(Hash(pi, j, 41)) * c.z;
                    var p = new Vector2(c.x + Mathf.Cos(angle) * radius, c.y + Mathf.Sin(angle) * radius);
                    AddTree(p, 0.06f + Hash(pi, j, 42) * 0.04f, 43, false);
                }
            }
            const float treeCell = 13f;
            for (int iy = 0; iy <= Mathf.CeilToInt(FieldH / treeCell); iy++)
            for (int ix = 0; ix <= Mathf.CeilToInt(FieldW / treeCell); ix++)
            {
                var p = new Vector2((ix + 0.5f + (Hash(ix, iy, 50) - 0.5f) * 0.8f) * treeCell, (iy + 0.5f + (Hash(ix, iy, 51) - 0.5f) * 0.8f) * treeCell);
                float e = Ring(p);
                bool forest = e > 1.06f && Noise(p.x, p.y, 60f, 52) > 0.55f && Hash(ix, iy, 53) < 0.8f * treeDensity;
                bool yard = e < 1f && Hash(ix, iy, 55) < 0.035f * treeDensity;
                bool bank = Nearest(p, river) < 30f && Hash(ix, iy, 56) < 0.35f * treeDensity;
                if (forest || yard || bank) AddTree(p, forest ? 0.07f + Hash(ix, iy, 54) * 0.04f : 0.05f + Hash(ix, iy, 54) * 0.03f, 57, forest);
            }
            // Рощи на холмах вне города: чем выше, тем гуще.
            if (Relief.Active)
            {
                const float grove = 16f;
                for (int iy = 0; iy <= Mathf.CeilToInt(FieldH / grove); iy++)
                for (int ix = 0; ix <= Mathf.CeilToInt(FieldW / grove); ix++)
                {
                    var p = new Vector2((ix + Hash(ix, iy, 430)) * grove, (iy + Hash(ix, iy, 431)) * grove);
                    float height = ToWorld(p).y / ReliefField.Amplitude;
                    if (height < 0.45f || NearestPlace(p) < 60f || Hash(ix, iy, 432) > (height - 0.45f) * 2.2f * treeDensity) continue;
                    AddTree(p, 0.05f + Hash(ix, iy, 433) * 0.04f, 434, true);
                }
            }
            AddProps(map, plan, density, roads, river, points, NearestPlace, q => OnPaved(q) || InPark(q, 0f), FreeOfBuildings, NearHub);
            AddOutskirts(map, plan, buildingDensity, treeDensity, river, zones);
            plan.Decals.Sort((a, b) => Layer(a.Kind).CompareTo(Layer(b.Kind)));
            return plan;
        }

        // ================================================================== Мелочи города

        /// <summary>
        /// Фонари по обеим сторонам каждой дороги (через шаг, в шахматном порядке; у станций, на мостах и у воды —
        /// нет), кусты во дворах у домов и лодки на реке — по руслу, носом по течению.
        /// </summary>
        static void AddProps(CityMap map, Plan plan, float density, List<Seg> roads, List<Seg> river,
            Dictionary<string, Vector2> points, Func<Vector2, float> nearestPlace, Func<Vector2, bool> paved,
            Func<Vector2, float, bool> freeOfBuildings, Func<Vector2, bool> nearHub)
        {
            string DistrictAt(Vector2 p)
            {
                string best = null;
                float distance = float.MaxValue;
                foreach (var l in map.Locations)
                {
                    float d = (points[l.Id] - p).sqrMagnitude;
                    if (d < distance) { distance = d; best = map.DistrictOf(l.Id); }
                }
                return best;
            }

            // Фонари: шаг ~34 px, плечо над дорогой (дорога 11 px, фонарь в 9 px от оси).
            int seed = 0;
            foreach (var road in roads)
            {
                var d = road.B - road.A;
                float length = d.magnitude;
                if (length < 40f) continue;
                var dir = d / length;
                var normal = new Vector2(-dir.y, dir.x);
                int count = Mathf.FloorToInt(length / 34f);
                for (int i = 1; i < count; i++)
                {
                    seed++;
                    if (Hash(seed, i, 401) > 0.85f * density + 0.1f) continue;
                    float side = i % 2 == 0 ? 1f : -1f;
                    var at = road.A + dir * (length * i / count) + normal * side * 9f;
                    if (nearestPlace(at) < 22f || Nearest(at, river) < 22f || nearHub(at)) continue;
                    // Плечо — к оси дороги. Угол — как у зданий: поворот Euler(0, угол) переводит локальную +X
                    // в направление (x, y) поля (y поля вниз = −Z мира), то есть угол = atan2(y, x).
                    var toAxis = -normal * side;
                    float angle = Mathf.Atan2(toAxis.y, toAxis.x) * Mathf.Rad2Deg;
                    plan.Props.Add(new Prop { At = at, Angle = angle, Kind = PropKind.Lamp, District = DistrictAt(at), Seed = seed });
                }
            }

            // Кусты: у домов во дворах (не на дорогах, площадках и в воде).
            const float bushCell = 12f;
            for (int iy = 0; iy <= Mathf.CeilToInt(FieldH / bushCell); iy++)
            for (int ix = 0; ix <= Mathf.CeilToInt(FieldW / bushCell); ix++)
            {
                if (Hash(ix, iy, 410) > 0.16f * density) continue;
                var p = new Vector2((ix + Hash(ix, iy, 411)) * bushCell, (iy + Hash(ix, iy, 412)) * bushCell);
                if (Nearest(p, roads) < 10f || nearestPlace(p) < 16f || Nearest(p, river) < 18f || nearHub(p) || paved(p)) continue;
                if (!freeOfBuildings(p, 9f) || freeOfBuildings(p, 22f)) continue;   // рядом с домом, но не в нём
                plan.Props.Add(new Prop { At = p, Size = 0.012f + Hash(ix, iy, 413) * 0.01f, Kind = PropKind.Bush, District = DistrictAt(p), Seed = ix * 131 + iy * 977 });
            }

            // Лодки: вдоль русла, в стороне от оси, носом по течению (иногда против).
            foreach (var line in map.Rivers)
            {
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    var a = new Vector2(line[i].X * FieldW, line[i].Y * FieldH);
                    var b = new Vector2(line[i + 1].X * FieldW, line[i + 1].Y * FieldH);
                    var d = b - a;
                    if (d.sqrMagnitude < 1f || Hash(i, line.Count, 420) > 0.22f * density) continue;
                    var dir = d.normalized;
                    var at = a + d * 0.5f + new Vector2(-dir.y, dir.x) * (Hash(i, line.Count, 421) < 0.5f ? 6f : -6f);
                    if (Nearest(at, roads) < 14f) continue;   // не под мостом
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (Hash(i, line.Count, 422) < 0.3f ? 180f : 0f);
                    plan.Props.Add(new Prop { At = at, Angle = angle, Kind = PropKind.Boat, District = null, Seed = i * 7 + line.Count });
                }
            }
        }

        // ================================================================== Окрестности

        /// <summary>
        /// Сколько окрестностей за краем поля (пиксели поля): видно в перспективе до тумана. Со всех сторон одинаково
        /// далеко — камеру можно развернуть, и дальним краем станет любой.
        /// </summary>
        public const float OutskirtsLeft = -800f, OutskirtsRight = 2200f, OutskirtsTop = -1150f, OutskirtsBottom = 1930f;
        /// <summary>Лес в окрестностях (тот же шум рисует тёмную подстилку на текстуре окрестностей).</summary>
        public static float ForestNoise(float x, float y) => Noise(x, y, 150f, 301);
        static bool EdgePoint(float x, float y) => x < 0.03f || x > 0.97f || y < 0.03f || y > 0.97f;
        public static float VillageNoise(float x, float y) => Noise(x, y, 230f, 302);
        public const float ForestLevel = 0.56f, VillageLevel = 0.72f;

        /// <summary>
        /// Земля за краем поля — продолжение карты до горизонта: леса (вблизи — объёмные деревья, дальше —
        /// карточки), деревни из домиков с двускатными и вальмовыми крышами и лоскуты полей. Реки продолжены
        /// за край — у воды ничего не строится. Всё детерминировано.
        /// </summary>
        static void AddOutskirts(CityMap map, Plan plan, float buildingDensity, float treeDensity, List<Seg> river,
            List<ExclusionZone> zones)
        {
            bool IsExcluded(Vector2 p, bool building, float radius = 0f)
            {
                foreach (var zone in zones)
                    if (zone.Contains(p, building, radius)) return true;
                return false;
            }
            // Реки за краем: продолжаем крайние отрезки далеко наружу.
            var rivers = new List<Seg>(river);
            foreach (var line in map.Rivers)
            {
                if (line.Count < 2) continue;
                Vector2 F(int i) => new Vector2(line[i].X * FieldW, line[i].Y * FieldH);
                var a = F(0); var a2 = F(1); var b = F(line.Count - 1); var b2 = F(line.Count - 2);
                // Наружу уходят только концы у края карты; приток, впадающий в другую реку, кончается в ней.
                if (EdgePoint(line[0].X, line[0].Y)) rivers.Add(new Seg { A = a, B = a + (a - a2).normalized * 4000f });
                if (EdgePoint(line[line.Count - 1].X, line[line.Count - 1].Y)) rivers.Add(new Seg { A = b, B = b + (b - b2).normalized * 4000f });
            }
            bool Outside(Vector2 p, float pad) => p.x < -pad || p.x > FieldW + pad || p.y < -pad || p.y > FieldH + pad;
            float BeyondField(Vector2 p) => Mathf.Max(Mathf.Max(-p.x, p.x - FieldW), Mathf.Max(-p.y, p.y - FieldH));

            // Леса.
            const float treeCell = 17f;
            for (float y = OutskirtsTop; y < OutskirtsBottom; y += treeCell)
            for (float x = OutskirtsLeft; x < OutskirtsRight; x += treeCell)
            {
                int ix = Mathf.RoundToInt(x / treeCell), iy = Mathf.RoundToInt(y / treeCell);
                var p = new Vector2(x + (Hash(ix, iy, 310) - 0.5f) * treeCell * 0.8f, y + (Hash(ix, iy, 311) - 0.5f) * treeCell * 0.8f);
                if (!Outside(p, 6f) || ForestNoise(p.x, p.y) < ForestLevel || Hash(ix, iy, 312) > 0.78f * treeDensity) continue;
                if (Nearest(p, rivers) < 24f || IsExcluded(p, false)) continue;
                float size = 0.07f + Hash(ix, iy, 313) * 0.045f;
                plan.Trees.Add(new TreeSpot
                {
                    At = p, Size = size, Kind = Hash(ix, iy, 314) < 0.5f ? CityShapes.Tree.Spruce : CityShapes.Tree.Round,
                    District = null, Seed = ix * 92821 + iy * 68917, CardOnly = BeyondField(p) > 240f
                });
            }

            // Деревни: домики по сетке там, где шум деревни высок, а леса нет.
            const float houseCell = 19f;
            for (float y = OutskirtsTop; y < OutskirtsBottom; y += houseCell)
            for (float x = OutskirtsLeft; x < OutskirtsRight; x += houseCell)
            {
                int ix = Mathf.RoundToInt(x / houseCell), iy = Mathf.RoundToInt(y / houseCell);
                var p = new Vector2(x + (Hash(ix, iy, 320) - 0.5f) * 5f, y + (Hash(ix, iy, 321) - 0.5f) * 5f);
                if (!Outside(p, 10f) || VillageNoise(p.x, p.y) < VillageLevel || ForestNoise(p.x, p.y) > ForestLevel - 0.04f) continue;
                if (Hash(ix, iy, 322) > 0.62f * buildingDensity || Nearest(p, rivers) < 30f) continue;
                float w = 9f + Hash(ix, iy, 323) * 4f, d = 7f + Hash(ix, iy, 324) * 3f;
                if (IsExcluded(p, true, Mathf.Max(w, d) * 0.5f)) continue;
                plan.Buildings.Add(new Building
                {
                    Center = p, Width = w, Depth = d, Height = 0.032f + Hash(ix, iy, 325) * 0.022f,
                    Angle = (Hash(ix, iy, 326) < 0.5f ? 0f : 90f) + (Hash(ix, iy, 327) - 0.5f) * 14f,
                    Kind = plan.Style == CityStyle.Chinese ? CityShapes.Kind.ChineseHouse
                        : Hash(ix, iy, 328) < 0.55f ? CityShapes.Kind.Gable : CityShapes.Kind.Hip,
                    District = null, Seed = 500000 + ix * 7919 + iy * 104729
                });
            }

            // Поля: лоскуты пшеницы, всходов и пашни; в районе — общий наклон рядов, как у настоящих полей.
            const float fieldCell = 78f;
            for (float y = OutskirtsTop; y < OutskirtsBottom; y += fieldCell)
            for (float x = OutskirtsLeft; x < OutskirtsRight; x += fieldCell)
            {
                int ix = Mathf.RoundToInt(x / fieldCell), iy = Mathf.RoundToInt(y / fieldCell);
                var p = new Vector2(x + (Hash(ix, iy, 330) - 0.5f) * 20f, y + (Hash(ix, iy, 331) - 0.5f) * 20f);
                if (!Outside(p, 30f) || ForestNoise(p.x, p.y) > ForestLevel - 0.06f || VillageNoise(p.x, p.y) > VillageLevel - 0.08f) continue;
                if (Hash(ix, iy, 332) > 0.68f || Nearest(p, rivers) < 70f) continue;
                float pick = Hash(ix, iy, 333);
                var kind = pick < 0.4f ? DecalKind.FieldWheat : pick < 0.72f ? DecalKind.FieldGreen : DecalKind.FieldSoil;
                float tilt = (Noise(p.x, p.y, 600f, 334) - 0.5f) * 50f;
                plan.Decals.Add(new Decal
                {
                    Center = p, Width = 56f + Hash(ix, iy, 335) * 26f, Depth = 40f + Hash(ix, iy, 336) * 20f,
                    Angle = tilt + (Hash(ix, iy, 337) < 0.5f ? 0f : 90f), Kind = kind, District = null
                });
            }
        }
    }
}
