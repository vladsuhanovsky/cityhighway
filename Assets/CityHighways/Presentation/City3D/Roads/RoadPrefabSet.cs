using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Как искать реку под дорогой.</summary>
    public enum RiverDetection
    {
        /// <summary>Луч вниз по слою воды (LayerMask «Water»): у воды должен быть коллайдер на этом слое.</summary>
        Physics,
        /// <summary>По линиям рек карты (без физики) — точно совпадает с тем, как река нарисована.</summary>
        MapPolylines,
        /// <summary>Вода, если её нашёл любой из способов.</summary>
        Both
    }

    /// <summary>
    /// Набор префабов и размеров для RoadBuilder: Create → City Highways → Road Prefab Set.
    ///
    /// Соглашение для префабов кусков дороги (дорога, подъезд, полотно моста):
    ///   • pivot — в НАЧАЛЕ куска, по центру ВЕРХНЕЙ грани;
    ///   • длина 1 вдоль +Z, ширина 1 по X (от −0,5 до 0,5), верх на Y = 0, толщина вниз;
    ///   • корень префаба растягивается (X — ширина, Z — длина), поэтому мелкие детали (фонари, знаки)
    ///     в него не кладите — им нужен отдельный префаб.
    /// Так ширина дороги и моста задаётся ОДНИМ числом width, а коллайдер (BoxCollider 1 × толщина × 1)
    /// растягивается вместе с куском: визуальная и физическая ширина всегда совпадают.
    ///
    /// Опора: pivot внизу по центру, высота 1 по Y, диаметр 1. Перила: pivot в начале снизу, длина 1 по Z,
    /// высота 1 по Y, толщина 1 по X.
    /// </summary>
    [CreateAssetMenu(menuName = "City Highways/Road Prefab Set", fileName = "RoadPrefabs")]
    public sealed class RoadPrefabSet : ScriptableObject
    {
        [Header("Ширина — одна на дорогу, подъезды и мост (визуально и по коллайдеру)")]
        [Min(0.001f)] public float width = 0.11f;

        [Header("Префабы (пусто у подъезда — берётся полотно моста; пусто у моста — дорога)")]
        public GameObject roadPrefab;
        public GameObject bridgeDeckPrefab;
        public GameObject rampPrefab;
        public GameObject pierPrefab;
        public GameObject railingPrefab;

        [Header("Высоты (над землёй под дорогой)")]
        public float roadTop = 0.006f;
        public float roadThickness = 0.006f;
        public float deckTop = 0.05f;
        public float deckThickness = 0.03f;
        [Tooltip("Уровень воды над землёй: от него вниз уходят опоры")]
        public float waterLevel = 0.004f;
        [Tooltip("Насколько опора уходит под воду")]
        public float riverbedDepth = 0.02f;

        [Header("Мост")]
        [Tooltip("Насколько полотно заходит на берег за кромку воды — без зазора над набережной")]
        public float bankMargin = 0.045f;
        [Tooltip("Длина плавного подъезда от дороги к полотну моста")]
        public float rampLength = 0.16f;
        [Tooltip("Шаг опор над водой")]
        public float pierSpacing = 0.4f;
        public float pierDiameter = 0.056f;
        public float railingHeight = 0.02f;
        public float railingThickness = 0.008f;
        [Tooltip("Отступ перил от края полотна внутрь: перила не делают мост шире дороги")]
        public float railingInset = 0.004f;
        [Tooltip("Нахлёст соседних кусков: на изломе (дорога → подъезд → мост) не остаётся щели")]
        public float jointOverlap = 0.004f;

        [Header("Поиск реки")]
        public RiverDetection detection = RiverDetection.Physics;
        public LayerMask waterMask;
        [Tooltip("Шаг проверки вдоль дороги; границы воды затем уточняются делением пополам")]
        [Min(0.001f)] public float sampleStep = 0.02f;
        [Tooltip("С какой высоты над дорогой пускается луч вниз")]
        public float rayHeight = 2f;

        [Header("Появление при строительстве (DOTween)")]
        public float buildDuration = 0.8f;
        public float pierRiseDuration = 0.35f;
        public AnimationCurve growCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));

        void Reset()
        {
            waterMask = LayerMask.GetMask("Water");
        }

        /// <summary>Маска воды; если в ассете не задана — слой «Water».</summary>
        public LayerMask WaterMaskOrDefault => waterMask.value != 0 ? waterMask : (LayerMask)LayerMask.GetMask("Water");
    }
}
