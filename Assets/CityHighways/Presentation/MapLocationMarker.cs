using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>One draggable visual marker, identified by the stable map location ID.</summary>
    public sealed class MapLocationMarker : MonoBehaviour
    {
        [SerializeField] string locationId;
        public string Id => locationId;

#if UNITY_EDITOR
        public void SetId(string id) => locationId = id;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.25f, 0.85f);
            Gizmos.DrawSphere(transform.position, 0.045f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.10f);
        }
#endif
    }
}
