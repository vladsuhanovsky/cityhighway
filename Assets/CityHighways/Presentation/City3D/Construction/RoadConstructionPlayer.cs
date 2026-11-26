using System;
using System.Collections.Generic;
using UnityEngine;
#if DOTWEEN
using DG.Tweening;
#endif

namespace CityHighways.Presentation
{
    /// <summary>
    /// Анимация строительства дороги между двумя узлами.
    ///
    ///   PlayConstruction(start, end, duration):
    ///   • дорога (RoadBuilder, с мостом над рекой) проявляется от начала к концу — «фронт укладки»;
    ///   • впереди фронта жёлтый экскаватор «копает» основание (стрела, рукоять, ковш, поворот башни, пыль);
    ///   • позади фронта каток «утрамбовывает» свежий асфальт (валец крутится по пройденному пути, корпус дрожит);
    ///   • 2–3 рабочих в оранжевых жилетах бегают туда-сюда вокруг фронта по обочинам, иногда перебегая дорогу
    ///     (DOTween DOPath по точкам пути; без DOTween — тот же путь сплайном Катмулла — Рома);
    ///   • в конце техника и рабочие задерживаются на миг, «схлопываются» и возвращаются в пул.
    ///
    /// Всё берётся из пулов (PrefabPool): во время стройки ничего не создаётся и не удаляется.
    /// Несколько строек могут идти одновременно.
    /// </summary>
    public sealed class RoadConstructionPlayer : MonoBehaviour
    {
        [SerializeField] RoadBuilder roadBuilder;
        [SerializeField] RoadConstructionSet settings;

        /// <summary>Стройка закончилась (дорога целиком лежит, техника ушла).</summary>
        public event Action<BuiltRoad> Completed;

        PrefabPool _workers, _excavators, _rollers, _dust;
        Transform _poolRoot;
        ConstructionFleetPool _fleet;
        Material _fleetMaterial;
        readonly List<Site> _sites = new List<Site>();
        readonly List<Dust> _puffs = new List<Dust>();

        /// <summary>Одна стройка: дорога, время, техника и рабочие.</summary>
        sealed class Site
        {
            public BuiltRoad Road;
            public float Duration, Time;
            public Action<BuiltRoad> Done;
            public GameObject Excavator, Roller;
            public FleetVehicleVisual Fleet;
            public bool CosmeticBridge;
            public Vector3 BridgePosition;
            public ConstructionMachine Digger, Compactor;
            public Vector3 RollerScale, ExcavatorScale;
            public float RollerDistance;   // где каток был в прошлом кадре — для вращения вальца
            public float LastDigPhase;
            public readonly List<Runner> Workers = new List<Runner>();
        }

        /// <summary>Рабочий и его путь — точки во времени (t от 0 до Duration).</summary>
        sealed class Runner
        {
            public GameObject Go;
            public ConstructionWorker Worker;
            public Vector3 Scale;
            public Vector3[] Path;
            public float HopOffset;
        }

        struct Dust
        {
            public GameObject Go;
            public Vector3 Start, Scale;
            public float Age;
        }

        /// <summary>Настроить из кода (City3DScene); в сцене можно задать поля в Inspector.</summary>
        public void Init(RoadBuilder builder, RoadConstructionSet set)
        {
            roadBuilder = builder;
            settings = set;
            CreatePools();
        }

        void Awake()
        {
            if (roadBuilder == null) roadBuilder = GetComponent<RoadBuilder>();
        }

        void Start()
        {
            if (_workers == null && settings != null) CreatePools();
        }

        void CreatePools()
        {
            if (settings == null) return;
            if (_poolRoot == null)
            {
                _poolRoot = new GameObject("Construction pool").transform;
                _poolRoot.SetParent(transform, false);
            }
            _workers = settings.workerPrefab != null ? new PrefabPool(settings.workerPrefab, _poolRoot, settings.prewarmWorkers) : null;
            _excavators = settings.excavatorPrefab != null ? new PrefabPool(settings.excavatorPrefab, _poolRoot, settings.prewarmMachines) : null;
            _rollers = settings.rollerPrefab != null ? new PrefabPool(settings.rollerPrefab, _poolRoot, settings.prewarmMachines) : null;
            _dust = settings.dustPrefab != null ? new PrefabPool(settings.dustPrefab, _poolRoot, settings.prewarmDust) : null;
        }

