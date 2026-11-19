using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Вид куска дороги — по нему выбирается префаб.</summary>
    public enum RoadPieceKind { Road, Ramp, Bridge }

    /// <summary>Кусок плана дороги: расстояния вдоль дороги (по горизонтали) и высота верха над землёй в начале и конце.</summary>
    public struct RoadPiece
    {
        public RoadPieceKind Kind;
        public float From, To;
        public float FromHeight, ToHeight;
    }

    /// <summary>Построенная дорога: корневой объект, план и то, что нужно анимации появления.</summary>
    public sealed class BuiltRoad
    {
        public GameObject Root;
        public Vector3 Start, End;
        public float Length;
        public readonly List<RoadPiece> Pieces = new List<RoadPiece>();
        public readonly List<WaterSpan> WaterSpans = new List<WaterSpan>();
        public bool HasBridge => WaterSpans.Count > 0;

        /// <summary>Что и как растёт при появлении: куски и перила — вдоль дороги (Z), опоры — снизу вверх (Y).</summary>
        public readonly List<(Transform transform, Vector3 scale, float at, bool rise)> Growing =
            new List<(Transform, Vector3, float, bool)>();

        public Vector3 PointAt(float distance) => Length <= 0f ? Start : Vector3.Lerp(Start, End, distance / Length);
    }

    /// <summary>
    /// Строит дорогу между двумя узлами из префабов RoadPrefabSet. Если отрезок проходит над рекой, на этом
    /// участке автоматически встаёт мост: полотно от берега до берега с заходом на набережные (bankMargin),
    /// плавные подъезды (rampLength) с обеих сторон, опоры над водой и перила. Все куски — одной ширины
    /// width, коллайдеры растягиваются вместе с кусками, соседние куски чуть нахлёстываются — без щелей.
    ///
    /// Использование: builder.BuildRoad(nodeA.position, nodeB.position);
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadBuilder : MonoBehaviour
    {
        [SerializeField] RoadPrefabSet prefabs;
        [Tooltip("Куда класть построенные дороги (без масштаба); пусто — этот объект")]
        [SerializeField] Transform container;
        [Tooltip("Плавное появление (DOTween; без DOTween — встроенная анимация)")]
        [SerializeField] bool animate = true;

        RiverCrossingDetector _detector;

        public RoadPrefabSet Prefabs
        {
            get => prefabs;
            set { prefabs = value; _detector = null; }
        }

        public bool Animate { get => animate; set => animate = value; }
        public Transform Container { get => container; set => container = value; }

        /// <summary>Поиск реки. По умолчанию — из настроек набора (LayerMask «Water»); можно подменить своим.</summary>
        public RiverCrossingDetector Detector
        {
            get
            {
                if (_detector == null && prefabs != null)
                    _detector = new RiverCrossingDetector(prefabs.detection, prefabs.WaterMaskOrDefault, prefabs.rayHeight);
                return _detector;
            }
            set => _detector = value;
        }

        // ================================================================== Строительство

        /// <summary>Построить дорогу от start до end (мост — сам, если на пути река).</summary>
        public BuiltRoad BuildRoad(Vector3 start, Vector3 end) => BuildRoad(start, end, animate, null);

        /// <param name="animated">Плавное появление.</param>
        /// <param name="forcedSpan">Участок моста вручную (если река не найдена, а мост нужен по правилам игры).</param>
        public BuiltRoad BuildRoad(Vector3 start, Vector3 end, bool animated, WaterSpan? forcedSpan = null)
        {
            if (prefabs == null)
            {
                Debug.LogError("RoadBuilder: не задан RoadPrefabSet.", this);
                return null;
            }
            var road = new BuiltRoad { Start = start, End = end };
            var flat = end - start;
            flat.y = 0f;
            road.Length = flat.magnitude;
            if (forcedSpan.HasValue) road.WaterSpans.Add(forcedSpan.Value);
            else if (Detector != null) road.WaterSpans.AddRange(Detector.FindSpans(start, end, prefabs.sampleStep));
            road.Pieces.AddRange(PlanRoad(road.Length, road.WaterSpans));

            road.Root = new GameObject(road.HasBridge ? "Road + bridge" : "Road");
            road.Root.transform.SetParent(container != null ? container : transform, false);
            road.Root.transform.position = start;

            foreach (var piece in road.Pieces)
            {
                var prefab = SelectPrefab(piece.Kind);
                if (prefab == null) continue;
                var go = Instantiate(prefab, road.Root.transform);
                go.name = piece.Kind.ToString();
                float thickness = piece.Kind == RoadPieceKind.Road ? prefabs.roadThickness : prefabs.deckThickness;
                var scale = PlacePiece(road, go.transform, piece.From, piece.FromHeight, piece.To, piece.ToHeight, prefabs.width, prefabs.jointOverlap);
                EnsureCollider(go, thickness);
                road.Growing.Add((go.transform, scale, piece.From, false));
                if (piece.Kind != RoadPieceKind.Road) AddRailings(road, piece);
            }
            foreach (var span in road.WaterSpans) AddPiers(road, span);

            if (animated) RoadAppearAnimator.Play(road, prefabs, this);
            return road;
        }

        /// <summary>Убрать построенную дорогу.</summary>
        public void Remove(BuiltRoad road)
        {
            if (road?.Root == null) return;
            RoadAppearAnimator.Kill(road);
            if (Application.isPlaying) Destroy(road.Root);
            else DestroyImmediate(road.Root);
            road.Root = null;
        }

        // ================================================================== План

        /// <summary>
        /// Разбить дорогу длиной length на куски: дорога → подъезд вверх → мост → подъезд вниз → дорога.
        /// Мост заходит на берег на bankMargin с каждой стороны; если узел ближе к воде, чем подъезд, подъезд
        /// становится короче и круче (но всегда есть). Две реки ближе друг к другу, чем два подъезда, — один мост.
        /// </summary>
        public List<RoadPiece> PlanRoad(float length, IReadOnlyList<WaterSpan> water)
        {
            var pieces = new List<RoadPiece>();
            float roadTop = prefabs.roadTop, deckTop = prefabs.deckTop;
            float margin = Mathf.Max(0f, prefabs.bankMargin), ramp = Mathf.Max(0f, prefabs.rampLength);

            // Мостовые участки с заходом на берег, слитые, если между ними не помещаются два подъезда.
            var bridges = new List<WaterSpan>();
            foreach (var span in water)
            {
                var bridge = new WaterSpan(Mathf.Max(0f, span.From - margin), Mathf.Min(length, span.To + margin));
                if (bridges.Count > 0 && bridge.From - bridges[bridges.Count - 1].To < 2f * ramp)
                    bridges[bridges.Count - 1] = new WaterSpan(bridges[bridges.Count - 1].From, bridge.To);
                else bridges.Add(bridge);
            }

            float cursor = 0f;
            void Add(RoadPieceKind kind, float from, float to, float h0, float h1)
            {
                if (to - from > 1e-4f) pieces.Add(new RoadPiece { Kind = kind, From = from, To = to, FromHeight = h0, ToHeight = h1 });
            }
            foreach (var bridge in bridges)
            {
                // Подъезд вверх: если до узла меньше rampLength — от самого узла (короче и круче).
                float rampStart = Mathf.Max(cursor, bridge.From - ramp);
                Add(RoadPieceKind.Road, cursor, rampStart, roadTop, roadTop);
                if (bridge.From > rampStart) Add(RoadPieceKind.Ramp, rampStart, bridge.From, roadTop, deckTop);
                Add(RoadPieceKind.Bridge, bridge.From, bridge.To, deckTop, deckTop);
                float rampEnd = Mathf.Min(length, bridge.To + ramp);
                if (rampEnd > bridge.To) Add(RoadPieceKind.Ramp, bridge.To, rampEnd, deckTop, roadTop);
                cursor = rampEnd;
            }
            Add(RoadPieceKind.Road, cursor, length, roadTop, roadTop);
            return pieces;
        }

        /// <summary>
        /// Выбор префаба по виду куска: дорога — roadPrefab; мост — bridgeDeckPrefab (нет — дорога);
        /// подъезд — rampPrefab (нет — полотно моста, нет и его — дорога).
        /// </summary>
        public GameObject SelectPrefab(RoadPieceKind kind)
        {
            switch (kind)
            {
                case RoadPieceKind.Bridge: return First(prefabs.bridgeDeckPrefab, prefabs.roadPrefab);
                case RoadPieceKind.Ramp: return First(prefabs.rampPrefab, prefabs.bridgeDeckPrefab, prefabs.roadPrefab);
                default: return prefabs.roadPrefab;
            }
        }

        static GameObject First(params GameObject[] candidates)
        {
            foreach (var candidate in candidates)
                if (candidate != null) return candidate; // сравнение Unity: уничтоженный префаб тоже null
            return null;
        }

        // ================================================================== Размещение

        /// <summary>
        /// Поставить кусок от (from, h0) до (to, h1): pivot — в начало, +Z — вдоль куска (с наклоном у подъезда),
        /// масштаб (ширина, 1, длина). Нахлёст overlap с обеих сторон закрывает щель на изломе.
        /// Возвращает итоговый масштаб (для анимации).
        /// </summary>
        static Vector3 PlacePiece(BuiltRoad road, Transform piece, float from, float h0, float to, float h1, float width, float overlap)
        {
            var a = road.PointAt(from) + Vector3.up * h0;
            var b = road.PointAt(to) + Vector3.up * h1;
            var along = b - a;
            float length = along.magnitude;
            if (length < 1e-5f) along = Vector3.forward;
            along /= Mathf.Max(1e-5f, length);
            a -= along * overlap;
            length += overlap * 2f;
            piece.SetPositionAndRotation(a, Quaternion.LookRotation(along, Vector3.up));
            var scale = new Vector3(width, 1f, length);
            piece.localScale = scale;
            return scale;
        }

        /// <summary>
        /// Коллайдер куска: если в префабе его нет — BoxCollider 1 × толщина × 1 под верхней гранью. У BoxCollider
        /// корня ширина и центр по X приводятся к 1 и 0: коллайдер моста не может оказаться шире дороги.
        /// </summary>
        static void EnsureCollider(GameObject piece, float thickness)
        {
            var box = piece.GetComponent<BoxCollider>();
            if (box == null && piece.GetComponent<Collider>() == null)
            {
                box = piece.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, -thickness / 2f, 0.5f);
                box.size = new Vector3(1f, thickness, 1f);
            }
            if (box != null)
            {
                box.size = new Vector3(1f, box.size.y, box.size.z);
                box.center = new Vector3(0f, box.center.y, box.center.z);
            }
        }

        /// <summary>Перила по краям подъездов и моста — внутри ширины полотна (отступ railingInset).</summary>
        void AddRailings(BuiltRoad road, RoadPiece piece)
        {
            if (prefabs.railingPrefab == null) return;
            var flat = road.End - road.Start;
            flat.y = 0f;
            var side = new Vector3(flat.z, 0f, -flat.x).normalized;
            float offset = prefabs.width / 2f - prefabs.railingInset - prefabs.railingThickness / 2f;
            for (int s = -1; s <= 1; s += 2)
            {
                var go = Instantiate(prefabs.railingPrefab, road.Root.transform);
                go.name = "Railing";
                var shifted = new BuiltRoad { Start = road.Start + side * (offset * s), End = road.End + side * (offset * s), Length = road.Length };
                var scale = PlacePiece(shifted, go.transform, piece.From, piece.FromHeight, piece.To, piece.ToHeight, prefabs.railingThickness, 0f);
                scale.y = prefabs.railingHeight;
                go.transform.localScale = scale;
                road.Growing.Add((go.transform, scale, piece.From, false));
            }
        }

        /// <summary>Опоры над водой: ровным шагом между кромками, от дна до низа полотна.</summary>
        void AddPiers(BuiltRoad road, WaterSpan span)
        {
            if (prefabs.pierPrefab == null || span.Length < 1e-3f) return;
            int count = Mathf.Max(1, Mathf.FloorToInt(span.Length / Mathf.Max(0.05f, prefabs.pierSpacing)));
            float bottom = prefabs.waterLevel - prefabs.riverbedDepth;
            float height = prefabs.deckTop - prefabs.deckThickness - bottom;
            if (height <= 0f) return;
            var flat = road.End - road.Start;
            flat.y = 0f;
            var rotation = flat.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            for (int i = 1; i <= count; i++)
            {
                float at = Mathf.Lerp(span.From, span.To, i / (float)(count + 1));
                // Дно — на настоящей высоте воды (линия дороги на рельефе может идти выше), верх — под полотном.
                var point = road.PointAt(at);
                float pierHeight = point.y + prefabs.deckTop - prefabs.deckThickness - bottom;
                if (pierHeight <= 0f) continue;
                var go = Instantiate(prefabs.pierPrefab, road.Root.transform);
                go.name = "Pier";
                go.transform.SetPositionAndRotation(new Vector3(point.x, bottom, point.z), rotation);
                var scale = new Vector3(prefabs.pierDiameter, pierHeight, prefabs.pierDiameter);
                go.transform.localScale = scale;
                road.Growing.Add((go.transform, scale, at, true));
            }
        }
    }
}
