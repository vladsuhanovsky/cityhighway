using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Подвижные части техники. Любое поле может быть пустым — тогда эта часть просто не анимируется.
    /// Экскаватор: Turret (поворот по Y), Boom, Stick, Bucket (наклон по X). Каток: Drum (вращение по X).
    /// </summary>
    public sealed class ConstructionMachine : MonoBehaviour
    {
        [Header("Экскаватор")]
        public Transform turret;
        public Transform boom;
        public Transform stick;
        public Transform bucket;
        [Tooltip("Точка, откуда летит пыль (кончик ковша); пусто — сам ковш")]
        public Transform bucketTip;

        [Header("Каток")]
        public Transform drum;
        [Min(0.0001f)] public float drumRadius = 0.012f;
        [Tooltip("Корпус для вибрации (пусто — корень)")]
        public Transform chassis;
    }
}