        /// <summary>Идёт ли стройка этой дороги.</summary>
        public bool IsBuilding(BuiltRoad road)
        {
            foreach (var site in _sites)
                if (site.Road == road && !site.CosmeticBridge) return true;
            return false;
        }

        // ================================================================== Запуск

        /// <summary>
        /// Построить дорогу от start до end с анимацией стройки длительностью duration секунд (обычно 2–3).
        /// Возвращает построенную дорогу (её куски проявляются по ходу стройки).
        /// </summary>
        public BuiltRoad PlayConstruction(Vector3 start, Vector3 end, float duration, Action<BuiltRoad> completed = null, FleetLivery livery = null)
        {
            if (roadBuilder == null || settings == null)
            {
                Debug.LogError("RoadConstructionPlayer: не заданы RoadBuilder или RoadConstructionSet.", this);
                return null;
            }
            if (_workers == null && _excavators == null && _rollers == null) CreatePools();

            var road = roadBuilder.BuildRoad(start, end, false);
            if (road == null) return null;
            RoadAppearAnimator.Reveal(road, 0f); // всё спрятано, проявится по фронту

            var site = new Site { Road = road, Duration = Mathf.Max(0.3f, duration), Done = completed };
            var forward = Forward(road);
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (_excavators != null)
            {
                site.Excavator = _excavators.Get(ExcavatorPosition(site, 0f), rotation, transform);
                site.Digger = site.Excavator.GetComponent<ConstructionMachine>();
                site.ExcavatorScale = site.Excavator.transform.localScale;
            }
            // Use the personalized vehicle in the existing roller position. For bridges
            // and missing/over-budget visuals retain the original pooled roller.
            if (livery != null && !road.HasBridge && road.Length >= .12f)
                site.Fleet = AcquireFleet(livery);
            if (site.Fleet == null && _rollers != null)
            {
                site.Roller = _rollers.Get(RollerPosition(site, 0f), rotation, transform);
                site.Compactor = site.Roller.GetComponent<ConstructionMachine>();
                site.RollerScale = site.Roller.transform.localScale;
            }
            if (_workers != null)
            {
                var random = new System.Random(Mathf.RoundToInt((start.x + end.z * 31f) * 1000f));
                int count = Mathf.Clamp(random.Next(settings.minWorkers, settings.maxWorkers + 1), 1, 8);
                for (int i = 0; i < count; i++) site.Workers.Add(SpawnWorker(site, i, random));
            }
            _sites.Add(site);
            UpdateSite(site, 0f);
            return road;
        }

        /// <summary>Short cosmetic crane display on an existing bridge. Never reveals,
        /// rebuilds, owns or hides bridge geometry and never emits construction completion.</summary>
        public bool PlayBridgeFleet(BuiltRoad road, FleetLivery livery)
        {
            if (settings == null || road?.Root == null || !road.Root.activeInHierarchy ||
                livery == null || livery.Vehicle != "crane" ||
                !BridgeFleetClaims.TryDeckPosition(road, out var position)) return false;
            foreach (var existing in _sites)
                if (existing.Road == road) return false;
            var fleet = AcquireFleet(livery);
            if (fleet == null) return false;
            var site = new Site { Road = road, Duration = 1.4f, Fleet = fleet,
                CosmeticBridge = true, BridgePosition = position };
            _sites.Add(site);
            UpdateSite(site, 0f);
            return true;
        }

        Runner SpawnWorker(Site site, int index, System.Random random)
        {
            var path = WorkerPath(site, index, random);
            var go = _workers.Get(path[0], Quaternion.LookRotation(Forward(site.Road), Vector3.up), transform);
            var runner = new Runner
            {
                Go = go,
                Worker = go.GetComponent<ConstructionWorker>(),
                Scale = go.transform.localScale,
                Path = path,
                HopOffset = (float)random.NextDouble()
            };
#if DOTWEEN
            // Бег по пути: DOPath через все точки за время стройки, взгляд — по ходу движения.
            go.transform.DOPath(path, site.Duration, PathType.CatmullRom)
                .SetEase(Ease.Linear)
                .SetLookAt(0.02f)
                .SetTarget(go.transform);
            // Подпрыгивание тела на бегу — бесконечный «маятник».
            if (runner.Worker != null && runner.Worker.body != null)
            {
                var body = runner.Worker.body;
                body.localPosition = Vector3.zero;
                body.DOLocalMoveY(settings.workerHopHeight, settings.workerHopTime)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(runner.HopOffset * settings.workerHopTime)
                    .SetTarget(body);
            }
#endif
            return runner;
        }

