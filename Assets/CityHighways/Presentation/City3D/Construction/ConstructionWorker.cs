using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Рабочий в оранжевом жилете. Body — дочерний объект с телом: подпрыгивает на бегу (корень двигается по пути,
    /// тело — вверх-вниз), поэтому прыжок не мешает движению.
    /// </summary>
    public sealed class ConstructionWorker : MonoBehaviour
    {
        [Tooltip("Дочерний объект с телом рабочего")]
        public Transform body;
    }
}
