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

        public sealed class Plan
        {
            public readonly List<Building> Buildings = new List<Building>();
            public readonly List<TreeSpot> Trees = new List<TreeSpot>();
            public readonly List<Decal> Decals = new List<Decal>();
            /// <summary>Основания для затемнения на земле: центр и полуразмеры в координатах карты.</summary>
            public readonly List<Vector4> Footprints = new List<Vector4>();
        }

        struct Seg { public Vector2 A, B; }

        public static Vector3 ToWorld(Vector2 field, float y = 0f) =>
            new Vector3(field.x / PixelsPerUnit - BoardLayout.WorldWidth / 2f, y, BoardLayout.WorldDepth / 2f - field.y / PixelsPerUnit);

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

        static float Noise(float x, float y, float cell, int salt)
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
        public static Plan Build(CityMap map, float density = 1f)
        {
            var plan = new Plan();
            Vector2 Field(float x, float y) => new Vector2(x * FieldW, y * FieldH);

            var points = map.Locations.ToDictionary(l => l.Id, l => Field(l.X, l.Y));
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
                float keep = (Mathf.Min(1f, u * 1.1f) + (e > 1.08f ? 0.06f : 0f)) * density;
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

                // Отступы: чем выше здание, тем дальше от дорог; севернее здания (за ним на экране) — ещё дальше.
                float heightPx = h * PixelsPerUnit;
                float clearance = 14f + Mathf.Max(w, d) * 0.45f;
                var top = p + new Vector2(0f, -heightPx * 0.75f);   // где на экране окажется крыша
                if (Nearest(p, roads) < clearance || Nearest(top, roads) < 10f + heightPx * 0.15f) continue;
                if (NearestPlace(p) < 26f + heightPx * 0.2f || NearestPlace(top) < 22f) continue;
                if (Nearest(p, river) < 24f + Mathf.Max(w, d) * 0.4f || InPark(p, 6f)) continue;

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
                var location = NearestLocation(p);
                float pick = Hash((int)p.x, (int)p.y, salt);
                var kind = forest ? (pick < 0.45f ? CityShapes.Tree.Spruce : CityShapes.Tree.Round)
                                  : pick < 0.15f ? CityShapes.Tree.Poplar : CityShapes.Tree.Round;
                plan.Trees.Add(new TreeSpot { At = p, Size = size, Kind = kind, District = location == null ? null : map.DistrictOf(location.Id), Seed = (int)(p.x * 31 + p.y * 17) });
            }
            for (int pi = 0; pi < parks.Count; pi++)
            {
                var c = parks[pi];
                int count = Mathf.RoundToInt(c.z * c.z / 55f * density);
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
                bool forest = e > 1.06f && Noise(p.x, p.y, 60f, 52) > 0.55f && Hash(ix, iy, 53) < 0.8f * density;
                bool yard = e < 1f && Hash(ix, iy, 55) < 0.035f * density;
                bool bank = Nearest(p, river) < 30f && Hash(ix, iy, 56) < 0.35f * density;
                if (forest || yard || bank) AddTree(p, forest ? 0.07f + Hash(ix, iy, 54) * 0.04f : 0.05f + Hash(ix, iy, 54) * 0.03f, 57, forest);
            }
            return plan;
        }
    }
}