        /// <summary>
        /// Путь рабочего: точка каждые workerLegTime секунд — рядом с фронтом укладки в этот момент,
        /// попеременно впереди и позади него (туда-сюда), на своей обочине; иногда рабочий перебегает дорогу.
        /// </summary>
        Vector3[] WorkerPath(Site site, int index, System.Random random)
        {
            int legs = Mathf.Max(2, Mathf.CeilToInt(site.Duration / Mathf.Max(0.1f, settings.workerLegTime)));
            var points = new Vector3[legs + 1];
            var side = Side(site.Road);
            float sign = index % 2 == 0 ? 1f : -1f;
            float shoulder = roadBuilder.Prefabs.width / 2f + settings.workerShoulder;
            for (int k = 0; k <= legs; k++)
            {
                float time = site.Duration * k / legs;
                if (random.NextDouble() < 0.18) sign = -sign;             // перебежал на другую сторону
                float along = (k % 2 == 0 ? 1f : -1f) * settings.workerRange * (0.35f + 0.65f * (float)random.NextDouble())
                              - settings.workerRange * 0.3f;             // чаще — позади фронта, у свежего асфальта
                float lateral = sign * (shoulder + (float)random.NextDouble() * settings.workerShoulder);
                float distance = Mathf.Clamp(Front(site, time) + along, 0f, site.Road.Length);
                points[k] = Ground(site.Road, distance) + side * lateral;
            }
            return points;
        }

