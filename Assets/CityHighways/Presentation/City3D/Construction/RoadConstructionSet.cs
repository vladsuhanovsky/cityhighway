using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Префабы и параметры стройки дороги: Create → City Highways → Road Construction Set.
    ///
    /// Соглашения для префабов (pivot у земли по центру, вперёд — +Z, размеры — в метрах сцены, 1 м = 100 px поля):
    ///   • Рабочий: компонент ConstructionWorker, в поле Body — дочерний объект с телом (он подпрыгивает на бегу).
    ///     Рост около 0,03.
    ///   • Экскаватор: компонент ConstructionMachine, поля Turret (поворот башни по Y), Boom / Stick / Bucket
    ///     (стрела, рукоять, ковш — наклон по X). Длина около 0,08.
    ///   • Каток: компонент ConstructionMachine, поле Drum (валец, вращается по X), DrumRadius — радиус вальца.
    ///   • Облачко пыли: любой небольшой меш (пыль из-под ковша).
    /// Пустые поля игра заполняет низкополигональными моделями, собранными кодом (ConstructionArt).
    /// </summary>
    [CreateAssetMenu(menuName = "City Highways/Road Construction Set", fileName = "RoadConstruction")]
    public sealed class RoadConstructionSet : ScriptableObject
    {
        [Header("Префабы")]
        public GameObject workerPrefab;
        public GameObject excavatorPrefab;
        public GameObject rollerPrefab;
        public GameObject dustPrefab;

        [Header("Пул (сколько создать заранее)")]
        [Min(0)] public int prewarmWorkers = 6;
        [Min(0)] public int prewarmMachines = 2;
        [Min(0)] public int prewarmDust = 12;

        [Header("Рабочие")]
        [Min(1)] public int minWorkers = 2;
        [Min(1)] public int maxWorkers = 3;
        [Tooltip("Раз в сколько секунд рабочий меняет направление — «бегает туда-сюда»")]
        public float workerLegTime = 0.42f;
        [Tooltip("Как далеко рабочий отбегает вперёд и назад от места укладки")]
        public float workerRange = 0.09f;
        [Tooltip("Отступ рабочих от края дороги (обочина)")]
        public float workerShoulder = 0.018f;
        public float workerHopHeight = 0.004f;
        public float workerHopTime = 0.11f;

        [Header("Техника")]
        [Tooltip("Экскаватор идёт впереди укладки — «копает» основание")]
        public float excavatorLead = 0.075f;
        [Tooltip("Сдвиг экскаватора к обочине")]
        public float excavatorSide = 0.03f;
        [Tooltip("Один цикл копания, с")]
        public float digCycle = 1.3f;
        [Tooltip("Каток идёт позади укладки — «утрамбовывает» свежий асфальт")]
        public float rollerLag = 0.055f;
        public float rollerShake = 0.0007f;

        [Header("Появление и исчезновение")]
        public float popIn = 0.25f;
        [Tooltip("Пауза после укладки перед уходом техники")]
        public float linger = 0.15f;
        public float popOut = 0.25f;
        [Tooltip("Время жизни облачка пыли")]
        public float dustLife = 0.6f;
    }
}
