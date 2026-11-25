using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Полноценный 3D, этап 2: всё, что раньше рисовал плоский слой поверх города, теперь живёт в сцене.
    ///   • Дороги: построенная дорога — 3D-асфальт (RoadBuilder) с бордюрами цвета владельца и белой разметкой.
    ///     Городские дороги магната — без цветных бордюров.
    ///   • Станции: у каждой станции (кроме хабов магната — у них свои комплексы) павильон остановки в самом
    ///     широком промежутке между дорогами: модель из Scenario или процедурный навес со знаком.
    ///   • Машины: легковушки и автобусы едут по построенным дорогам в обе стороны по своим полосам; чем больше
    ///     загрузка дороги, тем их больше, в пробке ползут.
    ///   • Фуры магната: модель из Scenario (или процедурная), окрашенная в цвет компании, с ящиком груза цвета вида.
    /// Плоский слой оставляет себе только подсказки: подсветку путей, пунктир свободных дорог, цены, рамки.
    /// </summary>
    public sealed partial class City3DScene
    {
        /// <summary>Есть ли в сцене 3D-фуры (тогда плоский слой рисует только их кольца выбора и часы пробок).</summary>
        public bool HasTrucks => _truckViews.Count > 0;

        // ================================================================== Бордюры и разметка

        readonly Dictionary<string, (GameObject go, int owner, BuiltRoad road)> _curbs = new Dictionary<string, (GameObject, int, BuiltRoad)>();
        readonly Dictionary<int, Material> _ownerMaterials = new Dictionary<int, Material>();

        /// <summary>
        /// Материал полос и бордюров цвета компании. Копия CityBuildings.mat, а в нём включено свечение белым
        /// (_EmissionColor = 1) без карты свечения — копия светилась чисто белым, и цвет владельца не был виден:
        /// у построенных дорог было белое обрамление. Теперь свечение — слабое, в цвет владельца (полосы видны и
        /// в тени, и ночью), без бликов.
        /// </summary>
        Material OwnerMaterial(int owner, Color color)
        {
            if (!_ownerMaterials.TryGetValue(owner, out var material))
            {
                material = LoadMaterial("CityBuildings", color);
                material.SetTexture("_BaseMap", CityAtlas.Albedo);
                material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                material.SetColor("_EmissionColor", color * 0.3f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetFloat("_Smoothness", 0.2f);
                NoSpecular(material);
                _ownerMaterials[owner] = material;
            }
            return material;
        }

        /// <summary>
        /// Бордюры цвета владельца и разметка на каждой построенной дороге (кроме мостов и дорог в стройке).
        /// ownerColor(владелец) — цвет компании; publicOwner — номер «города» (его дороги без цветных бордюров).
        /// </summary>
        public void SyncRoadDetails(Game game, Func<int, Color> ownerColor, int publicOwner)
        {
            if (_root == null || game == null) return;
            foreach (var road in game.Map.Roads)
            {
                var owner = game.GetRoadOwner(road.Id);
                // Мосты тоже: на них — только полосы цвета владельца (без бордюров и разметки по земле).
                bool hasRoad = _builtRoads.TryGetValue(road.Id, out var built);
                bool want = owner.HasValue && hasRoad && built != null && built.Root != null && !_constructing.Contains(road.Id);
                bool has = _curbs.TryGetValue(road.Id, out var curb);
                // Даже при прежнем владельце заново построенная дорога может иметь другое полотно.
                if (has && (!want || curb.owner != owner.Value || !ReferenceEquals(curb.road, built) || curb.go == null))
                {
                    if (curb.go != null)
                    {
                        curb.go.SetActive(false);
                        Destroy(curb.go);
                    }
                    _curbs.Remove(road.Id);
                    has = false;
                }
                if (!want || has) continue;
                bool isPublic = owner.Value >= publicOwner;
                var go = BuildCurbs(road.Id, built, isPublic ? null : OwnerMaterial(owner.Value, ownerColor(owner.Value)),
                    road.IsBridge || built.HasBridge);
                _curbs[road.Id] = (go, owner.Value, built);
            }
        }

        const float RoadTop = 0.006f;

        /// <summary>
        /// Все детали строятся по окончательному BuiltRoad, а не повторно по координатам станций.
        /// На мостах остаются только цветные полосы, в том числе на подъездах.
        /// </summary>
        GameObject BuildCurbs(string roadId, BuiltRoad built, Material curbMaterial, bool bridge)
        {
            var root = new GameObject("Road details " + roadId);
            root.transform.SetParent(_root.transform, false);
            if (built.Length < 0.01f) return root;
            float edge = RoadWidth / 2f - 0.004f;
            var white = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);

            if (curbMaterial != null)
            {
                var bands = new MeshBuilder();
                var paint = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
                float outer = RoadWidth / 2f - (bridge ? 0.006f : 0.008f), inner = outer - 0.014f;
                foreach (var q in RoadMarkingGeometry.Strips(built, 0f, built.Length, inner, outer, 0.0006f))
                    bands.Quad(q.A, q.B, q.C, q.D, paint);
                foreach (var q in RoadMarkingGeometry.Strips(built, 0f, built.Length, -outer, -inner, 0.0006f))
                    bands.Quad(q.A, q.B, q.C, q.D, paint);
                if (bands.VertexCount > 0) AddMesh("Owner bands", bands.ToMesh("City3D Owner " + roadId), curbMaterial, root.transform, false);
            }
            if (bridge) return root;

            if (curbMaterial != null)
            {
                var curbs = new MeshBuilder();
                var solid = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
                var across = RoadMarkingGeometry.Across(built);
                foreach (var piece in built.Pieces)
                {
                    if (piece.Kind != RoadPieceKind.Road || piece.To - piece.From < 1e-5f) continue;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var offset = across * (edge * side) + Vector3.up * 0.0015f;
                        var a = RoadMarkingGeometry.SurfaceAt(built, piece, piece.From) + offset;
                        var b = RoadMarkingGeometry.SurfaceAt(built, piece, piece.To) + offset;
                        curbs.Beam(a, b, 0.006f, 0.003f, solid, solid);
                    }
                }
                if (curbs.VertexCount > 0) AddMesh("Curbs", curbs.ToMesh("City3D Curbs " + roadId), curbMaterial, root.transform, false);
            }

            var marks = new MeshBuilder();
            foreach (var q in RoadMarkingGeometry.LaneMarks(built))
                marks.Quad(q.A, q.B, q.C, q.D, white);
            if (marks.VertexCount > 0) AddMesh("Lane marks", marks.ToMesh("City3D Marks " + roadId), _buildings, root.transform, false);
            return root;
        }

        // ================================================================== Павильоны станций

        /// <summary>
        /// Павильон у каждой станции (не у хабов магната): в самом широком промежутке между её дорогами,
        /// входом к перекрёстку. Модель из Scenario (Models3D.StationStop) или процедурный навес со знаком.
        /// Павильоны районов-расширений прячутся вместе с районом.
        /// </summary>
        void BuildStations()
        {
            var hubs = new HashSet<string>(_hubSites ?? new List<string>());
            bool model = Models3D.Has(Models3D.StationStop);
            var builders = new Dictionary<string, MeshBuilder>();
            var roots = new Dictionary<string, GameObject>();
            var concrete = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeConcrete);
            var roof = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
            var glass = CityAtlas.Uv(CityAtlas.Row.Tall, 0);
            var post = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
            var sign = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofRed);
            var local = new MeshBuilder();
            foreach (var location in _map.Locations)
            {
                if (hubs.Contains(location.Id)) continue;
                var center = BoardLayout.World(location.X, location.Y);
                float direction = WidestGap(location.Id, center);
                var forward = new Vector3(Mathf.Cos(direction), 0f, Mathf.Sin(direction));
                var at = center + forward * 0.105f;
                at.y = Relief.At(at.x, at.z);
                // Вход (−Z модели) — к перекрёстку.
                float yaw = -direction * Mathf.Rad2Deg + 90f;
                _stopSpots.Add((location.Id, at, Quaternion.Euler(0f, yaw, 0f)));
                string key = _map.GetDistrict(location.DistrictId)?.IsExpansion == true ? location.DistrictId : "_";
                if (model)
                {
                    if (!roots.TryGetValue(key, out var root))
                    {
                        root = new GameObject("Station stops " + key);
                        root.transform.SetParent(_root.transform, false);
                        roots[key] = root;
                    }
                    Models3D.Instantiate(Models3D.StationStop, root.transform, at, yaw + 180f, 0.06f);
                    continue;
                }
                local.Clear();
                // Плита, стеклянная стенка сзади, крыша на четырёх стойках и знак на столбе.
                local.Box(0f, 0f, 0.05f, 0.026f, 0f, 0.003f, concrete, concrete);
                local.Box(0f, 0.011f, 0.04f, 0.002f, 0.003f, 0.016f, glass, glass);
                foreach (var (x, z) in new[] { (-0.019f, -0.009f), (0.019f, -0.009f), (-0.019f, 0.011f), (0.019f, 0.011f) })
                    local.Box(x, z, 0.0016f, 0.0016f, 0.003f, 0.017f, post, post);
                local.Box(0f, 0.001f, 0.046f, 0.026f, 0.02f, 0.0018f, roof, roof);
                local.Prism(0.03f, -0.008f, 0.0011f, 0f, 0.03f, 5, post, post);
                local.Prism(0.03f, -0.008f, 0.0055f, 0.03f, 0.0016f, 10, sign, sign);
                if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                b.Append(local, Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            }
            foreach (var pair in builders)
            {
                var go = AddMesh("Station stops " + pair.Key, pair.Value.ToMesh("City3D Stops " + pair.Key), _buildings, _root.transform, true);
                if (pair.Key != "_") _districts[pair.Key + "#stops"] = go;
            }
            foreach (var pair in roots)
                if (pair.Key != "_") _districts[pair.Key + "#stopmodels"] = pair.Value;
        }

        /// <summary>Направление (радианы, в плоскости XZ) середины самого широкого промежутка между дорогами станции.</summary>
        float WidestGap(string locationId, Vector3 center)
        {
            var angles = new List<float>();
            foreach (var road in _map.RoadsAt(locationId))
            {
                var other = _map.GetLocation(road.A == locationId ? road.B : road.A);
                if (other == null) continue;
                var w = BoardLayout.World(other.X, other.Y) - center;
                angles.Add(Mathf.Atan2(w.z, w.x));
            }
            if (angles.Count == 0) return -Mathf.PI / 2f;
            angles.Sort();
            float direction = 0f, best = -1f;
            for (int i = 0; i < angles.Count; i++)
            {
                float a = angles[i], b = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Mathf.PI * 2f;
                if (b - a > best) { best = b - a; direction = (a + b) / 2f; }
            }
            return direction;
        }

        // ================================================================== Машины на дорогах

        sealed class Car
        {
            public GameObject Go;
            public float Phase, Speed;
            public bool Bus;
        }

        readonly Dictionary<string, List<Car>> _cars = new Dictionary<string, List<Car>>();
        Mesh[] _carMeshes;
        Mesh _busMesh;
        GameObject _carRoot;
        MaterialPropertyBlock _tint;

        static readonly Color[] CarColors =
        {
            new Color(0.92f, 0.92f, 0.94f), new Color(0.80f, 0.18f, 0.16f), new Color(0.20f, 0.36f, 0.72f),
            new Color(0.15f, 0.16f, 0.18f), new Color(0.95f, 0.78f, 0.20f), new Color(0.55f, 0.58f, 0.62f),
            new Color(0.25f, 0.55f, 0.32f)
        };

        /// <summary>
        /// Машины на построенных дорогах (кроме мостов и дорог в стройке): одна-две на дорогу и ещё по одной
        /// на каждую единицу загрузки (не больше шести), иногда автобус. Едут туда-обратно по своим полосам;
        /// в пробке ползут. time — время партии в секундах.
        /// </summary>
        /// <summary>Слои карты: машины на дорогах и пешеходы (выключенные — спрятаны и не двигаются).</summary>
        public bool ShowTraffic = true, ShowPeople = true;

        public void SyncTraffic(Game game, float time)
        {
            if (_root == null || game == null) return;
            if (_carRoot == null)
            {
                _carRoot = new GameObject("Traffic");
                _carRoot.transform.SetParent(_root.transform, false);
            }
            if (_carRoot.activeSelf != ShowTraffic) _carRoot.SetActive(ShowTraffic);
            if (!ShowTraffic) return;
            foreach (var road in game.Map.Roads)
            {
                var owner = game.GetRoadOwner(road.Id);
                bool drive = owner.HasValue && !road.IsBridge && _builtRoads.ContainsKey(road.Id) && !_constructing.Contains(road.Id)
                             && game.IsRoadOpen(road.Id);
                int want = 0;
                if (drive)
                {
                    want = road.Length >= 4 ? 2 : 1;
                    if (game.IsRushHourRoad(road.Id)) want++;
                    want = Mathf.Min(6, want + game.TrafficOn(road.Id));
                }
                _cars.TryGetValue(road.Id, out var cars);
                if (want == 0)
                {
                    if (cars != null)
                    {
                        foreach (var car in cars) if (car.Go != null) Destroy(car.Go);
                        _cars.Remove(road.Id);
                    }
                    continue;
                }
                if (cars == null) _cars[road.Id] = cars = new List<Car>();
                int seed = road.Id.GetHashCode();
                while (cars.Count < want) cars.Add(MakeCar(seed, cars.Count));
                while (cars.Count > want)
                {
                    var last = cars[cars.Count - 1];
                    if (last.Go != null) Destroy(last.Go);
                    cars.RemoveAt(cars.Count - 1);
                }

                var la = game.Map.GetLocation(road.A);
                var lb = game.Map.GetLocation(road.B);
                if (la == null || lb == null) continue;
                var start = BoardLayout.World(la.X, la.Y);
                var end = BoardLayout.World(lb.X, lb.Y);
                TrimForJunctions(ref start, ref end);
                var along = end - start;
                float length = Mathf.Max(0.01f, along.magnitude);
                along /= length;
                var across = new Vector3(-along.z, 0f, along.x);
                bool jammed = game.IsJammed(road.Id);
                foreach (var car in cars)
                {
                    float speed = (jammed ? 0.02f : car.Speed) / length;
                    float run = time * speed + car.Phase;
                    float t = Mathf.PingPong(run, 1f);
                    bool forward = Mathf.Repeat(run, 2f) < 1f;
                    // Правостороннее движение: полоса справа по ходу.
                    var lane = across * (forward ? -0.022f : 0.022f);
                    var position = Vector3.Lerp(start, end, Mathf.Lerp(0.04f, 0.96f, t)) + lane + Vector3.up * RoadTop;
                    car.Go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward ? along : -along, Vector3.up));
                }
            }
            // Дороги, которых больше нет (снесли, перешли к другому), — машины убираем.
            List<string> gone = null;
            foreach (var pair in _cars)
                if (game.Map.GetRoad(pair.Key) == null || !game.GetRoadOwner(pair.Key).HasValue)
                    (gone ??= new List<string>()).Add(pair.Key);
            if (gone != null)
                foreach (var id in gone)
                {
                    foreach (var car in _cars[id]) if (car.Go != null) Destroy(car.Go);
                    _cars.Remove(id);
                }
        }

        Car MakeCar(int seed, int index)
        {
            var random = new System.Random(seed * 31 + index * 7919);
            var car = new Car
            {
                Phase = (float)random.NextDouble() * 2f,
                Speed = 0.10f + (float)random.NextDouble() * 0.06f,
                Bus = random.NextDouble() < 0.12
            };
            var color = CarColors[random.Next(CarColors.Length)];
            string modelId = car.Bus ? Models3D.Bus : Models3D.Car;
            if (Models3D.Has(modelId))
            {
                // Модель стоит основанием на земле, центр — в нуле обёртки; поворачиваем обёртку целиком.
                var holder = new GameObject(car.Bus ? "Bus" : "Car");
                holder.transform.SetParent(_carRoot.transform, false);
                float along = Models3D.LongAlongX(modelId) ? 90f : 0f;
                var model = Models3D.Instantiate(modelId, holder.transform, Vector3.zero, along, car.Bus ? 0.05f : 0.026f);
                if (model != null && !car.Bus) Tint(model, color, 0.65f);
                car.Go = holder;
                return car;
            }
            EnsureCarMeshes();
            var go = new GameObject(car.Bus ? "Bus" : "Car");
            go.transform.SetParent(_carRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = car.Bus ? _busMesh : _carMeshes[random.Next(_carMeshes.Length)];
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _buildings;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            car.Go = go;
            return car;
        }

        /// <summary>Процедурные машины (если моделей нет): корпус цвета из атласа, салон со стёклами; автобус — длинный.</summary>
        void EnsureCarMeshes()
        {
            if (_carMeshes != null) return;
            var paints = new[] { CityAtlas.RoofLight, CityAtlas.RoofRed, CityAtlas.RoofDark, CityAtlas.RoofCopper, CityAtlas.RoofBrown, CityAtlas.RoofMetal };
            var glass = CityAtlas.Uv(CityAtlas.Row.Tall, 2);
            var tire = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
            _carMeshes = new Mesh[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                var paint = CityAtlas.Solid(CityAtlas.Row.Roof, paints[i]);
                var b = new MeshBuilder();
                b.Box(0f, 0f, 0.012f, 0.026f, 0.0015f, 0.0055f, paint, paint);
                b.Box(0f, -0.002f, 0.0105f, 0.013f, 0.007f, 0.004f, glass, paint);
                foreach (float z in new[] { -0.008f, 0.008f })
                    foreach (float x in new[] { -0.006f, 0.006f })
                        b.Box(x, z, 0.0016f, 0.005f, 0f, 0.003f, tire, tire);
                _carMeshes[i] = b.ToMesh("City3D Car " + i);
                _owned.Add(_carMeshes[i]);
            }
            var yellow = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire);
            var bus = new MeshBuilder();
            bus.Box(0f, 0f, 0.014f, 0.05f, 0.0015f, 0.011f, CityAtlas.Uv(CityAtlas.Row.Tall, 1), yellow);
            _busMesh = bus.ToMesh("City3D Bus");
            _owned.Add(_busMesh);
        }

        /// <summary>Подкрасить модель: и у материалов URP (_BaseColor), и у glTFast (baseColorFactor).</summary>
        void Tint(GameObject model, Color color, float amount)
        {
            _tint ??= new MaterialPropertyBlock();
            var c = Color.Lerp(Color.white, color, amount);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(_tint);
                _tint.SetColor("_BaseColor", c);
                _tint.SetColor("baseColorFactor", c);
                renderer.SetPropertyBlock(_tint);
            }
        }

        // ================================================================== Фуры магната

        /// <summary>Фура для сцены: где стоит, куда смотрит (в мире), цвет компании и цвет груза (null — пустая).</summary>
        public struct TruckView
        {
            public int Id;
            public Vector3 Position, Heading;
            public Color Company;
            public Color? Cargo;
        }

        sealed class TruckObject
        {
            public GameObject Root, Cargo;
            public Color Company;
            public Color? CargoColor;
            bool _cargoSet;
            public bool CargoChanged(Color? c)
            {
                bool changed = !_cargoSet || c.HasValue != CargoColor.HasValue || (c.HasValue && c.Value != CargoColor.Value);
                _cargoSet = true;
                CargoColor = c;
                return changed;
            }
        }

        readonly Dictionary<int, TruckObject> _truckViews = new Dictionary<int, TruckObject>();
        Mesh _cargoMesh, _cabMesh, _trailerMesh;
        readonly HashSet<int> _seenTrucks = new HashSet<int>();

        /// <summary>
        /// Поставить фуры: модель из Scenario (или процедурная кабина и кузов), кузов — в цвет компании, сверху ящик
        /// цвета груза. Фуры, которых нет в списке, убираются.
        /// </summary>
        public void SyncTrucks(IReadOnlyList<TruckView> trucks)
        {
            if (_root == null) return;
            _seenTrucks.Clear();
            foreach (var view in trucks)
            {
                _seenTrucks.Add(view.Id);
                if (!_truckViews.TryGetValue(view.Id, out var truck))
                {
                    truck = MakeTruck(view.Company);
                    _truckViews[view.Id] = truck;
                }
                var heading = view.Heading;
                heading.y = 0f;
                if (heading.sqrMagnitude < 1e-8f) heading = Vector3.forward;
                truck.Root.transform.SetPositionAndRotation(view.Position + Vector3.up * RoadTop, Quaternion.LookRotation(heading.normalized, Vector3.up));
                if (truck.CargoChanged(view.Cargo))
                {
                    truck.Cargo.SetActive(view.Cargo.HasValue);
                    if (view.Cargo.HasValue) Tint(truck.Cargo, view.Cargo.Value, 1f);
                }
            }
            List<int> gone = null;
            foreach (var pair in _truckViews)
                if (!_seenTrucks.Contains(pair.Key)) (gone ??= new List<int>()).Add(pair.Key);
            if (gone != null)
                foreach (var id in gone)
                {
                    if (_truckViews[id].Root != null) Destroy(_truckViews[id].Root);
                    _truckViews.Remove(id);
                }
        }

        const float TruckLength = 0.05f;

        TruckObject MakeTruck(Color company)
        {
            var root = new GameObject("Truck");
            root.transform.SetParent(_root.transform, false);
            var truck = new TruckObject { Root = root, Company = company };
            float roofHeight = 0.014f;
            if (Models3D.Has(Models3D.Truck))
            {
                float along = Models3D.LongAlongX(Models3D.Truck) ? 90f : 0f;
                var model = Models3D.Instantiate(Models3D.Truck, root.transform, Vector3.zero, along, TruckLength);
                if (model != null) Tint(model, company, 0.55f);
            }
            else
            {
                EnsureTruckMeshes();
                var trailer = Part(root.transform, "Trailer", _trailerMesh, new Vector3(0f, 0f, -0.006f));
                Tint(trailer, company, 1f);
                var cab = Part(root.transform, "Cab", _cabMesh, new Vector3(0f, 0f, 0.018f));
                Tint(cab, Color.Lerp(company, Color.white, 0.3f), 1f);
                roofHeight = 0.0135f;
            }
            EnsureTruckMeshes();
            truck.Cargo = Part(root.transform, "Cargo", _cargoMesh, new Vector3(0f, roofHeight, -0.006f));
            truck.Cargo.SetActive(false);
            return truck;
        }

        GameObject Part(Transform parent, string name, Mesh mesh, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _buildings;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return go;
        }

        void EnsureTruckMeshes()
        {
            if (_cargoMesh != null) return;
            var white = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
            var glass = CityAtlas.Uv(CityAtlas.Row.Tall, 2);
            var tire = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
            var cargo = new MeshBuilder();
            cargo.Box(0f, 0f, 0.009f, 0.016f, 0f, 0.005f, white, white);
            _cargoMesh = cargo.ToMesh("City3D Truck Cargo");
            var trailer = new MeshBuilder();
            trailer.Box(0f, 0f, 0.013f, 0.034f, 0.003f, 0.0105f, white, white);
            foreach (float z in new[] { -0.012f, -0.006f })
                foreach (float x in new[] { -0.0062f, 0.0062f })
                    trailer.Box(x, z, 0.0018f, 0.0045f, 0f, 0.0035f, tire, tire);
            _trailerMesh = trailer.ToMesh("City3D Truck Trailer");
            var cab = new MeshBuilder();
            cab.Box(0f, 0f, 0.012f, 0.011f, 0.002f, 0.0095f, white, white);
            cab.Box(0f, 0.0045f, 0.0105f, 0.0025f, 0.0065f, 0.004f, glass, glass);
            foreach (float x in new[] { -0.0058f, 0.0058f })
                cab.Box(x, 0f, 0.0018f, 0.0045f, 0f, 0.0035f, tire, tire);
            _cabMesh = cab.ToMesh("City3D Truck Cab");
            _owned.Add(_cargoMesh);
            _owned.Add(_trailerMesh);
            _owned.Add(_cabMesh);
        }

        // ================================================================== Люди

        readonly List<(string location, Vector3 at, Quaternion turn)> _stopSpots = new List<(string, Vector3, Quaternion)>();
        readonly Dictionary<string, (Vector3 origin, Quaternion turn)> _hubSpots = new Dictionary<string, (Vector3, Quaternion)>();

        sealed class Walker
        {
            public GameObject Go;
            public string Location;
            public Vector3 A, B;
            public float Phase, Speed;
            public bool OnPad;     // по площадке хаба (высота площадки), а не по земле
        }

        readonly List<Walker> _walkers = new List<Walker>();
        int _walkersHubCount = -1;
        Mesh[] _personMeshes;
        GameObject _peopleRoot;

        /// <summary>
        /// Пешеходы: у каждого павильона станции двое-трое ходят вдоль входа, у хабов — по полдюжины перед
        /// главным зданием. Станции закрытых районов — без людей. time — время партии в секундах.
        /// </summary>
        public void SyncPeople(Game game, float time)
        {
            if (_root == null || game == null) return;
            if (_peopleRoot == null)
            {
                _peopleRoot = new GameObject("People");
                _peopleRoot.transform.SetParent(_root.transform, false);
            }
            if (_peopleRoot.activeSelf != ShowPeople) _peopleRoot.SetActive(ShowPeople);
            if (!ShowPeople) return;
            if (_walkersHubCount != _hubSpots.Count)
            {
                foreach (var walker in _walkers) if (walker.Go != null) Destroy(walker.Go);
                _walkers.Clear();
                BuildWalkers();
                _walkersHubCount = _hubSpots.Count;
            }
            foreach (var walker in _walkers)
            {
                bool open = game.IsLocationOpen(walker.Location);
                if (walker.Go.activeSelf != open) walker.Go.SetActive(open);
                if (!open) continue;
                float run = time * walker.Speed + walker.Phase;
                float t = Mathf.PingPong(run, 1f);
                bool there = Mathf.Repeat(run, 2f) < 1f;
                var p = Vector3.Lerp(walker.A, walker.B, t);
                if (!walker.OnPad) p.y = Relief.At(p.x, p.z);
                var heading = there ? walker.B - walker.A : walker.A - walker.B;
                heading.y = 0f;
                walker.Go.transform.SetPositionAndRotation(p, heading.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(heading, Vector3.up) : Quaternion.identity);
            }
        }

        void BuildWalkers()
        {
            EnsurePersonMeshes();
            int seed = 1;
            void Add(string location, Vector3 a, Vector3 b, bool onPad)
            {
                var random = new System.Random(seed++ * 7919);
                var go = new GameObject("Walker");
                go.transform.SetParent(_peopleRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _personMeshes[random.Next(_personMeshes.Length)];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _buildings;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                _walkers.Add(new Walker
                {
                    Go = go, Location = location, A = a, B = b, OnPad = onPad,
                    Phase = (float)random.NextDouble() * 2f,
                    Speed = 0.05f + (float)random.NextDouble() * 0.05f
                });
            }
            foreach (var (location, at, turn) in _stopSpots)
            {
                int count = 2 + Mathf.Abs(location.GetHashCode()) % 2;
                for (int i = 0; i < count; i++)
                {
                    // Вдоль входа павильона (вход — −Z), на разном удалении от него.
                    float z = -0.02f - i * 0.006f;
                    Add(location, at + turn * new Vector3(-0.028f, 0f, z), at + turn * new Vector3(0.028f, 0f, z), false);
                }
            }
            foreach (var pair in _hubSpots)
            {
                var (origin, turn) = pair.Value;
                for (int i = 0; i < 6; i++)
                {
                    float z = -0.068f - (i % 3) * 0.008f, x0 = -0.065f + (i / 3) * 0.02f;
                    Add(pair.Key, origin + turn * new Vector3(x0, 0.0035f, z), origin + turn * new Vector3(x0 + 0.09f, 0.0035f, z), true);
                }
            }
        }

        /// <summary>Человечек 1,1 см: туловище цвета одежды, голова. Шесть вариантов одежды.</summary>
        void EnsurePersonMeshes()
        {
            if (_personMeshes != null) return;
            var clothes = new[] { CityAtlas.RoofRed, CityAtlas.RoofDark, CityAtlas.RoofLight, CityAtlas.RoofCopper, CityAtlas.RoofBrown, CityAtlas.RoofMetal };
            var skin = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBrown);
            var legs = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
            _personMeshes = new Mesh[clothes.Length];
            for (int i = 0; i < clothes.Length; i++)
            {
                var cloth = CityAtlas.Solid(CityAtlas.Row.Roof, clothes[i]);
                var b = new MeshBuilder();
                b.Box(0f, 0f, 0.0026f, 0.0016f, 0f, 0.0045f, legs, legs);
                b.Prism(0f, 0f, 0.0017f, 0.0045f, 0.0045f, 6, cloth, cloth);
                b.Blob(new Vector3(0f, 0.0104f, 0f), 0.0014f, 0.0014f, skin);
                _personMeshes[i] = b.ToMesh("City3D Person " + i);
                _owned.Add(_personMeshes[i]);
            }
        }

        /// <summary>Очистить живые объекты (вызывается из Dispose вместе со сценой).</summary>
        void ClearLife()
        {
            _stopSpots.Clear();
            _hubSpots.Clear();
            _walkers.Clear();
            _walkersHubCount = -1;
            _personMeshes = null;
            _peopleRoot = null;
            _curbs.Clear();
            _ownerMaterials.Clear();
            _cars.Clear();
            _truckViews.Clear();
            _carRoot = null;
            _carMeshes = null;
            _busMesh = null;
            _cargoMesh = _cabMesh = _trailerMesh = null;
        }
    }
}