        // ================================================================== Каждый кадр

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                var site = _sites[i];
                if (site.Road.Root == null || (site.CosmeticBridge && !site.Road.Root.activeInHierarchy)) { Abort(site); _sites.RemoveAt(i); continue; }
                site.Time += dt;
                UpdateSite(site, dt);
                if (site.Time >= site.Duration + settings.linger + settings.popOut)
                {
                    Finish(site);
                    _sites.RemoveAt(i);
                }
            }
            UpdateDust(dt);
        }

        void UpdateSite(Site site, float dt)
        {
            var road = site.Road;
            float front = Front(site, site.Time);
            // Постепенное появление: куски вытягиваются до фронта, опоры поднимаются, когда фронт над ними.
            if (!site.CosmeticBridge) RoadAppearAnimator.Reveal(road, front);

            // Появление (пружинкой) и исчезновение (схлопывание) техники и рабочих.
            float appear = OutBack(Mathf.Clamp01(site.Time / Mathf.Max(0.01f, settings.popIn)));
            float vanish = 1f - InBack(Mathf.Clamp01((site.Time - site.Duration - settings.linger) / Mathf.Max(0.01f, settings.popOut)));
            float scale = Mathf.Max(0.0001f, appear * vanish);

            if (site.Excavator != null) UpdateExcavator(site, scale);
            if (site.Roller != null) UpdateRoller(site, scale);
            if (site.Fleet != null && site.Fleet.Root != null)
            {
                var t = site.Fleet.Root.transform;
                t.SetPositionAndRotation(site.CosmeticBridge ? site.BridgePosition : RollerPosition(site, site.Time), Quaternion.LookRotation(Forward(road), Vector3.up));
                // Original fleet geometry is ~2 units long; the existing machinery is ~0.08.
                t.localScale = Vector3.one * (.032f * scale);
                float distance = site.CosmeticBridge ? 0f : Mathf.Clamp(front - settings.rollerLag, 0f, road.Length) / .032f;
                if (Ui.ReducedMotion) site.Fleet.ResetMotion();
                else site.Fleet.SampleMotion(distance, site.Time, site.Time <= site.Duration + settings.linger);
            }
            foreach (var runner in site.Workers) UpdateWorker(site, runner, scale);
        }

        /// <summary>Экскаватор впереди фронта: едет вместе с ним и копает — стрела вниз, ковш загребает, башня отворачивает.</summary>
        void UpdateExcavator(Site site, float scale)
        {
            var t = site.Excavator.transform;
            t.position = ExcavatorPosition(site, site.Time);
            t.localScale = site.ExcavatorScale * scale;
            var m = site.Digger;
            if (m == null) return;
            float phase = Mathf.Repeat(site.Time / Mathf.Max(0.2f, settings.digCycle), 1f);
            float dig = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f); // 0 — стрела поднята, 1 — ковш в земле
            if (m.boom != null) m.boom.localRotation = Quaternion.Euler(Mathf.Lerp(-18f, 22f, dig), 0f, 0f);
            if (m.stick != null) m.stick.localRotation = Quaternion.Euler(Mathf.Lerp(25f, 70f, dig), 0f, 0f);
            if (m.bucket != null) m.bucket.localRotation = Quaternion.Euler(Mathf.Lerp(-50f, 35f, dig), 0f, 0f);
            if (m.turret != null) m.turret.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * Mathf.PI * 2f + 1.2f) * 28f, 0f);
            // В нижней точке — облачко пыли из-под ковша.
            if (site.LastDigPhase < 0.5f && phase >= 0.5f && scale > 0.5f)
            {
                var tip = m.bucketTip != null ? m.bucketTip : m.bucket != null ? m.bucket : t;
                SpawnDust(tip.position);
            }
            site.LastDigPhase = phase;
        }

        /// <summary>Каток позади фронта: валец крутится ровно на пройденный путь, корпус мелко дрожит.</summary>
        void UpdateRoller(Site site, float scale)
        {
            var t = site.Roller.transform;
            float distance = Mathf.Clamp(Front(site, site.Time) - settings.rollerLag, 0f, site.Road.Length);
            t.position = RollerPosition(site, site.Time);
            t.localScale = site.RollerScale * scale;
            var m = site.Compactor;
            if (m == null) return;
            float travelled = distance - site.RollerDistance;
            site.RollerDistance = distance;
            if (m.drum != null) m.drum.Rotate(Vector3.right, travelled / m.drumRadius * Mathf.Rad2Deg, Space.Self);
            if (m.chassis != null)
                m.chassis.localPosition = new Vector3(0f, Mathf.Sin(site.Time * 190f) * settings.rollerShake, 0f);
        }

        void UpdateWorker(Site site, Runner runner, float scale)
        {
            var t = runner.Go.transform;
            t.localScale = runner.Scale * scale;
#if !DOTWEEN
            // Без DOTween — тот же путь вручную: сплайн Катмулла — Рома по точкам, взгляд по ходу, прыжок тела.
            float u = Mathf.Clamp01(site.Time / site.Duration) * (runner.Path.Length - 1);
            int k = Mathf.Min(runner.Path.Length - 2, Mathf.FloorToInt(u));
            float f = u - k;
            var position = CatmullRom(runner.Path, k, f);
            var ahead = CatmullRom(runner.Path, k, Mathf.Min(1f, f + 0.05f));
            var look = ahead - position;
            look.y = 0f;
            t.position = position;
            if (look.sqrMagnitude > 1e-10f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 0.35f);
            if (runner.Worker != null && runner.Worker.body != null)
            {
                float hop = Mathf.Abs(Mathf.Sin((site.Time / Mathf.Max(0.02f, settings.workerHopTime) + runner.HopOffset) * Mathf.PI * 0.5f));
                runner.Worker.body.localPosition = new Vector3(0f, hop * settings.workerHopHeight, 0f);
            }
#endif
        }

        static Vector3 CatmullRom(Vector3[] p, int k, float t)
        {
            var p0 = p[Mathf.Max(0, k - 1)];
            var p1 = p[k];
            var p2 = p[Mathf.Min(p.Length - 1, k + 1)];
            var p3 = p[Mathf.Min(p.Length - 1, k + 2)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ================================================================== Пыль

        void SpawnDust(Vector3 position)
        {
            if (_dust == null) return;
            var go = _dust.Get(position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), _poolRoot);
            _puffs.Add(new Dust { Go = go, Start = position, Scale = go.transform.localScale, Age = 0f });
        }

        /// <summary>Облачко растёт, поднимается и тает (сжимается) за dustLife, потом — обратно в пул.</summary>
        void UpdateDust(float dt)
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var puff = _puffs[i];
                puff.Age += dt;
                float k = puff.Age / Mathf.Max(0.05f, settings.dustLife);
                if (k >= 1f || puff.Go == null)
                {
                    _dust.Release(puff.Go);
                    _puffs.RemoveAt(i);
                    continue;
                }
                float size = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * (0.6f + k);
                puff.Go.transform.localScale = puff.Scale * Mathf.Max(0.0001f, size);
                puff.Go.transform.position = puff.Start + Vector3.up * (0.012f * k);
                _puffs[i] = puff;
            }
        }

        // ================================================================== Конец стройки

        void Finish(Site site)
        {
            if (site.CosmeticBridge) { ReleaseAll(site); return; }
            RoadAppearAnimator.Reveal(site.Road, float.MaxValue); // дорога целиком, в конечном размере
            ReleaseAll(site);
            site.Done?.Invoke(site.Road);
            Completed?.Invoke(site.Road);
        }

        void Abort(Site site)
        {
            ReleaseAll(site);
        }

        public void Cancel(BuiltRoad road)
        {
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                if (_sites[i].Road != road) continue;
                Abort(_sites[i]);
                _sites.RemoveAt(i);
            }
        }

        FleetVehicleVisual AcquireFleet(FleetLivery livery)
        {
            try
            {
                if (_fleet == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (shader == null || !shader.isSupported || _poolRoot == null) return null;
                    _fleetMaterial = new Material(shader) { name = "Construction fleet paint", hideFlags = HideFlags.DontSave };
                    _fleet = new ConstructionFleetPool(_poolRoot, _fleetMaterial);
                }
                return _fleet.Acquire(livery);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Construction fleet unavailable: " + ex.Message);
                return null;
            }
        }

        void OnDestroy()
        {
            _fleet?.Clear();
            _fleet = null;
            if (_fleetMaterial != null)
            {
                if (Application.isPlaying) Destroy(_fleetMaterial); else DestroyImmediate(_fleetMaterial);
                _fleetMaterial = null;
            }
        }

        void ReleaseAll(Site site)
        {
            if (site.Excavator != null) _excavators.Release(site.Excavator);
            if (site.Roller != null) _rollers.Release(site.Roller);
            foreach (var runner in site.Workers)
                if (runner.Go != null) _workers.Release(runner.Go);
            if (site.Fleet != null) _fleet?.Release(site.Fleet);
            site.Fleet = null;
            site.Excavator = site.Roller = null;
            site.Workers.Clear();
        }

        /// <summary>Остановить твины объекта и его детей (вызывает пул при возврате объекта).</summary>
        public static void KillTweens(GameObject go)
        {
#if DOTWEEN
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) DOTween.Kill(t);
#endif
        }

        // ================================================================== Геометрия стройки

        /// <summary>Фронт укладки (метры от начала дороги) в момент time: плавный разгон и торможение.</summary>
        static float Front(Site site, float time)
        {
            float t = Mathf.Clamp01(time / site.Duration);
            float eased = t * t * (3f - 2f * t);
            return site.Road.Length * Mathf.Lerp(t, eased, 0.6f);
        }

        Vector3 ExcavatorPosition(Site site, float time)
        {
            float distance = Mathf.Clamp(Front(site, time) + settings.excavatorLead, 0f, site.Road.Length);
            return Ground(site.Road, distance) + Side(site.Road) * settings.excavatorSide;
        }

        Vector3 RollerPosition(Site site, float time)
        {
            float distance = Mathf.Clamp(Front(site, time) - settings.rollerLag, 0f, site.Road.Length);
            return Surface(site.Road, distance);
        }

        static Vector3 Forward(BuiltRoad road)
        {
            var flat = road.End - road.Start;
            flat.y = 0f;
            return flat.sqrMagnitude > 1e-10f ? flat.normalized : Vector3.forward;
        }

        static Vector3 Side(BuiltRoad road)
        {
            var f = Forward(road);
            return new Vector3(f.z, 0f, -f.x);
        }

        /// <summary>Точка на земле под дорогой.</summary>
        static Vector3 Ground(BuiltRoad road, float distance) => road.PointAt(distance);

        /// <summary>Точка на покрытии: высота куска плана в этом месте (дорога, подъезд или мост).</summary>
        static Vector3 Surface(BuiltRoad road, float distance)
        {
            float height = 0f;
            foreach (var piece in road.Pieces)
            {
                if (distance < piece.From || distance > piece.To) continue;
                float k = piece.To > piece.From ? (distance - piece.From) / (piece.To - piece.From) : 0f;
                height = Mathf.Lerp(piece.FromHeight, piece.ToHeight, k);
                break;
            }
            return road.PointAt(distance) + Vector3.up * height;
        }

        static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        static float InBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }
    }
}
